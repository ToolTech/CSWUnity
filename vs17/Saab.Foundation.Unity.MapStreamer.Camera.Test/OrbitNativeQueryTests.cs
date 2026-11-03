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

using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Saab.Foundation.Map;
using Saab.Foundation.Unity.MapStreamer;
using Saab.Foundation.Unity.MapStreamer.GeodeticCamera;
using Saab.Utility.Unity.NodeUtils;
using static Saab.Foundation.Unity.MapStreamer.GeodeticCamera.GizmoCameraMath;
using Quaternion = GizmoSDK.GizmoBase.Quaternion;
using Vector2 = GizmoSDK.GizmoBase.Vec2;
using Vector3 = GizmoSDK.GizmoBase.Vec3;
using UnityVector3 = UnityEngine.Vector3;

namespace Saab.Foundation.Unity.MapStreamer.Camera.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class OrbitNativeQueryTests
    {
        [TestCleanup]
        public void ClearNativeContext()
        {
            MapControl.SystemMap = null!;
            NodeUtils.FindTransform = _ => null;
        }

        [DataTestMethod]
        [DataRow("missing-transform")]
        [DataRow("missing-node")]
        [DataRow("invalid-node")]
        [DataRow("nonfinite-local")]
        [DataRow("overflow-local-input")]
        [DataRow("missing-map")]
        public void TerrainHitWithoutLocalTransform_EndsWithoutForwardFallback(string failure)
        {
            var world = new World();
            if (failure == "missing-transform") world.TerrainTransform = null;
            if (failure == "missing-node") world.Terrain.node = null;
            if (failure == "invalid-node") world.Terrain.node!.Reference = IntPtr.Zero;
            if (failure == "nonfinite-local")
                world.TerrainTransform!.Project = _ => new UnityVector3(0, float.PositiveInfinity, 0);
            if (failure == "overflow-local-input")
            {
                world.Terrain.position = new Vec3D(double.MaxValue, 0, 0);
                world.Terrain.ToGlobal = () => new Vec3D(10, -20, 30);
                world.TerrainTransform!.Project = _ => throw new AssertFailedException("Invalid input reached native TransformPoint.");
            }
            if (failure == "missing-map")
                MapControl.SystemMap.Terrain = (origin, direction, mask, flags) =>
                {
                    world.Calls.Add("terrain");
                    MapControl.SystemMap = null!;
                    return (true, world.Terrain);
                };
            OrbitGesture gesture = world.Camera.CreateGesture();
            Vec3D position = world.Camera.Position;
            Quaternion orientation = world.Camera.Orientation;

            gesture.Begin(Zero2, 800, 600);
            gesture.Rotate(MathF.PI / 72, 0);

            Assert.AreEqual(0, world.Camera.Preparations);
            Assert.AreEqual(0, world.Camera.Applications);
            Assert.AreEqual(position, world.Camera.Position);
            Assert.AreEqual(orientation, world.Camera.Orientation);
            CollectionAssert.AreEqual(new[] { "ray", "terrain" }, world.Calls);
        }

        [DataTestMethod]
        [DataRow("map-conversion")]
        [DataRow("nonfinite-world")]
        [DataRow("map-position")]
        [DataRow("missing-camera-node")]
        [DataRow("missing-local-transform")]
        [DataRow("nonfinite-local")]
        [DataRow("overflow-local-input")]
        [DataRow("missing-map")]
        [DataRow("missing-camera")]
        [DataRow("nonfinite-forward")]
        [DataRow("zero-forward")]
        [DataRow("zero-point")]
        [DataRow("degenerate-point")]
        [DataRow("degenerate-forward")]
        [DataRow("overflow-point-length")]
        public void FailedForwardFallback_EndsWithoutPreparingOrApplyingPose(string failure)
        {
            var world = new World();
            world.Terrain.clampResult = IntersectQuery.NULL;
            var map = MapControl.SystemMap;
            if (failure == "map-conversion") map.ToWorld = _ => (false, default);
            if (failure == "nonfinite-world") map.ToWorld = _ => (true, new CartPos(double.NaN, 0, 0));
            if (failure == "map-position") map.PositionFromWorld = (position, cart) => false;
            if (failure == "missing-camera-node") map.PositionFromWorld = (position, cart) => true;
            if (failure == "missing-local-transform") world.CameraTransform = null;
            if (failure == "nonfinite-local")
                world.CameraTransform!.Project = _ => new UnityVector3(float.NaN, 0, 0);
            if (failure == "overflow-local-input")
                map.PositionFromWorld = (position, cart) =>
                {
                    position.node = new Node();
                    position.position = new Vec3D(double.MaxValue, 0, 0);
                    return true;
                };
            if (failure == "missing-map")
                map.Terrain = (origin, direction, mask, flags) =>
                {
                    MapControl.SystemMap = null!;
                    return (true, world.Terrain);
                };
            if (failure == "missing-camera") world.Camera.Camera = null!;
            if (failure == "nonfinite-forward") world.Camera.Camera.transform.forward = new UnityVector3(float.PositiveInfinity, 0, 0);
            if (failure == "zero-forward") world.Camera.Camera.transform.forward = default;
            if (failure == "degenerate-forward") world.Camera.Camera.transform.forward = new UnityVector3(0.0000001f, 0, 0);
            if (failure == "overflow-point-length") world.CameraTransform!.Project = _ => new UnityVector3(float.MaxValue, 0, 0);
            if (failure == "zero-point" || failure == "degenerate-point")
                world.CameraTransform!.Project = _ => new UnityVector3(failure == "zero-point" ? 0 : 0.0000001f, 300, -400);

            OrbitGesture gesture = world.Camera.CreateGesture();
            Vec3D position = world.Camera.Position;
            Quaternion orientation = world.Camera.Orientation;
            gesture.Begin(Zero2, 800, 600);
            gesture.Rotate(MathF.PI / 72, 0);

            Assert.AreEqual(0, world.Camera.Preparations);
            Assert.AreEqual(0, world.Camera.Applications);
            Assert.AreEqual(position, world.Camera.Position);
            Assert.AreEqual(orientation, world.Camera.Orientation);
        }

        [DataTestMethod]
        [DataRow("terrain")]
        [DataRow("miss")]
        [DataRow("zero-local-hit")]
        public void ValidCenterQueries_PreserveNativeLocalCoordinatesAndLegacyForwardPoint(string reference)
        {
            var world = new World();
            if (reference == "miss") world.Terrain.clampResult = IntersectQuery.NULL;
            var expected = reference == "terrain" ? new Vector3(50, -100, 150) : new Vector3(0, -300, 400);
            world.TerrainTransform!.Project = input =>
            {
                Assert.AreEqual(10, input.x);
                Assert.AreEqual(-20, input.y);
                Assert.AreEqual(30, input.z, "Native node coordinates must not acquire pointer-mode Z inversion.");
                return reference == "zero-local-hit" ? default : new UnityVector3(50, -100, 150);
            };
            world.CameraTransform!.Project = input =>
            {
                Assert.AreEqual(0, input.sqrMagnitude, "SetCartPos supplies the camera's native node-local position.");
                return input;
            };
            var gesture = world.Camera.CreateGesture();

            gesture.Begin(Zero2, 800, 600);

            Assert.AreEqual(1, world.Camera.Preparations);
            Assert.AreEqual(1, world.Camera.Applications);
            Assert.IsTrue(
                (Vector3.Normalize(expected) - Transform(UnitZ, world.Camera.Orientation)).Length()
                < 0.00001f);
            CollectionAssert.AreEqual(reference == "terrain" ? new[] { "ray", "terrain" }
                : new[] { "ray", "terrain", "world", "set-position" }, world.Calls);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void LocalQueryRecovery_RequiresANewBegin(bool forwardFallback)
        {
            var world = new World();
            if (forwardFallback)
            {
                world.Terrain.clampResult = IntersectQuery.NULL;
                world.CameraTransform = null;
            }
            else world.TerrainTransform = null;
            var gesture = world.Camera.CreateGesture();
            gesture.Begin(Zero2, 800, 600);
            world.TerrainTransform = new UnityEngine.Transform();
            world.CameraTransform = new UnityEngine.Transform();
            gesture.Rotate(MathF.PI / 72, 0);
            Assert.AreEqual(0, world.Camera.Preparations);

            gesture.Begin(new Vector2(40, 0), 800, 600);
            gesture.Rotate(MathF.PI / 72, 0);
            Assert.AreEqual(2, world.Camera.Applications);
        }

        private sealed class World
        {
            internal readonly GeodeticCameraControl Camera = new();
            internal readonly List<string> Calls = new();
            internal readonly MapPos Terrain = new()
            {
                position = new Vec3D(10, -20, 30),
                node = new Node { Reference = new IntPtr(2) },
                clampResult = IntersectQuery.INTERSECT
            };
            internal UnityEngine.Transform? TerrainTransform = new();
            internal UnityEngine.Transform? CameraTransform = new();

            internal World()
            {
                NodeUtils.FindTransform = reference => reference == new IntPtr(2) ? TerrainTransform
                    : reference == new IntPtr(1) ? CameraTransform : null;
                MapControl.SystemMap = new MapControl
                {
                    Ray = (x, y, width, height) =>
                    {
                        Calls.Add("ray");
                        return (true, new Vec3D(0, 0, 0), new Vec3(0, -0.6f, 0.8f));
                    },
                    Terrain = (origin, direction, mask, flags) =>
                    {
                        Calls.Add("terrain");
                        return (true, Terrain);
                    },
                    ToWorld = position =>
                    {
                        Calls.Add("world");
                        return (true, new CartPos(6378237, 0, 0));
                    },
                    PositionFromWorld = (position, cart) =>
                    {
                        Calls.Add("set-position");
                        Assert.IsNull(position.node);
                        position.node = new Node();
                        position.position = new Vec3D(0, 0, 0);
                        return true;
                    }
                };
            }
        }
    }
}
