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
 * AMO	261022	Added borrowed presentation adapters and explicit module resource teardown.	(2.12.327)
 */

using UnityEngine;
using Saab.Utility.GfxCaps;
using System.Linq;
using System.Collections.Generic;
using System;
using Saab.Foundation.Unity.MapStreamer.Utils;
using Saab.Foundation.Unity.MapStreamer.Csw;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    public struct TerrainData : IDisposable
    {
        public Material Material;
        public ComputeBuffer NormalBuffer;
        public GameObject Object;

        public void GenerateNormal(Mesh mesh, ComputeShader normalComputeShader)
        {
            // Extract vertex positions and indices from the mesh
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            // Create and initialize compute buffers
            var vertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
            var indexBuffer = new ComputeBuffer(triangles.Length / 3, sizeof(int) * 3);
            NormalBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);

            vertexBuffer.SetData(vertices);
            indexBuffer.SetData(triangles);

            // Set compute shader parameters
            normalComputeShader.SetBuffer(0, "vertexPositions", vertexBuffer);
            normalComputeShader.SetBuffer(0, "triangleIndices", indexBuffer);
            normalComputeShader.SetBuffer(0, "vertexNormals", NormalBuffer);
            normalComputeShader.SetInt("numVertices", vertices.Length);
            normalComputeShader.SetInt("triangleIndicesLength", triangles.Length);

            // Dispatch the compute shader
            int threadGroups = Mathf.CeilToInt(vertices.Length / 64.0f);
            normalComputeShader.Dispatch(0, threadGroups, 1, 1);

            vertexBuffer.Release();
            indexBuffer.Release();
        }

        public TerrainData(Material material, GameObject go)
        {
            Object = go;
            Material = material;
            NormalBuffer = null;
        }

        public void Dispose()
        {
            NormalBuffer?.Release();
        }
    }

    public class MapShadingModule : MonoBehaviour
    {
        public SceneManager SceneManager;
        public IScenePresentation Presentation { get; private set; }
        private bool _initialized;
        private bool _subscribed;
        private bool _awoken;

        /// <summary>Switches borrowed presentation sources after detaching previous subscriptions.</summary>
        /// <remarks>Call on the Unity main thread before releasing the previous source.</remarks>
        public void SetPresentation(IScenePresentation presentation)
        {
            Unsubscribe();
            Presentation = presentation;
            if (_awoken)
                TryInitialize();
        }

        public bool EnableDetailedTextures
        {
            get
            {
                return _enableDetailedTextures;
            }
            set
            {
                _enableDetailedTextures = value;
            }
        }

        public float HueShiftInclusion
        {
            get
            {
                return _hueShiftInclusion;
            }
            set
            {
                _hueShiftInclusion = value;
            }
        }

        public TerrainDetailTextureAssetSet DetailTextureSet
        {
            get
            {
                return _detailTextureSet;
            }
            set
            {
                _detailTextureSet = value;
            }
        }

        [Header("Main Settings")]
        [SerializeField] private bool _enableDetailedTextures = true;
        [Range(0, 1)]
        [SerializeField] private float _hueShiftInclusion = 0.4f;
        [SerializeField] private Color _targetHue = new Color(70f / 256f, 140f / 256f, 70f / 256f);

        [Header("Module Settings")]
        [SerializeField] private TerrainDetailTextureAssetSet _detailTextureSet;

        private ComputeBuffer _mappingBuffer;
        private Texture2DArray _textureArray;
        private Texture2DArray _normalMapArray;

        private readonly Dictionary<GameObject, TerrainData> _terrainData = new Dictionary<GameObject, TerrainData>();

        private void Awake()
        {
            _awoken = true;
            TryInitialize();
        }

        private void TryInitialize()
        {
            if (_initialized)
            {
                InitMapModules();
                return;
            }
            if (SceneManager == null && Presentation == null)
                return;

            _enableDetailedTextures = GfxCaps.CurrentCaps.HasFlag(Capability.UseTerrainDetailTextures);
            InitializeModule();
            RefreshSettings();
        }

        private static class MaterialParameterID
        {
            public static readonly int NormalBuffer = Shader.PropertyToID("_NormalBuffer");
            public static readonly int FeatureMap = Shader.PropertyToID("_FeatureMap");
            public static readonly int Textures = Shader.PropertyToID("_Textures");
            public static readonly int NormalMaps = Shader.PropertyToID("_NormalMaps");
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_mappingBuffer != null)
                _mappingBuffer.Release();
            if (_textureArray)
                Destroy(_textureArray);
            if (_normalMapArray)
                Destroy(_normalMapArray);
        }

        public void InitializeModule()
        {
            if (_initialized)
            {
                InitMapModules();
                return;
            }
            if ((SceneManager || Presentation != null) && _enableDetailedTextures)
            {
                InitDetailTexturing();
                _initialized = true;
                InitMapModules();
            }
        }

        private void InitMapModules()
        {
            if (_subscribed)
                return;
            if (Presentation != null)
            {
                Presentation.OnNewTerrain += SceneManager_OnNewTerrain;
                _subscribed = true;
            }
            else if (SceneManager)
            {
                SceneManager.OnNewTerrain += SceneManager_OnNewTerrain;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;
            if (Presentation != null)
                Presentation.OnNewTerrain -= SceneManager_OnNewTerrain;
            else if (SceneManager)
                SceneManager.OnNewTerrain -= SceneManager_OnNewTerrain;
            _subscribed = false;
        }

        private void InitDetailTexturing()
        {
            if (!_enableDetailedTextures || !_detailTextureSet)
                return;

            // TODO: look if we can do this on build instead in runtime (decrease load time)

            var mapping = TerrainMapping.MapFeatureData();
            var mapResult = new int[256];

            if (!_textureArray)
            {
                if (_detailTextureSet.Textures == null || _detailTextureSet.Textures.Count < 1)
                    return;

                int width = _detailTextureSet.Textures[0].Asset.Albedo.width;
                int height = _detailTextureSet.Textures[0].Asset.Albedo.height;

                List<int> resolved = new List<int>();
                for (int i = 0; i < _detailTextureSet.Textures.Count; i++)
                {
                    var textureAsset = _detailTextureSet.Textures[i];

                    var map = TerrainMapping.FeatureTruthTable(mapping, textureAsset.Mapping);

                    for (int j = 0; j < map.Length; j++)
                    {
                        var index = map[j];
                        if (index == 1)
                            mapResult[j] = i + 1;
                    }
                }

#if UNITY_ANDROID
            var format = TextureFormat.ETC2_RGBA8;
            Debug.Log("foliage Use ETC2");
#else
                var format = TextureFormat.DXT5;
#endif

                List<Texture2D> albedo = _detailTextureSet.Textures.Select(t => t.Asset.Albedo).ToList();
                List<Texture2D> normal = _detailTextureSet.Textures.Select(t => t.Asset.Normal).ToList();

                _textureArray = TextureUtility.Create2DArray(albedo, format);
                _normalMapArray = TextureUtility.Create2DArray(normal, format); ;
            }

            if (_mappingBuffer == null)
            {
                _mappingBuffer = new ComputeBuffer(mapResult.Length, sizeof(int));
                _mappingBuffer.SetData(mapResult);
            }
        }

        private void SceneManager_OnNewTerrain(GameObject go, bool isAsset)
        {
            if (Presentation != null)
            {
                if (!Presentation.TryGetTerrain(go, out var terrain) ||
                    !terrain.Feature || !terrain.Texture)
                    return;
            }
            else
            {
                if (!go.TryGetComponent<NodeHandle>(out var nodehandle) ||
                    !nodehandle.feature || !nodehandle.texture)
                    return;
            }

            if (!go.TryGetComponent<MeshRenderer>(out var meshRenderer))
                return;

            var material = meshRenderer.sharedMaterial;

            material.SetTexture(MaterialParameterID.Textures, _textureArray);
            material.SetTexture(MaterialParameterID.NormalMaps, _normalMapArray);

            material.SetBuffer("_MappingBuffer", _mappingBuffer);

            //TODO: Use TerrainMapping to find internal ID for water.
            material.SetInt("_WaterIndex", 60);
        }

        private void OnValidate()
        {
            RefreshSettings();
        }

        public void RefreshSettings()
        {
            Shader.SetGlobalColor("_TargetTerrainColor", _targetHue);
            Shader.SetGlobalFloat("_HueShift", _hueShiftInclusion);
        }
    }
}