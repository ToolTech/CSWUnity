//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: MapManager.cs
// Description	: CSW session ownership, map lifecycle and refresh backpressure
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Added map session lifecycle, bounded command consumption and refresh backpressure.	(2.12.327)
// AMO	261023	Submitted coherent camera and refresh batches without main-thread graph-lock waits.	(2.12.327)
// AMO	261023	Instrumented command-lane Tick independently of Unity frame-pacing waits.	(2.12.327)
// AMO	261026	Added Omni control and bounded frame completion without SDK access during notification waits.	(2.12.327)
// AMO	261028	Defaulted Omni traversal to on for CSWUnreal coverage parity.	(2.12.327)
//
//*****************************************************************************

using System;
using Stopwatch = System.Diagnostics.Stopwatch;
using CSW;
using GizmoSDK.GizmoBase;
using UnityEngine;
using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;
using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;

namespace Saab.Foundation.Unity.MapStreamer.Csw
{
	/// <summary>Lifecycle state of the current native map session.</summary>
	public enum MapManagerState
	{
		Stopped,
		Loading,
		Running,
		Faulted
	}

	/// <summary>Owns a native command session while borrowing a Unity scene and its builders.</summary>
	/// <remarks>
	/// Use on the Unity main thread; notifications run on that thread.
	/// Disposal stops the session and clears, but does not dispose, the borrowed scene.
	/// </remarks>
	public sealed class MapManager : IDisposable
	{
		private static readonly ProfilerMarker _tickMarker = new ProfilerMarker(ProfilerCategory.Render, "CSW-Tick");
		private readonly UnityScene _scene;
		private readonly GeometryBuilderBase[] _builders;
		private readonly uint _loaders;
		private SceneManagerSession _session;
		private SceneCommandPump _pump;
		private uint _mapRequest;
		private uint _refreshRequest;
		private bool _disposed;

		public MapManagerState State { get; private set; }
		public string MapUrl { get; private set; }
		public string Error { get; private set; }
		public int PendingBuffers => _pump?.PendingBuffers ?? 0;
		public bool RefreshOutstanding => _refreshRequest != 0;
		public ulong CompletedFrames { get; private set; }
		internal bool IsDisposed => _disposed;
		internal bool NativeCommandsPending => _session?.HasPendingCommands ?? false;
		internal bool NativeWorkerRunning => _session?.IsRunning ?? false;
		public int MaximumCommandsPerTick { get; set; } = 128;
		public TimeSpan BuildBudget { get; set; } = TimeSpan.FromMilliseconds(4);
		/// <summary>Traverses in all directions by default, like CSWUnreal; Unity's renderer retains its own culling.</summary>
		public bool OmniView { get; set; } = true;
		/// <summary>Finishes the submitted camera frame in this Tick instead of deferring its output.</summary>
		public bool SynchronizeFrames { get; set; }
		/// <summary>Bounds frame synchronization; expiration faults the session, never accepts a partial frame.</summary>
		public TimeSpan FrameWaitTimeout { get; set; } = TimeSpan.FromSeconds(5);
		/// <summary>Raised after the borrowed scene's coordinate context changes.</summary>
		public event Action CoordinateContextChanged;
		/// <summary>Publishes a copied camera snapshot after its matching frame is consumed.</summary>
		public event Action<CameraRelativeFrame> FrameCommitted;
		/// <summary>Reports a session failure without transferring ownership of scene resources.</summary>
		public event Action<string> Failed;

		public MapManager(UnityScene scene, GeometryBuilderBase[] builders, uint loaders = 4)
		{
			_scene = scene ?? throw new ArgumentNullException(nameof(scene));
			_builders = builders ?? throw new ArgumentNullException(nameof(builders));
			_loaders = loaders;
		}

		/// <summary>Stops and drains the previous session before starting a new map generation.</summary>
		public void OpenMap(string url)
		{
			_scene.RequireMainThread();
			if (_disposed)
				throw new ObjectDisposedException(nameof(MapManager));
			if (string.IsNullOrWhiteSpace(url))
				throw new ArgumentException("A map URL is required.", nameof(url));
			Clear();
			MapUrl = url;
			Error = null;
			CompletedFrames = 0;
			State = MapManagerState.Loading;
			try
			{
				_session = new SceneManagerSession(_loaders);
				_pump = new SceneCommandPump(_scene, _session.Inbox, _builders);
				_pump.GeoInfoReceived += HandleGeoInfo;
				_pump.FrameConsumed += HandleFrame;
				_pump.ErrorReceived += HandleError;
				_mapRequest = _session.Submit(new SceneCommandAddNewMap(url));
			}
			catch (Exception exception)
			{
				Fail(exception);
			}
		}

		/// <summary>Consumes output and submits at most one outstanding refresh for the supplied camera.</summary>
		/// <remarks>
		/// The optional camera argument is a value snapshot, not a live camera reference.
		/// Synchronization repeats bounded capture slices through the matching fence;
		/// callers must release SDK graph access before requesting it.
		/// </remarks>
		public void Tick(CameraRelativeFrame? latestCamera = null)
		{
			_scene.RequireMainThread();
			if (_session == null || State == MapManagerState.Faulted)
				return;
			if (SynchronizeFrames && latestCamera.HasValue && GizmoSDK.Gizmo3D.NodeLock.IsLockedByMe())
				throw new InvalidOperationException("Frame synchronization requires the caller to release SDK graph access.");
			using var sample = _tickMarker.Auto();
			try
			{
				var synchronize = SynchronizeFrames && latestCamera.HasValue;
				var timeout = FrameWaitTimeout;
				if (synchronize && (timeout <= TimeSpan.Zero ||
					timeout.TotalMilliseconds > int.MaxValue))
					throw new ArgumentOutOfRangeException(nameof(FrameWaitTimeout));
				var timer = synchronize ? Stopwatch.StartNew() : null;
				_pump.Pump(MaximumCommandsPerTick, BuildBudget);
				if (State == MapManagerState.Faulted)
				{
					StopSession();
					_scene.Clear(_scene.Generation + 1);
					return;
				}
				if (synchronize && _refreshRequest != 0)
					FinishFrame(_refreshRequest, timer, timeout);
				if (_refreshRequest == 0 && _scene.Coordinates.IsConfigured && latestCamera.HasValue)
				{
					var camera = latestCamera.Value;
					_refreshRequest = _session.SubmitFrame(camera, OmniView);
					_pump.ExpectFrame(_refreshRequest, camera);
					if (synchronize)
						FinishFrame(_refreshRequest, timer, timeout);
				}
			}
			catch (Exception exception)
			{
				Fail(exception);
			}
		}

		private void FinishFrame(uint refresh, Stopwatch timer, TimeSpan timeout)
		{
			while (_refreshRequest == refresh)
			{
				var remaining = timeout - timer.Elapsed;
				if (remaining <= TimeSpan.Zero)
					throw new TimeoutException($"SceneManager frame {refresh} did not complete within {timeout.TotalMilliseconds} ms.");
				var version = _session.Inbox.ActivityVersion;
				var processed = _pump.Pump(MaximumCommandsPerTick,
					BuildBudget < remaining ? BuildBudget : remaining, refresh);
				if (State == MapManagerState.Faulted)
					throw new InvalidOperationException(Error);
				if (_refreshRequest != refresh)
					return;
				if (!_session.IsRunning)
					throw new InvalidOperationException("The native worker stopped before the synchronized frame completed.");
				if (processed == 0)
				{
					var milliseconds = Math.Max(1, (int)Math.Ceiling((timeout - timer.Elapsed).TotalMilliseconds));
					_session.Inbox.WaitForActivity(version, Math.Min(milliseconds, _pump.PendingBuffers > 0 ? 1 : 50));
				}
			}
		}

		private void HandleGeoInfo()
		{
			CoordinateContextChanged?.Invoke();
		}

		private void HandleFrame(CameraRelativeFrame frame)
		{
			if (frame.RefreshId != _refreshRequest)
				throw new InvalidOperationException("The consumed frame does not match the outstanding refresh.");
			FrameCommitted?.Invoke(frame);
			_refreshRequest = 0;
			CompletedFrames++;
			State = MapManagerState.Running;
		}

		private void HandleError(string message, uint id, MessageLevel level)
		{
			if ((id != 0 && (id == _mapRequest || id == _refreshRequest)) ||
				(level & MessageLevel.LEVEL_MASK) == MessageLevel.FATAL)
			{
				Error = message;
				State = MapManagerState.Faulted;
				Debug.LogError($"CSW Map Streamer: {message}");
				Failed?.Invoke(message);
			}
			else
			{
				Debug.LogWarning($"CSW SceneManager ({id}): {message}");
			}
		}

		private void Fail(Exception exception)
		{
			Error = exception.Message;
			State = MapManagerState.Faulted;
			Debug.LogException(exception);
			var cleanup = new SceneCleanup();
			cleanup.Attempt(StopSession);
			cleanup.Attempt(() => _scene.Clear(_scene.Generation + 1));
			cleanup.Attempt(() => _scene.MapError(Error));
			cleanup.Attempt(() => Failed?.Invoke(Error));
			cleanup.Complete("Map failure cleanup failed.");
		}

		private void StopSession()
		{
			var cleanup = new SceneCleanup();
			cleanup.Attempt(() =>
			{
				try { _session?.Dispose(); }
				finally
				{
					if (_session?.IsDisposed == true)
						_session = null;
				}
			});
			cleanup.Attempt(() =>
			{
				try { _pump?.Dispose(); }
				finally
				{
					if (_pump?.IsDisposed == true)
						_pump = null;
				}
			});
			_mapRequest = 0;
			_refreshRequest = 0;
			cleanup.Complete("Could not release the native map session.");
		}

		/// <summary>Stops native work and clears the borrowed scene's current map generation.</summary>
		public void Clear()
		{
			_scene.RequireMainThread();
			// Recreate the native session: generation alone cannot reject late same-session output.
			var cleanup = new SceneCleanup();
			cleanup.Attempt(StopSession);
			cleanup.Attempt(() => _scene.Clear(_scene.Generation + 1));
			cleanup.Attempt(() => _scene.Coordinates.Configure("", default));
			MapUrl = null;
			Error = null;
			State = _session == null && _pump == null ? MapManagerState.Stopped : MapManagerState.Faulted;
			CompletedFrames = 0;
			cleanup.Attempt(_scene.MapChanged);
			cleanup.Complete("Could not clear the map completely.");
		}

		/// <summary>Releases the owned session; the supplied scene and builders remain caller-owned.</summary>
		public void Dispose()
		{
			if (_disposed)
				return;
			try { Clear(); }
			finally
			{
				_disposed = _session == null && _pump == null && _scene.InstanceCount == 0;
			}
		}
	}
}
