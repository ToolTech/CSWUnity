//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: SceneCommandPump.cs
// Description	: Ordered edit-locked native capture and main-thread publication
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Captured commands under edit access and published ordered occurrence and frame updates.	(2.12.327)
// AMO	261023	Captured bounded frame slices with non-waiting buffer access and unlocked publication.	(2.12.327)
// AMO	261023	Added separate native-capture and Unity-publication profiler scopes.	(2.12.327)
// AMO	261026	Stopped synchronous draining at its exact matching frame fence.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using CSW;
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using Saab.Unity.Extensions;
using UnityEngine;
using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;
using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;

namespace Saab.Foundation.Unity.MapStreamer.Csw
{
	internal enum SceneChangeKind
	{
		None,
		New,
		Update,
		Transform,
		Delete,
		Activation,
		GeoInfo,
		Error,
		StartFrame,
		EndFrame
	}

	internal sealed class SceneChange : IDisposable
	{
		internal SceneChangeKind Kind;
		internal SceneNodeData Node;
		internal SceneInstanceKey Key;
		internal Matrix4x4 Matrix;
		internal Activation Activation;
		internal string Text;
		internal Vec3D Position;
		internal Vec3 Hpr;
		internal uint Id;
		internal double Time;
		internal MessageLevel Level;
		internal ulong Lifetime;
		internal uint ParentVersion;
		internal SceneInstanceKey Parent;

		public void Dispose()
		{
			Node?.Dispose();
		}
	}

	internal sealed class SceneCommandPump : IDisposable
	{
		private static readonly ProfilerMarker _captureMarker = new ProfilerMarker(ProfilerCategory.Render, "CSW-Capture");
		private static readonly ProfilerMarker _publishMarker = new ProfilerMarker(ProfilerCategory.Render, "CSW-Publish");
		private readonly UnityScene _scene;
		private readonly SceneCommandInbox _inbox;
		private readonly GeometryBuilderBase[] _builders;
		private readonly List<SceneChange> _frameChanges = new List<SceneChange>();
		private readonly List<SceneChange> _capturedChanges = new List<SceneChange>();
		private CommandBuffer _current;
		private CameraRelativeFrame _submitted;
		private CameraRelativeFrame _frame;
		private bool _inFrame;
		private uint _expectedRefresh;
		private readonly ulong _generation;
		internal bool IsDisposed { get; private set; }
		internal int LastLockAttempts { get; private set; }

		internal event Action<CameraRelativeFrame> FrameConsumed;
		internal event Action GeoInfoReceived;
		internal event Action<string, uint, MessageLevel> ErrorReceived;
		internal int PendingBuffers => _inbox.PendingBuffers + (_current != null ? 1 : 0);

		internal SceneCommandPump(UnityScene scene, SceneCommandInbox inbox, GeometryBuilderBase[] builders)
		{
			_scene = scene;
			_inbox = inbox;
			_builders = builders ?? Array.Empty<GeometryBuilderBase>();
			_generation = scene.Generation;
		}

		internal void ExpectFrame(uint id, CameraRelativeFrame submitted)
		{
			if (_expectedRefresh != 0 || _inFrame)
				throw new InvalidOperationException("Only one refresh may be outstanding through consumed EndFrame.");
			_expectedRefresh = id;
			_submitted = submitted;
		}

		internal int Pump(int maximumCommands, TimeSpan budget, uint stopAfterRefresh = 0)
		{
			_scene.RequireMainThread();
			if (_scene.Generation != _generation)
				throw new InvalidOperationException("A stale command pump cannot publish into a new map generation.");
			var fault = _inbox.Fault;
			if (fault != null)
				throw new InvalidOperationException(fault);
			LastLockAttempts = 0;
			var timer = Stopwatch.StartNew();
			var processed = 0;
			while (processed < maximumCommands && (processed == 0 || timer.Elapsed < budget))
			{
				if (_current == null && !_inbox.TryDequeue(out _current))
					break;
				LastLockAttempts++;
				if (!_current.TryLockEdit(0))
					break;

				try
				{
					try
					{
						var capturingFrame = _inFrame;
						do
						{
							SceneChange change = null;
							var command = _current.GetCommand();
							if (command != null)
							{
								try { change = Capture(command); }
								finally { command.Dispose(); }
							}
							if (change != null)
							{
								_capturedChanges.Add(change);
								if (change.Kind == SceneChangeKind.StartFrame)
									capturingFrame = true;
							}
							if (!_current.HasCommands)
							{
								_current.Dispose();
								_current = null;
							}
							if ((change != null && !CanDeferPublication(change.Kind, capturingFrame)) ||
								processed + _capturedChanges.Count >= maximumCommands || timer.Elapsed >= budget)
								break;
						}
						while (_current != null);
					}
					finally
					{
						NodeLock.UnLock();
					}

					foreach (var change in _capturedChanges)
					{
						Publish(change);
						processed++;
						if (stopAfterRefresh != 0 && change.Kind == SceneChangeKind.EndFrame &&
							change.Id == stopAfterRefresh)
							return processed;
					}
				}
				finally
				{
					try
					{
						foreach (var change in _capturedChanges)
							change.Dispose();
					}
					finally { _capturedChanges.Clear(); }
				}
			}
			return processed;
		}

		private static bool CanDeferPublication(SceneChangeKind kind, bool inFrame)
		{
			// Hierarchy/resources and frame completion must publish before dependent captures.
			return kind == SceneChangeKind.None || kind == SceneChangeKind.StartFrame ||
				(inFrame && (kind == SceneChangeKind.Transform || kind == SceneChangeKind.Activation));
		}

		private SceneChange Capture(SceneCommand command)
		{
			using var sample = _captureMarker.Auto();
			switch (command)
			{
				case SceneCommandStartFrame start:
					return new SceneChange
					{
						Kind = SceneChangeKind.StartFrame, Id = start.RefreshID,
						Time = start.RenderTime, Position = start.Position, Hpr = start.HPR
					};
				case SceneCommandEndFrame end:
					if (end.CommandRefID != end.RefreshID)
						throw new InvalidOperationException("Native EndFrame response and refresh fences do not match.");
					return new SceneChange { Kind = SceneChangeKind.EndFrame, Id = end.RefreshID };
				case SceneCommandNewNode created:
					return CaptureNode(created, false);
				case SceneCommandUpdateNode updated:
					return CaptureNode(updated, true);
				case SceneCommandDeleteNode deleted:
					return CaptureInstance(deleted, SceneChangeKind.Delete);
				case SceneCommandActivation activation:
					var active = CaptureInstance(activation, SceneChangeKind.Activation);
					active.Activation = activation.Activation;
					return active;
				case SceneCommandGeoInfo geo:
					return new SceneChange
					{
						Kind = SceneChangeKind.GeoInfo, Text = geo.CoordinateSystem,
						Position = geo.Origin, Id = geo.CommandRefID
					};
				case SceneCommandErrorMessage error:
					return new SceneChange
					{
						Kind = SceneChangeKind.Error, Text = error.Message,
						Level = error.MessageLevel, Id = error.CommandRefID
					};
				case SceneCommandResponse _:
					return new SceneChange();
				default:
					throw new NotSupportedException($"Unsupported SceneManager output '{command.GetNativeTypeName()}'.");
			}
		}

		private SceneChange CaptureInstance(SceneCommandInstance command, SceneChangeKind kind)
		{
			var node = command.Node;
			try
			{
				if (node == null)
					throw new InvalidOperationException("SceneManager emitted an occurrence without a node.");
				return new SceneChange
				{
					Kind = kind,
					Key = new SceneInstanceKey(node.GetNativeReference(), command.PathID)
				};
			}
			finally
			{
				node?.ReleaseAlreadyLocked();
			}
		}

		private SceneChange CaptureNode(SceneCommandHierarchyInstance command, bool update)
		{
			Node node = null;
			Group parent = null;
			State state = null;
			SceneNodeData data = null;
			try
			{
				node = command.Node;
				parent = command.Parent;
				if (node == null)
					throw new InvalidOperationException("SceneManager emitted a hierarchy occurrence without a node.");
				var key = new SceneInstanceKey(node.GetNativeReference(), command.PathID);
				var parentKey = new SceneInstanceKey(parent?.GetNativeReference() ?? IntPtr.Zero, command.ParentPathID);
				if (command is SceneCommandUpdateNode transformUpdate && transformUpdate.TransformOnly)
				{
					if (!_scene.TryGetInstance(key, out var handle) || !handle.ParentKey.Equals(parentKey))
						throw new InvalidOperationException($"Transform-only update has an unknown occurrence or parent: {key}.");
					return new SceneChange
					{
						Kind = SceneChangeKind.Transform, Key = key,
						Matrix = transformUpdate.LocalTransform.ToMatrix4x4()
					};
				}

				state = command is SceneCommandNewNode created
					? created.State
					: ((SceneCommandUpdateNode)command).State;
				var parentHandle = _scene.Parent(parentKey);
				var mask = node.IntersectMask;
				var name = node.Name;
				data = new SceneNodeData
				{
					Key = key,
					Parent = parentKey,
					Name = string.IsNullOrEmpty(name) ? node.GetNativeTypeName() : name,
					IntersectMask = mask == IntersectMaskValue.NOTHING
						? parentHandle.IntersectMask
						: parentHandle.IntersectMask & mask,
					IsStatic = parentHandle.IsStatic || node is RefNode,
					BoundaryCenter = node.BoundaryCenter.ToVector3(),
					Kind = node is Geometry ? SceneNodeKind.Geometry :
						node is DynamicLoader ? SceneNodeKind.Loader :
						node is Lod ? SceneNodeKind.Lod :
						node is Crossboard ? SceneNodeKind.Crossboard : SceneNodeKind.Group
				};
				if (node is GizmoSDK.Gizmo3D.Transform transform)
				{
					transform.GetTransform(out var matrix);
					data.LocalTransform = matrix.ToMatrix4x4();
				}
				if (node is Geometry geometry && data.IntersectMask != IntersectMaskValue.NOTHING)
				{
					var traversal = data.IsStatic ? TraversalState.AssetInstance : TraversalState.None;
					foreach (var builder in _builders)
					{
						if (builder && builder.CanBuild(node, traversal, data.IntersectMask))
						{
							data.Builder = builder;
							break;
						}
					}
					if (!data.Builder)
						throw new InvalidOperationException($"No geometry builder accepts '{data.Name}' ({data.IntersectMask}).");
					data.Prepared = _scene.Resources.Capture(geometry, state, update);
				}
				return new SceneChange { Kind = update ? SceneChangeKind.Update : SceneChangeKind.New, Node = data };
			}
			catch
			{
				data?.Dispose();
				throw;
			}
			finally
			{
				state?.ReleaseAlreadyLocked();
				parent?.ReleaseAlreadyLocked();
				node?.ReleaseAlreadyLocked();
			}
		}

		private void Publish(SceneChange change)
		{
			using var sample = _publishMarker.Auto();
			switch (change.Kind)
			{
				case SceneChangeKind.New:
					_scene.Create(change.Node);
					break;
				case SceneChangeKind.Update:
					_scene.Update(change.Node);
					break;
				case SceneChangeKind.Delete:
					_scene.Delete(change.Key);
					break;
				case SceneChangeKind.Transform:
				case SceneChangeKind.Activation:
					if (_inFrame)
					{
						if (_frameChanges.Count >= 262144)
							throw new InvalidOperationException("A native frame exceeded the bounded presentation transaction.");
						if (!_scene.TryGetInstance(change.Key, out var handle))
							throw new InvalidOperationException($"Frame change referenced missing occurrence {change.Key}.");
						change.Lifetime = handle.Lifetime;
						change.ParentVersion = handle.ParentVersion;
						change.Parent = handle.ParentKey;
						_frameChanges.Add(change);
					}
					else
						ApplyFrameChange(change);
					break;
				case SceneChangeKind.GeoInfo:
					_scene.Coordinates.Configure(change.Text, change.Position);
					_scene.MapChanged();
					GeoInfoReceived?.Invoke();
					break;
				case SceneChangeKind.Error:
					_scene.MapError(change.Text);
					ErrorReceived?.Invoke(change.Text, change.Id, change.Level);
					break;
				case SceneChangeKind.StartFrame:
					if (_inFrame || change.Id != _expectedRefresh)
						throw new InvalidOperationException($"Unexpected native StartFrame {change.Id}; expected {_expectedRefresh}.");
					_frame = _submitted.WithNativePose(change.Id, change.Time, change.Position, change.Hpr);
					_inFrame = true;
					_scene.BeginFrame();
					break;
				case SceneChangeKind.EndFrame:
					if (!_inFrame || change.Id != _frame.RefreshId)
						throw new InvalidOperationException($"Mismatched native EndFrame {change.Id}.");
					foreach (var item in _frameChanges)
						if (item.Kind == SceneChangeKind.Transform && IsCurrent(item))
							_scene.SetTransform(item.Key, item.Matrix, _frame.GlobalPosition);
					foreach (var item in _frameChanges)
						if (item.Kind == SceneChangeKind.Activation && IsCurrent(item))
							_scene.SetActivation(item.Key, item.Activation, false);
					_scene.CommitFrame(_frame);
					_frameChanges.Clear();
					_inFrame = false;
					_expectedRefresh = 0;
					FrameConsumed?.Invoke(_frame);
					break;
			}
		}

		private void ApplyFrameChange(SceneChange change)
		{
			if (change.Kind == SceneChangeKind.Transform)
				_scene.SetTransform(change.Key, change.Matrix);
			else
				_scene.SetActivation(change.Key, change.Activation);
		}

		private bool IsCurrent(SceneChange change)
		{
			return _scene.TryGetInstance(change.Key, out var handle) &&
				handle.Lifetime == change.Lifetime && handle.ParentVersion == change.ParentVersion &&
				handle.ParentKey.Equals(change.Parent);
		}

		public void Dispose()
		{
			if (IsDisposed)
				return;
			_inbox.StopAccepting();
			var cleanup = new SceneCleanup();
			NodeLock.WaitLockEdit();
			try
			{
				cleanup.Attempt(() =>
				{
					_current?.Dispose();
					_current = null;
				});
				cleanup.Attempt(_inbox.DrainAlreadyLocked);
			}
			finally
			{
				NodeLock.UnLock();
			}
			_frameChanges.Clear();
			IsDisposed = _current == null && _inbox.PendingBuffers == 0;
			cleanup.Complete("SceneManager command-pump cleanup failed.");
		}
	}
}
