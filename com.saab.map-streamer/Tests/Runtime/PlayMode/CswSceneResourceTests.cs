//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: CswSceneResourceTests.cs
// Description	: Shared rendering-resource and optional-module lifetime tests
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Source sharing, replacement and resource lifetime tests	(2.12.327)
// AMO	261023	Bounded publication visits independently of unrelated and recycled scene occurrences.	(2.12.327)
// AMO	261028	Used the scene release delegate after legacy SceneManager removal.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using CSW;
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using GizmoSDK.Coordinate;
using NUnit.Framework;
using Saab.Foundation.Unity.MapStreamer.Csw;
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Unity.Extensions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
	public sealed class CswSceneResourceTests
	{
		private GameObject _host;
		private GameObject _cameraObject;
		private SceneCoordinateContext _coordinates;
		private UnityScene _scene;
		private SceneCommandInbox _inbox;
		private SceneCommandPump _pump;
		private AssetNodeBuilder _builder;
		private Material _template;
		private readonly List<Node> _nodes = new List<Node>();
		private readonly List<State> _states = new List<State>();

		[OneTimeSetUp]
		public void InitializeNative()
		{
			Assert.That(CSW.Platform.Initialize(), Is.True);
			Assert.That(GizmoSDK.Coordinate.Platform.Initialize(), Is.True);
			DbManager.Initialize();
		}

		[OneTimeTearDown]
		public void UninitializeNative()
		{
			DbManager.Uninitialize();
			Assert.That(GizmoSDK.Coordinate.Platform.Uninitialize(), Is.True);
			Assert.That(CSW.Platform.Uninitialize(), Is.True);
		}

		[SetUp]
		public void SetUp()
		{
			_host = new GameObject("Resource test host");
			_cameraObject = new GameObject("Camera");
			var camera = _cameraObject.AddComponent<UnityEngine.Camera>();
			_coordinates = new SceneCoordinateContext();
			_scene = new UnityScene(_host.transform, camera, _coordinates);
			_inbox = new SceneCommandInbox();
			_builder = _host.AddComponent<AssetNodeBuilder>();
			_template = new Material(Shader.Find("Standard"));
			Configure(_builder, _template);
			_pump = new SceneCommandPump(_scene, _inbox, new GeometryBuilderBase[] { _builder });
		}

		[TearDown]
		public void TearDown()
		{
			_pump.Dispose();
			_scene.Dispose();
			_coordinates.Dispose();
			NodeLock.WaitLockEdit();
			try
			{
				foreach (var state in _states)
					state.ReleaseAlreadyLocked();
				foreach (var node in _nodes)
					node.ReleaseAlreadyLocked();
				_states.Clear();
				_nodes.Clear();
			}
			finally { NodeLock.UnLock(); }
			Object.Destroy(_template);
			Object.Destroy(_cameraObject);
			Object.Destroy(_host);
			Texture2DCache.Clear();
		}

		[Test]
		public void SameSourceSharesResourcesButNotOccurrenceRendererOrTransform()
		{
			var geometry = Sphere();
			Enqueue(
				() => new SceneCommandNewNode(geometry, 1, null, 0, null),
				() => new SceneCommandNewNode(geometry, ulong.MaxValue, null, 0, null));
			var first = Handle(geometry, 1);
			var second = Handle(geometry, ulong.MaxValue);
			var mesh = first.GetComponent<MeshFilter>().sharedMesh;
			Assert.That(mesh.vertexCount, Is.GreaterThan(3));
			Assert.That(mesh.GetIndices(0).Length, Is.GreaterThan(3));
			Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
			Assert.That(first.transform, Is.Not.SameAs(second.transform));
			Assert.That(first.GetComponent<MeshRenderer>(), Is.Not.SameAs(second.GetComponent<MeshRenderer>()));
			Assert.That(second.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
			Assert.That(second.GetComponent<MeshRenderer>().sharedMaterial,
				Is.SameAs(first.GetComponent<MeshRenderer>().sharedMaterial));
			Assert.That(_scene.MeshCount, Is.EqualTo(1));
			Assert.That(_scene.MaterialCount, Is.EqualTo(1));
			Enqueue(() => new SceneCommandDeleteNode(geometry, 1));
			Assert.That(_scene.MeshCount, Is.EqualTo(1));
			Assert.That(second.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
			Enqueue(() => new SceneCommandDeleteNode(geometry, ulong.MaxValue));
			Assert.That(_scene.MeshCount, Is.Zero);
			Assert.That(_scene.MaterialCount, Is.Zero);
		}

		[Test]
		public void ResourcePublicationVisitsOnlySharedSourceUsersAndDropsPooledUsers()
		{
			for (var index = 0; index < 1024; index++)
				_scene.Create(new SceneNodeData
				{
					Key = new SceneInstanceKey(new IntPtr(100000 + index), 1),
					Parent = SceneInstanceKey.Root,
					Name = "Unrelated group"
				});
			var geometry = Sphere();
			Enqueue(
				() => new SceneCommandNewNode(geometry, 1, null, 0, null),
				() => new SceneCommandNewNode(geometry, 2, null, 0, null));
			var before = _scene.ResourcePublicationVisits;
			Enqueue(() => new SceneCommandUpdateNode(geometry, 1, null, 0, null));
			Assert.That(_scene.ResourcePublicationVisits - before, Is.LessThanOrEqualTo(4),
				"A source update must visit its users, not scan unrelated scene occurrences.");
			Enqueue(() => new SceneCommandDeleteNode(geometry, 1));
			_scene.Create(new SceneNodeData
			{
				Key = new SceneInstanceKey(new IntPtr(200000), 1),
				Parent = SceneInstanceKey.Root,
				Name = "Reused pooled group"
			});
			before = _scene.ResourcePublicationVisits;
			Enqueue(() => new SceneCommandUpdateNode(geometry, 2, null, 0, null));
			Assert.That(_scene.ResourcePublicationVisits - before, Is.LessThanOrEqualTo(2),
				"Pooled handles must not remain in the resource-source index.");
			Assert.That(_scene.MeshCount, Is.EqualTo(1));
		}

		[Test]
		public void FullUpdateRebindsEverySharedSourceBeforeRetiringItsPreviousMesh()
		{
			var geometry = Sphere();
			Enqueue(
				() => new SceneCommandNewNode(geometry, 1, null, 0, null),
				() => new SceneCommandNewNode(geometry, 2, null, 0, null));
			var first = Handle(geometry, 1);
			var second = Handle(geometry, 2);
			var oldMesh = first.GetComponent<MeshFilter>().sharedMesh;
			var oldVertices = oldMesh.vertices;
			var oldNormals = oldMesh.normals;
			var oldUv = oldMesh.uv;
			var oldIndices = oldMesh.GetIndices(0);
			var removed = 0;
			SceneGameObjectReleaseHandler onRemove = go =>
			{
				Assert.That(go.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(oldMesh));
				removed++;
			};
			_scene.OnRemoveGeometry += onRemove;
			Enqueue(() => new SceneCommandUpdateNode(geometry, 1, null, 0, null));
			Assert.That(removed, Is.EqualTo(2));
			var replacement = first.GetComponent<MeshFilter>().sharedMesh;
			Assert.That(replacement, Is.Not.SameAs(oldMesh));
			Assert.That(second.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(replacement));
			Assert.That(replacement.vertices, Is.EqualTo(oldVertices));
			Assert.That(replacement.normals, Is.EqualTo(oldNormals));
			Assert.That(replacement.uv, Is.EqualTo(oldUv));
			Assert.That(replacement.GetIndices(0), Is.EqualTo(oldIndices));
			Assert.That(_scene.MeshCount, Is.EqualTo(1));
			_scene.OnRemoveGeometry -= onRemove;
		}

		[Test]
		public void EffectiveUntexturedStateIsTakenFromCommandWithoutRetainingNativeWrappers()
		{
			var geometry = Sphere();
			State effective;
			NodeLock.WaitLockEdit();
			try
			{
				effective = new State();
				_states.Add(effective);
			}
			finally { NodeLock.UnLock(); }
			var references = effective.GetReferenceCount();
			Enqueue(() => new SceneCommandNewNode(geometry, 1, null, 0, effective));
			var instance = Handle(geometry, 1);
			Assert.That(instance.Resources.State.Source, Is.EqualTo(effective.GetNativeReference()));
			Assert.That(effective.GetReferenceCount(), Is.EqualTo(references));
			Assert.That(instance.GetComponent<MeshRenderer>().sharedMaterial, Is.Not.Null);
			Assert.That(_scene.TextureDiagnostics.ActiveTextures, Is.Zero);
		}

		[Test]
		public void ReplacingOneEffectiveStatePreservesTheOtherOccurrenceLease()
		{
			var geometry = Sphere();
			State original;
			State replacement;
			NodeLock.WaitLockEdit();
			try
			{
				original = new State();
				replacement = new State();
				_states.Add(original);
				_states.Add(replacement);
			}
			finally { NodeLock.UnLock(); }
			Enqueue(
				() => new SceneCommandNewNode(geometry, 1, null, 0, original),
				() => new SceneCommandNewNode(geometry, 2, null, 0, original));
			var first = Handle(geometry, 1);
			var second = Handle(geometry, 2);
			var oldMesh = first.GetComponent<MeshFilter>().sharedMesh;
			var shared = first.GetComponent<MeshRenderer>().sharedMaterial;
			var removed = 0;
			SceneGameObjectReleaseHandler onRemove = go =>
			{
				Assert.That(go.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(oldMesh));
				Assert.That(go.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(shared));
				removed++;
			};
			_scene.OnRemoveGeometry += onRemove;
			_scene.OnNewGeometry += (go, isAsset) => Assert.That(removed, Is.EqualTo(2));
			Enqueue(() => new SceneCommandUpdateNode(geometry, 1, null, 0, replacement));
			_scene.OnRemoveGeometry -= onRemove;
			Assert.That(removed, Is.EqualTo(2));
			Assert.That(first.Resources.State.Source, Is.EqualTo(replacement.GetNativeReference()));
			Assert.That(second.Resources.State.Source, Is.EqualTo(original.GetNativeReference()));
			Assert.That(first.GetComponent<MeshRenderer>().sharedMaterial, Is.Not.SameAs(shared));
			Assert.That(second.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(shared));
			Assert.That(_scene.MaterialCount, Is.EqualTo(2));
			Enqueue(() => new SceneCommandDeleteNode(geometry, 1));
			Assert.That(second.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(shared));
			Assert.That(_scene.MaterialCount, Is.EqualTo(1));
		}

		[Test]
		public void DifferentEffectiveStatesShareAndRefreshOneNativeTextureSource()
		{
			var firstGeometry = TexturedFixture();
			var secondGeometry = Sphere();
			State firstState;
			State secondState;
			GizmoSDK.Gizmo3D.Texture source;
			NodeLock.WaitLockEdit();
			try
			{
				using (var original = firstGeometry.State)
				{
					firstState = (State)original.Clone();
					secondState = (State)original.Clone();
					_states.Add(firstState);
					_states.Add(secondState);
					source = original.GetTexture(0);
					firstState.SetTexture(source);
					secondState.SetTexture(source);
					original.ReleaseAlreadyLocked();
				}
				Assert.That(firstState.GetMode(StateMode.TEXTURE), Is.EqualTo(StateModeActivation.ON));
				Assert.That(secondState.GetMode(StateMode.TEXTURE), Is.EqualTo(StateModeActivation.ON));
			}
			finally { NodeLock.UnLock(); }
			try
			{
				var nativeReferences = source.GetReferenceCount();
				Enqueue(
					() => new SceneCommandNewNode(firstGeometry, 1, null, 0, firstState),
					() => new SceneCommandNewNode(secondGeometry, 2, null, 0, secondState));
				var first = Handle(firstGeometry, 1);
				var second = Handle(secondGeometry, 2);
				var previous = first.Resources.State.Data.Texture;
				var previousSecondMesh = second.GetComponent<MeshFilter>().sharedMesh;
				Assert.That(previous.width, Is.EqualTo(2));
				Assert.That(previous.height, Is.EqualTo(2));
				Assert.That(previous.GetPixels32()[0], Is.EqualTo(new Color32(255, 0, 0, 255)));
				Assert.That(second.Resources.State.Data.Texture, Is.SameAs(previous));
				Assert.That(_scene.TextureDiagnostics.ActiveTextures, Is.EqualTo(1));
				Assert.That(_scene.TextureDiagnostics.ActiveReferences, Is.EqualTo(2));
				Assert.That(source.GetReferenceCount(), Is.EqualTo(nativeReferences));
				using (var held = _scene.Resources.Acquire(
					firstGeometry.GetNativeReference(), firstState.GetNativeReference(), _builder))
				{
					var heldMesh = held.Mesh.Mesh;
					NodeLock.WaitLockEdit();
					try
					{
						using (var image = source.GetImage())
							Assert.That(image.SetPixel(0, 0, new RGBA(1, 0, 1, 1)), Is.True);
					}
					finally { NodeLock.UnLock(); }
					var removed = 0;
					SceneGameObjectReleaseHandler onRemove = go =>
					{
						Assert.That(go.GetComponent<MeshRenderer>().sharedMaterial.mainTexture, Is.SameAs(previous));
						removed++;
					};
					_scene.OnRemoveGeometry += onRemove;
					Enqueue(() => new SceneCommandUpdateNode(firstGeometry, 1, null, 0, firstState));
					_scene.OnRemoveGeometry -= onRemove;
					var refreshed = first.Resources.State.Data.Texture;
					Assert.That(removed, Is.EqualTo(2));
					Assert.That(refreshed, Is.Not.SameAs(previous));
					Assert.That(refreshed.GetPixels32()[0], Is.EqualTo(new Color32(255, 0, 255, 255)));
					Assert.That(second.Resources.State.Data.Texture, Is.SameAs(refreshed));
					Assert.That(second.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(previousSecondMesh));
					Assert.That(held.Mesh.Mesh, Is.SameAs(heldMesh));
					Assert.That(held.State.Data.Texture, Is.SameAs(previous));
					Assert.That(held.Material.Material.mainTexture, Is.SameAs(previous));
					Assert.That(previous.GetPixels32()[0], Is.EqualTo(new Color32(255, 0, 0, 255)));
					Assert.That(_scene.TextureDiagnostics.ActiveTextures, Is.EqualTo(2));
					Assert.That(_scene.TextureDiagnostics.ActiveReferences, Is.EqualTo(3));
					Assert.That(_scene.MeshCount, Is.EqualTo(3));
					Assert.That(source.GetReferenceCount(), Is.EqualTo(nativeReferences));
				}
				Assert.That(_scene.TextureDiagnostics.ActiveTextures, Is.EqualTo(1));
				Assert.That(_scene.TextureDiagnostics.ActiveReferences, Is.EqualTo(2));
				Assert.That(_scene.MeshCount, Is.EqualTo(2));
				State replacementState;
				NodeLock.WaitLockEdit();
				try
				{
					replacementState = (State)firstState.Clone();
					_states.Add(replacementState);
				}
				finally { NodeLock.UnLock(); }
				Enqueue(() => new SceneCommandUpdateNode(firstGeometry, 1, null, 0, replacementState));
				var current = first.Resources.State.Data.Texture;
				Assert.That(current.GetPixels32()[0], Is.EqualTo(new Color32(255, 0, 255, 255)));
				Assert.That(second.Resources.State.Data.Texture, Is.SameAs(current));
				Assert.That(_scene.TextureDiagnostics.ActiveTextures, Is.EqualTo(1));
				Assert.That(_scene.TextureDiagnostics.ActiveReferences, Is.EqualTo(2));
				Enqueue(() => new SceneCommandDeleteNode(firstGeometry, 1));
				Assert.That(second.Resources.State.Data.Texture, Is.SameAs(current));
				Assert.That(current.GetPixels32()[0], Is.EqualTo(new Color32(255, 0, 255, 255)));
				Assert.That(_scene.TextureDiagnostics.ActiveReferences, Is.EqualTo(1));
				Enqueue(() => new SceneCommandDeleteNode(secondGeometry, 2));
				Assert.That(_scene.TextureDiagnostics.ActiveTextures, Is.Zero);
				Assert.That(_scene.TextureDiagnostics.ActiveReferences, Is.Zero);
			}
			finally
			{
				NodeLock.WaitLockEdit();
				try { source.ReleaseAlreadyLocked(); }
				finally { NodeLock.UnLock(); }
			}
		}

		[Test]
		public void ScopeHideAndTransformOnlyReentryDoNotRebuildOrFreeSharedGeometry()
		{
			var geometry = Sphere();
			Enqueue(
				() => new SceneCommandNewNode(geometry, 1, null, 0, null),
				() => new SceneCommandActivation(geometry, 1, Activation.ON));
			_scene.CommitFrame(new CameraRelativeFrame(default, default, 1, 60, 1, 1, 1000, 128, 128));
			var instance = Handle(geometry, 1);
			var mesh = instance.GetComponent<MeshFilter>().sharedMesh;
			var material = instance.GetComponent<MeshRenderer>().sharedMaterial;
			var meshBuilds = _scene.MeshTotalDiagnostics.TotalMeshes;
			var removed = 0;
			_scene.OnRemoveGeometry += go => removed++;
			Enqueue(
				() => new SceneCommandActivation(geometry, 1, Activation.OFF),
				() => new SceneCommandUpdateNode(geometry, 1, null, 0, null,
					new Matrix4 { v11 = 1, v22 = 1, v33 = 1, v44 = 1, v14 = 25, v24 = -3, v34 = 8 }),
				() => new SceneCommandActivation(geometry, 1, Activation.ON));
			Assert.That(instance.transform.localPosition, Is.EqualTo(new Vector3(25, -3, 8)));
			Assert.That(instance.gameObject.activeSelf, Is.True);
			Assert.That(instance.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
			Assert.That(instance.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(material));
			Assert.That(_scene.MeshTotalDiagnostics.TotalMeshes, Is.EqualTo(meshBuilds));
			Assert.That(removed, Is.Zero);
		}

		[Test]
		public void ResourceCompatibilityIncludesUniformColorAndBuilderTemplate()
		{
			var first = Sphere();
			var second = Sphere();
			ScenePreparedResources red;
			ScenePreparedResources blue;
			NodeLock.WaitLockEdit();
			try
			{
				red = _scene.Resources.Capture(first, null, false);
				blue = _scene.Resources.Capture(second, null, false);
			}
			finally { NodeLock.UnLock(); }
			using (red)
			using (blue)
			{
				red.Color = Color.red;
				blue.Color = Color.blue;
				using (_scene.Resources.Publish(red)) { }
				using (_scene.Resources.Publish(blue)) { }
			}
			using (var a = _scene.Resources.Acquire(first.GetNativeReference(), IntPtr.Zero, _builder))
			using (var b = _scene.Resources.Acquire(second.GetNativeReference(), IntPtr.Zero, _builder))
			{
				Assert.That(a.Material.Material, Is.Not.SameAs(b.Material.Material));
				Assert.That(a.Material.Material.color, Is.EqualTo(Color.red));
				Assert.That(b.Material.Material.color, Is.EqualTo(Color.blue));
				var otherBuilder = _host.AddComponent<AssetNodeBuilder>();
				Configure(otherBuilder, _template);
				using (var c = _scene.Resources.Acquire(first.GetNativeReference(), IntPtr.Zero, otherBuilder))
					Assert.That(c.Material.Material, Is.Not.SameAs(a.Material.Material));
				Assert.That(a.Material.Material.color, Is.EqualTo(Color.red));
			}
		}

		[Test]
		public void SharedFoliagePlacementSurvivesUntilTheLastFeatureReleasesIt()
		{
			var buffer = new ComputeBuffer(4, 16);
			var surface = new RenderTexture(2, 2, 0, RenderTextureFormat.RFloat);
			Assert.That(surface.Create(), Is.True);
			var owner = new FoliagePlacementLease(buffer, surface);
			var grass = owner.Retain();
			var trees = owner.Retain();
			owner.Dispose();
			grass.Dispose();
			Assert.That(buffer.IsValid(), Is.True);
			Assert.That(surface.IsCreated(), Is.True);
			trees.Dispose();
			Assert.That(buffer.IsValid(), Is.False);
			Assert.That(surface.IsCreated(), Is.False);
		}

		[Test]
		public void HiddenFirstTerrainNotificationUsesTheCommittedCameraAndCopiedData()
		{
			_pump.Dispose();
			_inbox = new SceneCommandInbox();
			var terrainBuilder = _host.AddComponent<TerrainNodeBuilder>();
			Configure(terrainBuilder, _template);
			_pump = new SceneCommandPump(_scene, _inbox, new GeometryBuilderBase[] { terrainBuilder });
			using (var system = new CoordinateSystem(
				Datum.WGS84_ELLIPSOID, FlatGaussProjection.NOT_DEFINED, CoordinateType.GEOCENTRIC))
				_coordinates.Configure(system.AsString(), new Vec3D(6378137, 0, 0));
			var geometry = Sphere();
			Vector3 boundaryCenter;
			NodeLock.WaitLockEdit();
			try { boundaryCenter = geometry.BoundaryCenter.ToVector3(); }
			finally { NodeLock.UnLock(); }
			var bridge = new CameraBridge(_scene.Camera, _coordinates);
			_scene.FrameCommitting += bridge.Commit;
			var notifications = 0;
			_scene.OnNewTerrain += (go, isAsset) =>
			{
				Assert.That(go.activeInHierarchy, Is.False);
				Assert.That(isAsset, Is.False);
				Assert.That(_scene.TryGetTerrain(go, out var terrain), Is.True);
				Assert.That(terrain.BoundaryCenter, Is.EqualTo(boundaryCenter));
				Assert.That(_scene.Camera.transform.position, Is.EqualTo(Vector3.zero));
				Assert.That(Shader.GetGlobalVector("_WorldOffset").x, Is.EqualTo(3637).Within(0.001));
				notifications++;
			};
			Enqueue(
				() => new SceneCommandNewNode(geometry, 1, null, 0, null),
				() => new SceneCommandActivation(geometry, 1, Activation.INIT));
			Assert.That(notifications, Is.Zero);
			var global = new Vec3D(6378637, 0, 0);
			_pump.ExpectFrame(99, new CameraRelativeFrame(global, default, 1, 60, 1, 1, 10000, 128, 128));
			Enqueue(
				() => new SceneCommandStartFrame(99, 1, global),
				() => new SceneCommandUpdateNode(geometry, 1, null, 0, null,
					Matrix4x4.Translate(new Vector3(-500, 0, 0)).ToMatrix4()),
				() => new SceneCommandActivation(geometry, 1, Activation.ON),
				() => new SceneCommandEndFrame(99));
			Assert.That(notifications, Is.EqualTo(1));
			Assert.That(Handle(geometry, 1).gameObject.activeSelf, Is.True);
		}

		[Test]
		public void ZeroEffectiveMaskRetainsStructureWithoutBuildingOrFaulting()
		{
			_pump.Dispose();
			_scene.Dispose();
			_scene = new UnityScene(_host.transform, _cameraObject.GetComponent<UnityEngine.Camera>(),
				_coordinates, intersectMask: IntersectMaskValue.NOTHING);
			_inbox = new SceneCommandInbox();
			_pump = new SceneCommandPump(_scene, _inbox, new GeometryBuilderBase[] { _builder });
			var geometry = Sphere();
			Enqueue(() => new SceneCommandNewNode(geometry, 1, null, 0, null));
			Assert.That(_scene.InstanceCount, Is.EqualTo(1));
			Assert.That(_scene.MeshCount, Is.Zero);
			Assert.That(Handle(geometry, 1).GetComponent<MeshRenderer>(), Is.Null);
			Assert.That(Handle(geometry, 1).IntersectMask, Is.EqualTo(IntersectMaskValue.NOTHING));
		}

		[Test]
		public void FailingRemovalObserversCannotPreventResourceOrHierarchyCleanup()
		{
			var geometry = Sphere();
			Enqueue(
				() => new SceneCommandNewNode(geometry, 1, null, 0, null),
				() => new SceneCommandNewNode(geometry, 2, null, 0, null));
			_scene.OnRemoveGeometry += go => throw new InvalidOperationException("Injected remove failure.");
			_scene.OnEnterPool += go => throw new InvalidOperationException("Injected pool failure.");
			var failure = Assert.Throws<AggregateException>(() => _scene.Clear(1));
			Assert.That(failure.Flatten().InnerExceptions.Count, Is.EqualTo(4));
			Assert.That(_scene.InstanceCount, Is.Zero);
			Assert.That(_scene.MeshCount, Is.Zero);
			Assert.That(_scene.MaterialCount, Is.Zero);
			Assert.That(_scene.PooledObjectCount, Is.EqualTo(2));
			Assert.DoesNotThrow(() => _scene.Clear(2));
		}

		private Geometry Sphere()
		{
			NodeLock.WaitLockEdit();
			try
			{
				using (var name = new NativeString("gzGeometrySphere"))
				{
					var geometry = Reference.CreateObject(name) as Geometry;
					Assert.That(geometry, Is.Not.Null);
					_nodes.Add(geometry);
					return geometry;
				}
			}
			finally { NodeLock.UnLock(); }
		}

		private Geometry TexturedFixture([CallerFilePath] string sourceFile = "")
		{
			var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile),
				@"..\..\Fixtures\editable-rgba-2x2.gzd"));
			Assert.That(File.Exists(path), Is.True, path);
			NodeLock.WaitLockEdit();
			try
			{
				var geometry = DbManager.LoadDB(path) as Geometry;
				Assert.That(geometry, Is.Not.Null);
				_nodes.Add(geometry);
				return geometry;
			}
			finally { NodeLock.UnLock(); }
		}

		private SceneInstanceHandle Handle(Node node, ulong path)
		{
			Assert.That(_scene.TryGetInstance(new SceneInstanceKey(node.GetNativeReference(), path), out var handle), Is.True);
			return handle;
		}

		private void Enqueue(params Func<SceneCommand>[] commands)
		{
			NodeLock.WaitLockEdit();
			try
			{
				var buffer = new CommandBuffer();
				foreach (var create in commands)
				{
					using (var command = create())
						buffer.AddCommand(command);
				}
				_inbox.Receive(null, null, buffer);
			}
			finally { NodeLock.UnLock(); }
			_pump.Pump(1000, TimeSpan.FromSeconds(1));
		}

		internal static void Configure(GeometryBuilderBase builder, Material material)
		{
			var flags = BindingFlags.Instance | BindingFlags.NonPublic;
			typeof(GeometryBuilderBase).GetField("_material", flags).SetValue(builder, material);
			typeof(GeometryBuilderBase).GetField("_fallbackMaterial", flags).SetValue(builder, material);
			typeof(GeometryBuilderBase).GetField("_mask", flags).SetValue(builder, IntersectMaskValue.ALL);
		}
	}
}
