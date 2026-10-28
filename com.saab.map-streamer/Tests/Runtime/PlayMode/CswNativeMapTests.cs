//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: CswNativeMapTests.cs
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
//
//*****************************************************************************

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Saab.Foundation.Unity.MapStreamer.Csw;
using Saab.Foundation.Unity.MapStreamer.Modules;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
	public sealed class CswNativeMapTests
	{
		private GameObject _hostObject;
		private GameObject _cameraObject;
		private CswMapStreamerHost _host;
		private Camera _camera;
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
			_camera = _cameraObject.AddComponent<Camera>();
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
			_hostObject = new GameObject("Acceptance host");
			_host = _hostObject.AddComponent<CswMapStreamerHost>();
			_host.enabled = false;
			_host.ViewCamera = _camera;
			_host.ReadConfiguration = false;
			_host.FlightInput = false;
			_host.InitialAltitude = 500;
			_template = new Material(Shader.Find("Standard"));
			var terrain = _hostObject.AddComponent<TerrainNodeBuilder>();
			var assets = _hostObject.AddComponent<AssetNodeBuilder>();
			CswSceneResourceTests.Configure(terrain, _template);
			CswSceneResourceTests.Configure(assets, _template);
			_host.Builders = new GeometryBuilderBase[] { terrain, assets };
		}

		[TearDown]
		public void TearDown()
		{
			_host?.StopStreaming();
			_camera.targetTexture = null;
			_target.Release();
			Object.Destroy(_target);
			Object.Destroy(_template);
			Object.Destroy(_hostObject);
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
				_host?.StopStreaming();
				if (_camera)
					_camera.targetTexture = null;
				_host = _hostObject.GetComponent<CswMapStreamerHost>();
				_camera = _cameraObject.GetComponent<Camera>();
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
				Assert.That(_host.Foliage.Presentation, Is.SameAs(_host.Scene));
				Assert.That(_host.TerrainShading.Presentation, Is.SameAs(_host.Scene));
				if (_host.Foliage.HiZDepthPyramid)
					Assert.That(_host.Foliage.HiZDepthPyramid.TargetCamera, Is.SameAs(_camera));
			}
			yield return WaitFor(() => _host.Scene.MeshCount > 0 &&
				_host.Manager.CompletedFrames >= 3 && ActiveRenderers().Any(), "initial map realization");
			var scene = _host.Scene;
			var root = scene.Instances.First(handle =>
				handle.Key.Node != IntPtr.Zero && handle.ParentKey.Equals(SceneInstanceKey.Root));
			var rootId = root.gameObject.GetInstanceID();
			Assert.That(root.gameObject.activeInHierarchy, Is.True);
			Assert.That(scene.Coordinates.IsConfigured, Is.True);
			Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
			var initialPose = _host.Camera.GlobalPosition;
			var initialRotation = _host.Camera.Rotation;
			var initialPosition = root.transform.localPosition;
			var before = ActiveRenderers().ToDictionary(
				handle => handle.Key,
				handle => handle.GetComponent<MeshFilter>().sharedMesh);
			var initialPixels = Capture("initial");
			Assert.That(initialPixels, Is.GreaterThan(64), "The real camera must see map pixels, not only allocated meshes.");

			var completed = _host.Manager.CompletedFrames;
			var displacement = new Vector3(25, 0, 0);
			var nativeDisplacement = scene.Coordinates.Axes.ToNativeDirection(initialRotation * displacement);
			var sample = _host.Camera.Sample(0);
			TestContext.WriteLine($"CRS={scene.Coordinates.Definition}");
			TestContext.WriteLine(FormattableString.Invariant(
				$"Initial position=({initialPose.x:R}, {initialPose.y:R}, {initialPose.z:R})"));
			TestContext.WriteLine(FormattableString.Invariant(
				$"HPR=({sample.Hpr.x:R}, {sample.Hpr.y:R}, {sample.Hpr.z:R})"));
			TestContext.WriteLine(FormattableString.Invariant(
				$"Native movement=({nativeDisplacement.x:R}, {nativeDisplacement.y:R}, {nativeDisplacement.z:R})"));
			_host.Camera.Move(displacement);
			yield return WaitFor(() => _host.Manager.CompletedFrames >= completed + 3, "camera movement");
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
			completed = _host.Manager.CompletedFrames;
			_host.Camera.Rotate(15, -5);
			yield return WaitFor(() => _host.Manager.CompletedFrames >= completed + 3, "camera rotation");
			Assert.That(Quaternion.Angle(_camera.transform.rotation, initialRotation), Is.GreaterThan(1));
			Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
			Assert.That(Capture("rotated"), Is.GreaterThan(64));
			var rotated = _host.Camera.Sample(0);
			TestContext.WriteLine(FormattableString.Invariant(
				$"Rotated HPR=({rotated.Hpr.x:R}, {rotated.Hpr.y:R}, {rotated.Hpr.z:R})"));
			completed = _host.Manager.CompletedFrames;
			GC.Collect();
			GC.WaitForPendingFinalizers();
			yield return WaitFor(() => _host.Manager.CompletedFrames >= completed + 32,
				"sustained one-outstanding refresh progress");
			// Leave native scope without leaving a projected CRS's horizontal domain.
			var up = scene.Coordinates.Orientation(initialPose).GetCol(2);
			var outside = initialPose + (GizmoSDK.GizmoBase.Vec3D)up * 100000000;
			scene.Coordinates.ToGeodetic(outside);
			_host.Camera.SetPose(outside, initialRotation);
			yield return WaitFor(() => !root.gameObject.activeInHierarchy, "native root scope Off");
			Assert.That(root.gameObject.GetInstanceID(), Is.EqualTo(rootId));
			Assert.That(root.IsLocallyActive, Is.False);
			_host.Camera.SetPose(initialPose, initialRotation);
			yield return WaitFor(() => root.gameObject.activeInHierarchy &&
				ActiveRenderers().Any(), "native root re-entry");
			Assert.That(root.gameObject.GetInstanceID(), Is.EqualTo(rootId));
			Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
			Assert.That(Capture("returned"), Is.GreaterThan(64));

			var definition = scene.Coordinates.Definition;
			var generation = scene.Generation;
			_host.ClearMap();
			Assert.That(_host.Manager.State, Is.EqualTo(MapManagerState.Stopped));
			Assert.That(_host.Manager.PendingBuffers, Is.Zero);
			Assert.That(scene.InstanceCount, Is.Zero);
			Assert.That(scene.MeshCount, Is.Zero);
			Assert.That(scene.MaterialCount, Is.Zero);
			Assert.That(scene.Generation, Is.GreaterThan(generation));
			Assert.That(scene.Coordinates.IsConfigured, Is.False);
			_host.OpenMap(url);
			yield return WaitFor(() => _host.Manager.CompletedFrames >= 3 &&
				ActiveRenderers().Any(), "clear/reload with a fresh native session");
			Assert.That(scene.Coordinates.Definition, Is.EqualTo(definition));
			Assert.That(Capture("reloaded"), Is.GreaterThan(64));
			TestContext.WriteLine(
				$"Map={url}; CRS={definition}; frames={_host.Manager.CompletedFrames}; " +
				$"instances={scene.InstanceCount}; meshes={scene.MeshCount}; materials={scene.MaterialCount}; " +
				$"initialPixels={initialPixels}; retainedMovingMeshes={retained}; scopeRoot={rootId}; " +
				$"UnityCamera={_camera.transform.position}");
			_host.StopStreaming();
			_host.StopStreaming();
			Assert.That(_host.Manager, Is.Null);
			Assert.That(_host.Scene, Is.Null);
			Assert.That(_hostObject, Is.Not.Null);
			Assert.That(_camera, Is.Not.Null);
			yield return null;
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
					_host = scene.GetRootGameObjects()
						.SelectMany(root => root.GetComponentsInChildren<CswMapStreamerHost>(true)).Single();
					Assert.That(_host.Manager, Is.Null, "Scene-load callbacks must be able to configure the host before Start.");
					_host.ReadConfiguration = false;
					_host.FlightInput = false;
					_camera = _host.ViewCamera;
					Assert.That(_camera, Is.Not.Null);
					_camera.targetTexture = _target;
					_camera.clearFlags = CameraClearFlags.SolidColor;
					_camera.backgroundColor = Background;
					_camera.nearClipPlane = 1;
					_camera.farClipPlane = 50000;
					_camera.fieldOfView = 60;
					Capture("empty");
					_host.StartStreaming(url);
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
				TestContext.WriteLine($"Example scene composition={path}; the host was started explicitly before Start.");
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
				yield return WaitFor(() => _host.Manager.CompletedFrames >= 3 &&
					_host.Scene.MeshCount > 0 && ActiveRenderers().Any(), $"lifecycle {cycle} realization");
				Assert.That(Capture($"lifecycle-{cycle}"), Is.GreaterThan(64));
				var scene = _host.Scene;
				var generation = scene.Generation;
				_host.Manager.MaximumCommandsPerTick = 1;
				_host.Camera.Rotate(120, 0);
				yield return WaitFor(() => _host.Manager.PendingBuffers > 0,
					$"lifecycle {cycle} queued output");
				if (cycle % 2 == 0)
				{
					_host.ClearMap();
					Assert.That(_host.Manager.State, Is.EqualTo(MapManagerState.Stopped));
					Assert.That(_host.Manager.PendingBuffers, Is.Zero);
					Assert.That(scene.Generation, Is.GreaterThan(generation));
					Assert.That(scene.Coordinates.IsConfigured, Is.False);
				}
				else
				{
					_host.StopStreaming();
					Assert.That(_host.Manager, Is.Null);
					Assert.That(_host.Scene, Is.Null);
				}
				Assert.That(scene.InstanceCount, Is.Zero);
				Assert.That(scene.MeshCount, Is.Zero);
				Assert.That(scene.MaterialCount, Is.Zero);
				GC.Collect();
				GC.WaitForPendingFinalizers();
				yield return null;
				if (cycle == cycles - 1)
					break;
				if (_host.Manager == null)
					_host.StartStreaming(url);
				else
				{
					_host.Manager.MaximumCommandsPerTick = 128;
					_host.OpenMap(url);
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
			yield return WaitFor(() => _host.Manager.CompletedFrames >= 3 &&
				ActiveRenderers().Any(), "synchronization baseline");
			Assert.That(_host.Manager.RefreshOutstanding, Is.True,
				"Enabling synchronization must also finish the existing asynchronous refresh.");
			_host.Manager.MaximumCommandsPerTick = 1;
			_host.Manager.BuildBudget = TimeSpan.FromTicks(1);
			_host.Settings.SynchronizeFrames = true;
			foreach (var omni in new[] { false, true, false })
			{
				_host.Settings.OmniView = omni;
				var completed = _host.Manager.CompletedFrames;
				_host.Camera.Move(new Vector3(5, 0, 0));
				_host.Camera.Rotate(45, 0);
				_camera.fieldOfView = omni ? 75 : 60;
				yield return WaitFor(() => _host.Manager.CompletedFrames >= completed + 2 &&
					!_host.Manager.RefreshOutstanding, $"synchronized omni={omni}");
				Assert.That(_host.Manager.SynchronizeFrames, Is.True);
				Assert.That(_host.Manager.OmniView, Is.EqualTo(omni));
				Assert.That(_host.Camera.SynchronizeFrames, Is.True);
				var frame = _host.Scene.CommittedFrame;
				Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
				Assert.That(Quaternion.Angle(_camera.transform.rotation,
					_host.Coordinates.Axes.CameraRotation(frame.Hpr)), Is.LessThan(0.05));
				var expected = Matrix4x4.Perspective(frame.VerticalFieldOfView, frame.Aspect,
					frame.NearDistance, frame.FarDistance);
				_host.Camera.Rotate(15, 0);
				_camera.transform.SetPositionAndRotation(new Vector3(100, 200, 300), Quaternion.identity);
				_camera.projectionMatrix = Matrix4x4.Perspective(120, 2, 1, 10000);
				Assert.That(Capture($"synchronized-omni-{omni}"), Is.GreaterThan(64));
				for (var i = 0; i < 16; i++)
					Assert.That(_camera.projectionMatrix[i], Is.EqualTo(expected[i]).Within(0.00001),
						"The before-render hook must retain the committed frustum, including manual renders.");
				Assert.That(Quaternion.Angle(_camera.transform.rotation,
					_host.Coordinates.Axes.CameraRotation(frame.Hpr)), Is.LessThan(0.05));
				Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
				Assert.That(GizmoSDK.Gizmo3D.NodeLock.IsLockedByMe(), Is.False);
			}
			_host.Settings.SynchronizeFrames = false;
			yield return WaitFor(() => !_host.Camera.SynchronizeFrames, "return to asynchronous presentation");
			_host.Camera.PresentPose();
			Assert.That(Quaternion.Angle(_camera.transform.rotation, _host.Camera.Rotation), Is.LessThan(0.05));
			_host.Settings.SynchronizeFrames = true;
			var frames = _host.Manager.CompletedFrames;
			yield return WaitFor(() => _host.Manager.CompletedFrames >= frames + 2 &&
				!_host.Manager.RefreshOutstanding, "synchronized mode re-entry");
			_host.ClearMap();
			Assert.That(_host.Scene.HasCommittedFrame, Is.False);
			_host.OpenMap(url);
			yield return WaitFor(() => _host.Manager.CompletedFrames >= 2 &&
				ActiveRenderers().Any(), "synchronized map reload");
			Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
			_host.StopStreaming();
			_camera.fieldOfView = 95;
			_camera.Render();
			Assert.That(_camera.projectionMatrix[1, 1],
				Is.EqualTo(Matrix4x4.Perspective(95, _camera.aspect,
					_camera.nearClipPlane, _camera.farClipPlane)[1, 1]).Within(0.00001),
				"Stopping must return projection ownership to the borrowed camera.");
		}

		[UnityTest]
		public IEnumerator SynchronizedFrameTimeoutFaultsAndClearsInsteadOfRenderingPartialUpdates()
		{
			var url = Environment.GetEnvironmentVariable("CSWUNITY_TEST_MAP");
			if (string.IsNullOrWhiteSpace(url))
				Assert.Inconclusive("Set CSWUNITY_TEST_MAP to run synchronization timeout acceptance.");
			StartHost(url);
			yield return WaitFor(() => _host.Manager.CompletedFrames >= 3 &&
				ActiveRenderers().Any(), "timeout baseline");
			_host.Manager.SynchronizeFrames = true;
			GizmoSDK.Gizmo3D.NodeLock.WaitLockEdit();
			try
			{
				Assert.Throws<InvalidOperationException>(() =>
					_host.Manager.Tick(_host.Camera.Sample(GizmoSDK.GizmoBase.Time.SystemSeconds)));
			}
			finally { GizmoSDK.Gizmo3D.NodeLock.UnLock(); }
			Assert.That(_host.Manager.State, Is.EqualTo(MapManagerState.Running));
			_host.Manager.FrameWaitTimeout = TimeSpan.FromTicks(1);
			_host.Settings.SynchronizeFrames = true;
			LogAssert.Expect(LogType.Exception,
				new System.Text.RegularExpressions.Regex("TimeoutException: SceneManager frame"));
			var deadline = UnityEngine.Time.realtimeSinceStartupAsDouble + 10;
			while (_host.Manager.State != MapManagerState.Faulted &&
				UnityEngine.Time.realtimeSinceStartupAsDouble < deadline)
				yield return null;
			Assert.That(_host.Manager.State, Is.EqualTo(MapManagerState.Faulted));
			Assert.That(_host.Manager.Error, Does.Contain("did not complete"));
			Assert.That(_host.Manager.PendingBuffers, Is.Zero);
			Assert.That(_host.Manager.RefreshOutstanding, Is.False);
			Assert.That(_host.Scene.InstanceCount, Is.Zero);
			Assert.That(_host.Scene.HasCommittedFrame, Is.False);
			Assert.That(GizmoSDK.Gizmo3D.NodeLock.IsLockedByMe(), Is.False);
		}

		[UnityTest]
		public IEnumerator MissingMapFailsExplicitlyAndAStoppedHostCanBeStoppedAgain()
		{
			const string missing = "file:///CSWUnity-nonexistent-fixture/map.gzd";
			Assert.That(_host.Manager, Is.Null);
			var foliage = _hostObject.AddComponent<FoliageModule>();
			foliage.enabled = false;
			var shading = _hostObject.AddComponent<MapShadingModule>();
			var hiZObject = new GameObject("Explicit Hi-Z");
			hiZObject.transform.SetParent(_hostObject.transform, false);
			hiZObject.SetActive(false);
			var otherCamera = hiZObject.AddComponent<Camera>();
			var hiZ = hiZObject.AddComponent<BuiltInHiZDepthPyramid>();
			hiZ.TargetCamera = otherCamera;
			foliage.HiZDepthPyramid = hiZ;
			_host.Foliage = foliage;
			_host.TerrainShading = shading;
			_host.MapUrl = missing;
			var sentinel = new Vector4(41, 43, 47, 0);
			Shader.SetGlobalVector("_WorldOffset", sentinel);
			LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("CSW Map Streamer:"));
			_host.enabled = true;
			Assert.That(_host.Manager, Is.Null, "OnEnable must leave the first start to Start.");
			yield return null;
			Assert.That(_host.Manager, Is.Not.Null, "Start must start the host from its MapUrl.");
			Assert.That(_host.Manager.MapUrl, Is.EqualTo(missing));
			Assert.That(foliage.Presentation, Is.SameAs(_host.Scene));
			Assert.That(shading.Presentation, Is.SameAs(_host.Scene));
			Assert.That(foliage.Presentation.Camera, Is.SameAs(_camera));
			Assert.That(shading.Presentation.Camera, Is.SameAs(_camera));
			Assert.That(hiZ.TargetCamera, Is.SameAs(_camera));
			var deadline = UnityEngine.Time.realtimeSinceStartupAsDouble + 15;
			while (_host.Manager.State != MapManagerState.Faulted &&
				UnityEngine.Time.realtimeSinceStartupAsDouble < deadline)
				yield return null;
			Assert.That(_host.Manager.State, Is.EqualTo(MapManagerState.Faulted));
			Assert.That(_host.Manager.Error, Is.Not.Empty);
			Assert.That(_host.Manager.PendingBuffers, Is.Zero);
			Assert.That(_host.Scene.InstanceCount, Is.Zero);
			Assert.That(Shader.GetGlobalVector("_WorldOffset"), Is.EqualTo(sentinel),
				"A faulted session without map coordinates must not publish shader globals.");
			_host.StopStreaming();
			_host.StopStreaming();
			Assert.That(_host.Manager, Is.Null);
			Assert.That(foliage.Presentation, Is.Null);
			Assert.That(shading.Presentation, Is.Null);
		}

		private void StartHost(string url)
		{
			_host.enabled = true;
			_host.StartStreaming(url);
		}

		private IEnumerable<SceneInstanceHandle> ActiveRenderers()
		{
			return _host.Scene.Instances.Where(handle => handle.gameObject.activeInHierarchy &&
				handle.TryGetComponent<MeshFilter>(out var mesh) && mesh.sharedMesh &&
				handle.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled);
		}

		private IEnumerator WaitFor(Func<bool> ready, string operation)
		{
			var pose = _host.Camera.GlobalPosition;
			Debug.Log($"CSW map acceptance: waiting for {operation}; frames={_host.Manager.CompletedFrames}; " +
				FormattableString.Invariant($"camera=({pose.x:R}, {pose.y:R}, {pose.z:R}); ") +
				$"CRS={_host.Coordinates.Definition}");
			var deadline = UnityEngine.Time.realtimeSinceStartupAsDouble + 45;
			while (!ready() && UnityEngine.Time.realtimeSinceStartupAsDouble < deadline)
			{
				Assert.That(_host.Manager.State, Is.Not.EqualTo(MapManagerState.Faulted),
					$"{operation}: {_host.Manager.Error}");
				yield return null;
			}
			Assert.That(ready(), Is.True,
				$"Timed out waiting for {operation}; state={_host.Manager.State}, " +
				$"frames={_host.Manager.CompletedFrames}, queue={_host.Manager.PendingBuffers}, " +
				$"outstanding={_host.Manager.RefreshOutstanding}, nativePending={_host.Manager.NativeCommandsPending}, " +
				$"workerRunning={_host.Manager.NativeWorkerRunning}, " +
				$"instances={_host.Scene.InstanceCount}, meshes={_host.Scene.MeshCount}, error={_host.Manager.Error}");
			Debug.Log($"CSW map acceptance: completed {operation}; frames={_host.Manager.CompletedFrames}");
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
