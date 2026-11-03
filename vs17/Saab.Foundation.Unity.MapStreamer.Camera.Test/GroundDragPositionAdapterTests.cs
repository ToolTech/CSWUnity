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

using GizmoSDK.GizmoBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer;
using Saab.Foundation.Unity.MapStreamer.GeodeticCamera;

namespace Saab.Foundation.Unity.MapStreamer.Camera.Test
{
    [TestClass]
    public sealed class GroundDragPositionAdapterTests
    {
        [TestMethod]
        public void Apply_GeocentricMove_ConvertsAndAppliesUpdatedGlobalPosition()
        {
            var capturedEcef = new Vec3D(10.0, 20.0, 30.0);
            var movedEcef = new Vec3D(40.0, 50.0, 60.0);
            var convertedGlobal = new Vec3D(70.0, 80.0, 90.0);
            Vec3D setEcef = default;
            Vec3D appliedGlobal = default;
            double appliedEast = 0.0;
            double appliedNorth = 0.0;
            int orientationUpdates = 0;
            var adapter = new GroundDragPositionAdapter(
                (Vec3D position, double east, double north, out Vec3D moved) =>
                {
                    Assert.AreEqual(capturedEcef, position);
                    appliedEast = east;
                    appliedNorth = north;
                    moved = movedEcef;
                    return true;
                },
                () => (DefaultBasis, GizmoCameraMath.Identity),
                (Vec3D position, out GroundDragPositionAdapter.Basis basis, out Action<Quaternion> apply) =>
                {
                    setEcef = position;
                    basis = DefaultBasis;
                    apply = orientation =>
                    {
                        appliedGlobal = convertedGlobal;
                        orientationUpdates++;
                        Assert.AreEqual(1.0f, Math.Abs(GizmoCameraMath.Dot(
                            GizmoCameraMath.Identity, orientation)), 1e-6f);
                    };
                    return true;
                },
                position =>
                {
                    appliedGlobal = position;
                    return true;
                });

            bool applied = adapter.Apply(
                MapType.GEOCENTRIC, capturedEcef, east: -12.0f, north: 34.0f);

            Assert.IsTrue(applied);
            Assert.AreEqual(-12.0, appliedEast);
            Assert.AreEqual(34.0, appliedNorth);
            Assert.AreEqual(movedEcef, setEcef);
            Assert.AreEqual(convertedGlobal, appliedGlobal);
            Assert.AreEqual(1, orientationUpdates);
        }

        [TestMethod]
        public void Apply_ProjectedMove_UsesAcceptedMapGlobalPositionDirectly()
        {
            var accepted = new Vec3D(10.0, 20.0, 30.0);
            Vec3D applied = default;
            int geocentricCalls = 0;
            var adapter = new GroundDragPositionAdapter(
                (Vec3D position, double east, double north, out Vec3D moved) =>
                {
                    geocentricCalls++;
                    moved = position;
                    return true;
                },
                () =>
                {
                    geocentricCalls++;
                    return (DefaultBasis, GizmoCameraMath.Identity);
                },
                (Vec3D _, out GroundDragPositionAdapter.Basis basis, out Action<Quaternion> apply) =>
                {
                    geocentricCalls++;
                    basis = default;
                    apply = null!;
                    return false;
                },
                position =>
                {
                    applied = position;
                    return true;
                });

            bool result = adapter.Apply(MapType.UTM, accepted, east: 0.0f, north: 0.0f);

            Assert.IsTrue(result);
            Assert.AreEqual(accepted, applied);
            Assert.AreEqual(0, geocentricCalls);
        }

        [TestMethod]
        public void Apply_GeocentricConversionFailure_LeavesCameraUnchanged()
        {
            int applications = 0;
            int orientationUpdates = 0;
            var adapter = new GroundDragPositionAdapter(
                (Vec3D position, double _, double _, out Vec3D moved) =>
                {
                    moved = position;
                    return true;
                },
                () => (DefaultBasis, GizmoCameraMath.Identity),
                (Vec3D _, out GroundDragPositionAdapter.Basis basis, out Action<Quaternion> apply) =>
                {
                    basis = default;
                    apply = _ =>
                    {
                        applications++;
                        orientationUpdates++;
                        throw new AssertFailedException("Failed conversion must not apply a result.");
                    };
                    return false;
                },
                _ =>
                {
                    applications++;
                    return true;
                });

            bool result = adapter.Apply(
                MapType.GEOCENTRIC, new Vec3D(10.0, 20.0, 30.0), -4.0f, 5.0f);

            Assert.IsFalse(result);
            Assert.AreEqual(0, applications);
            Assert.AreEqual(0, orientationUpdates);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Apply_FailedEllipsoidMoveOrInvalidResult_DoesNotPrepareDestination(bool invalidResult)
        {
            int preparations = 0;
            var adapter = new GroundDragPositionAdapter(
                (Vec3D _, double _, double _, out Vec3D moved) =>
                {
                    moved = invalidResult ? new Vec3D(double.NaN, 0, 0) : new Vec3D(1, 2, 3);
                    return invalidResult;
                },
                () => (DefaultBasis, GizmoCameraMath.Identity),
                (Vec3D _, out GroundDragPositionAdapter.Basis basis, out Action<Quaternion> apply) =>
                {
                    preparations++;
                    basis = DefaultBasis;
                    apply = _ => Assert.Fail("A failed move cannot commit a pose.");
                    return true;
                },
                _ => throw new AssertFailedException("Geocentric movement cannot use projected application."));

            Assert.IsFalse(adapter.Apply(MapType.GEOCENTRIC, new Vec3D(1, 2, 3), 10, 20));
            Assert.AreEqual(0, preparations);
        }

        private static GroundDragPositionAdapter.Basis DefaultBasis =>
            new GroundDragPositionAdapter.Basis(
                GizmoCameraMath.UnitX, GizmoCameraMath.UnitZ, GizmoCameraMath.UnitY);
    }
}
