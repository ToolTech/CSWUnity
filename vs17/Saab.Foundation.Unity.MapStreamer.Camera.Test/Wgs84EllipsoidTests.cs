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
using Saab.Foundation.Unity.MapStreamer;
using Saab.Foundation.Unity.MapStreamer.GeodeticCamera;

namespace Saab.Foundation.Unity.MapStreamer.Camera.Test
{
    [TestClass]
    public sealed class Wgs84EllipsoidTests
    {
        private const double SemiMajorAxis = 6378137.0;
        private const double SemiMinorAxis = 6356752.314245;

        [TestMethod]
        public void DownwardRay_ReturnsNearestSurfacePoint()
        {
            var origin = new Vec3D(SemiMajorAxis + 1000.0, 0.0, 0.0);

            bool intersects = Wgs84Ellipsoid.TryIntersectRay(
                origin, new Vec3D(-1.0, 0.0, 0.0), out Vec3D result);

            Assert.IsTrue(intersects);
            Assert.AreEqual(SemiMajorAxis, result.x, 1e-6);
            Assert.AreEqual(0.0, result.y, 1e-6);
            Assert.AreEqual(0.0, result.z, 1e-6);
        }

        [TestMethod]
        public void PolarRay_UsesSemiMinorAxis()
        {
            var origin = new Vec3D(0.0, 0.0, SemiMinorAxis + 1000.0);

            bool intersects = Wgs84Ellipsoid.TryIntersectRay(
                origin, new Vec3D(0.0, 0.0, -2.0), out Vec3D result);

            Assert.IsTrue(intersects);
            Assert.AreEqual(SemiMinorAxis, result.z, 1e-6);
        }

        [TestMethod]
        public void ObliqueRay_ReturnsPointOnSurface()
        {
            var origin = new Vec3D(
                SemiMajorAxis + 1000.0,
                1000000.0,
                1000000.0);

            bool intersects = Wgs84Ellipsoid.TryIntersectRay(
                origin, new Vec3D(-origin.x, -origin.y, -origin.z), out Vec3D result);
            double ellipsoidEquation =
                (result.x * result.x + result.y * result.y)
                / (SemiMajorAxis * SemiMajorAxis)
                + result.z * result.z / (SemiMinorAxis * SemiMinorAxis);

            Assert.IsTrue(intersects);
            Assert.AreEqual(1.0, ellipsoidEquation, 1e-12);
        }

        [TestMethod]
        public void TangentRay_ReturnsContactPoint()
        {
            var origin = new Vec3D(SemiMajorAxis, -1000.0, 0.0);

            bool intersects = Wgs84Ellipsoid.TryIntersectRay(
                origin, new Vec3D(0.0, 1.0, 0.0), out Vec3D result);

            Assert.IsTrue(intersects);
            Assert.AreEqual(SemiMajorAxis, result.x, 1e-6);
            Assert.AreEqual(0.0, result.y, 1e-3);
            Assert.AreEqual(0.0, result.z, 1e-6);
        }

        [TestMethod]
        public void RayPointingAway_ReturnsFalse()
        {
            var origin = new Vec3D(SemiMajorAxis + 1000.0, 0.0, 0.0);

            Assert.IsFalse(Wgs84Ellipsoid.TryIntersectRay(
                origin, new Vec3D(1.0, 0.0, 0.0), out _));
        }

        [TestMethod]
        public void MissedRay_ReturnsFalse()
        {
            var origin = new Vec3D(SemiMajorAxis + 1000.0, 0.0, 0.0);

            Assert.IsFalse(Wgs84Ellipsoid.TryIntersectRay(
                origin, new Vec3D(0.0, 1.0, 0.0), out _));
        }

        [DataTestMethod]
        [DataRow(-1.0)]
        [DataRow(1.0)]
        public void OriginInside_ReturnsFalse(double direction)
        {
            var origin = new Vec3D(SemiMajorAxis - 400.0, 0.0, 0.0);

            Assert.IsFalse(Wgs84Ellipsoid.TryIntersectRay(
                origin, new Vec3D(direction, 0.0, 0.0), out _));
        }

        [TestMethod]
        public void ZeroDirection_ReturnsFalse()
        {
            var origin = new Vec3D(SemiMajorAxis + 1000.0, 0.0, 0.0);

            Assert.IsFalse(Wgs84Ellipsoid.TryIntersectRay(
                origin, new Vec3D(), out _));
        }
    }
}
