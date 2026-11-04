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
using GizmoSDK.Gizmo3D;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer;
using Saab.Foundation.Unity.MapStreamer.GeodeticCamera;
using Saab.Utility.Unity.NodeUtils;
using static Saab.Foundation.Unity.MapStreamer.GeodeticCamera.GizmoCameraMath;
using CameraComponent = Saab.Foundation.Unity.MapStreamer.GeodeticCamera.GeodeticCameraControl;
using UnityVector3 = UnityEngine.Vector3;

namespace Saab.Foundation.Unity.MapStreamer.Camera.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class CameraMovementTests
    {
        [TestCleanup]
        public void ClearNativeContext()
        {
            MapControl.SystemMap = null!;
            NodeUtils.FindTransform = _ => null;
        }

        [DataTestMethod]
        [DataRow(MapType.UTM, false)]
        [DataRow(MapType.PROJ_SWEREF99, false)]
        [DataRow(MapType.UTM, true)]
        [DataRow(MapType.PROJ_SWEREF99, true)]
        public void ScreenPan_PositiveNorthDecreasesProjectedGlobalZ(MapType mapType, bool damped)
        {
            var camera = new CameraComponent { DampedPan = damped };
            Vec3D origin = camera.Position;
            const float incidence = 0.01f;
            float horizontal = MathF.Sqrt(1 - incidence * incidence);
            var terrain = new MapPos
            {
                position = new Vec3D(origin.x, 0, origin.z),
                clampResult = IntersectQuery.INTERSECT
            };
            MapControl.SystemMap = new MapControl
            {
                MapType = mapType,
                Ray = (x, y, width, height) =>
                    (true, new Vec3D(origin.x - x - origin.y * horizontal / incidence,
                        origin.y, origin.z + height - y),
                        new Vec3(horizontal, -incidence, 0)),
                Terrain = (rayOrigin, direction, type, flags) => (true, terrain)
            };

            camera.TranslateByGroundDrag(new UnityVector3(0, 0, 0), true);
            camera.TranslateByGroundDrag(new UnityVector3(3, 10, 0), false);

            double scale = damped ? 0.01f / 0.17364818f : 1;
            Assert.AreEqual(origin.x + 3 * scale, camera.Position.x, 1e-5);
            Assert.AreEqual(origin.y, camera.Position.y);
            Assert.AreEqual(origin.z - 10 * scale, camera.Position.z, 1e-5,
                "Projected east/north/up is +X/-Z/+Y for both supported projected map models.");
        }

        [DataTestMethod]
        [DataRow(1f)]
        [DataRow(-1f)]
        public void ScreenOrbit_NewGestureAfterZoomCapturesCurrentRadiusAndFraming(float wheelSteps)
        {
            CameraComponent camera = CreateCamera();
            Vec3D pivot = default;
            camera.BeginOrbitAtScreenPoint(new UnityVector3(100, 200, 0), OrbitPivotMode.PointerSurface);
            Assert.IsTrue(camera.ContinueOrbit(0.1f, 0.05f));
            Vec3D beforeZoom = camera.Position;

            camera.ZoomAtScreenPoint(new UnityVector3(100, 200, 0), wheelSteps);

            Assert.AreNotEqual(beforeZoom, camera.Position);
            camera.BeginOrbitAtScreenPoint(new UnityVector3(100, 200, 0), OrbitPivotMode.PointerSurface);
            double radius = Distance(camera.Position, pivot);
            Vec3 capturedRay = CameraRay(camera, pivot);
            Assert.IsTrue(camera.ContinueOrbit(0.3f, -0.1f));
            Assert.AreEqual(radius, Distance(camera.Position, pivot), 1e-4,
                "A new orbit following zoom must capture the current offset.");
            Assert.IsTrue((CameraRay(camera, pivot) - capturedRay).Length() < 1e-5f);
        }

        [DataTestMethod]
        [DataRow(OrbitPivotMode.PointerSurface, "success")]
        [DataRow(OrbitPivotMode.PointerSurface, "query")]
        [DataRow(OrbitPivotMode.PointerSurface, "application")]
        [DataRow(OrbitPivotMode.PointerSurface, "invalid")]
        [DataRow(OrbitPivotMode.ViewportCenter, "success")]
        [DataRow(OrbitPivotMode.ViewportCenter, "query")]
        [DataRow(OrbitPivotMode.ViewportCenter, "application")]
        [DataRow(OrbitPivotMode.ViewportCenter, "invalid")]
        public void HeldScreenOrbit_AfterWheelContinuesWithoutAnotherBegin(OrbitPivotMode mode, string outcome)
        {
            CameraComponent camera = CreateCamera();
            Vec3D pivot = default;
            camera.BeginOrbitAtScreenPoint(new UnityVector3(100, 200, 0), mode);
            Assert.IsTrue(camera.ContinueOrbit(0.1f, 0.05f));
            double radius = Distance(camera.Position, pivot);
            Vec3 capturedRay = CameraRay(camera, pivot);

            ApplyScreenWheel(camera, outcome);

            Vec3D afterWheel = camera.Position;
            Assert.IsTrue(camera.ContinueOrbit(0.3f, -0.1f));
            Assert.AreNotEqual(afterWheel, camera.Position,
                "Wheel input while held must not end the screen orbit or require a new begin.");
            if (mode == OrbitPivotMode.PointerSurface && outcome != "success")
            {
                Assert.AreEqual(radius, Distance(camera.Position, pivot), 1e-4);
                Assert.IsTrue((CameraRay(camera, pivot) - capturedRay).Length() < 1e-5f);
            }
        }

        [DataTestMethod]
        [DataRow(MapType.UTM, "success")]
        [DataRow(MapType.UTM, "query")]
        [DataRow(MapType.UTM, "application")]
        [DataRow(MapType.UTM, "invalid")]
        [DataRow(MapType.PROJ_SWEREF99, "success")]
        [DataRow(MapType.PROJ_SWEREF99, "query")]
        [DataRow(MapType.PROJ_SWEREF99, "application")]
        [DataRow(MapType.PROJ_SWEREF99, "invalid")]
        public void HeldScreenPan_AfterWheelContinuesWithoutAnotherRegrab(MapType mapType, string outcome)
        {
            CameraComponent camera = CreateCamera();
            MapControl.SystemMap.MapType = mapType;
            camera.TranslateByGroundDrag(new UnityVector3(100, 200, 0), true);

            ApplyScreenWheel(camera, outcome);

            Vec3D afterWheel = camera.Position;
            camera.TranslateByGroundDrag(new UnityVector3(110, 210, 0), false);
            Assert.AreNotEqual(afterWheel, camera.Position,
                "Wheel input while held must not end the screen pan or require a new regrab.");
        }

        private static CameraComponent CreateCamera()
        {
            var camera = new CameraComponent
            {
                Position = new Vec3D(0, 100, 100),
                Orientation = CreateLookRotation(new Vec3(0.2f, -1, 1), UnitY)
            };
            var terrain = new MapPos
            {
                position = default,
                node = new Node(),
                clampResult = IntersectQuery.INTERSECT
            };
            MapControl.SystemMap = new MapControl
            {
                MapType = MapType.UTM,
                Ray = (x, y, width, height) =>
                    (true, new Vec3D(camera.Position.x + x, camera.Position.y, camera.Position.z + y),
                        new Vec3(0, -1, 0)),
                Terrain = (origin, direction, type, flags) => (true, terrain)
            };
            NodeUtils.FindTransform = _ => new UnityEngine.Transform
            {
                Project = _ => new UnityVector3(0, -100, 100)
            };
            return camera;
        }

        private static void ApplyScreenWheel(CameraComponent camera, string outcome)
        {
            Vec3D position = camera.Position;
            var orientation = camera.Orientation;
            var ray = MapControl.SystemMap.Ray;
            if (outcome == "query")
                MapControl.SystemMap.Ray = (x, y, width, height) => (false, default, default);

            camera.PositionApplicationAllowed = outcome != "application";
            camera.ZoomAtScreenPoint(new UnityVector3(100, 200, 0), outcome == "invalid" ? float.NaN : 1);
            camera.PositionApplicationAllowed = true;
            MapControl.SystemMap.Ray = ray;

            if (outcome == "success")
                Assert.AreNotEqual(position, camera.Position);
            else
                Assert.AreEqual(position, camera.Position, "Rejected wheel movement must not change the accepted pose.");

            Assert.AreEqual(orientation, camera.Orientation);
        }

        private static double Distance(Vec3D first, Vec3D second)
        {
            Vec3D delta = first - second;
            return Math.Sqrt(delta.x * delta.x + delta.y * delta.y + delta.z * delta.z);
        }

        private static Vec3 CameraRay(CameraComponent camera, Vec3D pivot)
        {
            Vec3D offset = pivot - camera.Position;
            Vec3 ray = Transform(new Vec3((float)offset.x, (float)offset.y, (float)-offset.z),
                Inverse(camera.Orientation));
            Assert.IsTrue(TryNormalize(ray, out Vec3 normalized));
            return normalized;
        }
    }
}
