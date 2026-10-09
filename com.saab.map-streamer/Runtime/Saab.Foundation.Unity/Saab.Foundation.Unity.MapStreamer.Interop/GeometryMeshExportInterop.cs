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
// File			: GeometryMeshExportInterop.cs
// Description	: Unsafe Unity MeshData destination adapter for GizmoSDK geometry export
//
//******************************************************************************

using System;
using GizmoSDK.Gizmo3D;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace Saab.Foundation.Unity.MapStreamer.Interop
{
    public static class GeometryMeshExportInterop
    {
        public static unsafe GeometryMeshExport.Status Copy(
            GeometryMeshExport.Prepared preparation,
            in GeometryMeshExport.Info info,
            Mesh.MeshData meshData,
            IndexFormat indexFormat,
            out long dataBytes)
        {
            dataBytes = 0;
            var destination = GeometryMeshExport.Destination.Create(in info);
            var vertexData = meshData.GetVertexData<byte>(0);
            var vertexPointer =
                (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(vertexData);
            var vertexCapacityBytes = (uint)vertexData.Length;
            var vertexStrideBytes =
                (uint)meshData.GetVertexBufferStride(0);

            destination.Positions = CreateAttributeDestination(
                meshData,
                VertexAttribute.Position,
                vertexPointer,
                vertexCapacityBytes,
                vertexStrideBytes);
            destination.Normals = CreateAttributeDestination(
                meshData,
                VertexAttribute.Normal,
                vertexPointer,
                vertexCapacityBytes,
                vertexStrideBytes);

            if ((info.AvailableAttributes & GeometryMeshExport.Attributes.Color) != 0)
            {
                destination.Colors = CreateAttributeDestination(
                    meshData,
                    VertexAttribute.Color,
                    vertexPointer,
                    vertexCapacityBytes,
                    vertexStrideBytes);
            }

            if ((info.AvailableAttributes & GeometryMeshExport.Attributes.TexCoord0) != 0)
            {
                destination.TexCoord0 = CreateAttributeDestination(
                    meshData,
                    VertexAttribute.TexCoord0,
                    vertexPointer,
                    vertexCapacityBytes,
                    vertexStrideBytes);
            }

            if (indexFormat == IndexFormat.UInt16)
            {
                var indices = meshData.GetIndexData<ushort>();
                var indexCapacityBytes =
                    (uint)indices.Length * sizeof(ushort);
                destination.Indices = new GeometryMeshExport.IndexDestination(
                    (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(indices),
                    indexCapacityBytes,
                    GeometryMeshExport.IndexFormat.UInt16);
                dataBytes = (long)vertexCapacityBytes + indexCapacityBytes;
            }
            else
            {
                var indices = meshData.GetIndexData<uint>();
                var indexCapacityBytes =
                    checked((uint)indices.Length * sizeof(uint));
                destination.Indices = new GeometryMeshExport.IndexDestination(
                    (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(indices),
                    indexCapacityBytes,
                    GeometryMeshExport.IndexFormat.UInt32);
                dataBytes = (long)vertexCapacityBytes + indexCapacityBytes;
            }

            return preparation.Copy(ref destination);
        }

        private static unsafe GeometryMeshExport.AttributeDestination
            CreateAttributeDestination(
                Mesh.MeshData meshData,
                VertexAttribute attribute,
                IntPtr pointer,
                uint capacityBytes,
                uint strideBytes)
        {
            return new GeometryMeshExport.AttributeDestination(
                pointer,
                capacityBytes,
                strideBytes,
                (uint)meshData.GetVertexAttributeOffset(attribute));
        }
    }
}
