//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: CswPresentationModuleTests.cs
// Description	: Copied presentation injection and GPU scope regressions
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Command-lane module lifecycle and GPU tests		(2.12.327)
// AMO	261028	Used scene presentation delegates after legacy SceneManager removal.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using GizmoSDK.GizmoBase;
using NUnit.Framework;
using Saab.Foundation.Unity.MapStreamer.Csw;
using Saab.Foundation.Unity.MapStreamer.Modules;
using Saab.Utility.GfxCaps;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Quaternion = UnityEngine.Quaternion;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
	public sealed class CswPresentationModuleTests
	{
		private readonly List<Object> _objects = new List<Object>();
		private Camera _camera;
		private Capability _caps;

		[OneTimeSetUp]
		public void InitializeNative()
		{
			Assert.That(CSW.Platform.Initialize(), Is.True);
		}

		[OneTimeTearDown]
		public void UninitializeNative()
		{
			Assert.That(CSW.Platform.Uninitialize(), Is.True);
		}

		[SetUp]
		public void SetUp()
		{
			_caps = GfxCaps.CurrentCaps;
			_camera = Own(new GameObject("Presentation camera")).AddComponent<Camera>();
			_camera.nearClipPlane = 0.1f;
			_camera.farClipPlane = 1000;
			_camera.fieldOfView = 100;
			_camera.aspect = 1;
		}

		[TearDown]
		public void TearDown()
		{
			for (var i = _objects.Count - 1; i >= 0; i--)
				if (_objects[i])
					Object.DestroyImmediate(_objects[i]);
			_objects.Clear();
			GfxCaps.CurrentCaps = _caps;
		}

		[Test]
		public void SourceLessAwakeAcceptsFirstInjectionReinjectionAndDetachRebind()
		{
			GfxCaps.CurrentCaps |= Capability.UseTerrainDetailTextures;
			var moduleObject = Own(new GameObject("Late detail module"));
			var module = moduleObject.AddComponent<MapShadingModule>();
			Assert.That(Field<ComputeBuffer>(module, "_mappingBuffer"), Is.Null);
			var set = Own(ScriptableObject.CreateInstance<TerrainDetailTextureAssetSet>());
			var asset = Own(ScriptableObject.CreateInstance<TerrainDetailTextureAsset>());
			asset.Albedo = Image(Color.green);
			asset.Normal = Image(new Color(0.5f, 0.5f, 1, 1));
			set.Textures = new List<MappedDetailTextureAsset>
			{
				new MappedDetailTextureAsset { Asset = asset, Mapping = MapFeature.Grass }
			};
			module.DetailTextureSet = set;
			var first = new Presentation(_camera);
			var second = new Presentation(_camera);
			module.SetPresentation(first);
			var mapping = Field<ComputeBuffer>(module, "_mappingBuffer");
			var textures = Field<Texture2DArray>(module, "_textureArray");
			var normals = Field<Texture2DArray>(module, "_normalMapArray");
			Assert.That(mapping, Is.Not.Null);
			Assert.That(mapping.IsValid(), Is.True);
			Assert.That(textures.depth, Is.EqualTo(1));
			Assert.That(normals.depth, Is.EqualTo(1));
			Assert.That(textures.width, Is.EqualTo(4));
			Assert.That(textures.height, Is.EqualTo(4));
			Assert.That(textures.format, Is.EqualTo(TextureFormat.DXT5));
			var actual = new int[256];
			mapping.GetData(actual);
			Assert.That(actual, Is.EqualTo(TerrainMapping.FeatureTruthTable(
				TerrainMapping.MapFeatureData(), MapFeature.Grass)));
			Assert.That(actual.Any(value => value == 1), Is.True);
			Assert.That(first.TerrainSubscribers, Is.EqualTo(1));

			module.SetPresentation(first);
			module.InitializeModule();
			Assert.That(first.TerrainSubscribers, Is.EqualTo(1));
			Assert.That(Field<ComputeBuffer>(module, "_mappingBuffer"), Is.SameAs(mapping));
			Assert.That(Field<Texture2DArray>(module, "_textureArray"), Is.SameAs(textures));
			module.SetPresentation(null);
			Assert.That(first.TerrainSubscribers, Is.Zero);
			module.SetPresentation(second);
			Assert.That(second.TerrainSubscribers, Is.EqualTo(1));
			Assert.That(second.TerrainSubscriber, Is.SameAs(module));
			var terrain = Own(new GameObject("Hidden copied terrain"));
			terrain.SetActive(false);
			var material = Own(new Material(Shader.Find("Custom/TerrainShader")));
			terrain.AddComponent<MeshRenderer>().sharedMaterial = material;
			var data = new TerrainPresentationData(
				new StateBuildOutput { Texture = asset.Albedo, Feature = asset.Albedo },
				Vector3.zero, Matrix4x4.identity, Vector3.zero);
			second.AddTerrain(terrain, data);
			Assert.That(Field<Texture2DArray>(module, "_textureArray"), Is.SameAs(textures));
			Assert.That(Field<Texture2DArray>(module, "_normalMapArray"), Is.SameAs(normals));
			Object.DestroyImmediate(moduleObject);
			Assert.That(second.TerrainSubscribers, Is.Zero);
			Assert.That(mapping.IsValid(), Is.False);
		}

		[Test]
		public void HiddenTerrainRegistersCopiedGpuDataAndScopeOffClearsIndirectCount()
		{
			var compute = PlacementShader();
			var moduleObject = Own(new GameObject("Copied foliage module"));
			var module = moduleObject.AddComponent<FoliageModule>();
			module.enabled = false;
			module.ComputeShader = compute;
			module.Occlusion = false;
			module.DebugNoDraw = true;
			var feature = new FeatureSet
			{
				Enabled = true,
				SettingsType = SettingsFeatureType.Trees,
				Density = 1,
				NodeMaxWidth = 32,
				MaxHeight = 10,
				ScreenCoverage = 0.0001f,
				FoliageFeature = new FoliageFeature(128, 1, Enumerable.Repeat(1, 256).ToArray(), compute),
				InderectBuffer = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments),
				FoliageData = new ComputeBuffer(1, Marshal.SizeOf<FoliageType>()),
				FoliageMaterial = new Material(Shader.Find("Custom/Foliage/Billboard"))
			};
			module.Features.Add(feature);
			feature.InderectBuffer.SetData(new uint[] { 0, 1, 0, 0 });
			feature.FoliageData.SetData(new[]
			{
				new FoliageType { MinMax = Vector2.one, Offset = Vector2.zero, Weight = 1, CullAreaWidth = 1 }
			});
			Field<Dictionary<SettingsFeatureType, SettingsFeature>>(module, "_settingsCache")
				.Add(SettingsFeatureType.Trees, new SettingsFeature(1, 1, false));
			SetField(module, "_hasEnabledFeatures", true);
			var source = new Presentation(_camera);
			module.SetPresentation(source);
			Assert.That(source.TerrainSubscribers, Is.EqualTo(1));
			Assert.That(source.PostSubscribers, Is.EqualTo(1));

			var scope = Own(new GameObject("Native scope"));
			scope.transform.localScale = new Vector3(1, 1, -1);
			var parent = Own(new GameObject("Raw native parent"));
			parent.transform.SetParent(scope.transform, false);
			parent.transform.localPosition = new Vector3(3, 5, 7);
			parent.transform.localRotation = Quaternion.Euler(12, 25, -8);
			var terrain = Own(new GameObject("Raw native child"));
			terrain.transform.SetParent(parent.transform, false);
			terrain.transform.localPosition = new Vector3(1, -2, 3);
			terrain.transform.localRotation = Quaternion.Euler(7, -13, 19);
			terrain.AddComponent<MeshFilter>().sharedMesh = Plane();
			var lodOff = Own(new GameObject("Locally Off LOD child"));
			lodOff.transform.SetParent(terrain.transform, false);
			lodOff.SetActive(false);
			var nativeLocal = Matrix4x4.TRS(parent.transform.localPosition,
				parent.transform.localRotation, Vector3.one) *
				Matrix4x4.TRS(terrain.transform.localPosition, terrain.transform.localRotation, Vector3.one);
			var nativeUp = new Vector3(0.2f, 0.9f, 0.3f).normalized;
			var nativeEast = Vector3.Cross(nativeUp, Vector3.forward).normalized;
			var eun = Matrix4x4.identity;
			eun.SetColumn(0, nativeEast);
			eun.SetColumn(1, nativeUp);
			eun.SetColumn(2, -Vector3.Cross(nativeEast, nativeUp).normalized);
			var data = new TerrainPresentationData(
				new StateBuildOutput
				{
					Texture = Image(Color.white),
					Feature = Image(Color.white),
					SurfaceHeight = Image(Color.white),
					Feature_homography = new Matrix3D { v11 = 1, v22 = 1, v33 = 1 }
				}, Vector3.zero, nativeLocal.inverse * eun, Vector3.zero);
			_camera.transform.rotation = Quaternion.LookRotation(
				terrain.transform.TransformPoint(new Vector3(2, 0, 2)), Vector3.up);
			scope.SetActive(false);
			source.AddTerrain(terrain, data);
			Assert.That(feature.FoliageFeature.FoliageCount, Is.EqualTo(1));
			Assert.That(feature.FoliageFeature.HasActiveNodes, Is.False);
			var item = Field<List<FeatureData>>(feature.FoliageFeature, "_items")[0];
			var count = FirstUInt(item.TerrainPointCount);
			Assert.That(count, Is.GreaterThan(0), "The unchanged placement kernel must create real points.");
			var points = new FoliagePoint[count];
			item.TerrainPoints.GetData(points, 0, 0, points.Length);
			Assert.That(Vector3.Dot(UnpackNormal(points[0].up), nativeUp), Is.GreaterThan(0.999f));
			Assert.That(Vector3.Dot(UnpackNormal(points[0].right), nativeEast), Is.GreaterThan(0.999f));
			var pixels = new Vector4[16];
			item.PixelToObject.GetData(pixels);
			Assert.That(Vector4.Distance(pixels[5], new Vector4(1, 0, 1, 1)), Is.LessThan(0.001f));

			scope.SetActive(true);
			source.PostPresent();
			Assert.That(Field<Camera>(module, "_cullingCamera"), Is.SameAs(_camera));
			var visible = FirstUInt(feature.InderectBuffer);
			Assert.That(visible, Is.GreaterThan(0));
			var culled = new FoliagePoint[visible];
			feature.CulledBuffer.GetData(culled, 0, 0, culled.Length);
			var expectedWorld = Matrix4x4.Scale(new Vector3(1, 1, -1)) * nativeLocal;
			foreach (var point in culled)
				Assert.That(points.Min(raw => Vector3.Distance(
					expectedWorld.MultiplyPoint3x4(raw.Position), point.Position)), Is.LessThan(0.001f));
			Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
			feature.CulledBuffer.SetCounterValue(7);
			feature.InderectBuffer.SetData(new uint[] { 7, 1, 0, 0 });
			scope.SetActive(false);
			Invoke(module, "UpdateCulling", _camera);
			Assert.That(FirstUInt(feature.InderectBuffer), Is.Zero);
			Assert.That(FirstUInt(item.TerrainPointCount), Is.EqualTo(count));
			Assert.That(item.TerrainPoints.IsValid(), Is.True);
			Assert.That(item.PixelToObject.IsValid(), Is.True);
			Assert.That(feature.FoliageFeature.FoliageCount, Is.EqualTo(1));
			scope.SetActive(true);
			Assert.That(lodOff.activeSelf, Is.False);
			Invoke(module, "UpdateCulling", _camera);
			Assert.That(FirstUInt(feature.InderectBuffer), Is.EqualTo(visible));
			Assert.That(feature.FoliageFeature.FoliageCount, Is.EqualTo(1));
			source.RemoveTerrain(terrain);
			Assert.That(feature.FoliageFeature.FoliageCount, Is.Zero);
			Assert.That(item.TerrainPoints.IsValid(), Is.False);
			Assert.That(item.PixelToObject.IsValid(), Is.False);
			Object.DestroyImmediate(moduleObject);
			Assert.That(source.TerrainSubscribers, Is.Zero);
			Assert.That(source.PostSubscribers, Is.Zero);
		}

		[TestCase("Custom/AssetShader")]
		[TestCase("Custom/TerrainShader")]
		public void RawNonSymmetricHierarchyRendersOneBasisWithPreservedWindingAndNormals(string shader)
		{
			var host = Own(new GameObject("Rendered raw hierarchy"));
			var builder = host.AddComponent<AssetNodeBuilder>();
			var template = Own(new Material(Shader.Find(shader)));
			CswSceneResourceTests.Configure(builder, template);
			var target = Own(new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32));
			var normalTarget = Own(new RenderTexture(128, 128, 0, RenderTextureFormat.ARGBFloat));
			Assert.That(target.Create(), Is.True);
			Assert.That(normalTarget.Create(), Is.True);
			_camera.targetTexture = target;
			_camera.renderingPath = RenderingPath.DeferredShading;
			_camera.clearFlags = CameraClearFlags.SolidColor;
			_camera.backgroundColor = Color.magenta;
			_camera.fieldOfView = 60;
			var parent = Matrix4x4.TRS(new Vector3(3, 5, 7), Quaternion.Euler(12, 25, -8),
				new Vector3(1.2f, 0.8f, 1.4f));
			var child = Matrix4x4.TRS(new Vector3(1, -2, 3), Quaternion.Euler(7, -13, 19),
				new Vector3(0.9f, 1.1f, 0.7f));
			var expected = Matrix4x4.Scale(new Vector3(1, 1, -1)) * parent * child;
			var vertices = new[]
			{
				new Vector3(-2, -1, 0), new Vector3(3, -1, 0), new Vector3(-1, 2, 0)
			};
			var center = expected.MultiplyPoint3x4((vertices[0] + vertices[1] + vertices[2]) / 3);
			_camera.transform.rotation = Quaternion.LookRotation(center, Vector3.up);
			var mesh = new Mesh
			{
				vertices = vertices,
				normals = Enumerable.Repeat(Vector3.back, 3).ToArray(),
				tangents = Enumerable.Repeat(new Vector4(1, 0, 0, 1), 3).ToArray(),
				uv = new[] { Vector2.zero, Vector2.right, Vector2.up },
				triangles = new[] { 0, 2, 1 }
			};
			mesh.RecalculateBounds();
			var capture = new CommandBuffer { name = "CSW world-normal regression" };
			capture.Blit(BuiltinRenderTextureType.GBuffer2, normalTarget);
			_camera.AddCommandBuffer(CameraEvent.AfterGBuffer, capture);
			var parentKey = new SceneInstanceKey(new IntPtr(1), 1);
			var geometryKey = new SceneInstanceKey(new IntPtr(2), ulong.MaxValue);
			var mapping = new ComputeBuffer(256, sizeof(int));
			try
			{
				using (var coordinates = new SceneCoordinateContext())
				using (var scene = new UnityScene(host.transform, _camera, coordinates))
				{
					scene.Create(new SceneNodeData
					{
						Key = parentKey, Parent = SceneInstanceKey.Root, Kind = SceneNodeKind.Group,
						LocalTransform = parent, Name = "Native parent"
					});
					using (var data = new SceneNodeData
					{
						Key = geometryKey, Parent = parentKey, Kind = SceneNodeKind.Geometry,
						LocalTransform = child, Builder = builder, Name = "Native triangle",
						Prepared = new ScenePreparedResources(scene.Resources)
						{
							MeshSource = geometryKey.Node, Mesh = mesh, Color = Color.white, HasMesh = true
						}
					})
						scene.Create(data);
					scene.SetActivation(parentKey, CSW.Activation.ON);
					scene.CommitFrame(new CameraRelativeFrame(default, default, 1, 60, 1, 0.1f, 1000, 128, 128));
					Assert.That(scene.TryGetInstance(geometryKey, out var instance), Is.True);
					for (var i = 0; i < vertices.Length; i++)
						Assert.That(Vector3.Distance(instance.transform.TransformPoint(vertices[i]),
							expected.MultiplyPoint3x4(vertices[i])), Is.LessThan(0.001f));
					var renderer = instance.GetComponent<MeshRenderer>();
					var material = renderer.sharedMaterial;
					mapping.SetData(new int[256]);
					if (shader == "Custom/TerrainShader")
					{
						var albedo = Own(new Texture2DArray(4, 4, 1, TextureFormat.RGBA32, false, true));
						albedo.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray(), 0);
						albedo.Apply(false);
						var normals = Own(new Texture2DArray(4, 4, 1, TextureFormat.RGBA32, false, true));
						normals.SetPixels(Enumerable.Repeat(new Color(0.5f, 0.5f, 1, 1), 16).ToArray(), 0);
						normals.Apply(false);
						material.SetTexture("_Textures", albedo);
						material.SetTexture("_NormalMaps", normals);
						material.SetBuffer("_MappingBuffer", mapping);
						material.SetInt("_WaterIndex", 60);
					}
					renderer.enabled = false;
					_camera.Render();
					var empty = Read(target);
					renderer.enabled = true;
					_camera.Render();
					var front = Read(target);
					Assert.That(front.Where((pixel, index) => Different(pixel, empty[index])).Count(),
						Is.GreaterThan(64), "Native front winding must remain visible below the single reflected root.");
					var encoded = Read(normalTarget)[64 * 128 + 64];
					var normal = new Vector3(encoded.r, encoded.g, encoded.b) * 2 - Vector3.one;
					var expectedNormal = expected.inverse.transpose.MultiplyVector(Vector3.back).normalized;
					Assert.That(Vector3.Dot(normal.normalized, expectedNormal), Is.GreaterThan(0.99f),
						"The unchanged shader must receive inverse-transpose world normals, without another basis conversion.");
					mesh.triangles = new[] { 0, 1, 2 };
					_camera.Render();
					var back = Read(target);
					Assert.That(back.Where((pixel, index) => Different(pixel, empty[index])).Count(), Is.Zero,
						"Reversing only the raw native winding must expose the culled back face.");
					Assert.That(_camera.transform.position, Is.EqualTo(Vector3.zero));
				}
			}
			finally
			{
				mapping.Dispose();
				_camera.RemoveCommandBuffer(CameraEvent.AfterGBuffer, capture);
				capture.Release();
				_camera.targetTexture = null;
				target.Release();
				normalTarget.Release();
			}
		}

		private Color[] Read(RenderTexture target)
		{
			var previous = RenderTexture.active;
			var image = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
			try
			{
				RenderTexture.active = target;
				image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
				image.Apply(false);
				return image.GetPixels();
			}
			finally
			{
				RenderTexture.active = previous;
				Object.DestroyImmediate(image);
			}
		}

		private static bool Different(Color first, Color second)
		{
			return Mathf.Abs(first.r - second.r) > 0.05f ||
				Mathf.Abs(first.g - second.g) > 0.05f || Mathf.Abs(first.b - second.b) > 0.05f;
		}

		private ComputeShader PlacementShader()
		{
#if UNITY_EDITOR
			var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
				"Packages/com.saab.map-streamer/Runtime/Shaders/Foliage/FoliagePlacement.compute");
			Assert.That(asset, Is.Not.Null);
			return Own(Object.Instantiate(asset));
#else
			Assert.Inconclusive("The package compute fixture is loaded by the Unity Editor test runner.");
			return null;
#endif
		}

		private Texture2D Image(Color color)
		{
			var image = Own(new Texture2D(4, 4, TextureFormat.RGBA32, false, true));
			image.SetPixels(Enumerable.Repeat(color, 16).ToArray());
			image.Apply(false);
			return image;
		}

		private Mesh Plane()
		{
			var mesh = Own(new Mesh
			{
				vertices = new[]
				{
					new Vector3(0, 0, 0), new Vector3(4, 0, 0),
					new Vector3(0, 0, 4), new Vector3(4, 0, 4)
				},
				normals = Enumerable.Repeat(Vector3.up, 4).ToArray(),
				uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
				triangles = new[] { 0, 2, 1, 1, 2, 3 }
			});
			mesh.RecalculateBounds();
			return mesh;
		}

		private T Own<T>(T value) where T : Object
		{
			_objects.Add(value);
			return value;
		}

		private static T Field<T>(object target, string name)
		{
			return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
				.GetValue(target);
		}

		private static void SetField(object target, string name, object value)
		{
			target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
				.SetValue(target, value);
		}

		private static void Invoke(object target, string name, params object[] args)
		{
			target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(target, args);
		}

		private static uint FirstUInt(ComputeBuffer buffer)
		{
			var value = new uint[1];
			buffer.GetData(value);
			return value[0];
		}

		private static Vector3 UnpackNormal(uint value)
		{
			var x = (short)(value & 0xffff) / 32767f;
			var y = (short)(value >> 16) / 32767f;
			var result = new Vector3(x, y, 1 - Mathf.Abs(x) - Mathf.Abs(y));
			if (result.z < 0)
			{
				result.x = (1 - Mathf.Abs(y)) * (x >= 0 ? 1 : -1);
				result.y = (1 - Mathf.Abs(x)) * (y >= 0 ? 1 : -1);
			}
			return result.normalized;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct FoliageType
		{
			internal Vector2 MinMax;
			internal Vector2 Offset;
			internal float Weight;
			internal float CullAreaWidth;
		}

		private sealed class Presentation : IScenePresentation
		{
			private readonly Dictionary<GameObject, TerrainPresentationData> _terrain =
				new Dictionary<GameObject, TerrainPresentationData>();
			public Camera Camera { get; }
			internal int TerrainSubscribers => OnNewTerrain?.GetInvocationList().Length ?? 0;
			internal object TerrainSubscriber => OnNewTerrain?.GetInvocationList()[0].Target;
			internal int PostSubscribers => OnPostTraverse?.GetInvocationList().Length ?? 0;

			internal Presentation(Camera camera) { Camera = camera; }
			public bool TryGetTerrain(GameObject go, out TerrainPresentationData data) =>
				_terrain.TryGetValue(go, out data);

			internal void AddTerrain(GameObject go, TerrainPresentationData data)
			{
				_terrain.Add(go, data);
				OnNewTerrain?.Invoke(go, false);
			}

			internal void RemoveTerrain(GameObject go)
			{
				OnRemoveTerrain?.Invoke(go);
				_terrain.Remove(go);
			}

			internal void PostPresent() { OnPostTraverse?.Invoke(false); }

			public event SceneGameObjectHandler OnNewTerrain;
			public event SceneGameObjectReleaseHandler OnRemoveTerrain;
			public event SceneTraverseHandler OnPostTraverse;
			public event SceneGameObjectHandler OnNewGeometry { add { } remove { } }
			public event SceneGameObjectHandler OnNewCrossboard { add { } remove { } }
			public event SceneGameObjectHandler OnNewLod { add { } remove { } }
			public event SceneGameObjectHandler OnNewLoader { add { } remove { } }
			public event SceneGameObjectReleaseHandler OnEnterPool { add { } remove { } }
			public event SceneGameObjectReleaseHandler OnRemoveGeometry { add { } remove { } }
			public event SceneTraverseHandler OnPreTraverse { add { } remove { } }
			public event Action OnMapChanged { add { } remove { } }
			public event Action<string> OnMapLoadError { add { } remove { } }
			public event SceneUpdateCameraHandler OnUpdateCamera { add { } remove { } }
		}
	}
}
