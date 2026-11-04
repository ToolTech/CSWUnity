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
using GizmoSDK.GizmoBase;
using NUnit.Framework;
using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer;
using Saab.Foundation.Unity.MapStreamer.GeodeticCamera;
using UnityEngine;
using GizmoQuaternion = GizmoSDK.GizmoBase.Quaternion;
using GizmoVector3 = GizmoSDK.GizmoBase.Vec3;
using UnityQuaternion = UnityEngine.Quaternion;
using FreeGeodeticCamera = Saab.Foundation.Unity.MapStreamer.GeodeticCamera.GeodeticCameraControl;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
    public sealed class GeodeticCameraControlTests
    {
        [Test]
        public void OrbitFacing_MatchesUnityLookRotation()
        {
            Vector3 forward = new Vector3(0.3f, -0.2f, 1.0f).normalized;
            Vector3 up = Vector3.up;

            GizmoQuaternion result = UnityCameraNavigationAdapter.FaceOrbitPivot(
                ToGizmo(forward), ToGizmo(up));
            var actual = new UnityQuaternion(result.x, result.y, result.z, result.w);
            UnityQuaternion expected = UnityQuaternion.LookRotation(forward, up);

            Assert.That(Mathf.Abs(UnityQuaternion.Dot(expected, actual)), Is.EqualTo(1.0f).Within(1e-6f));
        }

        [Test]
        public void OrbitFrame_PreservesDoublePrecisionPositionAndUnityAxes()
        {
            var position = new Vec3D(1000000000.25, -2000000000.5, 3000000000.75);
            UnityQuaternion orientation = UnityQuaternion.Euler(10.0f, 20.0f, 30.0f);

            OrbitGesture.Frame frame = UnityCameraNavigationAdapter.CreateOrbitFrame(
                position, orientation, Vector3.right, Vector3.forward, Vector3.up);

            Assert.That(frame.WorldPosition, Is.EqualTo(position));
            Assert.That(frame.East, Is.EqualTo(new GizmoVector3(1.0f, 0.0f, 0.0f)));
            Assert.That(frame.North, Is.EqualTo(new GizmoVector3(0.0f, 0.0f, 1.0f)));
            Assert.That(frame.Up, Is.EqualTo(new GizmoVector3(0.0f, 1.0f, 0.0f)));
            Assert.That(
                Mathf.Abs(UnityQuaternion.Dot(
                    orientation,
                    new UnityQuaternion(
                        frame.Orientation.x,
                        frame.Orientation.y,
                        frame.Orientation.z,
                        frame.Orientation.w))),
                Is.EqualTo(1.0f).Within(1e-6f));
        }

        [Test]
        public void GroundDragPlane_UsesUnityRayIntersectionAndSignedIncidence()
        {
            var ray = new Ray(new Vector3(0.0f, 10.0f, 0.0f), new Vector3(1.0f, -1.0f, 0.0f));

            bool hit = UnityCameraNavigationAdapter.TryIntersectGroundDragPlane(
                ray,
                new GizmoVector3(0.0f, 1.0f, 0.0f),
                default,
                out GizmoVector3 intersection,
                out float incidence);

            Assert.IsTrue(hit);
            Assert.That(intersection.x, Is.EqualTo(10.0f).Within(1e-5f));
            Assert.That(intersection.y, Is.EqualTo(0.0f).Within(1e-5f));
            Assert.That(intersection.z, Is.EqualTo(0.0f).Within(1e-5f));
            Assert.That(incidence, Is.EqualTo(1.0f / Mathf.Sqrt(2.0f)).Within(1e-6f));
        }

        [Test]
        public void MapPositionWithoutActiveMap_PreservesGlobalValueAndRejectsWorldConversion()
        {
            MapControl.SystemMap = null;
            var position = new MapPos
            {
                node = null,
                position = new Vec3D(10.0, 20.0, 30.0)
            };

            Assert.That(position.GlobalPosition(), Is.EqualTo(position.position));
            Assert.IsFalse(position.SetCartPos(1.0, 2.0, 3.0));
            Assert.IsFalse(position.SetLatPos(0.1, 0.2, 100.0));
        }

        [Test]
        public void CameraPublicSurfaces_ShareMovementSeparateConstraintsAndExcludeApplicationReset()
        {
            Type bare = typeof(FreeGeodeticCamera);
            Type navigation = typeof(ConstrainedGeodeticCamera);
            Assert.That(navigation.BaseType, Is.EqualTo(bare));
            Assert.IsTrue(typeof(ICameraPose).IsAssignableFrom(bare));
            Assert.IsNull(bare.Assembly.GetType(
                "Saab.Foundation.Unity.MapStreamer.GeodeticCamera.ICameraMovement"));
            foreach (string method in new[] { "TryOrbit", "TryPan", "TryZoom" })
            {
                Assert.IsNull(bare.GetMethod(method));
                Assert.IsNull(navigation.GetMethod(method));
            }

            foreach (string method in new[]
                { "TranslateByGroundDrag", "ZoomAtScreenPoint", "BeginOrbitAtScreenPoint", "ContinueOrbit" })
            {
                Assert.IsNotNull(bare.GetMethod(method));
                Assert.IsNotNull(navigation.GetMethod(method));
            }

            foreach (string method in new[] { "SetZoomDistanceLimits", "SetOrbitPitchLimits" })
            {
                Assert.IsNull(bare.GetMethod(method));
                Assert.IsNotNull(navigation.GetMethod(method));
            }

            foreach (Type camera in new[] { bare, navigation })
            {
                Assert.IsNull(camera.GetMethod("RequestInitialView"));
                Assert.IsNull(camera.GetMethod("TrySetInitialViewPose"));
                Assert.IsNull(camera.GetProperty("InitialViewController"));
                Assert.That(camera.GetMethod("LateUpdate").DeclaringType, Is.EqualTo(bare));
            }
        }

        [Test]
        public void BareCamera_MapChangeAndUnavailableBasis_PreserveQueuedRollUntilTraversal()
        {
            WithCamera<BarePoseProbeCamera>(camera =>
            {
                var position = new Vec3D(12, 34, 56);
                var rotation = UnityQuaternion.Euler(10, 20, 35);
                Assert.IsTrue(camera.TrySetPose(position, rotation));
                camera.MapChanged();
                camera.LateUpdate();
                camera.LateUpdate();
                camera.PreTraverse(false);

                Assert.That(camera.GlobalPosition, Is.EqualTo(position));
                Assert.That(Mathf.Abs(UnityQuaternion.Dot(camera.transform.rotation, rotation)),
                    Is.EqualTo(1).Within(1e-6f));
                Assert.That(camera.Orientation, Is.EqualTo(camera.transform.rotation));
            });
        }

        [Test]
        public void ScreenZoom_FreeCameraHasNoDistanceBounds_ConstrainedCameraUsesConfiguredBounds()
        {
            WithCamera<BarePoseProbeCamera>(camera =>
            {
                camera.TrySetPose(new Vec3D(0, 0, 10), UnityQuaternion.Euler(10, 20, 35));
                UnityQuaternion orientation = camera.Orientation;
                camera.ZoomAtScreenPoint(new Vector3(100, 200, 0), 100);
                Assert.That(Math.Abs(camera.GlobalPosition.z), Is.LessThan(1));
                Assert.That(Mathf.Abs(UnityQuaternion.Dot(camera.Orientation, orientation)),
                    Is.EqualTo(1).Within(1e-6f));
            });

            WithCamera<NavigationProbeCamera>(camera =>
            {
                camera.TrySetPose(new Vec3D(0, 0, 10), UnityQuaternion.Euler(10, 20, 35));
                camera.ZoomAtScreenPoint(new Vector3(100, 200, 0), 100);
                Assert.That(camera.GlobalPosition.z, Is.EqualTo(1).Within(1e-6));

                camera.SetZoomDistanceLimits(null, 20);
                camera.ZoomAtScreenPoint(new Vector3(100, 200, 0), 20);
                Assert.That(camera.GlobalPosition.z, Is.LessThan(1));
                camera.ZoomAtScreenPoint(new Vector3(100, 200, 0), -100);
                Assert.That(camera.GlobalPosition.z, Is.EqualTo(20).Within(1e-6));
            });
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void ScreenOrbit_NewGestureAfterZoomCapturesCurrentFraming(float steps)
        {
            WithCamera<BarePoseProbeCamera>(camera => AssertNewOrbitAfterScreenZoom(camera, steps));
            WithCamera<NavigationProbeCamera>(camera => AssertNewOrbitAfterScreenZoom(camera, steps));
        }

        private static void AssertNewOrbitAfterScreenZoom(FreeGeodeticCamera camera, float steps)
        {
            Assert.IsTrue(camera.TrySetPose(new Vec3D(0, 100, 100), UnityQuaternion.Euler(45, 0, 15)));
            Vec3D pivot = default;
            camera.BeginOrbitAtScreenPoint(new Vector3(100, 200, 0), OrbitPivotMode.PointerSurface);
            Assert.IsTrue(camera.ContinueOrbit(0.1f, 0.02f));
            Vec3D beforeZoom = camera.GlobalPosition;
            Vector3 capturedRay = OrbitTestCoordinates.CameraRay(camera, pivot);

            camera.ZoomAtScreenPoint(new Vector3(100, 200, 0), steps);

            Assert.That(camera.GlobalPosition, Is.Not.EqualTo(beforeZoom));
            camera.BeginOrbitAtScreenPoint(new Vector3(100, 200, 0), OrbitPivotMode.PointerSurface);
            double radius = OrbitTestCoordinates.Distance(camera.GlobalPosition, pivot);
            Assert.IsTrue(camera.ContinueOrbit(0.1f, 0.02f));
            Assert.That(OrbitTestCoordinates.Distance(camera.GlobalPosition, pivot), Is.EqualTo(radius).Within(1e-4));
            OrbitTestCoordinates.AssertFraming(camera, pivot, capturedRay);
        }

        [TestCase(OrbitPivotMode.PointerSurface, "success")]
        [TestCase(OrbitPivotMode.PointerSurface, "query")]
        [TestCase(OrbitPivotMode.PointerSurface, "application")]
        [TestCase(OrbitPivotMode.ViewportCenter, "success")]
        [TestCase(OrbitPivotMode.ViewportCenter, "query")]
        [TestCase(OrbitPivotMode.ViewportCenter, "application")]
        public void ScreenOrbit_WheelDoesNotEndHeldGesture(OrbitPivotMode mode, string outcome)
        {
            WithCamera<NavigationProbeCamera>(camera =>
            {
                Assert.IsTrue(camera.TrySetPose(new Vec3D(0, 100, 100), UnityQuaternion.Euler(45, 0, 15)));
                Vec3D pivot = default;
                camera.BeginOrbitAtScreenPoint(new Vector3(100, 200, 0), mode);
                Assert.IsTrue(camera.ContinueOrbit(0.1f, 0.02f));
                double radius = OrbitTestCoordinates.Distance(camera.GlobalPosition, pivot);
                Vector3 capturedRay = OrbitTestCoordinates.CameraRay(camera, pivot);
                AssertScreenWheel(camera, outcome);

                Vec3D afterWheel = camera.GlobalPosition;
                Assert.IsTrue(camera.ContinueOrbit(0.1f, 0.02f));
                Assert.That(camera.GlobalPosition, Is.Not.EqualTo(afterWheel));
                if (mode == OrbitPivotMode.PointerSurface && outcome != "success")
                {
                    Assert.That(OrbitTestCoordinates.Distance(camera.GlobalPosition, pivot),
                        Is.EqualTo(radius).Within(1e-4));
                    OrbitTestCoordinates.AssertFraming(camera, pivot, capturedRay);
                }
            });
        }

        private static void AssertScreenWheel(NavigationProbeCamera camera, string outcome)
        {
            Vec3D position = camera.GlobalPosition;
            UnityQuaternion orientation = camera.Orientation;
            camera.Available = outcome != "query";
            if (outcome == "application")
                camera.BeforePreparation = () => camera.Available = false;

            camera.ZoomAtScreenPoint(new Vector3(100, 200, 0), 1);
            camera.Available = true;
            camera.BeforePreparation = null;
            Assert.That(camera.GlobalPosition, outcome == "success" ? Is.Not.EqualTo(position) : Is.EqualTo(position));
            Assert.That(camera.Orientation, Is.EqualTo(orientation));
        }

        [Test]
        public void ScreenOrbit_UsesSharedGeometry_AndOnlyConstrainedViewStopsAtFirstBoundary()
        {
            WithCamera<BarePoseProbeCamera>(camera =>
            {
                camera.TrySetPose(new Vec3D(1000, 2000, 3000), UnityQuaternion.Euler(30, 20, 0));
                Vector3 ray = new Vector3(0.4f, -0.2f, 1).normalized;
                Vec3D pivot = OrbitTestCoordinates.Pivot(camera, ray, 1000);
                camera.OrbitPivot = pivot;
                camera.BeginOrbitAtScreenPoint(new Vector3(100, 200, 0), OrbitPivotMode.PointerSurface);
                Assert.IsTrue(camera.ContinueOrbit(0, 100 * Mathf.Deg2Rad));
                Assert.That(Vector3.Dot(camera.Orientation * Vector3.up, Vector3.up), Is.LessThan(0));
                OrbitTestCoordinates.AssertFraming(camera, pivot, ray);
                Vec3D accepted = camera.GlobalPosition;
                UnityQuaternion orientation = camera.Orientation;
                Assert.IsTrue(camera.ContinueOrbit(0, 0));
                Assert.That(camera.GlobalPosition, Is.EqualTo(accepted));
                Assert.That(camera.Orientation, Is.EqualTo(orientation));
            });

            WithCamera<NavigationProbeCamera>(camera =>
            {
                camera.SetOrbitPitchLimits(-89, 89);
                camera.TrySetPose(new Vec3D(1000, 2000, 3000), UnityQuaternion.Euler(30, 20, 0));
                Vector3 ray = new Vector3(0.4f, -0.2f, 1).normalized;
                Vec3D pivot = OrbitTestCoordinates.Pivot(camera, ray, 1000);
                camera.OrbitPivot = pivot;
                camera.BeginOrbitAtScreenPoint(new Vector3(100, 200, 0), OrbitPivotMode.PointerSurface);
                Assert.IsTrue(camera.ContinueOrbit(0, 400 * Mathf.Deg2Rad));
                Assert.That(Vector3.Dot(camera.Orientation * Vector3.up, Vector3.up), Is.GreaterThanOrEqualTo(-1e-6f));
                OrbitTestCoordinates.AssertFraming(camera, pivot, ray);
                Vec3D stopped = camera.GlobalPosition;
                Assert.IsTrue(camera.ContinueOrbit(0, -Mathf.Deg2Rad));
                Assert.That(camera.GlobalPosition, Is.Not.EqualTo(stopped));
                OrbitTestCoordinates.AssertFraming(camera, pivot, ray);

                Vec3D accepted = camera.GlobalPosition;
                UnityQuaternion orientation = camera.Orientation;
                camera.BeforePreparation = camera.MapChanged;
                Assert.IsFalse(camera.ContinueOrbit(Mathf.Deg2Rad, 0));
                Assert.That(camera.GlobalPosition, Is.EqualTo(accepted));
                Assert.That(camera.Orientation, Is.EqualTo(orientation));
                camera.BeforePreparation = null;
                Assert.IsFalse(camera.ContinueOrbit(Mathf.Deg2Rad, 0));
                camera.BeginOrbitAtScreenPoint(new Vector3(100, 200, 0), OrbitPivotMode.PointerSurface);
                Assert.IsTrue(camera.ContinueOrbit(Mathf.Deg2Rad, 0));
                OrbitTestCoordinates.AssertFraming(camera, pivot, ray);

                accepted = camera.GlobalPosition;
                camera.Available = false;
                Assert.IsFalse(camera.ContinueOrbit(Mathf.Deg2Rad, 0));
                Assert.That(camera.GlobalPosition, Is.EqualTo(accepted));
                camera.Available = true;
                Assert.IsFalse(camera.ContinueOrbit(Mathf.Deg2Rad, 0));
                camera.BeginOrbitAtScreenPoint(new Vector3(100, 200, 0), OrbitPivotMode.PointerSurface);
                Assert.IsTrue(camera.ContinueOrbit(Mathf.Deg2Rad, 0));
                OrbitTestCoordinates.AssertFraming(camera, pivot, ray);
            });
        }

        [Test]
        public void ConstrainedDirectPose_BypassesDistanceAndUprightNavigationPolicy()
        {
            WithCamera<NavigationProbeCamera>(camera =>
            {
                camera.SetZoomDistanceLimits(1, 10);
                camera.SetOrbitPitchLimits(-45, 45);
                var position = new Vec3D(0, 0, 1000);
                UnityQuaternion rotation = UnityQuaternion.Euler(120, 40, 180);
                Assert.IsTrue(camera.TrySetPose(position, rotation));
                Assert.That(camera.GlobalPosition, Is.EqualTo(position));
                Assert.That(Mathf.Abs(UnityQuaternion.Dot(camera.Orientation, rotation)),
                    Is.EqualTo(1).Within(1e-6f));
                camera.PreTraverse(false);
                Assert.That(Mathf.Abs(UnityQuaternion.Dot(camera.transform.rotation, rotation)),
                    Is.EqualTo(1).Within(1e-6f));
            });
        }

        private static void WithCamera<T>(Action<T> test) where T : FreeGeodeticCamera
        {
            var previousMap = MapControl.SystemMap;
            MapControl.SystemMap = null;
            var cameraObject = new GameObject(typeof(T).Name + " test");
            try
            {
                test(cameraObject.AddComponent<T>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                MapControl.SystemMap = previousMap;
            }
        }

        private static GizmoVector3 ToGizmo(Vector3 value)
            => new(value.x, value.y, value.z);
    }
}
