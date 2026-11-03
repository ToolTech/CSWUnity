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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Saab.Foundation.Unity.MapStreamer;
using Saab.Foundation.Unity.MapStreamer.GeodeticCamera;

namespace Saab.Foundation.Unity.MapStreamer.Camera.Test
{
    [TestClass]
    public sealed class CameraZoomMathTests
    {
        [TestMethod]
        public void ZeroDistance_ReturnsFalse()
        {
            bool canZoom = CameraZoomMath.TryCalculateDistanceScale(
                0.0, 1.0f, 1.0, 100.0, out double scale);

            Assert.IsFalse(canZoom);
            Assert.AreEqual(0.0, scale);
        }

        [DataTestMethod]
        [DataRow(100.0, 1.0f, 1000.0, 0.8)]
        [DataRow(100.0, -1.0f, 1000.0, 1.25)]
        [DataRow(900.0, -10.0f, 1000.0, 1.1111111111111112)]
        public void ValidInput_PreservesExponentialZoom(
            double distance,
            float steps,
            double maximumDistance,
            double expectedScale)
        {
            Assert.IsTrue(CameraZoomMath.TryCalculateDistanceScale(
                distance, steps, 1.0, maximumDistance, out double scale));
            Assert.AreEqual(expectedScale, scale, 1e-12);
        }

        [TestMethod]
        public void CloseInspection_StopsAtOneMetre()
        {
            Assert.IsTrue(CameraZoomMath.TryCalculateDistanceScale(
                10.0, 100.0f, 1.0, 10.0, out double scale));

            Assert.AreEqual(0.1, scale, 1e-12);
        }

        [DataTestMethod]
        [DataRow(0.5, 1.0f, 100.0)]
        [DataRow(1500.0, -1.0f, 1000.0)]
        public void OutsideRequestedLimit_DoesNotReverseDirection(
            double distance,
            float steps,
            double maximumDistance)
        {
            Assert.IsTrue(CameraZoomMath.TryCalculateDistanceScale(
                distance, steps, 1.0, maximumDistance, out double scale));

            Assert.AreEqual(1.0, scale);
        }
    }
}
