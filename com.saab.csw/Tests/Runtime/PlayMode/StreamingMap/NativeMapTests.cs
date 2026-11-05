//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: NativeMapTests.cs
// Description	: Injected real-map rendering, flight, scope and reload acceptance
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Command-lane map, camera and lifecycle acceptance	(2.12.327)
// AMO	261023	Repeated real-map queued-output and platform lifetimes	(2.12.327)
// AMO	261026	Verified synchronized Omni/frustum rendering, runtime switches, projection cleanup and timeout faults.	(2.12.327)
// AMO	261028	Removed legacy-lane checks and covered Start with the component MapUrl.	(2.12.327)
// AMO	261030	Verified pipelined synchronization lockstep, the host timeout setting and the last host's loader stop.	(2.12.327)
// AMO	261103	Verified scope Off through committed visibility and renderer state on the real map.	(2.12.327)
// AMO	261103	Verified committed far plane modes, zoom-scaled LOD factors and release of the infinite projection on the real map.	(2.12.327)
// AMO	261103	Renamed from CswNativeMapTests into the Csw test namespace.	(2.12.327)
// AMO	261104	Moved to namespace CSW.Unity.Tests.	(2.12.327)
// AMO	261104	Converts through CSW.Unity.Conversion.	(2.12.327)
// AMO	261104	Uses CSW.Unity.Camera and tests an application camera from CameraFactory.	(2.12.327)
// AMO	261104	Expects the timeout exception as a FATAL message.	(2.12.327)
// AMO	261104	Drives CSW.Unity.MapControl, checks its forwarded frames and the CSW.Unity.FlightInput of the example scene.	(2.12.327)
// AMO	261104	Analyzes holes, frame and commit timing, GC and locked finalizers during fast full motion.	(2.12.327)
// AMO	261105	Reported main-thread mesh builds, loader-thread mesh preparation and per-frame marker times in load bursts during fast full motion.	(2.12.327)
//
//*****************************************************************************

using CSW.Unity.StreamingMap;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CSW.Unity.Tests
{
	public sealed class NativeMapTests
	{
		private GameObject _controlObject;
		private GameObject _cameraObject;
		private MapControl _control;
		private UnityEngine.Camera _camera;
		private Material _template;
		private RenderTexture _target;
		private AmbientMode _ambientMode;
		private Color _ambientColor;
		private Color32[] _emptyPixels;
		private Vector4 _worldOffset;
		private Matrix4x4 _localToEun;
		private Scene _exampleScene;
		private bool _fixtureCswInitialized;
		private bool _fixtureCoordinateInitialized;
		private static readonly Color Background = new Color(0.95f, 0.05f, 0.95f, 1);

		[SetUp]
		public void SetUp()
		{
			_ambientMode = RenderSettings.ambientMode;
			_ambientColor = RenderSettings.ambientLight;
			_worldOffset = Shader.GetGlobalVector("_WorldOffset");
			_localToEun = Shader.GetGlobalMatrix("_LocalToEUN");
			RenderSettings.ambientMode = AmbientMode.Flat;
			RenderSettings.ambientLight = Color.white;
			_cameraObject = new GameObject("Acceptance camera");
			_camera = _cameraObject.AddComponent<UnityEngine.Camera>();
			_camera.clearFlags = CameraClearFlags.SolidColor;
			_camera.backgroundColor = Background;
			_camera.nearClipPlane = 1;
			_camera.farClipPlane = 50000;
			_camera.fieldOfView = 60;
			_camera.renderingPath = RenderingPath.DeferredShading;
			_target = new RenderTexture(320, 240, 24, RenderTextureFormat.ARGB32);
			Assert.That(_target.Create(), Is.True);
			_camera.targetTexture = _target;
			Capture("empty");
			_controlObject = new GameObject("Acceptance map control");
			_control = _controlObject.AddComponent<MapControl>();
			_control.enabled = false;
			_control.ViewCamera = _camera;
			_control.ReadConfiguration = false;
			_control.InitialAltitude = 500;
			_template = new Material(Shader.Find("Standard"));
			var terrain = _controlObject.AddComponent<TerrainNodeBuilder>();
			var assets = _controlObject.AddComponent<AssetNodeBuilder>();
			SceneResourceTests.Configure(terrain, _template);
			SceneResourceTests.Configure(assets, _template);
			_control.Builders = new GeometryBuilderBase[] { terrain, assets };
		}

		[TearDown]
		public void TearDown()
		{
			_control?.StopStreaming();
			_camera.targetTexture = null;
			_target.Release();
			Object.Destroy(_target);
			Object.Destroy(_template);
			Object.Destroy(_controlObject);
			Object.Destroy(_cameraObject);
			Texture2DCache.Clear();
			RenderSettings.ambientMode = _ambientMode;
			RenderSettings.ambientLight = _ambientColor;
			Shader.SetGlobalVector("_WorldOffset", _worldOffset);
			Shader.SetGlobalMatrix("_LocalToEUN", _localToEun);
		}

		[UnityTearDown]
		public IEnumerator ReleaseExampleScene()
		{
			if (_exampleScene.IsValid())
			{
				_control?.StopStreaming();
				if (_camera)
					_camera.targetTexture = null;
				_control = _controlObject.GetComponent<MapControl>();
				_camera = _cameraObject.GetComponent<UnityEngine.Camera>();
				yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(_exampleScene);
				_exampleScene = default;
			}
			if (_fixtureCoordinateInitialized)
			{
				Assert.That(GizmoSDK.Coordinate.Platform.Uninitialize(), Is.True);
				_fixtureCoordinateInitialized = false;
			}
			if (_fixtureCswInitialized)
			{
				Assert.That(CSW.Platform.Uninitialize(), Is.True);
				_fixtureCswInitialized = false;
			}
		}

		[UnityTest]
		public IEnumerator RealMapRendersNavigatesLeavesScopeReturnsAndReloads()
		{
			var url = Environment.GetEnvironmentVariable("CSWUNITY_TEST_MAP");
			if (string.IsNullOrWhiteSpace(url))
				Assert.Inconclusive("Set CSWUNITY_TEST_MAP to a published GZD fixture URL to run real-map acceptance.");
			var example = Environment.GetEnvironmentVariable("CSWUNITY_TEST_SCENE");
			if (string.IsNullOrWhiteSpace(example))
				StartHost(url);
			else
				yield return StartInjectedExampleComposition(example, url);
			GC.Collect();
			GC.WaitForPendingFinalizers();
			if (_exampleScene.IsValid())
			{
				Assert.That(_control.Foliage.Presentation, Is.SameAs(_control.Scene));
				Assert.That(_control.TerrainShading.Presentation, Is.SameAs(_control.Scene));
				if (_control.Foliage.HiZDepthPyramid)
					Assert.That(_control.Foliage.HiZDepthPyramid.TargetCamera, Is.SameAs(_camera));
			}
			yield return WaitFor(() => _control.Scene.MeshCount > 0 &&
				_control.Session.CompletedFrames >= 3 && ActiveRenderers().Any(), "initial map realization");
			var scene = _control.Scene;
			var root = scene.Instances.First(handle =>
				handle.Key.Node != IntPtr.Zero && handle.ParentKey.Equals(SceneInstanceKey.Root));
			var rootId = root.gameObject.GetInstanceID();
			Assert.That(root.IsVisible, Is.True);
			Assert.That(scene.Coordinates.IsConfigured, Is.True);
			Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
			var initialPose = _control.Camera.GlobalPosition;
			var initialRotation = _control.Camera.Rotation;
			var initialPosition = root.transform.localPosition;
			var before = ActiveRenderers().ToDictionary(
				handle => handle.Key,
				handle => handle.GetComponent<MeshFilter>().sharedMesh);
			var initialPixels = Capture("initial");
			Assert.That(initialPixels, Is.GreaterThan(64), "The real camera must see map pixels, not only allocated meshes.");

			var completed = _control.Session.CompletedFrames;
			var committed = 0UL;
			_control.FrameCommitted += frame => committed++;
			var displacement = new Vector3(25, 0, 0);
			var nativeDisplacement = Conversion.ToGizmoDirection(initialRotation * displacement);
			var sample = _control.Camera.Sample(0);
			TestContext.WriteLine($"CRS={scene.Coordinates.Definition}");
			TestContext.WriteLine(FormattableString.Invariant(
				$"Initial position=({initialPose.x:R}, {initialPose.y:R}, {initialPose.z:R})"));
			TestContext.WriteLine(FormattableString.Invariant(
				$"HPR=({sample.Hpr.x:R}, {sample.Hpr.y:R}, {sample.Hpr.z:R})"));
			TestContext.WriteLine(FormattableString.Invariant(
				$"Native movement=({nativeDisplacement.x:R}, {nativeDisplacement.y:R}, {nativeDisplacement.z:R})"));
			_control.Camera.Move(displacement);
			yield return WaitFor(() => _control.Session.CompletedFrames >= completed + 3, "camera movement");
			Assert.That(committed, Is.EqualTo(_control.CompletedFrames - completed),
				"MapControl must forward every committed frame.");
			var expectedPosition = initialPosition -
				new Vector3(nativeDisplacement.x, nativeDisplacement.y, nativeDisplacement.z);
			Debug.Log($"CSW map acceptance: root movement actual={root.transform.localPosition}, " +
				$"expected={expectedPosition}, error={Vector3.Distance(root.transform.localPosition, expectedPosition)}");
			Assert.That(Vector3.Distance(root.transform.localPosition, expectedPosition),
				Is.LessThan(0.05f), "Native ROI transforms must carry the double camera motion.");
			Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
			var retained = 0;
			foreach (var instance in scene.Instances)
			{
				if (before.TryGetValue(instance.Key, out var mesh))
				{
					Assert.That(instance.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
					retained++;
				}
			}
			Assert.That(retained, Is.GreaterThan(0),
				"Flight must preserve existing occurrence mesh leases, including LOD-Off occurrences.");
			Assert.That(Capture("moved"), Is.GreaterThan(64));
			completed = _control.Session.CompletedFrames;
			_control.Camera.Rotate(15, -5);
			yield return WaitFor(() => _control.Session.CompletedFrames >= completed + 3, "camera rotation");
			Assert.That(Quaternion.Angle(_camera.transform.rotation, initialRotation), Is.GreaterThan(1));
			Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
			Assert.That(Capture("rotated"), Is.GreaterThan(64));
			var rotated = _control.Camera.Sample(0);
			TestContext.WriteLine(FormattableString.Invariant(
				$"Rotated HPR=({rotated.Hpr.x:R}, {rotated.Hpr.y:R}, {rotated.Hpr.z:R})"));
			completed = _control.Session.CompletedFrames;
			GC.Collect();
			GC.WaitForPendingFinalizers();
			yield return WaitFor(() => _control.Session.CompletedFrames >= completed + 32,
				"sustained one-outstanding refresh progress");
			// Leave native scope without leaving a projected CRS's horizontal domain.
			var up = scene.Coordinates.Orientation(initialPose).GetCol(2);
			var outside = initialPose + (GizmoSDK.GizmoBase.Vec3D)up * 100000000;
			scene.Coordinates.ToGeodetic(outside);
			_control.Camera.SetPose(outside, initialRotation);
			yield return WaitFor(() => !root.IsVisible, "native root scope Off");
			Assert.That(root.gameObject.GetInstanceID(), Is.EqualTo(rootId));
			Assert.That(root.IsLocallyActive, Is.False);
			Assert.That(root.gameObject.activeInHierarchy, Is.True,
				"A hidden occurrence keeps its GameObject active (REQ-CSWU-PERF-012).");
			Assert.That(root.GetComponentsInChildren<MeshRenderer>().All(renderer => renderer.forceRenderingOff),
				Is.True, "Scope Off must stop rendering of the whole subtree.");
			_control.Camera.SetPose(initialPose, initialRotation);
			yield return WaitFor(() => root.IsVisible &&
				ActiveRenderers().Any(), "native root re-entry");
			Assert.That(root.gameObject.GetInstanceID(), Is.EqualTo(rootId));
			Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
			Assert.That(Capture("returned"), Is.GreaterThan(64));

			var definition = scene.Coordinates.Definition;
			var generation = scene.Generation;
			_control.ClearMap();
			Assert.That(_control.Session.State, Is.EqualTo(MapControlState.Stopped));
			Assert.That(_control.Session.PendingBuffers, Is.Zero);
			Assert.That(scene.InstanceCount, Is.Zero);
			Assert.That(scene.MeshCount, Is.Zero);
			Assert.That(scene.MaterialCount, Is.Zero);
			Assert.That(scene.Generation, Is.GreaterThan(generation));
			Assert.That(scene.Coordinates.IsConfigured, Is.False);
			_control.OpenMap(url);
			yield return WaitFor(() => _control.Session.CompletedFrames >= 3 &&
				ActiveRenderers().Any(), "clear/reload with a fresh native session");
			Assert.That(scene.Coordinates.Definition, Is.EqualTo(definition));
			Assert.That(Capture("reloaded"), Is.GreaterThan(64));
			TestContext.WriteLine(
				$"Map={url}; CRS={definition}; frames={_control.Session.CompletedFrames}; " +
				$"instances={scene.InstanceCount}; meshes={scene.MeshCount}; materials={scene.MaterialCount}; " +
				$"initialPixels={initialPixels}; retainedMovingMeshes={retained}; scopeRoot={rootId}; " +
				$"UnityCamera={_camera.transform.position}");
			_control.StopStreaming();
			_control.StopStreaming();
			Assert.That(_control.Session, Is.Null);
			Assert.That(_control.Scene, Is.Null);
			Assert.That(_controlObject, Is.Not.Null);
			Assert.That(_camera, Is.Not.Null);
			yield return null;
		}

		/// <summary>One presented frame of the fast-motion flight; readback fields are filled asynchronously.</summary>
		private sealed class MotionSample
		{
			public int Frame;
			public double Time;
			public float Delta;
			public float Speed;
			public double CommitAge;
			public float CommitLag;
			public int Collections;
			public long Allocated;
			public bool Probed;
			public bool Read;
			public int MustHit;
			public int Holes;
			public int Sky;
			public int SkyBackground;
			public int Instances;
			public int Meshes;
			public double[] Markers;
		}

		// Main-thread markers recorded per frame during fast motion; "Main Thread" is Unity's CPU frame time.
		private static readonly (ProfilerCategory category, string name)[] FrameMarkers =
		{
			(ProfilerCategory.Internal, "Main Thread"),
			(ProfilerCategory.Render, "CSW-Tick"),
			(ProfilerCategory.Render, "CSW-Capture"),
			(ProfilerCategory.Render, "CSW-Publish"),
			(ProfilerCategory.Render, "CSW-PublishResources"),
			(ProfilerCategory.Render, "CSW-CommitFrame"),
			(ProfilerCategory.Render, "SM-BuildGeometry"),
			(ProfilerCategory.Render, "SM-BuildGeometry-Prepared"),
			(ProfilerCategory.Render, "SM-ReadTexture"),
		};

		[UnityTest]
		public IEnumerator FastFullMotionKeepsTheMapClosedSmoothAndFinalizerFree()
		{
			if (!SystemInfo.supportsAsyncGPUReadback)
				Assert.Inconclusive("Fast-motion hole analysis needs asynchronous GPU readback.");
			try
			{
				// CSWUNITY_PREPARE_MESHES=0 builds every mesh from the graph on the main thread for A/B runs.
				_control.Settings.PrepareMeshesOnLoaders = Environment.GetEnvironmentVariable("CSWUNITY_PREPARE_MESHES") != "0";
				StartHost(null);
			}
			catch (InvalidOperationException error) when (error.Message.StartsWith("No map URL", StringComparison.Ordinal))
			{
				Assert.Inconclusive("Set MapUrl on the map control or SceneManager/MapUrl in config.xml, for example %CSWUNITY_TEST_MAP%.");
			}
			var url = _control.LoadedMapUrl;
			if (System.Text.RegularExpressions.Regex.IsMatch(Environment.ExpandEnvironmentVariables(url), "%[A-Za-z_][A-Za-z0-9_]*%"))
				Assert.Inconclusive($"The map URL {url} names an unset environment variable.");
			yield return WaitFor(() => _control.Scene.MeshCount > 0 &&
				_control.Session.CompletedFrames >= 3 && ActiveRenderers().Any(), "initial map realization");
			var settled = _control.Session.CompletedFrames;
			var settleTime = UnityEngine.Time.realtimeSinceStartupAsDouble + 2;
			yield return WaitFor(() => _control.Session.CompletedFrames >= settled + 30 &&
				UnityEngine.Time.realtimeSinceStartupAsDouble >= settleTime, "initial map coverage");

			// Map box from the realized renderers in the map's east/north/up basis at the origin.
			var coordinates = _control.Coordinates;
			var origin = coordinates.Origin;
			var basis = coordinates.Orientation(origin);
			var east = Conversion.ToUnityDirection(basis.GetCol(0));
			var north = Conversion.ToUnityDirection(basis.GetCol(1));
			var up = Conversion.ToUnityDirection(basis.GetCol(2));
			var reference = Conversion.ToUnityPosition(_control.Camera.GlobalPosition - origin) - _camera.transform.position;
			var low = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
			var high = new Vector3(float.MinValue, float.MinValue, float.MinValue);
			foreach (var handle in ActiveRenderers())
			{
				var bounds = handle.GetComponent<MeshRenderer>().bounds;
				for (var corner = 0; corner < 8; corner++)
				{
					var point = reference + bounds.center + Vector3.Scale(bounds.extents, new Vector3(
						(corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
					var local = new Vector3(Vector3.Dot(point, east), Vector3.Dot(point, north), Vector3.Dot(point, up));
					low = Vector3.Min(low, local);
					high = Vector3.Max(high, local);
				}
			}
			var center = (low + high) * 0.5f;
			var half = (high - low) * 0.5f;
			if (Mathf.Min(half.x, half.y) < 2000)
				Assert.Inconclusive($"Fast-motion analysis needs a map of at least 4 km; realized half extents are {half}.");
			var inner = new Vector2(half.x * 0.75f, half.y * 0.75f);
			var radius = Mathf.Clamp(0.12f * Mathf.Min(half.x, half.y), 400, 1500);
			var cruise = high.z + 550;
			const float Speed = 300;
			const double Duration = 12;
			var angularRate = Speed / radius;

			// Full motion: circling at Speed, climbing and descending at up to 314 m/s, yawing +-35 degrees and pitching.
			(GizmoSDK.GizmoBase.Vec3D position, Quaternion rotation) PoseAt(double time)
			{
				var phase = (float)(time * angularRate);
				var radial = Mathf.Cos(phase) * east + Mathf.Sin(phase) * north;
				var track = -Mathf.Sin(phase) * east + Mathf.Cos(phase) * north;
				var altitude = cruise + 300 * Mathf.Sin((float)(2 * Math.PI * time / 6));
				var yaw = 35 * Mathf.Sin((float)(2 * Math.PI * time / 4));
				var pitch = (-35 + 10 * Mathf.Sin((float)(2 * Math.PI * time / 3))) * Mathf.Deg2Rad;
				var heading = Quaternion.AngleAxis(yaw, up) * track;
				var forward = Mathf.Cos(pitch) * heading + Mathf.Sin(pitch) * up;
				var offset = center.x * east + center.y * north + radius * radial + altitude * up;
				return (origin + Conversion.ToGizmoPosition(offset), Quaternion.LookRotation(forward, up));
			}

			var start = PoseAt(0);
			_control.Camera.SetPose(start.position, start.rotation);
			settled = _control.Session.CompletedFrames;
			settleTime = UnityEngine.Time.realtimeSinceStartupAsDouble + 3;
			yield return WaitFor(() => _control.Session.CompletedFrames >= settled + 30 &&
				UnityEngine.Time.realtimeSinceStartupAsDouble >= settleTime && ActiveRenderers().Any(), "start pose coverage");
			Assert.That(Capture("fast-motion-start"), Is.GreaterThan(64));
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();

			// Must-hit rays descend at least 2 degrees and reach the lowest map level inside the inner map box,
			// so they cross terrain before leaving the map; background colour there is a hole.
			var width = _target.width;
			var height = _target.height;
			const int Step = 2;
			var tanHalf = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
			var aspect = (float)width / height;
			var rays = new List<Vector3>();
			var pixelIndices = new List<int>();
			for (var y = Step / 2; y < height; y += Step)
				for (var x = Step / 2; x < width; x += Step)
				{
					rays.Add(new Vector3((2 * (x + 0.5f) / width - 1) * tanHalf * aspect,
						(2 * (y + 0.5f) / height - 1) * tanHalf, 1).normalized);
					pixelIndices.Add(y * width + x);
				}
			var rayDirections = rays.ToArray();
			var rayPixels = pixelIndices.ToArray();
			var background = _emptyPixels[_emptyPixels.Length / 2];
			var minimumDescent = Mathf.Sin(2 * Mathf.Deg2Rad);
			var maximumDistance = _camera.farClipPlane * 0.9f;

			bool MustHit(int ray, Vector3 eye, Quaternion view)
			{
				var direction = view * rayDirections[ray];
				var descent = Vector3.Dot(direction, up);
				if (descent > -minimumDescent)
					return false;
				var distance = (low.z - Vector3.Dot(eye, up)) / descent;
				if (distance > maximumDistance)
					return false;
				var hit = eye + direction * distance;
				return Mathf.Abs(Vector3.Dot(hit, east) - center.x) <= inner.x &&
					Mathf.Abs(Vector3.Dot(hit, north) - center.y) <= inner.y;
			}

			bool IsBackground(Color32 pixel)
			{
				return Math.Abs(pixel.r - background.r) <= 16 && Math.Abs(pixel.g - background.g) <= 16 &&
					Math.Abs(pixel.b - background.b) <= 16;
			}

			var worstPixels = new Color32[width * height];
			MotionSample worst = null;
			var worstEye = Vector3.zero;
			var worstView = Quaternion.identity;

			void Analyze(AsyncGPUReadbackRequest request, MotionSample sample, Vector3 eye, Quaternion view)
			{
				if (request.hasError)
					return;
				var pixels = request.GetData<Color32>();
				var mustHit = 0;
				var holes = 0;
				var sky = 0;
				var skyBackground = 0;
				for (var ray = 0; ray < rayDirections.Length; ray++)
				{
					if (Vector3.Dot(view * rayDirections[ray], up) > minimumDescent)
					{
						sky++;
						if (IsBackground(pixels[rayPixels[ray]]))
							skyBackground++;
						continue;
					}
					if (!MustHit(ray, eye, view))
						continue;
					mustHit++;
					if (IsBackground(pixels[rayPixels[ray]]))
						holes++;
				}
				sample.Read = true;
				sample.MustHit = mustHit;
				sample.Holes = holes;
				sample.Sky = sky;
				sample.SkyBackground = skyBackground;
				if (holes > (worst?.Holes ?? 0))
				{
					worst = sample;
					worstEye = eye;
					worstView = view;
					pixels.CopyTo(worstPixels);
				}
			}

			var samples = new List<MotionSample>(8192);
			var commits = 0;
			var lastCommit = 0.0;
			var committedPosition = start.position;
			Action<CameraFrame> onCommit = frame =>
			{
				commits++;
				lastCommit = UnityEngine.Time.realtimeSinceStartupAsDouble;
				committedPosition = frame.GlobalPosition;
			};
			var allocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
			var markers = FrameMarkers.Select(marker => ProfilerRecorder.StartNew(marker.category, marker.name)).ToArray();
			var previousInstances = _control.Scene.InstanceCount;
			var previousMeshes = _control.Scene.MeshCount;
			var hook = _controlObject.AddComponent<PresentedFrameHook>();
			hook.Display = Application.isBatchMode ? null : _target;
			var moving = true;
			var probeFrame = -10;
			var probes = new List<(double lockWait, double collect, bool released)>();
			var previousCollections = GC.CollectionCount(0);
			var previousEye = Conversion.ToUnityPosition(start.position - origin);
			var poseTime = 0.0;
			var previousPoseTime = 0.0;
			var motionStart = UnityEngine.Time.realtimeSinceStartupAsDouble;
			var meshesBefore = BuildDiagnostics.MeshTotal;
			var preparedBefore = BuildDiagnostics.PreparedMeshTotal;
			_control.FrameCommitted += onCommit;
			hook.Presented = () =>
			{
				if (!moving)
					return;
				var now = UnityEngine.Time.realtimeSinceStartupAsDouble;
				var elapsed = now - motionStart;
				var position = _control.Camera.GlobalPosition;
				var view = _control.Camera.Rotation;
				var eye = Conversion.ToUnityPosition(position - origin);
				var delta = UnityEngine.Time.unscaledDeltaTime;
				var collections = GC.CollectionCount(0);
				var age = UnityEngine.Time.frameCount - probeFrame;
				var sample = new MotionSample
				{
					Frame = UnityEngine.Time.frameCount,
					Time = elapsed,
					Delta = delta,
					Speed = poseTime > previousPoseTime ? (eye - previousEye).magnitude / (float)(poseTime - previousPoseTime) : 0,
					CommitAge = commits > 0 ? now - lastCommit : elapsed,
					CommitLag = Conversion.ToUnityPosition(position - committedPosition).magnitude,
					Collections = collections - previousCollections,
					Allocated = allocated.Valid ? allocated.LastValue : -1,
					Probed = age >= 0 && age <= 1,
					Instances = _control.Scene.InstanceCount - previousInstances,
					Meshes = _control.Scene.MeshCount - previousMeshes,
					Markers = markers.Select(marker => marker.Valid ? marker.LastValue * 1e-6 : -1).ToArray()
				};
				previousInstances = _control.Scene.InstanceCount;
				previousMeshes = _control.Scene.MeshCount;
				previousCollections = collections;
				previousEye = eye;
				previousPoseTime = poseTime;
				samples.Add(sample);
				_camera.Render();
				AsyncGPUReadback.Request(_target, 0, TextureFormat.RGBA32,
					request => Analyze(request, sample, eye, view));
				if (elapsed >= Duration)
				{
					moving = false;
					return;
				}
				var next = PoseAt(elapsed);
				poseTime = elapsed;
				_control.Camera.SetPose(next.position, next.rotation);
			};
			try
			{
				var nextProbe = 1.0;
				while (moving)
				{
					Assert.That(_control.Session.State, Is.Not.EqualTo(MapControlState.Faulted), _control.Session.Error);
					if (UnityEngine.Time.realtimeSinceStartupAsDouble - motionStart >= nextProbe)
					{
						nextProbe += 1;
						probeFrame = UnityEngine.Time.frameCount;
						probes.Add(LockedFinalizerProbe());
					}
					yield return null;
				}
				AsyncGPUReadback.WaitAllRequests();
				yield return null;
				yield return null;
			}
			finally
			{
				hook.Presented = null;
				_control.FrameCommitted -= onCommit;
				allocated.Dispose();
				foreach (var marker in markers)
					marker.Dispose();
				Object.Destroy(hook);
			}

			// Analysis; probe frames and the first quarter second are excluded from timing statistics.
			var steady = samples.Where(sample => !sample.Probed && sample.Time > 0.25).ToList();
			var read = samples.Where(sample => sample.Read).ToList();
			var deltas = steady.Select(sample => (double)sample.Delta).OrderBy(value => value).ToArray();
			var commitAges = steady.Select(sample => sample.CommitAge).OrderBy(value => value).ToArray();
			double Percentile(double[] sorted, double fraction)
			{
				return sorted[Math.Max(0, Math.Min(sorted.Length - 1, (int)Math.Ceiling(fraction * sorted.Length) - 1))];
			}
			Assert.That(steady.Count, Is.GreaterThanOrEqualTo(120), "Too few frames to analyze the flight.");
			var medianDelta = Percentile(deltas, 0.5);
			var spikeLimit = Math.Max(3 * medianDelta, 1.0 / 30);
			var byFrame = samples.ToDictionary(sample => sample.Frame);
			bool AfterCollection(MotionSample sample)
			{
				return sample.Collections > 0 ||
					byFrame.TryGetValue(sample.Frame - 1, out var previous) && previous.Collections > 0 && !previous.Probed;
			}
			var spikes = steady.Where(sample => sample.Delta > spikeLimit).ToList();
			var naturalCollections = steady.Sum(sample => sample.Collections);
			var collectionFrames = steady.Where(sample => sample.Collections > 0).ToList();
			var mustHitTotal = read.Sum(sample => (long)sample.MustHit);
			var holeTotal = read.Sum(sample => (long)sample.Holes);
			var holeFrames = read.Count(sample => sample.Holes > 0);
			var skyTotal = read.Sum(sample => (long)sample.Sky);
			var skyBackgroundTotal = read.Sum(sample => (long)sample.SkyBackground);
			var worstFraction = read.Where(sample => sample.MustHit > 0)
				.Select(sample => (double)sample.Holes / sample.MustHit).DefaultIfEmpty(0).Max();
			var allocations = steady.Where(sample => sample.Allocated >= 0).Select(sample => sample.Allocated)
				.OrderBy(value => value).ToArray();
			var allocationMedian = allocations.Length > 0 ? allocations[allocations.Length / 2] : -1;
			var meshesAfter = BuildDiagnostics.MeshTotal;
			var preparedAfter = BuildDiagnostics.PreparedMeshTotal;
			var meshBuilds = meshesAfter.TotalMeshes - meshesBefore.TotalMeshes;
			var meshMilliseconds = (meshesAfter.TotalTime - meshesBefore.TotalTime).TotalMilliseconds;
			var report = Invariant(
				$"Fast motion: map={url}; box={half.x * 2:F0}x{half.y * 2:F0} m, radius={radius:F0} m, ",
				$"cruise={cruise:F0} m; frames={samples.Count}, steady={steady.Count}, read={read.Count}; ",
				$"speed max={steady.Max(sample => sample.Speed):F0} m/s; ",
				$"dt p50={medianDelta * 1000:F1} p95={Percentile(deltas, 0.95) * 1000:F1} ",
				$"p99={Percentile(deltas, 0.99) * 1000:F1} max={deltas.Last() * 1000:F1} ms, ",
				$"spikes>{spikeLimit * 1000:F0}ms={spikes.Count} (after GC {spikes.Count(AfterCollection)}); ",
				$"commits={commits}, commit age p99={Percentile(commitAges, 0.99) * 1000:F1} ",
				$"max={commitAges.Last() * 1000:F1} ms, lag max={steady.Max(sample => sample.CommitLag):F1} m; ",
				$"GC gen0={naturalCollections} in {collectionFrames.Count} frames ",
				$"(dt max {collectionFrames.Select(sample => sample.Delta * 1000).DefaultIfEmpty(0).Max():F1} ms), ",
				$"alloc/frame median={allocationMedian} avg={(allocations.Length > 0 ? allocations.Average() : -1):F0} ",
				$"max={(allocations.Length > 0 ? allocations.Max() : -1)} B; ",
				$"holes={holeTotal}/{mustHitTotal} in {holeFrames} frames, worst={worstFraction:P3} ",
				$"(frame {worst?.Frame} t={worst?.Time:F2} s), sky background={skyBackgroundTotal}/{skyTotal}; ",
				$"locked finalizer probes={probes.Count}, released={probes.Count(probe => probe.released)}, ",
				$"lock wait max={probes.Select(probe => probe.lockWait).DefaultIfEmpty(0).Max():F1} ms, ",
				$"collect max={probes.Select(probe => probe.collect).DefaultIfEmpty(0).Max():F1} ms; ",
				$"main meshes={meshBuilds} in {meshMilliseconds:F1} ms ",
				$"(avg {(meshBuilds > 0 ? meshMilliseconds / meshBuilds : 0):F3} ms, worst all {meshesAfter.WorstTime.TotalMilliseconds:F2} ms); ",
				$"loader prepare={_control.Settings.PrepareMeshesOnLoaders}: prepared={preparedAfter.Prepared - preparedBefore.Prepared} ",
				$"in {(preparedAfter.PrepareTime - preparedBefore.PrepareTime).TotalMilliseconds:F1} ms, ",
				$"used={preparedAfter.Used - preparedBefore.Used}, discarded={preparedAfter.Discarded - preparedBefore.Discarded}, ",
				$"expired={preparedAfter.Expired - preparedBefore.Expired}, unprepared={preparedAfter.Unprepared - preparedBefore.Unprepared}");
			TestContext.WriteLine(report);
			Debug.Log("CSW map acceptance: " + report);
			var frameLoad = FrameLoadReport(steady, samples);
			TestContext.WriteLine(frameLoad);
			Debug.Log("CSW map acceptance: " + frameLoad);
			var directory = Environment.GetEnvironmentVariable("CSWUNITY_TEST_ARTIFACTS");
			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
				var csv = new System.Text.StringBuilder(
					"frame,time,dt_ms,speed,commit_age_ms,commit_lag_m,gc0,alloc,probed,read,must_hit,holes,sky,sky_background\r\n");
				foreach (var sample in samples)
					csv.Append(Invariant(
						$"{sample.Frame},{sample.Time:F4},{sample.Delta * 1000:F2},{sample.Speed:F1},",
						$"{sample.CommitAge * 1000:F2},{sample.CommitLag:F2},{sample.Collections},{sample.Allocated},",
						$"{(sample.Probed ? 1 : 0)},{(sample.Read ? 1 : 0)},{sample.MustHit},{sample.Holes},{sample.Sky},{sample.SkyBackground}\r\n"));
				File.WriteAllText(Path.Combine(directory, "csw-map-fast-motion.csv"), csv.ToString());
				if (worst != null)
				{
					for (var ray = 0; ray < rayDirections.Length; ray++)
						if (MustHit(ray, worstEye, worstView) && IsBackground(worstPixels[rayPixels[ray]]))
							worstPixels[rayPixels[ray]] = new Color32(0, 255, 255, 255);
					var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
					image.SetPixels32(worstPixels);
					image.Apply(false);
					File.WriteAllBytes(Path.Combine(directory, "csw-map-fast-motion-worst.png"), image.EncodeToPNG());
					Object.Destroy(image);
				}
			}

			Assert.That(probes.Count, Is.GreaterThanOrEqualTo(5));
			Assert.That(probes.All(probe => probe.released), Is.True,
				"Finalizers must not wait for the edit lock: release GizmoSDK node, state and texture wrappers " +
				"inside the held lock with ReleaseAlreadyLocked.");
			Assert.That(read.Count, Is.GreaterThanOrEqualTo(samples.Count * 9 / 10), "GPU readbacks were lost.");
			Assert.That(skyTotal, Is.GreaterThan(1000), "The flight must also look above the horizon.");
			Assert.That(skyBackgroundTotal, Is.GreaterThanOrEqualTo(skyTotal * 95 / 100),
				"Rays above the horizon must read the background; the readback rows or colour do not match the rays.");
			Assert.That(mustHitTotal, Is.GreaterThan(read.Count * 1000L), "The must-hit region must cover the view.");
			Assert.That(worstFraction, Is.LessThan(0.005), "Fast motion must not open holes in the inner map.");
			Assert.That(holeTotal, Is.LessThanOrEqualTo(mustHitTotal / 10000), "Fast motion must not open holes in the inner map.");
			Assert.That(commitAges.Last(), Is.LessThan(0.5), "Native frames must keep committing during fast motion.");
			Assert.That(Percentile(commitAges, 0.99), Is.LessThan(0.15), "Native frames must commit steadily during fast motion.");
			Assert.That(Percentile(deltas, 0.99), Is.LessThan(0.05), "Fast motion must keep the frame time smooth.");
			Assert.That(spikes.Count, Is.LessThanOrEqualTo(Math.Max(3, steady.Count / 100)), "Too many frame-time spikes.");
			Assert.That(collectionFrames.All(sample => sample.Delta < 0.05), Is.True, "A garbage collection stalled a frame.");
			Assert.That(allocationMedian, Is.LessThan(32 * 1024), "Steady flight must not allocate much managed memory per frame.");
			Assert.That(probes.Max(probe => probe.collect), Is.LessThan(500), "A locked collection must not wait on finalizers.");
			if (_control.Settings.PrepareMeshesOnLoaders)
				Assert.That(preparedAfter.Used, Is.GreaterThan(0), "New meshes must use their loader-thread preparation.");
			LeakNodeWrappers(64);
			Assert.That(LockedFinalizerProbe().released, Is.False,
				"The locked finalizer probe must detect GizmoSDK node wrappers left to their finalizers.");
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
		private static void LeakNodeWrappers(int count)
		{
			GizmoSDK.Gizmo3D.NodeLock.WaitLockEdit();
			try
			{
				for (var index = 0; index < count; index++)
					new GizmoSDK.Gizmo3D.Group("Leaked finalizer probe");
			}
			finally
			{
				GizmoSDK.Gizmo3D.NodeLock.UnLock();
			}
		}

		private static string Invariant(params FormattableString[] parts)
		{
			return string.Concat(parts.Select(FormattableString.Invariant));
		}

		/// <summary>Reports main-thread marker times per frame and in the 5 % frames that add the most instances and meshes.</summary>
		/// <remarks>A recorder's LastValue is the previous frame, so the markers of the next sample belong to a sample's frame.</remarks>
		private static string FrameLoadReport(List<MotionSample> steady, List<MotionSample> samples)
		{
			var byFrame = samples.Where(sample => sample.Markers != null).ToDictionary(sample => sample.Frame);
			var frames = steady.Where(sample => byFrame.ContainsKey(sample.Frame + 1))
				.Select(sample => (sample, markers: byFrame[sample.Frame + 1].Markers)).ToList();
			if (frames.Count == 0)
				return "Frame load: no marker samples";
			var burst = frames.OrderByDescending(frame => frame.sample.Instances + frame.sample.Meshes)
				.Take(Math.Max(1, frames.Count / 20)).ToList();
			var parts = new List<string>
			{
				Invariant($"Frame load: frames={frames.Count}, burst={burst.Count} frames with ",
					$"instances avg={burst.Average(frame => frame.sample.Instances):F0} max={burst.Max(frame => frame.sample.Instances)}, ",
					$"meshes avg={burst.Average(frame => frame.sample.Meshes):F1} max={burst.Max(frame => frame.sample.Meshes)}")
			};
			for (var index = 0; index < FrameMarkers.Length; index++)
			{
				var values = frames.Select(frame => frame.markers[index]).Where(value => value >= 0).OrderBy(value => value).ToArray();
				if (values.Length == 0)
				{
					parts.Add($"{FrameMarkers[index].name} n/a");
					continue;
				}
				double Percentile(double fraction) =>
					values[Math.Max(0, Math.Min(values.Length - 1, (int)Math.Ceiling(fraction * values.Length) - 1))];
				var inBurst = burst.Select(frame => frame.markers[index]).Where(value => value >= 0).DefaultIfEmpty(0);
				parts.Add(Invariant($"{FrameMarkers[index].name} p50={Percentile(0.5):F2} p95={Percentile(0.95):F2} ",
					$"p99={Percentile(0.99):F2} max={values.Last():F2} sum={values.Sum():F0} burst avg={inBurst.Average():F2} ",
					$"max={inBurst.Max():F2} ms"));
			}
			return string.Join("; ", parts);
		}

		/// <summary>Collects garbage on a worker while this thread holds the edit lock.</summary>
		/// <remarks>
		/// A GizmoSDK wrapper finalized without ReleaseAlreadyLocked waits for the edit lock; the collection then does
		/// not finish before the timeout. The lock is released afterwards, so the probe cannot deadlock.
		/// </remarks>
		private static (double lockWait, double collect, bool released) LockedFinalizerProbe()
		{
			var watch = System.Diagnostics.Stopwatch.StartNew();
			System.Threading.Tasks.Task collection;
			bool released;
			double lockWait;
			GizmoSDK.Gizmo3D.NodeLock.WaitLockEdit();
			try
			{
				lockWait = watch.Elapsed.TotalMilliseconds;
				collection = System.Threading.Tasks.Task.Run(() =>
				{
					GC.Collect();
					GC.WaitForPendingFinalizers();
				});
				released = collection.Wait(TimeSpan.FromSeconds(2));
			}
			finally
			{
				GizmoSDK.Gizmo3D.NodeLock.UnLock();
			}
			var collect = watch.Elapsed.TotalMilliseconds - lockWait;
			collection.Wait();
			return (lockWait, collect, released);
		}

		private IEnumerator StartInjectedExampleComposition(string path, string url)
		{
#if UNITY_EDITOR
			_fixtureCswInitialized = CSW.Platform.Initialize();
			Assert.That(_fixtureCswInitialized, Is.True);
			_fixtureCoordinateInitialized = GizmoSDK.Coordinate.Platform.Initialize();
			Assert.That(_fixtureCoordinateInitialized, Is.True);
			Exception loadError = null;
			UnityEngine.Events.UnityAction<Scene, LoadSceneMode> configure = (scene, mode) =>
			{
				if (scene.path != path)
					return;
				_exampleScene = scene;
				try
				{
					_control = scene.GetRootGameObjects()
						.SelectMany(root => root.GetComponentsInChildren<MapControl>(true)).Single();
					Assert.That(_control.Session, Is.Null, "Scene-load callbacks must be able to configure the map control before Start.");
					_control.ReadConfiguration = false;
							var input = _control.GetComponent<FlightInput>();
							Assert.That(input, Is.Not.Null, "The example scene flies its map control with CSW.Unity.FlightInput.");
							Assert.That(input.MapControl, Is.SameAs(_control));
							input.enabled = false;
							_camera = _control.ViewCamera;
					Assert.That(_camera, Is.Not.Null);
					_camera.targetTexture = _target;
					_camera.clearFlags = CameraClearFlags.SolidColor;
					_camera.backgroundColor = Background;
					_camera.nearClipPlane = 1;
					_camera.farClipPlane = 50000;
					_camera.fieldOfView = 60;
					Capture("empty");
					_control.StartStreaming(url);
				}
				catch (Exception exception) { loadError = exception; }
			};
			UnityEngine.SceneManagement.SceneManager.sceneLoaded += configure;
			try
			{
				yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
					path, new LoadSceneParameters(LoadSceneMode.Additive));
				if (loadError != null)
					throw loadError;
				Assert.That(_exampleScene.IsValid() && _exampleScene.isLoaded, Is.True);
				TestContext.WriteLine($"Example scene composition={path}; the map control was started explicitly before Start.");
			}
			finally { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= configure; }
#else
			Assert.Fail("Injected scene acceptance requires the Unity Editor test runner.");
			yield break;
#endif
		}

		[UnityTest]
		public IEnumerator RepeatedRealMapLifetimesDrainPendingOutputBeforeReloadAndRestart()
		{
			var url = Environment.GetEnvironmentVariable("CSWUNITY_TEST_MAP");
			if (string.IsNullOrWhiteSpace(url))
				Assert.Inconclusive("Set CSWUNITY_TEST_MAP to a published GZD fixture URL to run real-map acceptance.");
			var example = Environment.GetEnvironmentVariable("CSWUNITY_TEST_SCENE");
			if (string.IsNullOrWhiteSpace(example))
				StartHost(url);
			else
				yield return StartInjectedExampleComposition(example, url);
			const int cycles = 8;
			for (var cycle = 0; cycle < cycles; cycle++)
			{
				yield return WaitFor(() => _control.Session.CompletedFrames >= 3 &&
					_control.Scene.MeshCount > 0 && ActiveRenderers().Any(), $"lifecycle {cycle} realization");
				Assert.That(GizmoSDK.Gizmo3D.DynamicLoaderManager.IsManagerRunning(0), Is.True,
					"Each session's SetLoaders must run the dynamic loaders.");
				Assert.That(Capture($"lifecycle-{cycle}"), Is.GreaterThan(64));
				var scene = _control.Scene;
				var generation = scene.Generation;
				_control.Session.MaximumCommandsPerTick = 1;
				_control.Camera.Rotate(120, 0);
				yield return WaitFor(() => _control.Session.PendingBuffers > 0,
					$"lifecycle {cycle} queued output");
				if (cycle % 2 == 0)
				{
					_control.ClearMap();
					Assert.That(_control.Session.State, Is.EqualTo(MapControlState.Stopped));
					Assert.That(_control.Session.PendingBuffers, Is.Zero);
					Assert.That(scene.Generation, Is.GreaterThan(generation));
					Assert.That(scene.Coordinates.IsConfigured, Is.False);
				}
				else
				{
					_control.StopStreaming();
					Assert.That(_control.Session, Is.Null);
					Assert.That(_control.Scene, Is.Null);
					Assert.That(GizmoSDK.Gizmo3D.DynamicLoaderManager.IsManagerRunning(0), Is.False,
						"The last map control must stop the dynamic loaders before releasing its platforms.");
				}
				Assert.That(scene.InstanceCount, Is.Zero);
				Assert.That(scene.MeshCount, Is.Zero);
				Assert.That(scene.MaterialCount, Is.Zero);
				GC.Collect();
				GC.WaitForPendingFinalizers();
				yield return null;
				if (cycle == cycles - 1)
					break;
				if (_control.Session == null)
					_control.StartStreaming(url);
				else
				{
					_control.Session.MaximumCommandsPerTick = 128;
					_control.OpenMap(url);
				}
			}
		}

		[UnityTest]
		public IEnumerator RealMapSupportsSynchronizedFrustumAndOmniWithOneCommandPerTick()
		{
			var url = Environment.GetEnvironmentVariable("CSWUNITY_TEST_MAP");
			if (string.IsNullOrWhiteSpace(url))
				Assert.Inconclusive("Set CSWUNITY_TEST_MAP to run synchronized camera acceptance.");
			var example = Environment.GetEnvironmentVariable("CSWUNITY_TEST_SCENE");
			if (string.IsNullOrWhiteSpace(example))
				StartHost(url);
			else
				yield return StartInjectedExampleComposition(example, url);
			yield return WaitFor(() => _control.Session.CompletedFrames >= 3 &&
				ActiveRenderers().Any(), "synchronization baseline");
			Assert.That(_control.Session.RefreshOutstanding, Is.True,
				"Enabling synchronization must also finish the existing asynchronous refresh.");
			_control.Session.MaximumCommandsPerTick = 1;
			_control.Session.BuildBudget = TimeSpan.FromTicks(1);
			_control.Settings.SynchronizeFrames = true;
			_control.Settings.LodFactor = 1;
			_control.Settings.LodReferenceFieldOfView = 60;
			foreach (var (omni, infinite) in new[] { (false, false), (true, true), (false, true) })
			{
				_control.Settings.OmniView = omni;
				_control.Settings.InfiniteFarPlane = infinite;
				var completed = _control.Session.CompletedFrames;
				_control.Camera.Move(new Vector3(5, 0, 0));
				_control.Camera.Rotate(45, 0);
				_camera.fieldOfView = omni ? 75 : 60;
				// Settings reach the camera in the next update, so the first refresh sampling them commits one update later.
				yield return WaitFor(() => _control.Session.CompletedFrames >= completed + 3,
					$"synchronized omni={omni} infinite={infinite}");
				Assert.That(_control.Session.SynchronizeFrames, Is.True);
				Assert.That(_control.Session.OmniView, Is.EqualTo(omni));
				Assert.That(_control.Camera.SynchronizeFrames, Is.True);
				var frame = _control.Scene.CommittedFrame;
				Assert.That(frame.InfiniteFar, Is.EqualTo(infinite),
					"The committed refresh must carry the map control's far plane mode.");
				Assert.That(frame.LodFactor, Is.EqualTo(
					Camera.EffectiveLodFactor(1, _camera.fieldOfView, 60)).Within(0.00001),
					"The submitted LOD factor must follow zoom against the reference field of view.");
				Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
				Assert.That(Quaternion.Angle(_camera.transform.rotation,
					Conversion.ToUnityRotation(frame.Hpr)), Is.LessThan(0.05));
				var expected = frame.Projection;
				_control.Camera.Rotate(15, 0);
				_camera.transform.SetPositionAndRotation(new Vector3(100, 200, 300), Quaternion.identity);
				_camera.projectionMatrix = Matrix4x4.Perspective(120, 2, 1, 10000);
				Assert.That(Capture($"synchronized-omni-{omni}"), Is.GreaterThan(64));
				for (var i = 0; i < 16; i++)
					Assert.That(_camera.projectionMatrix[i], Is.EqualTo(expected[i]).Within(0.00001),
						"The before-render hook must retain the committed frustum, including manual renders.");
				Assert.That(Quaternion.Angle(_camera.transform.rotation,
					Conversion.ToUnityRotation(frame.Hpr)), Is.LessThan(0.05));
				Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
				Assert.That(GizmoSDK.Gizmo3D.NodeLock.IsLockedByMe(), Is.False);
			}
			var outstanding = _control.Session.OutstandingRefresh;
			var consumed = _control.Session.CompletedFrames;
			Assert.That(outstanding, Is.Not.Zero,
				"A synchronized update must leave its submitted refresh outstanding while Unity renders.");
			yield return null;
			Assert.That(_control.Session.CompletedFrames, Is.EqualTo(consumed + 1));
			Assert.That(_control.Scene.CommittedFrame.RefreshId, Is.EqualTo(outstanding),
				"Each synchronized update must commit the refresh submitted by the previous update.");
			Assert.That(_control.Session.OutstandingRefresh, Is.Not.Zero.And.Not.EqualTo(outstanding),
				"Each synchronized update must submit the next refresh after consuming the previous one.");
			_control.Settings.SynchronizeFrames = false;
			yield return WaitFor(() => !_control.Camera.SynchronizeFrames, "return to asynchronous presentation");
			_control.Camera.Present();
			Assert.That(Quaternion.Angle(_camera.transform.rotation, _control.Camera.Rotation), Is.LessThan(0.05));
			Assert.That(_camera.projectionMatrix.m22, Is.EqualTo(-Camera.InfiniteFarNudge),
				"Live presentation must keep the selected infinite far plane.");
			_control.Settings.SynchronizeFrames = true;
			var frames = _control.Session.CompletedFrames;
			yield return WaitFor(() => _control.Session.CompletedFrames >= frames + 2,
				"synchronized mode re-entry");
			_control.ClearMap();
			Assert.That(_control.Scene.HasCommittedFrame, Is.False);
			_control.OpenMap(url);
			yield return WaitFor(() => _control.Session.CompletedFrames >= 2 &&
				ActiveRenderers().Any(), "synchronized map reload");
			Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
			_control.StopStreaming();
			_camera.fieldOfView = 95;
			_camera.Render();
			var released = Matrix4x4.Perspective(95, _camera.aspect, _camera.nearClipPlane, _camera.farClipPlane);
			Assert.That(_camera.projectionMatrix[1, 1], Is.EqualTo(released[1, 1]).Within(0.00001),
				"Stopping must return projection ownership to the borrowed camera.");
			Assert.That(_camera.projectionMatrix[2, 2], Is.EqualTo(released[2, 2]).Within(0.00001),
				"Stopping must also release the infinite far plane.");
		}

		[UnityTest]
		public IEnumerator InstalledApplicationCameraOverridesTheRefreshAndTheRenderedCamera()
		{
			var url = Environment.GetEnvironmentVariable("CSWUNITY_TEST_MAP");
			if (string.IsNullOrWhiteSpace(url))
				Assert.Inconclusive("Set CSWUNITY_TEST_MAP to run application camera acceptance.");
			ApplicationCamera installed = null;
			_control.CameraFactory = (view, coordinates) => installed = new ApplicationCamera(view, coordinates);
			_control.Settings.LodFactor = 1;
			_control.Settings.LodReferenceFieldOfView = 60;
			StartHost(url);
			Assert.That(_control.Camera, Is.SameAs(installed), "The map control must present through the installed camera.");
			yield return WaitFor(() => _control.Session.CompletedFrames >= 3 &&
				ActiveRenderers().Any(), "application camera baseline");
			var frame = _control.Scene.CommittedFrame;
			Assert.That(frame.VerticalFieldOfView, Is.EqualTo(ApplicationCamera.OverriddenFieldOfView),
				"The refresh must carry the overridden field of view.");
			Assert.That(frame.LodFactor, Is.EqualTo(ApplicationCamera.OverriddenLodFactor),
				"The refresh must carry the overridden LOD factor.");
			Assert.That(installed.Presentations, Is.Positive, "The map control must present through the overridden behavior.");
			Assert.That(_camera.fieldOfView, Is.EqualTo(60), "The installed camera must not write the Unity camera's settings.");
			Assert.That(Capture("application-camera"), Is.GreaterThan(64));
			var expected = installed.Projection(ApplicationCamera.OverriddenFieldOfView, _camera.aspect,
				_camera.nearClipPlane, _camera.farClipPlane, false);
			Assert.That(expected.m02, Is.EqualTo(ApplicationCamera.LensShift));
			for (var i = 0; i < 16; i++)
				Assert.That(_camera.projectionMatrix[i], Is.EqualTo(expected[i]).Within(0.00001),
					"The rendered camera must use the overridden projection.");
			_control.StopStreaming();
			_camera.Render();
			Assert.That(_camera.projectionMatrix.m02, Is.Zero,
				"Stopping must return projection ownership to the borrowed camera.");
		}

		private sealed class ApplicationCamera : Camera
		{
			public const float OverriddenFieldOfView = 40;
			public const float OverriddenLodFactor = 2.5f;
			public const float LensShift = 0.125f;

			public ApplicationCamera(UnityEngine.Camera camera, SceneCoordinateContext coordinates)
				: base(camera, coordinates)
			{
			}

			public int Presentations { get; private set; }
			public override float FieldOfView => OverriddenFieldOfView;
			public override bool OwnsProjection => true;

			public override float LodFactor(float lodFactor, float verticalFieldOfView)
			{
				return OverriddenLodFactor;
			}

			public override Matrix4x4 Projection(
				float verticalFieldOfView, float aspect, float nearDistance, float farDistance, bool infiniteFar)
			{
				var projection = base.Projection(verticalFieldOfView, aspect, nearDistance, farDistance, infiniteFar);
				projection.m02 = LensShift;
				return projection;
			}

			public override void Present()
			{
				Presentations++;
				base.Present();
			}
		}

		[UnityTest]
		public IEnumerator SynchronizedFrameTimeoutFaultsAndClearsInsteadOfRenderingPartialUpdates()
		{
			var url = Environment.GetEnvironmentVariable("CSWUNITY_TEST_MAP");
			if (string.IsNullOrWhiteSpace(url))
				Assert.Inconclusive("Set CSWUNITY_TEST_MAP to run synchronization timeout acceptance.");
			StartHost(url);
			yield return WaitFor(() => _control.Session.CompletedFrames >= 3 &&
				ActiveRenderers().Any(), "timeout baseline");
			_control.Settings.FrameWaitTimeout = 2.5;
			yield return null;
			Assert.That(_control.Session.FrameWaitTimeout, Is.EqualTo(TimeSpan.FromSeconds(2.5)),
				"The map control must apply the timeout setting every update.");
			_control.Session.SynchronizeFrames = true;
			GizmoSDK.Gizmo3D.NodeLock.WaitLockEdit();
			try
			{
				Assert.Throws<InvalidOperationException>(() =>
					_control.Session.Tick(_control.Camera.Sample(GizmoSDK.GizmoBase.Time.SystemSeconds)));
			}
			finally { GizmoSDK.Gizmo3D.NodeLock.UnLock(); }
			Assert.That(_control.Session.State, Is.EqualTo(MapControlState.Running));
			// Back-to-back Ticks leave the native worker no time to finish the refresh submitted by the previous Tick.
			_control.Session.FrameWaitTimeout = TimeSpan.FromTicks(1);
			LogAssert.Expect(LogType.Error,
				new System.Text.RegularExpressions.Regex("TimeoutException: SceneManager frame"));
			for (var i = 0; i < 100 && _control.Session.State != MapControlState.Faulted; i++)
				_control.Session.Tick(_control.Camera.Sample(GizmoSDK.GizmoBase.Time.SystemSeconds));
			Assert.That(_control.Session.State, Is.EqualTo(MapControlState.Faulted));
			Assert.That(_control.Session.Error, Does.Contain("did not complete"));
			Assert.That(_control.Session.PendingBuffers, Is.Zero);
			Assert.That(_control.Session.RefreshOutstanding, Is.False);
			Assert.That(_control.Scene.InstanceCount, Is.Zero);
			Assert.That(_control.Scene.HasCommittedFrame, Is.False);
			Assert.That(GizmoSDK.Gizmo3D.NodeLock.IsLockedByMe(), Is.False);
		}

		[UnityTest]
		public IEnumerator MissingMapFailsExplicitlyAndAStoppedControlCanBeStoppedAgain()
		{
			const string missing = "file:///CSWUnity-nonexistent-fixture/map.gzd";
			Assert.That(_control.Session, Is.Null);
			var foliage = _controlObject.AddComponent<FoliageModule>();
			foliage.enabled = false;
			var shading = _controlObject.AddComponent<MapShadingModule>();
			var hiZObject = new GameObject("Explicit Hi-Z");
			hiZObject.transform.SetParent(_controlObject.transform, false);
			hiZObject.SetActive(false);
			var otherCamera = hiZObject.AddComponent<UnityEngine.Camera>();
			var hiZ = hiZObject.AddComponent<BuiltInHiZDepthPyramid>();
			hiZ.TargetCamera = otherCamera;
			foliage.HiZDepthPyramid = hiZ;
			_control.Foliage = foliage;
			_control.TerrainShading = shading;
			_control.MapUrl = missing;
			var sentinel = new Vector4(41, 43, 47, 0);
			Shader.SetGlobalVector("_WorldOffset", sentinel);
			LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("CSW MapControl:"));
			var failures = new List<string>();
			_control.Failed += failures.Add;
			Assert.That(_control.State, Is.EqualTo(MapControlState.Stopped));
			_control.enabled = true;
			Assert.That(_control.Session, Is.Null, "OnEnable must leave the first start to Start.");
			yield return null;
			Assert.That(_control.Session, Is.Not.Null, "Start must start the map control from its MapUrl.");
			Assert.That(_control.LoadedMapUrl, Is.EqualTo(missing));
			Assert.That(foliage.Presentation, Is.SameAs(_control.Scene));
			Assert.That(shading.Presentation, Is.SameAs(_control.Scene));
			Assert.That(foliage.Presentation.Camera, Is.SameAs(_camera));
			Assert.That(shading.Presentation.Camera, Is.SameAs(_camera));
			Assert.That(hiZ.TargetCamera, Is.SameAs(_camera));
			var deadline = UnityEngine.Time.realtimeSinceStartupAsDouble + 15;
			while (_control.State != MapControlState.Faulted &&
				UnityEngine.Time.realtimeSinceStartupAsDouble < deadline)
				yield return null;
			Assert.That(_control.State, Is.EqualTo(MapControlState.Faulted));
			Assert.That(_control.Error, Is.Not.Empty);
			Assert.That(failures, Is.EqualTo(new[] { _control.Error }), "Failed must report the fault once.");
			Assert.That(_control.Session.PendingBuffers, Is.Zero);
			Assert.That(_control.Scene.InstanceCount, Is.Zero);
			Assert.That(Shader.GetGlobalVector("_WorldOffset"), Is.EqualTo(sentinel),
				"A faulted session without map coordinates must not publish shader globals.");
			_control.StopStreaming();
			_control.StopStreaming();
			Assert.That(_control.Session, Is.Null);
			Assert.That(_control.State, Is.EqualTo(MapControlState.Stopped));
			Assert.That(_control.LoadedMapUrl, Is.Null);
			Assert.That(_control.Error, Is.Null);
			Assert.That(foliage.Presentation, Is.Null);
			Assert.That(shading.Presentation, Is.Null);
		}

		private void StartHost(string url)
		{
			_control.enabled = true;
			_control.StartStreaming(url);
		}

		private IEnumerable<SceneInstanceHandle> ActiveRenderers()
		{
			return _control.Scene.Instances.Where(handle => handle.IsVisible &&
				handle.TryGetComponent<MeshFilter>(out var mesh) && mesh.sharedMesh &&
				handle.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled &&
				!renderer.forceRenderingOff);
		}

		private IEnumerator WaitFor(Func<bool> ready, string operation)
		{
			var pose = _control.Camera.GlobalPosition;
			Debug.Log($"CSW map acceptance: waiting for {operation}; frames={_control.Session.CompletedFrames}; " +
				FormattableString.Invariant($"camera=({pose.x:R}, {pose.y:R}, {pose.z:R}); ") +
				$"CRS={_control.Coordinates.Definition}");
			var deadline = UnityEngine.Time.realtimeSinceStartupAsDouble + 45;
			while (!ready() && UnityEngine.Time.realtimeSinceStartupAsDouble < deadline)
			{
				Assert.That(_control.Session.State, Is.Not.EqualTo(MapControlState.Faulted),
					$"{operation}: {_control.Session.Error}");
				yield return null;
			}
			Assert.That(ready(), Is.True,
				$"Timed out waiting for {operation}; state={_control.Session.State}, " +
				$"frames={_control.Session.CompletedFrames}, queue={_control.Session.PendingBuffers}, " +
				$"outstanding={_control.Session.RefreshOutstanding}, nativePending={_control.Session.NativeCommandsPending}, " +
				$"workerRunning={_control.Session.NativeWorkerRunning}, " +
				$"instances={_control.Scene.InstanceCount}, meshes={_control.Scene.MeshCount}, error={_control.Session.Error}");
			Debug.Log($"CSW map acceptance: completed {operation}; frames={_control.Session.CompletedFrames}");
		}

		private int Capture(string phase)
		{
			_camera.Render();
			var previous = RenderTexture.active;
			var image = new Texture2D(_target.width, _target.height, TextureFormat.RGBA32, false);
			try
			{
				RenderTexture.active = _target;
				image.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
				image.Apply(false);
				var pixels = image.GetPixels32();
				if (phase == "empty")
					_emptyPixels = pixels;
				var count = pixels.Where((pixel, index) =>
					Mathf.Abs(pixel.r - _emptyPixels[index].r) > 12 ||
					Mathf.Abs(pixel.g - _emptyPixels[index].g) > 12 ||
					Mathf.Abs(pixel.b - _emptyPixels[index].b) > 12).Count();
				var directory = Environment.GetEnvironmentVariable("CSWUNITY_TEST_ARTIFACTS");
				if (!string.IsNullOrWhiteSpace(directory))
				{
					Directory.CreateDirectory(directory);
					File.WriteAllBytes(Path.Combine(directory, $"csw-map-{phase}.png"), image.EncodeToPNG());
				}
				return count;
			}
			finally
			{
				RenderTexture.active = previous;
				Object.Destroy(image);
			}
		}
	}
}
