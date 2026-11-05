//******************************************************************************
//
// Copyright (C) SAAB AB
//
// All rights, including the copyright, to the computer program(s) 
// herein belong to Saab AB. The program(s) may be used and/or
// copied only with the written permission of Saab AB, or in
// accordance with the terms and conditions stipulated in the
// agreement/contract under which the program(s) have been
// supplied. 
//
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
//
// File			: NodeGeometryHelper.cs
// Module		:
// Description	: Helper class for vertice updates
// Author		: Anders Modén
// Product		: Gizmo3D 2.12.326
//
// NOTE:	Gizmo3D is a high performance 3D Scene Graph and effect visualisation 
//			C++ toolkit for Linux, Mac OS X, Windows, Android, iOS and HoloLens for  
//			usage in Game or VisSim development.
//
//
// Revision History...
//
// Who	Date	Description
//
// ZJP	200625	Created file                                        (2.10.6)
// ZJP	261009	Use GeometryExport to write directly to Unity mesh data (2.12.345)
// AMO	261028	Recorded mesh builds in the shared build diagnostics after removing the legacy SceneManager.	(2.12.327)
// AMO	261103	Added uniform vertex color lookup for fallback materials.	(2.12.350)
// AMO	261104	Moved to namespace CSW.Unity.	(2.12.327)
// AMO	261105	Built meshes from data prepared on loader threads.	(2.12.327)
//
//******************************************************************************

// Framework

// Unity
using CSW.Unity.StreamingMap;
using UnityEngine;
using UnityEngine.Rendering;

// GizmoSDK
using GizmoSDK.Gizmo3D;

using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;
using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;

namespace CSW.Unity
{
    public static class GeometryHelper
    {
        private static readonly ProfilerMarker _profilerMarker = new ProfilerMarker(ProfilerCategory.Render, "SM-BuildGeometry");
        private static readonly ProfilerMarker _profilerNativeWritableMeshData = new ProfilerMarker(ProfilerCategory.Render, "SM-BuildGeometry-NativeWritableMeshData");
        private static readonly ProfilerMarker _profilerPrepared = new ProfilerMarker(ProfilerCategory.Render, "SM-BuildGeometry-Prepared");

        private static readonly VertexAttributeDescriptor[] _positionNormalDescriptors =
        {
            new VertexAttributeDescriptor(
                VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(
                VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
        };

        private static readonly VertexAttributeDescriptor[] _positionNormalColorDescriptors =
        {
            new VertexAttributeDescriptor(
                VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(
                VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(
                VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
        };

        private static readonly VertexAttributeDescriptor[] _positionNormalUvDescriptors =
        {
            new VertexAttributeDescriptor(
                VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(
                VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(
                VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        };

        private static readonly VertexAttributeDescriptor[]
            _positionNormalColorUvDescriptors =
        {
            new VertexAttributeDescriptor(
                VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(
                VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(
                VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(
                VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        };

        private const MeshUpdateFlags NativeMeshUpdateFlags =
            MeshUpdateFlags.DontRecalculateBounds |
            MeshUpdateFlags.DontValidateIndices |
            MeshUpdateFlags.DontNotifyMeshUsers |
            MeshUpdateFlags.DontResetBoneBounds;

        private static VertexAttributeDescriptor[] CreateVertexDescriptors(bool hasColors, bool hasTexCoords)
        {
            if (hasColors && hasTexCoords)
                return _positionNormalColorUvDescriptors;

            if (hasColors)
                return _positionNormalColorDescriptors;

            if (hasTexCoords)
                return _positionNormalUvDescriptors;

            return _positionNormalDescriptors;
        }

        /// <summary>
        /// Returns the vertex color when every vertex of a readable mesh has the same color
        /// </summary>
        public static bool TryGetUniformVertexColor(Mesh mesh, out Color color)
        {
            color = Color.white;
            if (!mesh || !mesh.isReadable || !mesh.HasVertexAttribute(VertexAttribute.Color))
                return false;

            var colors = mesh.colors;
            if (colors.Length == 0)
                return false;

            for (var i = 1; i < colors.Length; ++i)
            {
                if (colors[i] != colors[0])
                    return false;
            }

            color = colors[0];
            return true;
        }

        /// <summary>
        /// Builds a mesh from data prepared off the main thread, without reading the geometry
        /// </summary>
        /// <returns>False without creating a mesh when the Unity layout differs from the prepared layout</returns>
        public static bool Build(PreparedMesh prepared, out Mesh output, out Color uniformColor)
        {
            _profilerPrepared.Begin();
            var startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            output = null;
            uniformColor = prepared.UniformColor;
            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            var disposeMeshData = true;
            try
            {
                var meshData = meshDataArray[0];
                meshData.SetVertexBufferParams(
                    prepared.VertexCount,
                    CreateVertexDescriptors(prepared.HasColors, prepared.HasTexCoords));
                meshData.SetIndexBufferParams(prepared.IndexCount, prepared.IndexFormat);
                if (!prepared.CopyTo(meshData))
                    return false;

                meshData.subMeshCount = 1;
                meshData.SetSubMesh(
                    0,
                    new SubMeshDescriptor(0, prepared.IndexCount, MeshTopology.Triangles)
                    {
                        firstVertex = 0,
                        vertexCount = prepared.VertexCount,
                        bounds = prepared.Bounds,
                    },
                    NativeMeshUpdateFlags);

                output = new Mesh();
                disposeMeshData = false;
                Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, output, NativeMeshUpdateFlags);
                output.bounds = prepared.Bounds;
                BuildDiagnostics.RecordMesh(
                    prepared.DataBytes,
                    System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp);
                return true;
            }
            finally
            {
                if (disposeMeshData)
                    meshDataArray.Dispose();
                _profilerPrepared.End();
            }
        }

        public static bool Build(Geometry geom, out Mesh output, out Color uniformColor)
        {
            return Build(geom, null, out output, out uniformColor);
        }

        public static bool Build(
            Geometry geom,
            Mesh reusableMesh,
            out Mesh output,
            out Color uniformColor)
        {
            _profilerMarker.Begin();
            var startTimestamp =
                System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                var result = BuildExport(
                    geom,
                    reusableMesh,
                    out output,
                    out uniformColor,
                    out var dataBytes);
                if (result)
                {
                    BuildDiagnostics.RecordMesh(
                        dataBytes,
                        System.Diagnostics.Stopwatch.GetTimestamp() -
                        startTimestamp);
                }
                return result;
            }
            finally
            {
                _profilerMarker.End();
            }
        }

        private static bool BuildExport(
            Geometry geom,
            Mesh reusableMesh,
            out Mesh mesh,
            out Color uniformColor,
            out long dataBytes)
        {
            _profilerNativeWritableMeshData.Begin();

            try
            {
                mesh = null;
                uniformColor = Color.white;
                dataBytes = 0;
                var queryStatus = geom.PrepareMeshExport(
                    out var preparation,
                    Geometry.PrimType.TRIS);

                if (queryStatus != GeometryMeshExport.Status.Success)
                    return false;

                using (preparation)
                {
                    var info = preparation.Info;
                    if (info.Topology != Geometry.PrimType.TRIS ||
                        info.VertexCount < 3 || info.IndexCount < 3 ||
                        info.VertexCount > int.MaxValue || info.IndexCount > int.MaxValue)
                        return false;

                    var hasColors =
                        (info.AvailableAttributes & GeometryMeshExport.Attributes.Color) != 0;
                    var hasTexCoords =
                        preparation.HasTextureCoordinateUnit(0);
                    var descriptors = CreateVertexDescriptors(hasColors, hasTexCoords);
                    var vertexCount = (int)info.VertexCount;
                    var indexCount = (int)info.IndexCount;
                    var indexFormat =
                        info.VertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
                    if (info.UsesOverallColor)
                    {
                        uniformColor = new Color(
                            info.OverallColor.x,
                            info.OverallColor.y,
                            info.OverallColor.z,
                            info.OverallColor.w);
                    }

                    var meshDataArray = Mesh.AllocateWritableMeshData(1);
                    var disposeMeshData = true;

                    try
                    {
                        var meshData = meshDataArray[0];
                        meshData.SetVertexBufferParams(vertexCount, descriptors);
                        meshData.SetIndexBufferParams(indexCount, indexFormat);
                        var copyStatus = GeometryMeshExportInterop.Copy(
                            preparation,
                            in info,
                            meshData,
                            indexFormat,
                            out var copiedBytes);
                        if (copyStatus != GeometryMeshExport.Status.Success)
                            return false;
                        dataBytes = copiedBytes;

                        meshData.subMeshCount = 1;
                        meshData.SetSubMesh(
                            0,
                            new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
                            {
                                firstVertex = 0,
                                vertexCount = vertexCount,
                            },
                            NativeMeshUpdateFlags);

                        mesh = reusableMesh ? reusableMesh : new Mesh();
                        disposeMeshData = false;
                        Mesh.ApplyAndDisposeWritableMeshData(
                            meshDataArray,
                            mesh,
                            NativeMeshUpdateFlags);
                        mesh.RecalculateBounds();

                        return true;
                    }
                    finally
                    {
                        if (disposeMeshData)
                            meshDataArray.Dispose();
                    }
                }
            }
            finally
            {
                _profilerNativeWritableMeshData.End();
            }
        }

    }
}
