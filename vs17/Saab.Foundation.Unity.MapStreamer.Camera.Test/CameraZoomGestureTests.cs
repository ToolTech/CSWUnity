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
    public sealed class CameraZoomGestureTests
    {
        [TestMethod]
        public void FreeZoom_HasNoImplicitInwardBound()
        {
            var position = new Vec3D(0, 0, 10);
            var gesture = new CameraZoomGesture(
                (float _, float _, out Vec3D reference) =>
                {
                    reference = default;
                    return true;
                },
                () => position,
                value =>
                {
                    position = value;
                    return true;
                });

            Assert.IsTrue(gesture.Move(0, 0, 20));
            Assert.AreEqual(10 * Math.Pow(0.8, 20), position.z, 1e-12);
        }

        [TestMethod]
        public void ExplicitReferenceZoom_DoesNotQueryPointerAndReportsRejectedApplication()
        {
            var position = new Vec3D(0, 0, 10);
            int queries = 0;
            bool canApply = false;
            var gesture = new CameraZoomGesture(
                (float _, float _, out Vec3D reference) =>
                {
                    queries++;
                    reference = default;
                    return false;
                },
                () => position,
                value =>
                {
                    if (!canApply)
                        return false;

                    position = value;
                    return true;
                });

            Assert.IsFalse(gesture.Move(default(Vec3D), 1));
            Assert.AreEqual(new Vec3D(0, 0, 10), position);
            canApply = true;
            Assert.IsTrue(gesture.Move(default(Vec3D), 1));
            Assert.AreEqual(new Vec3D(0, 0, 8), position);
            Assert.AreEqual(0, queries);
        }

        [TestMethod]
        public void Move_CloseInspection_AppliesPositionOneMetreFromSelectedReference()
        {
            var cameraPosition = new Vec3D(0.0, 0.0, 10.0);
            var gesture = new CameraZoomGesture(
                (float x, float y, out Vec3D reference) =>
                {
                    Assert.AreEqual(25.0f, x);
                    Assert.AreEqual(75.0f, y);
                    reference = new Vec3D(0.0, 0.0, 0.0);
                    return true;
                },
                () => cameraPosition,
                position =>
                {
                    cameraPosition = position;
                    return true;
                });

            gesture.SetDistanceLimits(1, null);
            bool moved = gesture.Move(25.0f, 75.0f, 100.0f);

            Assert.IsTrue(moved);
            Assert.AreEqual(0.0, cameraPosition.x, 1e-12);
            Assert.AreEqual(0.0, cameraPosition.y, 1e-12);
            Assert.AreEqual(1.0, cameraPosition.z, 1e-12);
        }

        [TestMethod]
        public void Move_LargeInitialDistance_ZoomInThenOut_RestoresInitialDistance()
        {
            var cameraPosition = new Vec3D(0.0, 0.0, 100000.0);
            var gesture = new CameraZoomGesture(
                (float _, float _, out Vec3D reference) =>
                {
                    reference = new Vec3D(0.0, 0.0, 0.0);
                    return true;
                },
                () => cameraPosition,
                position =>
                {
                    cameraPosition = position;
                    return true;
                });

            Assert.IsTrue(gesture.Move(25.0f, 75.0f, 11.0f));
            Assert.IsTrue(gesture.Move(25.0f, 75.0f, -11.0f));

            Assert.AreEqual(100000.0, cameraPosition.z, 1e-8);
        }

        [TestMethod]
        public void Move_ExplicitOutwardBound_StopsAtConfiguredDistance()
        {
            var cameraPosition = new Vec3D(0.0, 0.0, 1000.0);
            var gesture = new CameraZoomGesture(
                (float _, float _, out Vec3D reference) =>
                {
                    reference = new Vec3D(0.0, 0.0, 0.0);
                    return true;
                },
                () => cameraPosition,
                position =>
                {
                    cameraPosition = position;
                    return true;
                });

            gesture.SetDistanceLimits(1.0, 1000.0);
            Assert.IsTrue(gesture.Move(25.0f, 75.0f, -1.0f));

            Assert.AreEqual(1000.0, cameraPosition.z, 1e-8);
        }

        [TestMethod]
        public void Move_OptionalBounds_AreIndependentAndDoNotLearnObservedDistance()
        {
            var position = new Vec3D(0, 0, 10);
            var gesture = new CameraZoomGesture(
                (float _, float _, out Vec3D reference) =>
                {
                    reference = default;
                    return true;
                },
                () => position,
                value =>
                {
                    position = value;
                    return true;
                });

            Assert.IsTrue(gesture.Move(0, 0, -1));
            Assert.AreEqual(12.5, position.z, 1e-10);
            gesture.SetDistanceLimits(null, 20);
            Assert.IsTrue(gesture.Move(0, 0, 20));
            Assert.IsTrue(position.z < 1.0);
            Assert.IsTrue(gesture.Move(0, 0, -100));
            Assert.AreEqual(20.0, position.z, 1e-10);
            gesture.SetDistanceLimits(5, null);
            Assert.IsTrue(gesture.Move(0, 0, 100));
            Assert.AreEqual(5.0, position.z, 1e-10);
            gesture.SetDistanceLimits(null, null);
            Assert.IsTrue(gesture.Move(0, 0, -10));
            Assert.IsTrue(position.z > 20.0);
        }

        [TestMethod]
        public void InvalidLimits_AreRejectedBeforeChangingNavigationPolicy()
        {
            var position = new Vec3D(0, 0, 10);
            var gesture = new CameraZoomGesture(
                (float _, float _, out Vec3D reference) =>
                {
                    reference = default;
                    return true;
                },
                () => position,
                value =>
                {
                    position = value;
                    return true;
                });
            gesture.SetDistanceLimits(2, 20);

            foreach (double invalid in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                    gesture.SetDistanceLimits(invalid, null));
                Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                    gesture.SetDistanceLimits(null, invalid));
            }

            Assert.ThrowsException<ArgumentException>(() => gesture.SetDistanceLimits(20, 2));
            Assert.IsTrue(gesture.Move(0, 0, 100));
            Assert.AreEqual(2.0, position.z, 1e-10);
            Assert.IsTrue(gesture.Move(0, 0, -100));
            Assert.AreEqual(20.0, position.z, 1e-10);
        }

        [TestMethod]
        public void Move_MissingReference_LeavesCameraUnchanged()
        {
            var cameraPosition = new Vec3D(1.0, 2.0, 3.0);
            int applications = 0;
            var gesture = new CameraZoomGesture(
                (float _, float _, out Vec3D reference) =>
                {
                    reference = default;
                    return false;
                },
                () => cameraPosition,
                _ =>
                {
                    applications++;
                    return true;
                });

            bool moved = gesture.Move(25.0f, 75.0f, 1.0f);

            Assert.IsFalse(moved);
            Assert.AreEqual(0, applications);
            Assert.AreEqual(new Vec3D(1.0, 2.0, 3.0), cameraPosition);
        }

        [TestMethod]
        public void HugeFiniteSteps_ClampBeforeUnderflowOrOverflowWithoutReversingOutOfRangePoses()
        {
            var position = new Vec3D(0, 0, 10);
            var gesture = new CameraZoomGesture(
                (float _, float _, out Vec3D reference) =>
                {
                    reference = default;
                    return true;
                },
                () => position,
                value =>
                {
                    position = value;
                    return true;
                });
            gesture.SetDistanceLimits(2, 20);

            Assert.IsTrue(gesture.Move(0, 0, float.MaxValue));
            Assert.AreEqual(2.0, position.z);
            Assert.IsTrue(gesture.Move(0, 0, -float.MaxValue));
            Assert.AreEqual(20.0, position.z);

            position = new Vec3D(0, 0, 0.25);
            Assert.IsTrue(gesture.Move(0, 0, float.MaxValue));
            Assert.AreEqual(0.25, position.z);
            position = new Vec3D(0, 0, 100);
            Assert.IsTrue(gesture.Move(0, 0, -float.MaxValue));
            Assert.AreEqual(100.0, position.z);

            gesture.SetDistanceLimits(null, null);
            position = new Vec3D(0, 0, 10);
            Assert.IsTrue(gesture.Move(0, 0, 100));
            Assert.IsTrue(position.z < 1e-8 && position.z > 0);
            Assert.IsTrue(gesture.Move(0, 0, -200));
            Assert.IsTrue(position.z > 1e9);
        }

        [TestMethod]
        public void Move_NonFiniteResult_LeavesCameraUnchanged()
        {
            var cameraPosition = new Vec3D(1.0, 2.0, 3.0);
            int applications = 0;
            var gesture = new CameraZoomGesture(
                (float _, float _, out Vec3D reference) =>
                {
                    reference = new Vec3D(double.MaxValue, 0.0, 0.0);
                    return true;
                },
                () => cameraPosition,
                _ =>
                {
                    applications++;
                    return true;
                });

            bool moved = gesture.Move(25.0f, 75.0f, -1000.0f);

            Assert.IsFalse(moved);
            Assert.AreEqual(0, applications);
            Assert.AreEqual(new Vec3D(1.0, 2.0, 3.0), cameraPosition);
        }

        [DataTestMethod]
        [DataRow(float.NaN)]
        [DataRow(float.PositiveInfinity)]
        [DataRow(float.NegativeInfinity)]
        public void Move_NonFiniteStep_LeavesCameraUnchanged(float amount)
        {
            var cameraPosition = new Vec3D(0.0, 0.0, 10.0);
            int applications = 0;
            var gesture = new CameraZoomGesture(
                (float _, float _, out Vec3D reference) =>
                {
                    reference = new Vec3D(0.0, 0.0, 0.0);
                    return true;
                },
                () => cameraPosition,
                _ =>
                {
                    applications++;
                    return true;
                });

            bool moved = gesture.Move(25.0f, 75.0f, amount);

            Assert.IsFalse(moved);
            Assert.AreEqual(0, applications);
            Assert.AreEqual(new Vec3D(0.0, 0.0, 10.0), cameraPosition);
        }
    }
}
