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
// AMO	261030	Consumed started frames through their EndFrame and exempted frame transforms and activations from the command limit.	(2.12.327)
// AMO	261103	Released captured node, parent and state wrappers under the held edit access and suppressed their finalizers.	(2.12.327)
// AMO	261104	Moved to namespace CSW.Unity.StreamingMap.	(2.12.327)
// AMO	261104	Copies values through CSW.Unity.Conversion.	(2.12.327)
// AMO	261104	Uses CSW.Unity.CameraFrame.	(2.12.327)
// AMO	261104	Selects the realization through CSW.Unity.Factory.	(2.12.327)
// AMO	261105	Passed loader-thread mesh preparations to capture and expired unused ones under edit access.	(2.12.327)
// AMO	261105	Added profiler markers for capture and publish phases.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using UnityEngine;
using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;
using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;

namespace CSW.Unity.StreamingMap
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
		private static readonly ProfilerMarker _fetchMarker = new ProfilerMarker(ProfilerCategory.Render, "CSW-Capture-Fetch");
		private static readonly ProfilerMarker _wrappersMarker = new ProfilerMarker(ProfilerCategory.Render, "CSW-Capture-Wrappers");
		private static readonly ProfilerMarker _factoryMarker = new ProfilerMarker(ProfilerCategory.Render, "CSW-Capture-Factory");
		private static readonly ProfilerMarker _releaseMarker = new ProfilerMarker(ProfilerCategory.Render, "CSW-Capture-Release");
		private static readonly ProfilerMarker _deleteMarker = new ProfilerMarker(ProfilerCategory.Render, "CSW-Publish-Delete");
		private static readonly ProfilerMarker _publishMarker = new ProfilerMarker(ProfilerCategory.Render, "CSW-Publish");
		private readonly UnityScene _scene;
		private readonly SceneCommandInbox _inbox;
		private readonly FactoryCapture _capture;
		private readonly PreparedMeshCache _preparedMeshes;
		private readonly List<SceneChange> _frameChanges = new List<SceneChange>();
		private readonly List<SceneChange> _capturedChanges = new List<SceneChange>();
		private CommandBuffer _current;
		private CameraFrame _submitted;
		private CameraFrame _frame;
		private bool _inFrame;
		private uint _expectedRefresh;
		private readonly ulong _generation;
		internal bool IsDisposed { get; private set; }
		internal int LastLockAttempts { get; private set; }

		internal event Action<CameraFrame> FrameConsumed;
		internal event Action GeoInfoReceived;
		internal event Action<string, uint, MessageLevel> ErrorReceived;
		internal int PendingBuffers => _inbox.PendingBuffers + (_current != null ? 1 : 0);

		internal SceneCommandPump(UnityScene scene, SceneCommandInbox inbox, GeometryBuilderBase[] builders,
			PreparedMeshCache preparedMeshes = null)
		{
			_scene = scene;
			_inbox = inbox;
			_preparedMeshes = preparedMeshes;
			_capture = new FactoryCapture
			{
				Resources = scene.Resources,
				PreparedMeshes = preparedMeshes,
				Builders = builders ?? Array.Empty<GeometryBuilderBase>()
			};
			_generation = scene.Generation;
		}

		internal void ExpectFrame(uint id, CameraFrame submitted)
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
			var counted = 0;
			// The limits apply only outside frames, so a started frame is consumed through its EndFrame as in CSWUnreal.
			while (_inFrame || (counted < maximumCommands && (counted == 0 || timer.Elapsed < budget)))
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
						_preparedMeshes?.ExpireAlreadyLocked();
						var capturingFrame = _inFrame;
						do
						{
							SceneChange change = null;
							_fetchMarker.Begin();
							var command = _current.GetCommand();
							_fetchMarker.End();
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
								(!capturingFrame && (counted + _capturedChanges.Count >= maximumCommands ||
								timer.Elapsed >= budget)))
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
						if (CountsTowardLimit(change.Kind, _inFrame))
							counted++;
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

		private static bool CountsTowardLimit(SceneChangeKind kind, bool inFrame)
		{
			// The transforms and activations of a frame do not count toward the command limit.
			return !inFrame || (kind != SceneChangeKind.Transform && kind != SceneChangeKind.Activation);
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
				ReleaseLocked(node);
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
				_wrappersMarker.Begin();
				node = command.Node;
				parent = command.Parent;
				if (node == null)
				{
					_wrappersMarker.End();
					throw new InvalidOperationException("SceneManager emitted a hierarchy occurrence without a node.");
				}
				var key = new SceneInstanceKey(node.GetNativeReference(), command.PathID);
				var parentKey = new SceneInstanceKey(parent?.GetNativeReference() ?? IntPtr.Zero, command.ParentPathID);
				if (command is SceneCommandUpdateNode transformUpdate && transformUpdate.TransformOnly)
				{
					_wrappersMarker.End();
					if (!_scene.TryGetInstance(key, out var handle) || !handle.ParentKey.Equals(parentKey))
						throw new InvalidOperationException($"Transform-only update has an unknown occurrence or parent: {key}.");
					return new SceneChange
					{
						Kind = SceneChangeKind.Transform, Key = key,
						Matrix = transformUpdate.LocalTransform.CopyMatrix4x4()
					};
				}

				state = command is SceneCommandNewNode created
					? created.State
					: ((SceneCommandUpdateNode)command).State;
				_wrappersMarker.End();
				_factoryMarker.Begin();
				var factory = Factory.Select(node) ??
					throw new InvalidOperationException($"No CSW.Unity.Factory realizes '{node.GetNativeTypeName()}'.");
				data = new SceneNodeData { Key = key, Parent = parentKey };
				_capture.Node = node;
				_capture.State = state;
				_capture.IsUpdate = update;
				_capture.Parent = _scene.Parent(parentKey);
				_capture.Data = data;
				try
				{
					factory.CaptureOccurrence(_capture);
				}
				finally
				{
					_capture.Clear();
					_factoryMarker.End();
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
				using var sample = _releaseMarker.Auto();
				ReleaseLocked(state);
				ReleaseLocked(parent);
				ReleaseLocked(node);
			}
		}

		// The finalizers of node and state wrappers wait for edit access, so the wrappers are released under the held access.
		internal static void ReleaseLocked(NodeActionProvider wrapper)
		{
			if (wrapper == null)
				return;
			wrapper.ReleaseAlreadyLocked();
			GC.SuppressFinalize(wrapper);
		}

		internal static void ReleaseLocked(State wrapper)
		{
			if (wrapper == null)
				return;
			wrapper.ReleaseAlreadyLocked();
			GC.SuppressFinalize(wrapper);
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
					using (_deleteMarker.Auto())
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
