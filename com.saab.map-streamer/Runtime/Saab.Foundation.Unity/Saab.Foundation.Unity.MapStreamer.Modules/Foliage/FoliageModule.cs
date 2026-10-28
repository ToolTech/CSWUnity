/*
 * Copyright (C) SAAB AB
 *
 * All rights, including the copyright, to the computer program(s) 
 * herein belong to Saab AB. The program(s) may be used and/or
 * copied only with the written permission of Saab AB, or in
 * accordance with the terms and conditions stipulated in the
 * agreement/contract under which the program(s) have been
 * supplied. 
 * 
 * Information Class:          COMPANY RESTRICTED
 * Defence Secrecy:            UNCLASSIFIED
 * Export Control:             NOT EXPORT CONTROLLED
 *
 * Revision History...
 *
 * Who	Date	Description
 *
 * AMO	261022	Added presentation adapters and transactional GPU resource lifetime handling.	(2.12.327)
 * AMO	261028	Removed the legacy SceneManager source; foliage follows the command-driven presentation only.	(2.12.327)
 */

using GizmoSDK.GizmoBase;
using Saab.Utility.GfxCaps;
using Saab.Foundation.Unity.MapStreamer.Csw;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Serialization;

using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;
using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{

    [Serializable]
    public class FeatureSet
    {
        public SettingsFeatureType SettingsType;
        public MapFeature mapFeature;
        public FoliageSet FoliageSet;
        public bool Enabled;
        [Header("Main Settings")]
        public int BufferSize;
        [Range(0.001f, 1)]
        public float ScreenCoverage = 0.001f;
        public float Density;
        [Min(0.0f)]
        public float NearCullDistance;
        /// <summary>
        /// Node LOD's larger than this will not use features from this set.
        /// </summary>
        public uint NodeMaxWidth;
        public bool Shadows;
        public bool Crossboard;
        [Header("calculated drawDistance")]
        public float DrawDistance;

        public FoliageFeature FoliageFeature;
        public ComputeBuffer InderectBuffer;
        public ComputeBuffer FoliageData;
        public ComputeBuffer CulledBuffer { get; set; }

        public float MaxHeight { get; set; }

        public Material FoliageMaterial { get; set; }
        internal Texture2DArray FoliageTextures;

        public void Dispose()
        {
            var cleanup = new SceneCleanup();
            cleanup.Attempt(() => FoliageFeature?.Dispose());
            cleanup.Attempt(() => InderectBuffer?.Dispose());
            cleanup.Attempt(() => FoliageData?.Dispose());
            if (FoliageMaterial)
                UnityEngine.Object.Destroy(FoliageMaterial);
            if (FoliageTextures)
                UnityEngine.Object.Destroy(FoliageTextures);
            cleanup.Complete("Foliage set cleanup failed.");
        }
    }

    public class FoliageModule : MonoBehaviour
    {
        public IScenePresentation Presentation { get; private set; }
        public ComputeShader ComputeShader;
        public Shader FoliageShader;

        //_PerlinNoise
        public Texture2D PerlinNoise;
        public int Layer = 0;   // 0 = Unity "Default" layer 

        [Header("Debug Settings")]
        public bool DebugPrintCount = false;
        public bool Disabled = false;
        public bool DebugNoDraw = false;
        public bool NativeLeakDetection = false;
        public bool Occlusion = true;
        [Min(0.0f)]
        public float HiZDepthBias = 0.25f;

        [SerializeField]
        private BuiltInHiZDepthPyramid _hiZDepthPyramid;
        public BuiltInHiZDepthPyramid HiZDepthPyramid
        {
            get => _hiZDepthPyramid;
            set => _hiZDepthPyramid = value;
        }

        [Header("Foliage Draw")]
        public List<FeatureSet> Features = new List<FeatureSet>();

        private Vector4[] _frustum = new Vector4[6];
        private int[] _mappingTable;

        // **************** Generate HeightMap ****************
        private bool _hasCullingResult;
        private bool _hasEnabledFeatures;
        private UnityEngine.Camera _cullingCamera;
        private int _lastCullingFrame = -1;

        private Queue<FoliageJob> _futurePool = new Queue<FoliageJob>();
        private Dictionary<SettingsFeatureType, SettingsFeature> _settingsCache = new Dictionary<SettingsFeatureType, SettingsFeature>();
        private bool _subscribed;

        /// <summary>Switches borrowed presentation sources after detaching previous subscriptions.</summary>
        /// <remarks>Call on the Unity main thread before releasing the previous source.</remarks>
        public void SetPresentation(IScenePresentation presentation)
        {
            Unsubscribe();
            var cleanup = new SceneCleanup();
            if (!ReferenceEquals(Presentation, presentation))
                foreach (var set in Features)
                    cleanup.Attempt(() => set.FoliageFeature?.ClearNodes());
            Presentation = presentation;
            if (_hasEnabledFeatures)
                Subscribe();
            cleanup.Complete("The previous foliage presentation could not be cleared completely.");
        }

        private UnityEngine.Camera ActiveCamera => Presentation?.Camera;

        private void Subscribe()
        {
            if (_subscribed || Presentation == null)
                return;
            Presentation.OnNewTerrain += SceneManager_OnNewTerrain;
            Presentation.OnPostTraverse += SceneManager_OnPostTraverse;
            Presentation.OnRemoveTerrain += SceneManager_OnRemoveTerrain;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;
            if (Presentation != null)
            {
                Presentation.OnNewTerrain -= SceneManager_OnNewTerrain;
                Presentation.OnPostTraverse -= SceneManager_OnPostTraverse;
                Presentation.OnRemoveTerrain -= SceneManager_OnRemoveTerrain;
            }
            _subscribed = false;
            _hasCullingResult = false;
            _lastCullingFrame = -1;
        }

        private static class PlacementParameterID
        {
            public static readonly int FoliageData = Shader.PropertyToID("_foliageData");
            public static readonly int TerrainResolution = Shader.PropertyToID("terrainResolution");
            public static readonly int TerrainSize = Shader.PropertyToID("terrainSize");
            public static readonly int NodeOffset = Shader.PropertyToID("NodeOffset");
            public static readonly int Resolution = Shader.PropertyToID("Resolution");
            public static readonly int ObjToWorld = Shader.PropertyToID("ObjToWorld");
            public static readonly int IndexCount = Shader.PropertyToID("indexCount");
            public static readonly int UvCount = Shader.PropertyToID("uvCount");
            public static readonly int FrameCount = Shader.PropertyToID("FrameCount");

            public static readonly int IndexBuffer = Shader.PropertyToID("IndexBuffer");
            public static readonly int VertexBuffer = Shader.PropertyToID("VertexBuffer");
            public static readonly int MeshBoundsMax = Shader.PropertyToID("MeshBoundsMax");
            public static readonly int VertexBufferStride = Shader.PropertyToID("VertexBufferStride");
            public static readonly int TexcoordOffset = Shader.PropertyToID("TexcoordOffset");
            public static readonly int NormalOffset = Shader.PropertyToID("NormalOffset");
            public static readonly int PositionOffset = Shader.PropertyToID("PositionOffset");
            public static readonly int SurfaceHeightMap = Shader.PropertyToID("SurfaceHeightMap");
            public static readonly int PixelToObjectCoord = Shader.PropertyToID("PixelToObjectCoord");

            public static readonly int Texture = Shader.PropertyToID("Texture");
            public static readonly int Density = Shader.PropertyToID("Density");

            // Material
            public static readonly int IsToggled = Shader.PropertyToID("_isToggled");
            public static readonly int PerlinNoise = Shader.PropertyToID("_PerlinNoise");
            public static readonly int FoliageCount = Shader.PropertyToID("_foliageCount");
            public static readonly int MainTexArray = Shader.PropertyToID("_MainTexArray");
            public static readonly int PointBuffer = Shader.PropertyToID("_PointBuffer");

            public static readonly string CROSSBOARD_ON = "CROSSBOARD_ON";

        }
        public int GetFoliageCount
        {
            get
            {
                var count = 0;
                foreach (var feature in Features)
                {
                    count += feature.FoliageFeature?.FoliageCount ?? 0;
                }
                return count;
            }
        }

        // Start is called before the first frame update
        void Start()
        {
            for (int i = 0; i < Features.Count; i++)
            {
                var featureSet = Features[i];
                var settings = GetSettings(featureSet.SettingsType);
                featureSet.Enabled = settings.Enabled;
                featureSet.NearCullDistance = settings.NearCullDistance;
                _hasEnabledFeatures |= featureSet.Enabled;
            }

            if (!_hasEnabledFeatures)
            {
                if (HiZDepthPyramid != null)
                    HiZDepthPyramid.enabled = false;
                return;
            }

            _mappingTable = TerrainMapping.MapFeatureData();

            Subscribe();

            for (int i = 0; i < Features.Count; i++)
            {
                var featureSet = Features[i];
                if (!featureSet.Enabled)
                    continue;
                var settings = GetSettings(featureSet.SettingsType);
                featureSet.FoliageFeature = new FoliageFeature(Mathf.CeilToInt(featureSet.BufferSize * settings.Density), featureSet.Density * settings.Density, TerrainMapping.FeatureTruthTable(_mappingTable, featureSet.mapFeature), ComputeShader);

                var inderectBuffer = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
                inderectBuffer.SetData(new uint[] { 0, 1, 0, 0 });
                featureSet.InderectBuffer = inderectBuffer;
                featureSet.FoliageMaterial = new Material(FoliageShader);

                //TODO: use to create a random noise
                featureSet.FoliageMaterial.SetTexture(PlacementParameterID.PerlinNoise, PerlinNoise);

                if (featureSet.Crossboard)
                {
                    featureSet.FoliageMaterial.SetFloat(PlacementParameterID.IsToggled, 0);
                    featureSet.FoliageMaterial.EnableKeyword(PlacementParameterID.CROSSBOARD_ON);
                }
                else
                {
                    featureSet.FoliageMaterial.SetFloat(PlacementParameterID.IsToggled, 1);
                    featureSet.FoliageMaterial.DisableKeyword(PlacementParameterID.CROSSBOARD_ON);
                }
                featureSet.MaxHeight = featureSet.FoliageSet.GetMaxHeight;

                SetupFoliage(featureSet);
            }
        }

        struct FoliageShaderData
        {
            public Vector2 MaxMin;
            public Vector2 Offset;
            public float Weight;
            public float CullAreaWidth;
        };

        private SettingsFeature GetSettings(SettingsFeatureType settingsType)
        {
            if (!_settingsCache.TryGetValue(settingsType, out var settings))
            {
                settings = GfxCaps.GetFoliageSettings(settingsType);
                _settingsCache[settingsType] = settings;
            }
            return settings;
        }

        private void SetupFoliage(FeatureSet featureSet)
        {
#if UNITY_ANDROID
            var format = TextureFormat.ETC2_RGBA8;
            Debug.Log("foliage Use ETC2");
#else
            var format = TextureFormat.DXT5;
#endif           
            var foliageTypes = featureSet.FoliageSet.GetFoliageList;
            var mainTexs = Create2DArray(foliageTypes, format);
            featureSet.FoliageTextures = mainTexs;

            featureSet.FoliageMaterial.SetInt(PlacementParameterID.FoliageCount, foliageTypes.Count);
            featureSet.FoliageMaterial.SetTexture(PlacementParameterID.MainTexArray, mainTexs);

            var data = new FoliageShaderData[foliageTypes.Count];
            for (int i = 0; i < foliageTypes.Count; i++)
            {
                var foliage = foliageTypes[i];

                var item = new FoliageShaderData()
                {
                    MaxMin = foliage.MaxMin,
                    Offset = foliage.Offset,
                    Weight = foliage.Weight,
                    CullAreaWidth = foliage.CullAreaWidth,
                };
                data[i] = item;
            }

            if (featureSet.FoliageData != null)
                featureSet.FoliageData.Release();

            featureSet.FoliageData = new ComputeBuffer(
                foliageTypes.Count,
                UnsafeUtility.SizeOf<FoliageShaderData>(),
                ComputeBufferType.Default);
            featureSet.FoliageData.SetData(data);
            featureSet.FoliageMaterial.SetBuffer(PlacementParameterID.FoliageData, featureSet.FoliageData);
        }
        private static uint NextPowerOfTwo(uint v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;

            return v + 1;
        }
        public Texture2DArray Create2DArray(List<Foliage> foliages, TextureFormat targetFormat)
        {
            var textureCount = foliages.Count;
            var textureResolution = Mathf.Max(foliages.Max(item => item.MainTexture.width), foliages.Max(item => item.MainTexture.height));
            textureResolution = (int)NextPowerOfTwo((uint)textureResolution);

            Texture2DArray textureArray;

            textureArray = new Texture2DArray(textureResolution, textureResolution, textureCount, targetFormat, true)
            {
                wrapMode = TextureWrapMode.Clamp
            };

            RenderTexture temporaryRenderTexture = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                useMipMap = true,
                antiAliasing = 1,
                name = "foliagemodule - temporaryRenderTexture - 2dArray"
            };

            for (int i = 0; i < textureCount; i++)
            {
                //Debug.LogWarning($"graphic format: {foliages[i].MainTexture.graphicsFormat} format: {foliages[i].MainTexture.format}");
                Graphics.Blit(foliages[i].MainTexture, temporaryRenderTexture);
                RenderTexture.active = temporaryRenderTexture;

                Texture2D temporaryTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.ARGB32, true);
                temporaryTexture.ReadPixels(new Rect(0, 0, temporaryTexture.width, temporaryTexture.height), 0, 0);
                RenderTexture.active = null;
                temporaryTexture.Apply(updateMipmaps: true);
                temporaryTexture.Compress(true);
                Graphics.CopyTexture(temporaryTexture, 0, textureArray, i);

                DestroyImmediate(temporaryTexture);
            }
            textureArray.Apply(false, true);

            temporaryRenderTexture.Release();
            DestroyImmediate(temporaryRenderTexture);

            return textureArray;
        }

        private readonly Plane[] _frustrumPlanes = new Plane[6];
        private void GenerateFrustumPlane(UnityEngine.Camera camera)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, _frustrumPlanes);

            for (int i = 0; i < 6; i++)
            {
                Vector3 normal = _frustrumPlanes[i].normal;
                _frustum[i].x = normal.x;
                _frustum[i].y = normal.y;
                _frustum[i].z = normal.z;
                _frustum[i].w = _frustrumPlanes[i].distance;
            }
        }
        private void SceneManager_OnRemoveTerrain(GameObject go)
        {
            foreach (var set in Features)
            {
                set.FoliageFeature?.RemoveFoliage(go);
            }
        }

        struct FoliageJob
        {
            public GameObject gameObject { get; set; }
            public int FrameCount { get; set; }
        }

        private void SceneManager_OnNewTerrain(GameObject go, bool isAsset)
        {
            if (isAsset)
                return;

            AddJob(go);
        }

        private void AddJob(GameObject go)
        {
            if (Disabled || Presentation == null)
                return;

            if (!go.TryGetComponent<MeshFilter>(out var meshFilter))
                return;

            var mesh = meshFilter.sharedMesh;
            if (!mesh)
                return;

            if (!Presentation.TryGetTerrain(go, out var terrain))
                return;
            if (!terrain.Texture || !terrain.Feature)
                return;

            var featureInfo = terrain.FeatureInfo;

            var pixelSize = new Vector2((float)featureInfo.v11, (float)featureInfo.v22);
            float scale = 1000;

            var nodeOffset = new Vector2(
                (float)(featureInfo.v13 + featureInfo.v11) % scale,
                (float)(featureInfo.v23 + featureInfo.v22) % scale);

            var tex = terrain.Texture;
            var texSize = new Vector2(tex.width, tex.height);

            var fullNodeSize = pixelSize * texSize;
            var nodeSide = Mathf.Max(fullNodeSize.x, fullNodeSize.y);

            if (nodeSide > 2048)
                return;

            ComputeShader.SetVector(PlacementParameterID.TerrainResolution, texSize);
            ComputeShader.SetVector(PlacementParameterID.TerrainSize, mesh.bounds.size);
            ComputeShader.SetVector(PlacementParameterID.NodeOffset, nodeOffset);
            ComputeShader.SetVector(PlacementParameterID.Resolution, pixelSize);

            var pixelToWorld = GeneratePixelToWorld(
                texSize, pixelSize, mesh,
                new Vec3D(terrain.FeatureCenterOffset.x, terrain.FeatureCenterOffset.y, terrain.FeatureCenterOffset.z));
            RenderTexture surface = null;
            try
            {
                if (!terrain.SurfaceHeight)
                    surface = GenerateSurfaceHeight(tex);
            }
            catch
            {
                pixelToWorld.Release();
                throw;
            }
            using (var resources = new FoliagePlacementLease(pixelToWorld, surface))
            {
                foreach (var set in Features)
                {
                    if (!set.Enabled)
                        continue;
                    var setting = GetSettings(set.SettingsType);
                    ComputeShader.SetFloat(PlacementParameterID.Density, set.Density * setting.Density);
                    if (nodeSide < set.NodeMaxWidth)
                        set.FoliageFeature.AddFoliage(go, terrain, resources);
                }
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();

            foreach (var set in Features)
            {
                set?.Dispose();
            }

        }

        private RenderTexture GenerateSurfaceHeight(UnityEngine.Texture texture)
        {
            var surface = new RenderTexture(texture.width, texture.height, 24, RenderTextureFormat.RFloat);
            try
            {
                surface.enableRandomWrite = true;
                surface.name = "foliagemodule - surfaceheightMap";
                if (!surface.Create())
                    throw new InvalidOperationException("Could not allocate the foliage surface-height texture.");
                var kernel = ComputeShader.FindKernel("CSSurfaceHeightMap");
                ComputeShader.SetTexture(kernel, PlacementParameterID.Texture, texture);
                ComputeShader.SetTexture(kernel, PlacementParameterID.SurfaceHeightMap, surface);
                var threadx = Mathf.Max(1, Mathf.CeilToInt(texture.width / 8f));
                var thready = Mathf.Max(1, Mathf.CeilToInt(texture.height / 8f));
                ComputeShader.Dispatch(kernel, threadx, thready, 1);
                return surface;
            }
            catch
            {
                surface.Release();
                SceneObjects.Destroy(surface);
                throw;
            }
        }

        private static GraphicsBuffer CreateIndexBufferCopy(Mesh mesh)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));

            int[] indices = mesh.GetIndices(0); // TODO: switch to meshData.GetIndexData

            if (mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16)
            {
                int packedCount = (indices.Length + 1) / 2;

                uint[] packed = new uint[packedCount];

                for (int i = 0; i < indices.Length; i += 2)
                {
                    uint low = (uint)(indices[i] & 0xffff);

                    uint high = (i + 1 < indices.Length)
                        ? ((uint)(indices[i + 1] & 0xffff) << 16)
                        : 0u;

                    packed[i / 2] = low | high;
                }

                var dst = new GraphicsBuffer(
                    GraphicsBuffer.Target.Raw,
                    packed.Length,
                    sizeof(uint));

                try
                {
                    dst.SetData(packed);
                    return dst;
                }
                catch
                {
                    dst.Release();
                    throw;
                }
            }
            else
            {
                uint[] indices32 = new uint[indices.Length];

                for (int i = 0; i < indices.Length; i++)
                    indices32[i] = (uint)indices[i];

                var dst = new GraphicsBuffer(
                    GraphicsBuffer.Target.Raw,
                    indices32.Length,
                    sizeof(uint));

                try
                {
                    dst.SetData(indices32);
                    return dst;
                }
                catch
                {
                    dst.Release();
                    throw;
                }
            }
        }

        private ComputeBuffer GeneratePixelToWorld(Vector2 texSize, Vector2 pixelSize, Mesh mesh, Vec3D offset)
        {
            ComputeShader.SetVector(PlacementParameterID.Resolution, pixelSize);
            mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
            mesh.indexBufferTarget |= GraphicsBuffer.Target.CopySource;
            GraphicsBuffer vertexBuffer = null;
            GraphicsBuffer indexBuffer = null;
            GraphicsBuffer indexCopy = null;
            ComputeBuffer pixelToWorld = null;
            try
            {
                vertexBuffer = mesh.GetVertexBuffer(0);
                indexBuffer = mesh.GetIndexBuffer();
                indexCopy = CreateIndexBufferCopy(mesh);
                int stride = mesh.GetVertexBufferStride(0);
                int texOffset = mesh.GetVertexAttributeOffset(UnityEngine.Rendering.VertexAttribute.TexCoord0);
                int posOffset = mesh.GetVertexAttributeOffset(UnityEngine.Rendering.VertexAttribute.Position);
                int normalOffset = mesh.GetVertexAttributeOffset(UnityEngine.Rendering.VertexAttribute.Normal);
                if (posOffset < 0 || normalOffset < 0 || texOffset < 0)
                    throw new InvalidOperationException("Foliage placement requires mesh positions, normals and UV0.");
                ComputeShader.SetInt(PlacementParameterID.PositionOffset, posOffset);
                ComputeShader.SetInt(PlacementParameterID.TexcoordOffset, texOffset);
                ComputeShader.SetInt(PlacementParameterID.NormalOffset, normalOffset);
                ComputeShader.SetInt(PlacementParameterID.VertexBufferStride, stride);
                int indicesCount = (int)mesh.GetIndexCount(0);
                ComputeShader.SetInt(PlacementParameterID.UvCount, vertexBuffer.count);
                var nodeTexTopLeft = mesh.bounds.center -
                    new Vector3((float)-offset.x, (float)offset.y, (float)offset.z);
                ComputeShader.SetVector(PlacementParameterID.MeshBoundsMax, nodeTexTopLeft);
                int kernelHeight = ComputeShader.FindKernel("CSPixelToObject");
                pixelToWorld = new ComputeBuffer(
                    Mathf.CeilToInt(texSize.x * texSize.y), sizeof(float) * 4, ComputeBufferType.Default);
                int kernelClear = ComputeShader.FindKernel("CSClearPixelToObject");
                ComputeShader.SetBuffer(kernelClear, PlacementParameterID.PixelToObjectCoord, pixelToWorld);
                ComputeShader.Dispatch(kernelClear, Mathf.CeilToInt(pixelToWorld.count / 128f), 1, 1);
                int triangleCount = Mathf.CeilToInt(indicesCount / 3f);
                ComputeShader.SetInt(PlacementParameterID.IndexCount, triangleCount);
                ComputeShader.SetBuffer(kernelHeight, PlacementParameterID.VertexBuffer, vertexBuffer);
                ComputeShader.SetBuffer(kernelHeight, PlacementParameterID.IndexBuffer, indexCopy);
                ComputeShader.SetBuffer(kernelHeight, PlacementParameterID.PixelToObjectCoord, pixelToWorld);
                ComputeShader.Dispatch(kernelHeight, Mathf.Max(1, Mathf.CeilToInt(triangleCount / 4f)), 1, 1);
                var result = pixelToWorld;
                pixelToWorld = null;
                return result;
            }
            finally
            {
                pixelToWorld?.Release();
                indexCopy?.Release();
                indexBuffer?.Release();
                vertexBuffer?.Release();
            }
        }

        float CalculateDesiredDistance(UnityEngine.Camera camera, float objectHeight, float coverage)
        {
            // Convert FOV from degrees to radians and halve it
            float halfFov = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;

            // Calculate the desired distance so that the object covers the desired percentage of the screen
            float desiredDistance = objectHeight / (2.0f * Mathf.Tan(halfFov) * coverage);

            return desiredDistance;
        }

        private static readonly ProfilerMarker _profilerMarker = new ProfilerMarker(ProfilerCategory.Render, "Foliage-Render");

        private void SceneManager_OnPostTraverse(bool locked)
        {
            if (Disabled || !_hasEnabledFeatures)
                return;

            _profilerMarker.Begin();

            var camera = ActiveCamera;
            if (camera == null)
            {
                _profilerMarker.End();
                return;
            }
            if (camera != null && _cullingCamera != camera)
            {
                _cullingCamera = camera;
                _hasCullingResult = false;
                _lastCullingFrame = -1;
            }

            Cull(_cullingCamera);
            Draw();

            _profilerMarker.End();
        }

        private void Cull(UnityEngine.Camera camera)
        {
            var activeCamera = ActiveCamera;
            if (camera != activeCamera || Disabled || _lastCullingFrame == UnityEngine.Time.frameCount)
                return;

            if (_cullingCamera != camera)
            {
                _cullingCamera = camera;
                _hasCullingResult = false;
            }

            UpdateCulling(camera);
            _lastCullingFrame = UnityEngine.Time.frameCount;
            _hasCullingResult = true;
        }

        private void UpdateCulling(UnityEngine.Camera camera)
        {
            if (NativeLeakDetection)
                UnsafeUtility.SetLeakDetectionMode(NativeLeakDetectionMode.EnabledWithStackTrace);
            else
                UnsafeUtility.SetLeakDetectionMode(NativeLeakDetectionMode.Disabled);

            GenerateFrustumPlane(camera);

            ComputeShader.SetInt(PlacementParameterID.FrameCount, UnityEngine.Time.frameCount);

            // Camera switches, resize frames, and unavailable Hi-Z resources safely fall back to
            // frustum/distance culling instead of using stale depth or hiding foliage.
            bool useHiZ = Occlusion && HiZDepthPyramid != null && HiZDepthPyramid.IsReadyFor(camera);
            RenderTexture hiZTexture = useHiZ ? HiZDepthPyramid.HiZTexture : null;
            Vector2Int hiZSize = useHiZ ? HiZDepthPyramid.TextureSize : Vector2Int.one;
            int hiZMaxMipLevel = useHiZ ? HiZDepthPyramid.MaxMipLevel : 0;

            foreach (var set in Features)
            {
                var settings = GetSettings(set.SettingsType);
                if (set.FoliageFeature != null &&
                    (!settings.Enabled || !set.Enabled || !set.FoliageFeature.HasActiveNodes))
                {
                    set.CulledBuffer = set.FoliageFeature.ResetCulling();
                    ComputeBuffer.CopyCount(set.CulledBuffer, set.InderectBuffer, 0);
                    continue;
                }
                if (!settings.Enabled || !set.Enabled)
                    continue;

                if (set.FoliageMaterial == null)
                {
                    Debug.LogError($"FoliageMaterial for {set.mapFeature} can't be NULL!");
                    continue;
                }

                var dist = CalculateDesiredDistance(camera, set.MaxHeight, set.ScreenCoverage);
                set.DrawDistance = dist * settings.DrawDistance;
                _frustum[5].w = set.DrawDistance;  // draw distance

                // Render all points
                var buffer = set.FoliageFeature.Cull(
                    _frustum,
                    camera,
                    hiZTexture,
                    hiZSize,
                    hiZMaxMipLevel,
                    HiZDepthBias,
                    useHiZ,
                    set);
                set.CulledBuffer = buffer;
                set.FoliageMaterial.SetBuffer(PlacementParameterID.PointBuffer, buffer);
                ComputeBuffer.CopyCount(buffer, set.InderectBuffer, 0);
            }
        }

        private void Draw()
        {
            if (Disabled || !_hasCullingResult)
                return;

            foreach (var set in Features)
            {
                var settings = GetSettings(set.SettingsType);
                if (!settings.Enabled || !set.Enabled)
                    continue;

                if (set.FoliageMaterial == null)
                    continue;
                if (set.FoliageFeature == null || !set.FoliageFeature.HasActiveNodes)
                    continue;

                if (DebugPrintCount)
                {
                    int[] array = new int[4];
                    set.InderectBuffer.GetData(array);
                    int bufferCount = set.CulledBuffer != null ? set.CulledBuffer.count : 0;
                    UnityEngine.Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{2} :: {0}/{1}", array[0].ToString(), bufferCount, set.FoliageSet.name);
                }

                if (DebugNoDraw)
                    return;

                var renderBounds = new Bounds(Vector3.zero, new Vector3(set.DrawDistance, set.DrawDistance, set.DrawDistance));
                Graphics.DrawProceduralIndirect(set.FoliageMaterial, renderBounds, MeshTopology.Points, set.InderectBuffer, 0, null, null, settings.Shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off, true, Layer);
            }

            DebugPrintCount = false;
        }
    }
}