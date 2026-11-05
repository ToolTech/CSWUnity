//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: MapControl.cs
// Description	: Unity component that controls the loading of CSW maps
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Added opt-in command-driven host ownership and explicit platform teardown.	(2.12.327)
// AMO	261023	Presented the latest camera pose before post-presentation callbacks.	(2.12.327)
// AMO	261026	Applied native traversal settings and optional LateUpdate frame synchronization before rendering.	(2.12.327)
// AMO	261028	Removed the legacy lane; the host starts in Start and prefers its MapUrl over config.xml.	(2.12.327)
// AMO	261030	Stopped dynamic loaders before the last host releases its platforms, so DB managers outlive in-flight loads.	(2.12.327)
// AMO	261030	Applied the synchronized frame wait timeout setting every update.	(2.12.327)
// AMO	261103	Configured the occurrence pool cap and prewarmed the pools within a time budget each update.	(2.12.327)
// AMO	261103	Applied the infinite far plane, LOD factor and LOD reference field of view settings every update.	(2.12.327)
// AMO	261103	Renamed from CswMapStreamerHost; the Csw namespace carries the CSW name.	(2.12.327)
// AMO	261104	Moved to namespace CSW.Unity.StreamingMap.	(2.12.327)
// AMO	261104	Held a CSW.Unity.Platform reference instead of initializing platforms and the registry.	(2.12.327)
// AMO	261104	Created the session CSW.Unity.Camera through an optional CameraFactory and presented before render while it owns the projection.	(2.12.327)
// AMO	261104	Sent start failures through CSW.Unity.Platform.SendException.	(2.12.327)
// AMO	261104	Renamed from MapStreamerHost to CSW.Unity.MapControl, which owns the internal MapSession, reports its state and committed frames and moved the flight input to CSW.Unity.FlightInput.	(2.12.327)
// AMO	261104	Documented the GizmoSDK expansion of %VARIABLE% and $(key) in the map URL.	(2.12.327)
// AMO	261105	Applied the loader-thread mesh preparation setting to new sessions.	(2.12.327)
//
//*****************************************************************************

using CSW.Unity.StreamingMap;
using System;
using GizmoSDK.GizmoBase;
using UnityEngine;
using UnityEngine.Rendering;

namespace CSW.Unity
{
	/// <summary>Lifecycle state of the map of a CSW.Unity.MapControl.</summary>
	public enum MapControlState
	{
		Stopped,
		Loading,
		Running,
		Faulted
	}

	/// <summary>Controls the loading of CSW maps through the SceneManager and owns their Unity presentation lifetime.</summary>
	/// <remarks>
	/// Runs at most one streaming session. Starts in Start unless a caller has already started it, stops when
	/// disabled and restarts when re-enabled.
	/// The map URL is the explicit StartStreaming argument, otherwise MapUrl, otherwise SceneManager/MapUrl in config.xml.
	/// GizmoSDK expands %VARIABLE% environment variables and $(key) registry keys in the URL when it loads the map.
	/// Start and stop on the Unity main thread. Assigned Unity components are borrowed.
	/// Scene and Coordinates are views owned by the map control; callers must not dispose them.
	/// </remarks>
	[DefaultExecutionOrder(-2000)]
	public sealed class MapControl : MonoBehaviour
	{
		public UnityEngine.Camera ViewCamera;
		public UnityEngine.Transform TargetParent;
		public GeometryBuilderBase[] Builders = Array.Empty<GeometryBuilderBase>();
		public FoliageModule Foliage;
		public MapShadingModule TerrainShading;
		public StreamingDiagnosticsOverlay Diagnostics;
		[Tooltip("Map URL to stream. When empty, SceneManager/MapUrl in config.xml is used. GizmoSDK expands %VARIABLE% and $(key), for example %CSWUNITY_TEST_MAP%.")]
		public string MapUrl = "";
		[Tooltip("Apply config.xml overrides such as SceneManager/DynamicLoaders. An empty MapUrl always uses config.xml.")]
		public bool ReadConfiguration = true;
		public SceneManagerSettings Settings = SceneManagerSettings.Default;
		[Min(1)]
		public int MaximumCommandsPerTick = 128;
		[Tooltip("Free GameObjects kept in each occurrence pool; returned GameObjects beyond this cap are destroyed.")]
		[Min(0)]
		public int MaximumPooledObjects = 4096;
		[Tooltip("Free GameObjects that each occurrence pool is filled to ahead of demand. Zero disables prewarming.")]
		[Min(0)]
		public int PooledObjectReserve = 1024;
		[Tooltip("Longest time in seconds spent prewarming the occurrence pools in each Unity update.")]
		[Min(0)]
		public float PoolPrewarmTime = 0.001f;
		[Min(1)]
		public double InitialAltitude = 500;

		private PlatformReference _platform;
		private bool _startCalled;
		private bool _stopping;
		private bool _streaming;

		internal MapSession Session { get; private set; }
		/// <summary>State of the current map; Stopped when no session runs.</summary>
		public MapControlState State => Session?.State ?? MapControlState.Stopped;
		/// <summary>URL of the map that the session loads or shows; null when no map is open.</summary>
		public string LoadedMapUrl => Session?.MapUrl;
		/// <summary>Message of the failure that faulted the current map; null otherwise.</summary>
		public string Error => Session?.Error;
		/// <summary>Number of camera frames that the current map has completed.</summary>
		public ulong CompletedFrames => Session?.CompletedFrames ?? 0;
		/// <summary>Raised on the Unity main thread with the message of a failure that faults the current map.</summary>
		public event Action<string> Failed;
		/// <summary>Raised on the Unity main thread with the camera frame of each native frame that the current map commits.</summary>
		public event Action<CameraFrame> FrameCommitted;
		public UnityScene Scene { get; private set; }
		public SceneCoordinateContext Coordinates { get; private set; }
		public Camera Camera { get; private set; }
		/// <summary>Creates the session camera from the view camera and the map coordinates; null selects CSW.Unity.Camera.</summary>
		/// <remarks>Assign before the session starts, for example in Awake, to install an application camera.</remarks>
		public Func<UnityEngine.Camera, SceneCoordinateContext, Camera> CameraFactory { get; set; }

		private void Start()
		{
			_startCalled = true;
			if (Session == null)
				TryStart();
		}

		private void OnEnable()
		{
			if (_startCalled && Session == null)
				TryStart();
		}

		private void TryStart()
		{
			try
			{
				StartStreaming();
			}
			catch (Exception exception)
			{
				Platform.SendException(exception);
				enabled = false;
			}
		}

		/// <summary>Acquires a platform reference and starts owned scene resources using borrowed Unity components.</summary>
		/// <param name="mapUrl">Explicit map URL. When blank, MapUrl and then SceneManager/MapUrl in config.xml are used.</param>
		public void StartStreaming(string mapUrl = null)
		{
			if (Session != null)
				throw new InvalidOperationException("This map control already has a streaming session.");
			if (!ViewCamera)
				throw new InvalidOperationException("Assign the view camera of the map control explicitly.");
			if (Builders == null || Builders.Length == 0)
				throw new InvalidOperationException("Assign at least one geometry builder.");

			try
			{
				_platform = Platform.Acquire();

				var url = ResolveMapUrl(mapUrl, MapUrl, ReadConfiguredMapUrl);
				var loaders = ReadConfiguration
					? KeyDatabase.GetDefaultUserKey("SceneManager/DynamicLoaders", (uint)Settings.DynamicLoaders)
					: Settings.DynamicLoaders;

				Coordinates = new SceneCoordinateContext();
				Camera = CameraFactory != null
					? CameraFactory(ViewCamera, Coordinates) ??
						throw new InvalidOperationException("The camera factory returned no camera.")
					: new Camera(ViewCamera, Coordinates);
				if (Camera.UnityCamera != ViewCamera)
					throw new InvalidOperationException("The installed camera must present through the view camera of the map control.");
				Camera.SynchronizeFrames = Settings.SynchronizeFrames;
				Camera.InfiniteFarPlane = Settings.InfiniteFarPlane;
				Camera.LodReferenceFieldOfView = Settings.LodReferenceFieldOfView;
				Scene = new UnityScene(TargetParent ? TargetParent : transform, ViewCamera, Coordinates,
					Math.Max(0, MaximumPooledObjects), Settings.IntersectMask);
				Scene.FrameCommitting += Camera.Commit;
				if (Foliage)
				{
					Foliage.SetPresentation(Scene);
					if (Foliage.HiZDepthPyramid)
						Foliage.HiZDepthPyramid.TargetCamera = ViewCamera;
				}
				if (TerrainShading)
					TerrainShading.SetPresentation(Scene);
				if (Diagnostics)
					Diagnostics.SetProvider(Scene);
				Session = new MapSession(Scene, Builders, loaders)
				{
					MaximumCommandsPerTick = Math.Max(1, MaximumCommandsPerTick),
					BuildBudget = TimeSpan.FromSeconds(Math.Max(0.0001, Settings.MaxBuildTime)),
					OmniView = Settings.OmniView,
					PrepareMeshesOnLoaders = Settings.PrepareMeshesOnLoaders,
					SynchronizeFrames = Settings.SynchronizeFrames,
					FrameWaitTimeout = ToFrameWaitTimeout(Settings.FrameWaitTimeout)
				};
				Session.CoordinateContextChanged += InitializeCamera;
				Session.Failed += OnSessionFailed;
				Session.FrameCommitted += OnSessionFrameCommitted;
				Session.OpenMap(url);
				_streaming = true;
				UnityEngine.Camera.onPreCull += BeforeCameraRender;
				RenderPipelineManager.beginCameraRendering += BeforeScriptableCameraRender;
			}
			catch
			{
				StopStreaming();
				throw;
			}
		}

		private void InitializeCamera()
		{
			if (Coordinates.IsConfigured)
				Camera.InitializeFromMap(InitialAltitude);
		}

		private void OnSessionFailed(string message)
		{
			Failed?.Invoke(message);
		}

		private void OnSessionFrameCommitted(CameraFrame frame)
		{
			FrameCommitted?.Invoke(frame);
		}

		/// <summary>Returns the explicit URL, otherwise the component URL, otherwise the configured URL.</summary>
		/// <remarks>The configuration reader is only invoked when both URLs are blank.</remarks>
		internal static string ResolveMapUrl(string explicitUrl, string componentUrl, Func<string> readConfiguredUrl)
		{
			if (!string.IsNullOrWhiteSpace(explicitUrl))
				return explicitUrl;
			if (!string.IsNullOrWhiteSpace(componentUrl))
				return componentUrl;
			var url = readConfiguredUrl();
			if (string.IsNullOrWhiteSpace(url))
				throw new InvalidOperationException(
					"No map URL: set MapUrl on the map control or SceneManager/MapUrl in config.xml.");
			return url;
		}

		private string ReadConfiguredMapUrl()
		{
			return KeyDatabase.GetDefaultUserKey("SceneManager/MapUrl", "");
		}

		/// <summary>Converts the timeout setting in seconds; values that are not positive and finite become zero.</summary>
		/// <remarks>A zero timeout faults the next synchronized Tick once instead of throwing every update.</remarks>
		internal static TimeSpan ToFrameWaitTimeout(double seconds)
		{
			return seconds > 0 && seconds <= int.MaxValue / 1000.0
				? TimeSpan.FromTicks(Math.Max(1L, (long)(seconds * TimeSpan.TicksPerSecond)))
				: TimeSpan.Zero;
		}

		/// <summary>Returns a positive finite LOD factor setting, otherwise the native default 1.</summary>
		internal static float ToLodFactor(float value)
		{
			return value > 0 && float.IsFinite(value) ? value : 1;
		}

		/// <summary>Replaces the current map session and resets the stored global camera pose.</summary>
		public void OpenMap(string url)
		{
			if (Session == null)
				throw new InvalidOperationException("Start the map control before changing maps.");
			Camera.ResetPose();
			Session.OpenMap(url);
		}

		/// <summary>Clears the current map while retaining this map control and its presentation attachment.</summary>
		public void ClearMap()
		{
			Session?.Clear();
			Camera?.ResetPose();
		}

		private void Update()
		{
			if (Session == null || _stopping || !_streaming)
				return;
			Session.OmniView = Settings.OmniView;
			Session.SynchronizeFrames = Settings.SynchronizeFrames;
			Session.FrameWaitTimeout = ToFrameWaitTimeout(Settings.FrameWaitTimeout);
			Camera.SynchronizeFrames = Settings.SynchronizeFrames;
			Camera.InfiniteFarPlane = Settings.InfiniteFarPlane;
			Camera.LodReferenceFieldOfView = Settings.LodReferenceFieldOfView;
			if (!Session.SynchronizeFrames)
				Session.Tick(SampleCamera());
			if (PooledObjectReserve > 0 && PoolPrewarmTime > 0)
				Scene.PrewarmPools(PooledObjectReserve, TimeSpan.FromSeconds(PoolPrewarmTime));
		}

		private void LateUpdate()
		{
			if (_streaming && !_stopping && Session?.SynchronizeFrames == true)
				Session.Tick(SampleCamera());
			Camera?.Present();
			Scene?.PostPresent();
		}

		private CameraFrame? SampleCamera()
		{
			return Camera.HasPose
				? Camera.Sample(GizmoSDK.GizmoBase.Time.SystemSeconds, ToLodFactor(Settings.LodFactor))
				: (CameraFrame?)null;
		}

		private void BeforeCameraRender(UnityEngine.Camera camera)
		{
			if (_streaming && !_stopping && camera == ViewCamera && Camera?.OwnsProjection == true)
				Camera.Present();
		}

		private void BeforeScriptableCameraRender(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			BeforeCameraRender(camera);
		}

		/// <summary>Stops native work and releases owned session resources before the platform reference.</summary>
		/// <remarks>
		/// The reference is released only after the session and the coordinate context; the last released reference
		/// stops the dynamic loaders before CSW.Platform unregisters the DB managers. The next session's SetLoaders
		/// command starts them again.
		/// </remarks>
		public void StopStreaming()
		{
			if (_stopping)
				return;
			_stopping = true;
			_streaming = false;
			UnityEngine.Camera.onPreCull -= BeforeCameraRender;
			RenderPipelineManager.beginCameraRendering -= BeforeScriptableCameraRender;
			try
			{
				var cleanup = new SceneCleanup();
				cleanup.Attempt(() =>
				{
					try { Session?.Dispose(); }
					finally
					{
						if (Session?.IsDisposed == true)
							Session = null;
					}
				});
				cleanup.Attempt(() => { if (Foliage) Foliage.SetPresentation(null); });
				cleanup.Attempt(() => { if (TerrainShading) TerrainShading.SetPresentation(null); });
				cleanup.Attempt(() => { if (Diagnostics) Diagnostics.SetProvider(null); });
				cleanup.Attempt(() =>
				{
					try { Scene?.Dispose(); }
					finally { Scene = null; }
				});
				cleanup.Attempt(() =>
				{
					Coordinates?.Dispose();
					Coordinates = null;
				});
				cleanup.Attempt(() => Camera?.ResetPose());
				Camera = null;
				cleanup.Attempt(() =>
				{
					if (_platform != null && Session == null && Coordinates == null)
					{
						var platform = _platform;
						_platform = null;
						platform.Dispose();
					}
				});
				cleanup.Complete("Map control cleanup failed.");
			}
			finally
			{
				_stopping = false;
			}
		}

		private void OnDisable()
		{
			StopStreaming();
		}

		private void OnDestroy()
		{
			StopStreaming();
		}
	}
}
