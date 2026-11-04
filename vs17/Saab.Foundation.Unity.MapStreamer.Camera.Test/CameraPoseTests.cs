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
using Saab.Foundation.Unity.MapStreamer.GeodeticCamera;

namespace Saab.Foundation.Unity.MapStreamer.Camera.Test
{
    [TestClass]
    public sealed class CameraPoseTests
    {
        [TestMethod]
        public void DirectPose_PreservesRollAndPositionWithoutNavigationOrDistancePolicy()
        {
            Vec3D position = default;
            Quaternion orientation = default;
            var pose = new CameraPose(
                (Vec3D candidate, Quaternion rotation, out Action apply) =>
                {
                    apply = () =>
                    {
                        position = candidate;
                        orientation = rotation;
                    };
                    return true;
                });
            var rolled = new Quaternion(2, 0, 0, 2);
            var target = new Vec3D(1e12, -10000, 0.01);

            Assert.IsTrue(pose.TrySetPose(target, rolled));

            Assert.AreEqual(target, position);
            Assert.AreEqual(1.0f, GizmoCameraMath.Dot(orientation, orientation), 1e-6f);
            Assert.AreEqual((float)Math.Sqrt(0.5), orientation.z, 1e-6f);
            Assert.AreEqual((float)Math.Sqrt(0.5), orientation.w, 1e-6f);
        }

        [DataTestMethod]
        [DataRow(float.Epsilon)]
        [DataRow(float.MaxValue)]
        public void DirectPose_NormalizesExtremeFiniteQuaternionMagnitudes(float component)
        {
            Quaternion accepted = default;
            var pose = new CameraPose(
                (Vec3D _, Quaternion orientation, out Action apply) =>
                {
                    apply = () => accepted = orientation;
                    return true;
                });

            Assert.IsTrue(pose.TrySetPose(
                new Vec3D(1e100, -1e100, 0.01),
                new Quaternion(component, component, component, component)));
            Assert.AreEqual(0.5f, accepted.w, 1e-6f);
            Assert.AreEqual(0.5f, accepted.x, 1e-6f);
            Assert.AreEqual(0.5f, accepted.y, 1e-6f);
            Assert.AreEqual(0.5f, accepted.z, 1e-6f);
        }

        [TestMethod]
        public void FailedCoordinateOrBasisPreparation_DoesNotApplyAnyPartAndCanRetry()
        {
            var accepted = new Vec3D(1, 2, 3);
            bool basisAvailable = false;
            int commits = 0;
            var pose = new CameraPose(
                (Vec3D candidate, Quaternion _, out Action apply) =>
                {
                    apply = () =>
                    {
                        accepted = candidate;
                        commits++;
                    };
                    return basisAvailable;
                });
            var destination = new Vec3D(1e9, 2e9, 3e9);

            Assert.IsFalse(pose.TrySetPose(destination, GizmoCameraMath.Identity));
            Assert.AreEqual(new Vec3D(1, 2, 3), accepted);
            Assert.AreEqual(0, commits);
            basisAvailable = true;
            Assert.IsTrue(pose.TrySetPose(destination, GizmoCameraMath.Identity));
            Assert.AreEqual(destination, accepted);
            Assert.AreEqual(1, commits);
        }

        [TestMethod]
        public void NonFiniteOrZeroPose_IsRejectedBeforeNativePreparation()
        {
            int preparations = 0;
            var pose = new CameraPose(
                (Vec3D _, Quaternion _, out Action apply) =>
                {
                    preparations++;
                    apply = () => Assert.Fail("Invalid pose must not commit.");
                    return true;
                });

            foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Assert.IsFalse(pose.TrySetPose(new Vec3D(invalid, 0, 0), GizmoCameraMath.Identity));
                Assert.IsFalse(pose.TrySetPose(new Vec3D(0, invalid, 0), GizmoCameraMath.Identity));
                Assert.IsFalse(pose.TrySetPose(new Vec3D(0, 0, invalid), GizmoCameraMath.Identity));
            }

            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.IsFalse(pose.TrySetPose(default, new Quaternion(invalid, 1, 0, 0)));
                Assert.IsFalse(pose.TrySetPose(default, new Quaternion(1, invalid, 0, 0)));
                Assert.IsFalse(pose.TrySetPose(default, new Quaternion(1, 0, invalid, 0)));
                Assert.IsFalse(pose.TrySetPose(default, new Quaternion(1, 0, 0, invalid)));
            }

            Assert.IsFalse(pose.TrySetPose(default, default));
            Assert.AreEqual(0, preparations);
        }
    }
}
