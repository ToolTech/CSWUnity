//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: CswMapStreamerHost.cs
// Description	: Explicit opt-in host for CSW command-driven map streaming
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Added opt-in command-driven host ownership and explicit platform teardown.	(2.12.327)
// AMO	261023	Presented the latest camera pose before post-presentation callbacks.	(2.12.327)
// AMO	261026	Applied native traversal settings and optional LateUpdate frame synchronization before rendering.	(2.12.327)
//
//*****************************************************************************

using System;
using GizmoSDK.GizmoBase;
using Saab.Foundation.Unity.MapStreamer.Modules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Saab.Foundation.Unity.MapStreamer.Csw
{
	/// <summary>Owns the opt-in command-driven session and its Unity presentation lifetime.</summary>
	/// <remarks>
	/// Start and stop on the Unity main thread. Assigned Unity components are borrowed.
	/// Manager, Scene and Coordinates are host-owned views; callers must not dispose them.
	/// </remarks>
	[DefaultExecutionOrder(-2000)]
	public sealed class CswMapStreamerHost : MonoBehaviour
	{
		[SerializeField]
		private bool _useCommandLane;
		public Camera ViewCamera;
		public UnityEngine.Transform TargetParent;
		public GeometryBuilderBase[] Builders = Array.Empty<GeometryBuilderBase>();
		public Behaviour[] LegacyComponents = Array.Empty<Behaviour>();
		public FoliageModule Foliage;
		public MapShadingModule TerrainShading;
		public StreamingDiagnosticsOverlay Diagnostics;
		public string MapUrl = "";
		public bool ReadConfiguration = true;
		public SceneManagerSettings Settings = SceneManagerSettings.Default;
		[Min(1)]
		public int MaximumCommandsPerTick = 128;
		[Min(1)]
		public double InitialAltitude = 500;
		public bool FlightInput = true;
		[Min(0)]
		public float FlightSpeed = 100;
		[Min(0)]
		public float TurnSpeed = 60;

		private bool _cswInitialized;
		private bool _coordinateInitialized;
		private bool _awakeComplete;
		private bool _stopping;
		private bool _streaming;

		public MapManager Manager { get; private set; }
		public UnityScene Scene { get; private set; }
		public SceneCoordinateContext Coordinates { get; private set; }
		public CameraBridge Camera { get; private set; }
		public bool UseCommandLane => _useCommandLane;

		private void Awake()
		{
			if (_useCommandLane)
				TryStart();
			_awakeComplete = true;
		}

		private void OnEnable()
		{
			if (_awakeComplete && _useCommandLane && Manager == null)
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
				Debug.LogException(exception, this);
				enabled = false;
			}
		}

		/// <summary>Starts owned native platforms and scene resources using borrowed Unity components.</summary>
		public void StartStreaming(string mapUrl = null)
		{
			if (Manager != null)
				throw new InvalidOperationException("This host already has a streaming session.");
			if (!ViewCamera)
				throw new InvalidOperationException("Assign the host's Unity camera explicitly.");
			if (Builders == null || Builders.Length == 0)
				throw new InvalidOperationException("Assign at least one geometry builder.");

			_useCommandLane = true;
			foreach (var component in LegacyComponents)
				if (component && component != this)
					component.enabled = false;

			try
			{
				_cswInitialized = CSW.Platform.Initialize();
				if (!_cswInitialized)
					throw new InvalidOperationException("CSW.Platform initialization failed.");
				_coordinateInitialized = GizmoSDK.Coordinate.Platform.Initialize();
				if (!_coordinateInitialized)
					throw new InvalidOperationException("Coordinate platform initialization failed.");

				if (ReadConfiguration)
					KeyDatabase.SetLocalRegistry("config.xml");
				var url = !string.IsNullOrWhiteSpace(mapUrl) ? mapUrl : MapUrl;
				if (string.IsNullOrWhiteSpace(url) && ReadConfiguration)
					url = KeyDatabase.GetDefaultUserKey("SceneManager/MapUrl", "");
				var loaders = ReadConfiguration
					? KeyDatabase.GetDefaultUserKey("SceneManager/DynamicLoaders", (uint)Settings.DynamicLoaders)
					: Settings.DynamicLoaders;

				Coordinates = new SceneCoordinateContext();
				Camera = new CameraBridge(ViewCamera, Coordinates)
				{
					SynchronizeFrames = Settings.SynchronizeFrames
				};
				Scene = new UnityScene(TargetParent ? TargetParent : transform, ViewCamera, Coordinates,
					intersectMask: Settings.IntersectMask);
				Scene.FrameCommitting += Camera.Commit;
				if (Foliage)
				{
					Foliage.SetPresentation(Scene);
					Foliage.SceneManager = null;
					if (Foliage.HiZDepthPyramid)
						Foliage.HiZDepthPyramid.TargetCamera = ViewCamera;
				}
				if (TerrainShading)
				{
					TerrainShading.SetPresentation(Scene);
					TerrainShading.SceneManager = null;
				}
				if (Diagnostics)
					Diagnostics.SetProvider(Scene);
				Manager = new MapManager(Scene, Builders, loaders)
				{
					MaximumCommandsPerTick = Math.Max(1, MaximumCommandsPerTick),
					BuildBudget = TimeSpan.FromSeconds(Math.Max(0.0001, Settings.MaxBuildTime)),
					OmniView = Settings.OmniView,
					SynchronizeFrames = Settings.SynchronizeFrames
				};
				Manager.CoordinateContextChanged += InitializeCamera;
				Manager.OpenMap(url);
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

		/// <summary>Replaces the current map session and resets the stored global camera pose.</summary>
		public void OpenMap(string url)
		{
			if (Manager == null)
				throw new InvalidOperationException("Start the streaming host before changing maps.");
			Camera.ResetPose();
			Manager.OpenMap(url);
		}

		/// <summary>Clears the current map while retaining this host and its presentation attachment.</summary>
		public void ClearMap()
		{
			Manager?.Clear();
			Camera?.ResetPose();
		}

		private void Update()
		{
			if (Manager == null || _stopping || !_streaming)
				return;
			Manager.OmniView = Settings.OmniView;
			Manager.SynchronizeFrames = Settings.SynchronizeFrames;
			Camera.SynchronizeFrames = Settings.SynchronizeFrames;
			if (FlightInput && Camera.HasPose)
				UpdateFlightInput();
			if (!Manager.SynchronizeFrames)
				Manager.Tick(Camera.HasPose
					? Camera.Sample(GizmoSDK.GizmoBase.Time.SystemSeconds)
					: (CameraRelativeFrame?)null);
		}

		private void LateUpdate()
		{
			if (_streaming && !_stopping && Manager?.SynchronizeFrames == true)
				Manager.Tick(Camera.HasPose
					? Camera.Sample(GizmoSDK.GizmoBase.Time.SystemSeconds)
					: (CameraRelativeFrame?)null);
			Camera?.PresentPose();
			Scene?.PostPresent();
		}

		private void BeforeCameraRender(UnityEngine.Camera camera)
		{
			if (_streaming && !_stopping && camera == ViewCamera && Camera?.SynchronizeFrames == true)
				Camera.PresentPose();
		}

		private void BeforeScriptableCameraRender(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			BeforeCameraRender(camera);
		}

		private void UpdateFlightInput()
		{
			var movement = new Vector3(
				(Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
				(Input.GetKey(KeyCode.Space) ? 1 : 0) - (Input.GetKey(KeyCode.LeftControl) ? 1 : 0),
				(Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
			var speed = Input.GetKey(KeyCode.LeftShift) ? FlightSpeed * 10 : FlightSpeed;
			if (movement.sqrMagnitude > 0)
				Camera.Move(movement.normalized * (speed * UnityEngine.Time.unscaledDeltaTime));
			var heading = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
			var pitch = (Input.GetKey(KeyCode.DownArrow) ? 1 : 0) - (Input.GetKey(KeyCode.UpArrow) ? 1 : 0);
			if (heading != 0 || pitch != 0)
				Camera.Rotate(
					heading * TurnSpeed * UnityEngine.Time.unscaledDeltaTime,
					pitch * TurnSpeed * UnityEngine.Time.unscaledDeltaTime);
		}

		/// <summary>Stops native work and releases owned session resources before platform teardown.</summary>
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
					try { Manager?.Dispose(); }
					finally
					{
						if (Manager?.IsDisposed == true)
							Manager = null;
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
					if (_coordinateInitialized && Coordinates == null)
					{
						if (!GizmoSDK.Coordinate.Platform.Uninitialize())
							throw new InvalidOperationException("Coordinate platform shutdown failed.");
						_coordinateInitialized = false;
					}
				});
				cleanup.Attempt(() =>
				{
					if (_cswInitialized && Manager == null)
					{
						if (!CSW.Platform.Uninitialize())
							throw new InvalidOperationException("CSW platform shutdown failed.");
						_cswInitialized = false;
					}
				});
				cleanup.Complete("Map streamer host cleanup failed.");
			}
			finally
			{
				_stopping = false;
			}
		}

		private void OnDisable()
		{
			if (_useCommandLane)
				StopStreaming();
		}

		private void OnDestroy()
		{
			StopStreaming();
		}
	}
}
