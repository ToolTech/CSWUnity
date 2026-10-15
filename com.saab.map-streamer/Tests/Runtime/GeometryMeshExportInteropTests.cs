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

using System;
using System.Linq;
using System.Reflection;
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using NUnit.Framework;
using Saab.Foundation.Unity.MapStreamer.Interop;
using UnityEngine;
using UnityEngine.Rendering;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
    public sealed class GeometryMeshExportInteropTests
    {
        [OneTimeSetUp]
        public void InitializeGizmo()
        {
            Assert.That(GizmoSDK.Gizmo3D.Platform.Initialize(), Is.True);
            DbManager.Initialize();
        }

        [OneTimeTearDown]
        public void UninitializeGizmo()
        {
            DbManager.Uninitialize();
            GizmoSDK.Gizmo3D.Platform.Uninitialize();
        }

        [Test]
        public void AbiMatchesDeployedGeometryExportContract()
        {
            Assert.That(GeometryMeshExport.InfoVersion, Is.EqualTo(3));
            Assert.That(GeometryMeshExport.DestinationVersion, Is.EqualTo(3));
            Assert.That(typeof(GeometryMeshExport.Destination).IsClass, Is.True);
            Assert.That(
                typeof(GeometryMeshExport.Info)
                    .GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Select(field => field.Name),
                Is.EqualTo(new[]
                {
                    "Version",
                    "Size",
                    "VertexCount",
                    "IndexCount",
                    "PrimitiveLengthCount",
                    "AvailableAttributes",
                    "TextureCoordinateCount",
                    "NormalBinding",
                    "ColorBinding",
                    "Topology",
                    "HasOverallNormal",
                    "HasOverallColor",
                    "OverallNormal",
                    "OverallColor",
                }));
            Assert.That(
                typeof(GeometryMeshExport.Destination).GetMethod(
                    "SetTexCoord",
                    new[]
                    {
                        typeof(uint),
                        typeof(GeometryMeshExport.AttributeDestination),
                    }),
                Is.Not.Null);
            Assert.That(
                typeof(GeometryMeshExport.Prepared).GetMethod(
                    "Copy",
                    new[] { typeof(GeometryMeshExport.Destination) }),
                Is.Not.Null);
            Assert.That(
                typeof(GeometryMeshExport.Prepared).GetMethod(
                    "Copy",
                    new[]
                    {
                        typeof(GeometryMeshExport.Destination).MakeByRefType(),
                    }),
                Is.Null);
        }

        [Test]
        public void CopyExportsUvZeroFromSphereGeometry()
        {
            NodeLock.WaitLockEdit();
            try
            {
                using (var typeName = new NativeString("gzGeometrySphere"))
                using (var created = Reference.CreateObject(typeName))
                {
                    var geometry = created as Geometry;
                    Assert.That(geometry, Is.Not.Null);

                    float[] uv0 = null;
                    uint uvCount = 0;
                    Assert.That(
                        geometry.GetTexCoordData(ref uv0, ref uvCount, 0),
                        Is.True);
                    Assert.That(uvCount, Is.GreaterThan(0));

                    Assert.That(geometry.GetTextureUnits(), Is.EqualTo(1));

                    Assert.That(
                        geometry.PrepareMeshExport(
                            out var preparation,
                            Geometry.PrimType.TRIS),
                        Is.EqualTo(GeometryMeshExport.Status.Success));

                    using (preparation)
                    {
                        Assert.That(
                            preparation.TextureCoordinateUnits,
                            Is.EqualTo(new uint[] { 0 }));
                        Assert.That(
                            preparation.HasTextureCoordinateUnit(0),
                            Is.True);
                        Assert.That(
                            preparation.HasTextureCoordinateUnit(1),
                            Is.False);

                        var info = preparation.Info;
                        var meshDataArray = Mesh.AllocateWritableMeshData(1);
                        try
                        {
                            var meshData = meshDataArray[0];
                            meshData.SetVertexBufferParams(
                                (int)info.VertexCount,
                                new VertexAttributeDescriptor(
                                    VertexAttribute.Position,
                                    VertexAttributeFormat.Float32,
                                    3),
                                new VertexAttributeDescriptor(
                                    VertexAttribute.Normal,
                                    VertexAttributeFormat.Float32,
                                    3),
                                new VertexAttributeDescriptor(
                                    VertexAttribute.Color,
                                    VertexAttributeFormat.Float32,
                                    4),
                                new VertexAttributeDescriptor(
                                    VertexAttribute.TexCoord0,
                                    VertexAttributeFormat.Float32,
                                    2));
                            var indexFormat =
                                info.VertexCount > ushort.MaxValue
                                    ? IndexFormat.UInt32
                                    : IndexFormat.UInt16;
                            meshData.SetIndexBufferParams(
                                (int)info.IndexCount,
                                indexFormat);

                            Assert.That(
                                GeometryMeshExportInterop.Copy(
                                    preparation,
                                    in info,
                                    meshData,
                                    indexFormat,
                                    out var dataBytes),
                                Is.EqualTo(GeometryMeshExport.Status.Success));
                            Assert.That(dataBytes, Is.GreaterThan(0));
                            Assert.That(
                                meshData.HasVertexAttribute(
                                    VertexAttribute.TexCoord0),
                                Is.True);
                            Assert.That(
                                meshData.HasVertexAttribute(
                                    VertexAttribute.TexCoord1),
                                Is.False);
                        }
                        finally
                        {
                            meshDataArray.Dispose();
                        }
                    }
                }
            }
            finally
            {
                NodeLock.UnLock();
            }
        }
    }
}
