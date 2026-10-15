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
 */

using UnityEngine;
using System.Collections.Generic;
using System;
using GizmoSDK.GizmoBase;

using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;
using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;
using System.Runtime.InteropServices;
using Saab.Unity.Extensions;
using Saab.Foundation.Map;

namespace Saab.Foundation.Unity.MapStreamer.Modules
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FoliagePoint
    {
        public Vector3 Position;
        public uint Color;

        public uint up;
        public uint right;

        //Packed0
        public short Height;
        public short Random;

        //Packed1
        public short Visibility;
        public short Pad0;
    }

    public struct FeatureData : IDisposable
    {
        public GameObject Object { get; private set; }
        public ComputeBuffer PlacementMatrix { get; private set; }
        public Vector2 NodeOffset { get; private set; }
        //public ComputeBuffer MinXY { get; private set; }
        public ComputeBuffer TerrainPoints;
        public ComputeBuffer TerrainPointCount;
        public Texture2D FeatureMap;
        public Texture2D Texture;
        public Texture surfaceHeight;
        public ComputeBuffer PixelToObject;

        public FeatureData(GameObject gameObject, Matrix3D matrix, float density, uint maxSide, float scale = 1000)
        {
            Object = gameObject;

            var stepsize = (1 / density) * 10;

            PlacementMatrix = new ComputeBuffer(9, sizeof(float), ComputeBufferType.Default);
            float[] data = {
                (float)matrix.v11, (float)matrix.v12, (float)((matrix.v13) % stepsize),
                (float)matrix.v21, (float)matrix.v22, (float)((matrix.v23) % stepsize),
                (float)matrix.v31, (float)matrix.v32, (float)matrix.v33
            };

            NodeOffset = new Vector2((float)(matrix.v13 + matrix.v11) % scale, (float)(matrix.v23 + matrix.v22) % scale);
            PlacementMatrix.SetData(data);

            PixelToObject = null;
            surfaceHeight = null;
            Texture = null;
            TerrainPoints = null;
            TerrainPointCount = null;
            FeatureMap = null;
        }

        public void Dispose()
        {
            if (surfaceHeight is RenderTexture rt)
                rt?.Release();
            TerrainPoints?.Release();
            TerrainPointCount?.Release();
            PlacementMatrix?.Release();
            PixelToObject?.Release();
        }
    }

    public class FoliageFeature : IDisposable
    {
        // list of all instances currently being rendered
        private readonly List<FeatureData> _items = new List<FeatureData>(128);
        // if a go exists in the render list, it exists in this lookup, used to avoid searching the list
        private readonly HashSet<GameObject> _itemLookup = new HashSet<GameObject>();

        private Vector2 _resolution;
        private readonly ComputeShader _placement;
        private readonly int _kernelCull;
        private readonly int _kernelClear;
        private readonly int _kernelPlacement;
        private readonly float _density;
        private readonly float _scale = 10000;

        // *********** buffers ***********
        private ComputeBuffer _mappingBuffer;
        private Vector2 _fov;
        private readonly ComputeBuffer _pointCloud;

        private readonly int _foliageStride;

        public int FoliageCount
        {
            get { return _items.Count; }
        }

        public FoliageFeature(int BufferSize, float density, int[] map, ComputeShader computeShader)
        {
            _foliageStride = Marshal.SizeOf<FoliagePoint>();

            _placement = computeShader;
            _kernelCull = _placement.FindKernel("CSCull");
            _kernelClear = _placement.FindKernel("CSClear");
            _kernelPlacement = _placement.FindKernel("CSPlacement");

            _density = density;
            _pointCloud = new ComputeBuffer(BufferSize <= 0 ? 1 : BufferSize, _foliageStride, ComputeBufferType.Append);
            _mappingBuffer = new ComputeBuffer(map.Length, sizeof(int));
            _mappingBuffer.SetData(map);
        }

        public bool AddFoliage(GameObject go, NodeHandle node, ComputeBuffer pixelToObject, Texture surfaceHeight = null)
        {
            if (pixelToObject == null)
                return false;

            Texture2D featureMap = node.feature;
            if (featureMap == null)
                return false;

            Texture height = node.surfaceHeight ?? surfaceHeight;
            if (height == null)
                return false;

            _resolution = new Vector2((float)node.featureInfo.v11, (float)node.featureInfo.v22);
            var size = FindBufferSize(featureMap);

            if (size >= ushort.MaxValue * 128)
                return false;

            var maxside = Mathf.Max(featureMap.width, featureMap.height);

            Texture2D texture = node.texture;
            var data = new FeatureData(go, node.featureInfo, _density, (uint)maxside, _scale)
            {
                FeatureMap = featureMap,
                Texture = texture,
                surfaceHeight = height,
                PixelToObject = pixelToObject
            };

            data.TerrainPoints = new ComputeBuffer(size < 1 ? 1 : size, _foliageStride, ComputeBufferType.Append);
            data.TerrainPointCount = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);

            FeaturePlacement(data);

            _items.Add(data);
            _itemLookup.Add(go);

            return true;
        }
        public void RemoveFoliage(GameObject gameObj)
        {
            if (!_itemLookup.Contains(gameObj))
                return;

            _itemLookup.Remove(gameObj);

            for (var i = 0; i < _items.Count; ++i)
            {
                if (_items[i].Object != gameObj)
                    continue;

                ClearFeature(_items[i]);

                if ((i + 1) < _items.Count)
                    _items[i] = _items[_items.Count - 1];

                _items.RemoveAt(_items.Count - 1);

                return;
            }
        }
        public void Dispose()
        {
            _pointCloud?.Release();
            _mappingBuffer?.Release();

            for (var i = 0; i < _items.Count; ++i)
            {
                ClearFeature(_items[i]);
            }
            _items.Clear();
        }

        // needed to clear old valid tree data from gpu memory, if skipped when frustum culling old trees might get valid/visable
        private void ClearFeature(in FeatureData data)
        {
            data.TerrainPoints.SetCounterValue(0);

            _placement.SetBuffer(_kernelClear, PlacementParameterID.TerrainPoints, data.TerrainPoints);
            _placement.SetInt(PlacementParameterID.BufferCount, data.TerrainPoints.count);
            if (data.TerrainPoints.count > 0)
                _placement.Dispatch(_kernelClear, Mathf.CeilToInt(data.TerrainPoints.count / 128f), 1, 1);

            data.Dispose();
        }

        private int FindBufferSize(Texture2D featureMap)
        {
            var maxSize =
                Mathf.CeilToInt(featureMap.width * _resolution.x * _density) *
                Mathf.CeilToInt(featureMap.height * _resolution.y * _density);

            return Mathf.CeilToInt(maxSize) < 1 ? 1 : Mathf.CeilToInt(maxSize);
        }

        private void FeaturePlacement(FeatureData node)
        {
            _placement.SetTexture(_kernelPlacement, PlacementParameterID.SplatMap, node.FeatureMap);
            _placement.SetTexture(_kernelPlacement, PlacementParameterID.Texture, node.Texture);
            _placement.SetBuffer(_kernelPlacement, PlacementParameterID.PixelToObjectCoord, node.PixelToObject);

            _placement.SetTexture(_kernelPlacement, PlacementParameterID.HeightSurface, node.surfaceHeight);

            _placement.SetVector(PlacementParameterID.heightResolution, new Vector2(node.surfaceHeight.width, node.surfaceHeight.height));
            _placement.SetBuffer(_kernelPlacement, PlacementParameterID.TerrainPoints, node.TerrainPoints);
            _placement.SetBuffer(_kernelPlacement, PlacementParameterID.PixelToWorld, node.PlacementMatrix);

            // we need to set this everytime
            _placement.SetBuffer(_kernelPlacement, PlacementParameterID.FeatureMap, _mappingBuffer);

            int threadsX = Mathf.CeilToInt(node.FeatureMap.width / 4f);
            int threadsY = Mathf.CeilToInt(node.FeatureMap.height / 4f);

            _placement.SetMatrix(PlacementParameterID.ObjToWorld, LocalToWorldMatrix(node.Object));

            node.TerrainPoints.SetCounterValue(0);
            _placement.Dispatch(_kernelPlacement, threadsX < 1 ? 1 : threadsX, threadsY < 1 ? 1 : threadsY, 1);
            ComputeBuffer.CopyCount(node.TerrainPoints, node.TerrainPointCount, 0);
        }

        public Matrix4x4 GetClipToWorld(Camera camera)
        {
            var p = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);// Unity flips its 'Y' vector depending on if its in VR, Editor view or game view etc... (facepalm)
            p[2, 3] = p[3, 2] = 0.0f;
            p[3, 3] = 1.0f;
            //_worldToClip = (p * camera.worldToCameraMatrix) * Matrix4x4.TRS(new Vector3(0, 0, -p[2, 2]), UnityEngine.Quaternion.identity, Vector3.one);
            return Matrix4x4.Inverse(p * camera.worldToCameraMatrix) * Matrix4x4.TRS(new Vector3(0, 0, -p[2, 2]), UnityEngine.Quaternion.identity, Vector3.one);
        }

        private static class PlacementParameterID
        {
            public static readonly int HiZTexture = Shader.PropertyToID("HiZTexture");
            public static readonly int HiZTextureSize = Shader.PropertyToID("HiZTextureSize");
            public static readonly int HiZMaxMipLevel = Shader.PropertyToID("HiZMaxMipLevel");
            public static readonly int HiZDepthBias = Shader.PropertyToID("HiZDepthBias");
            public static readonly int HiZOcclusion = Shader.PropertyToID("HiZOcclusion");
            public static readonly int WorldToScreen = Shader.PropertyToID("WorldToScreen");
            public static readonly int OutputBuffer = Shader.PropertyToID("OutputBuffer");
            public static readonly int CameraPosition = Shader.PropertyToID("CameraPosition");
            public static readonly int CameraRightVector = Shader.PropertyToID("CameraRightVector");
            public static readonly int CameraForwardVector = Shader.PropertyToID("CameraForwardVector");
            public static readonly int frustumPlanes = Shader.PropertyToID("frustumPlanes");
            public static readonly int InputBuffer = Shader.PropertyToID("InputBuffer");
            public static readonly int InputCount = Shader.PropertyToID("InputCount");
            public static readonly int ObjToWorld = Shader.PropertyToID("ObjToWorld");
            public static readonly int TerrainPoints = Shader.PropertyToID("TerrainPoints");
            public static readonly int BufferCount = Shader.PropertyToID("BufferCount");
            public static readonly int SplatMap = Shader.PropertyToID("SplatMap");
            public static readonly int Texture = Shader.PropertyToID("Texture");

            //public static readonly int HeightMap = Shader.PropertyToID("HeightMap");
            public static readonly int HeightSurface = Shader.PropertyToID("HeightSurface");

            public static readonly int heightResolution = Shader.PropertyToID("heightResolution");
            public static readonly int PixelToObjectCoord = Shader.PropertyToID("PixelToObjectCoord");

            public static readonly int PixelToWorld = Shader.PropertyToID("PixelToWorld");
            public static readonly int FeatureMap = Shader.PropertyToID("FeatureMap");
            public static readonly int FoliageData = Shader.PropertyToID("FoliageData");
            public static readonly int FoliageCount = Shader.PropertyToID("FoliageCount");
            public static readonly int ScreenCoverage = Shader.PropertyToID("ScreenCoverage");
            public static readonly int NearCullDistance = Shader.PropertyToID("NearCullDistance");
        }

        private Matrix4x4 LocalToWorldMatrix(GameObject go)
        {
            if (!go.TryGetComponent<NodeHandle>(out var handle))
                return Matrix4x4.identity;

            var center = handle.node.BoundaryCenter;
            MapControl.SystemMap.GlobalToWorld(center, out GizmoSDK.Coordinate.LatPos latPos);

            var matrix = Matrix4x4.Translate(-center.ToVector3());
            var pos = new MapPos();
            pos.SetLatPos(latPos.Latitude, latPos.Longitude, latPos.Altitude);
            var enu = pos.EnuToLocal();

            var east = enu * new Vec3(1, 0, 0);
            var north = enu * new Vec3(0, 1, 0);
            var up = enu * new Vec3(0, 0, 1);

            var eunBasis = MapUtil.FromBasis((east.ToVector3()), (up.ToVector3()), -(north.ToVector3()));

            return go.transform.localToWorldMatrix * eunBasis;
        }

        private static readonly ProfilerMarker _hiZProfilerMarker =
            new ProfilerMarker(ProfilerCategory.Render, "Foliage-Cull-HiZ");
        private static readonly ProfilerMarker _frustumProfilerMarker =
            new ProfilerMarker(ProfilerCategory.Render, "Foliage-Cull-FrustumOnly");

        /// <summary>
        /// Culls all active terrain-node foliage into the shared append buffer for one foliage set.
        /// When a matching Hi-Z texture is available, each surviving frustum candidate also performs
        /// conservative occlusion testing; otherwise the method performs frustum and distance culling.
        /// </summary>
        public ComputeBuffer Cull(
            Vector4[] frustum,
            Camera camera,
            RenderTexture hiZTexture,
            Vector2Int hiZSize,
            int hiZMaxMipLevel,
            float hiZDepthBias,
            bool hiZOcclusion,
            FeatureSet set)
        {
            ProfilerMarker profilerMarker =
                hiZOcclusion ? _hiZProfilerMarker : _frustumProfilerMarker;
            profilerMarker.Begin();

            _pointCloud.SetCounterValue(0);     // only once every frame

            Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(
                camera.projectionMatrix,
                camera.targetTexture != null);
            Matrix4x4 world2Screen = gpuProjection * camera.worldToCameraMatrix;
            Texture occlusionTexture =
                hiZTexture != null ? (Texture)hiZTexture : Texture2D.blackTexture;

            // A valid texture is always bound because Unity requires the resource even when the
            // HiZOcclusion branch is disabled.
            _placement.SetTexture(
                _kernelCull,
                PlacementParameterID.HiZTexture,
                occlusionTexture);
            _placement.SetVector(
                PlacementParameterID.HiZTextureSize,
                new Vector4(
                    hiZSize.x,
                    hiZSize.y,
                    1.0f / hiZSize.x,
                    1.0f / hiZSize.y));
            _placement.SetInt(PlacementParameterID.HiZMaxMipLevel, hiZMaxMipLevel);
            _placement.SetFloat(PlacementParameterID.HiZDepthBias, hiZDepthBias);
            _placement.SetBool(PlacementParameterID.HiZOcclusion, hiZOcclusion);
            _placement.SetMatrix(PlacementParameterID.WorldToScreen, world2Screen);
            _placement.SetVector(PlacementParameterID.CameraPosition, camera.transform.position);
            _placement.SetVector(PlacementParameterID.CameraRightVector, camera.transform.right);
            _placement.SetVector(PlacementParameterID.CameraForwardVector, camera.transform.forward);
            _placement.SetVectorArray(PlacementParameterID.frustumPlanes, frustum);

            float verticalView = camera.fieldOfView;
            float horizontalView = Camera.VerticalToHorizontalFieldOfView(verticalView, camera.aspect);
            float fovTolerance = 3f;
            _fov = new Vector2(horizontalView + fovTolerance, verticalView + fovTolerance);
            _placement.SetVector("Fov", _fov);

            // we need to set this everytime
            _placement.SetBuffer(_kernelCull, PlacementParameterID.OutputBuffer, _pointCloud);
            _placement.SetBuffer(_kernelCull, PlacementParameterID.FoliageData, set.FoliageData);
            _placement.SetInt(PlacementParameterID.FoliageCount, set.FoliageData.count);
            _placement.SetFloat(PlacementParameterID.ScreenCoverage, set.ScreenCoverage);
            _placement.SetFloat(PlacementParameterID.NearCullDistance, set.NearCullDistance);

            for (var i = 0; i < _items.Count; ++i)
            {
                var item = _items[i];
                var go = item.Object;

                // don't cull disabled objects
                if (!go.activeInHierarchy)
                    continue;

                int itemPoints = item.TerrainPoints.count;
                int groups = Mathf.CeilToInt(itemPoints / 128f);

                _placement.SetBuffer(_kernelCull, PlacementParameterID.InputBuffer, item.TerrainPoints);
                _placement.SetBuffer(_kernelCull, PlacementParameterID.InputCount, item.TerrainPointCount);
                _placement.SetMatrix(PlacementParameterID.ObjToWorld, go.transform.localToWorldMatrix);

                _placement.Dispatch(_kernelCull, groups < 1 ? 1 : groups, 1, 1);
            }

            profilerMarker.End();

            return _pointCloud;
        }
    }
}