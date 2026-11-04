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
using static Saab.Foundation.Unity.MapStreamer.GeodeticCamera.GizmoCameraMath;
using Vector2 = GizmoSDK.GizmoBase.Vec2;
using Vector3 = GizmoSDK.GizmoBase.Vec3;
using ProductionMapType = Saab.Foundation.Map.MapType;

namespace Saab.Foundation.Unity.MapStreamer.Camera.Test
{
    [TestClass]
    public sealed class GroundDragGestureTests
    {
        public enum TestMapType
        {
            Unknown,
            Plain,
            Utm,
            Geocentric,
            Sweref99
        }

        [TestMethod]
        public void FreeProjectedPan_DoesNotDampenNearHorizonMovement()
        {
            var pan = ProjectedNavigationPlane.BeginPan(new Vec3D(0, 100, 0), default);
            Assert.IsTrue(pan.TryCalculateNextPosition(
                new Vec3D(20, 100, 0), new Vec3D(1, -0.125, 0), out Vec3D position));

            Assert.AreEqual(-820.0, position.x, 1e-6);
            Assert.AreEqual(100.0, position.y);
        }

        [DataTestMethod]
        [DataRow(TestMapType.Utm)]
        [DataRow(TestMapType.Sweref99)]
        [DataRow(TestMapType.Geocentric)]
        public void FreeGroundDrag_UsesFullNearHorizonDisplacementAndRetainsReference(TestMapType mapType)
        {
            var world = new World(mapType, dampen: false);
            world.RayDirection = new Vec3(1, -0.125f, 0);
            world.Surface = new Vec3D(world.InitialPosition.x + 400, 50, world.InitialPosition.z);
            world.Gesture.Move(0, 0, regrab: true);
            world.Gesture.Move(20, 30, regrab: false);

            Assert.AreEqual(world.InitialPosition.x - 20, world.Position.x, 1e-6);
            Assert.AreEqual(world.InitialPosition.z - 30, world.Position.z, 1e-6);
            Assert.AreEqual(world.InitialPosition.y, world.Position.y);
            Vec3D accepted = world.Position;
            world.Gesture.Move(20, 30, regrab: false);
            Assert.AreEqual(accepted, world.Position);
            Assert.AreEqual(1, world.SurfaceQueries);
            if (mapType == TestMapType.Geocentric)
            {
                Assert.AreEqual(new Vector2(-20, -30), world.AppliedOffset);
                Assert.AreEqual(world.InitialEcef, world.AppliedOrigin);
            }
        }

        [DataTestMethod]
        [DataRow(TestMapType.Utm)]
        [DataRow(TestMapType.Sweref99)]
        public void ProjectedBegin_CapturesWithoutMoving_ThenContinuesWithoutRepicking(
            TestMapType mapType)
        {
            var world = new World(mapType);
            world.Gesture.Move(0, 0, regrab: true);
            Assert.AreEqual(1, world.SurfaceQueries);
            Assert.AreEqual(0, world.Applications);
            Assert.AreEqual(world.InitialPosition, world.Position);

            world.Surface = new Vec3D(-999, -999, -999);
            world.Gesture.Move(10, 20, regrab: false);
            Assert.AreEqual(1, world.SurfaceQueries);
            Assert.AreEqual(1, world.Applications);
            Assert.AreEqual(new Vec3D(999990, 100, 2999980), world.Position);
        }

        [TestMethod]
        public void GeocentricBegin_CapturesPlaneFrameAndEcef_AndContinuesFromFrozenReference()
        {
            var world = new World(TestMapType.Geocentric);
            world.Gesture.Move(0, 0, regrab: true);
            Assert.AreEqual(1, world.FrameQueries);
            Assert.AreEqual(0, world.Applications);
            Assert.AreEqual(world.InitialPosition, world.Position);

            world.East = UnitZ;
            world.North = UnitX;
            world.Surface = new Vec3D(-999, -999, -999);
            world.Gesture.Move(10, 20, regrab: false);
            Assert.AreEqual(1, world.SurfaceQueries);
            Assert.AreEqual(1, world.FrameQueries);
            Assert.AreEqual(1, world.Applications);
            Assert.AreEqual(world.InitialEcef, world.AppliedOrigin);
            Assert.AreEqual(new Vector2(-10, -20), world.AppliedOffset);
            Assert.AreEqual(new Vec3D(999990, 100, 2999980), world.Position);
        }

        [DataTestMethod]
        [DataRow(TestMapType.Utm, "surface")]
        [DataRow(TestMapType.Geocentric, "surface")]
        [DataRow(TestMapType.Utm, "map")]
        [DataRow(TestMapType.Geocentric, "map")]
        [DataRow(TestMapType.Geocentric, "frame")]
        public void FailedRegrab_DiscardsPreviousGestureAndRequiresNewBegin(TestMapType mapType, string failure)
        {
            var world = new World(mapType);
            world.Gesture.Move(0, 0, regrab: true);
            world.SurfaceAvailable = failure != "surface";
            world.CurrentMapAvailable = failure != "map";
            world.FrameAvailable = failure != "frame";
            world.Gesture.Move(5, 5, regrab: true);
            world.SurfaceAvailable = world.CurrentMapAvailable = world.FrameAvailable = true;
            world.Gesture.Move(10, 20, regrab: false);
            Assert.AreEqual(0, world.Applications);
            Assert.AreEqual(world.InitialPosition, world.Position);
            world.Gesture.Move(0, 0, regrab: true);
            world.Gesture.Move(10, 20, regrab: false);
            Assert.AreEqual(1, world.Applications);
        }

        [DataTestMethod]
        [DataRow(TestMapType.Utm, false)]
        [DataRow(TestMapType.Geocentric, false)]
        [DataRow(TestMapType.Utm, true)]
        [DataRow(TestMapType.Geocentric, true)]
        public void MissingMapOrChangedMapType_ClearsGestureEvenIfOriginalMapTypeReturns(
            TestMapType mapType, bool missingMap)
        {
            var world = new World(mapType);
            world.Gesture.Move(0, 0, regrab: true);
            world.MapAvailable = !missingMap;
            if (!missingMap)
                world.MapType = mapType == TestMapType.Utm ? TestMapType.Sweref99 : TestMapType.Utm;
            world.Gesture.Move(10, 20, regrab: false);
            Assert.AreEqual(0, world.Applications);
            world.MapAvailable = true;
            world.MapType = mapType;
            world.Gesture.Move(10, 20, regrab: false);
            Assert.AreEqual(0, world.Applications);
            world.Gesture.Move(0, 0, regrab: true);
            world.Gesture.Move(10, 20, regrab: false);
            Assert.AreEqual(1, world.Applications);
        }

        [DataTestMethod]
        [DataRow(TestMapType.Utm, "ray")]
        [DataRow(TestMapType.Geocentric, "ray")]
        [DataRow(TestMapType.Utm, "parallel")]
        [DataRow(TestMapType.Geocentric, "parallel")]
        [DataRow(TestMapType.Utm, "behind")]
        [DataRow(TestMapType.Geocentric, "behind")]
        [DataRow(TestMapType.Geocentric, "application")]
        public void FailedContinuation_DoesNotMoveAndRequiresNewBegin(
            TestMapType mapType, string failure)
        {
            var world = new World(mapType);
            world.Gesture.Move(0, 0, regrab: true);
            world.RayAvailable = failure != "ray";
            world.ApplicationAllowed = failure != "application";
            if (failure == "parallel") world.RayDirection = new Vec3(1, 0, 0);
            if (failure == "behind") world.RayDirection = new Vec3(0, 1, 0);
            world.Gesture.Move(10, 20, regrab: false);
            Assert.AreEqual(0, world.Applications);
            Assert.AreEqual(world.InitialPosition, world.Position);
            world.RayAvailable = world.ApplicationAllowed = true;
            world.RayDirection = new Vec3(0, -1, 0);
            world.Gesture.Move(20, 30, regrab: false);
            Assert.AreEqual(0, world.Applications);
            world.Gesture.Move(0, 0, regrab: true);
            world.Gesture.Move(20, 30, regrab: false);
            Assert.AreEqual(1, world.Applications);
        }

        [DataTestMethod]
        [DataRow(TestMapType.Utm)]
        [DataRow(TestMapType.Geocentric)]
        public void ShallowRay_DampsMovement_AndStationaryPointerDoesNotCreep(TestMapType mapType)
        {
            var world = new World(mapType);
            world.RayDirection = new Vec3(1, -0.125f, 0);
            world.Surface = new Vec3D(world.InitialPosition.x + 400, 50, world.InitialPosition.z);
            world.Gesture.Move(0, 0, regrab: true);
            world.Gesture.Move(20, 30, regrab: false);
            float scale = (float)(0.125 / Math.Sqrt(1 + 0.125 * 0.125)) / 0.17364818f;
            Assert.AreEqual(world.InitialPosition.x - 20 * scale, world.Position.x, 0.0001);
            Assert.AreEqual(world.InitialPosition.z - 30 * scale, world.Position.z, 0.0001);
            Assert.AreEqual(world.InitialPosition.y, world.Position.y);
            Vec3D stopped = world.Position;
            world.SurfaceAvailable = false;
            for (int i = 0; i < 30; i++)
                world.Gesture.Move(20, 30, regrab: false);
            Assert.AreEqual(stopped.x, world.Position.x, 0.000001);
            Assert.AreEqual(stopped.z, world.Position.z, 0.000001);
            Assert.AreEqual(1, world.SurfaceQueries);
            Assert.AreEqual(31, world.Applications);
            if (mapType == TestMapType.Geocentric)
                Assert.AreEqual(world.InitialEcef, world.AppliedOrigin);
        }

        [TestMethod]
        public void GeocentricMovement_ProjectsDifferenceFromCapturedAnchorOntoFrozenEnuAxes()
        {
            var world = new World(TestMapType.Geocentric);
            world.East = new Vector3(0.8f, 0, 0.6f);
            world.North = new Vector3(-0.6f, 0, 0.8f);
            world.Surface = new Vec3D(world.Position.x + 30, 50, world.Position.z + 40);
            world.Gesture.Move(0, 0, regrab: true);
            world.Up = UnitZ;
            world.East = UnitY;
            world.North = UnitX;
            world.Gesture.Move(50, 60, regrab: false);
            Assert.AreEqual(1, world.Applications);
            Assert.AreEqual(-28f, world.AppliedOffset.x, 0.00001f);
            Assert.AreEqual(-4f, world.AppliedOffset.y, 0.00001f);
            Assert.AreEqual(world.InitialEcef, world.AppliedOrigin);
            Assert.AreEqual(1, world.FrameQueries);
        }

        [DataTestMethod]
        [DataRow(TestMapType.Utm)]
        [DataRow(TestMapType.Geocentric)]
        public void MapReset_ClearsGestureEvenWhenMapTypeIsUnchanged(TestMapType mapType)
        {
            var world = new World(mapType);
            world.Gesture.Move(0, 0, regrab: true);
            world.Gesture.Move(10, 20, regrab: false);
            Vec3D position = world.Position;
            world.Gesture.Clear();
            world.Gesture.Clear();
            world.Gesture.Move(20, 30, regrab: false);
            Assert.AreEqual(1, world.Applications);
            Assert.AreEqual(position, world.Position);
            world.Gesture.Move(0, 0, regrab: true);
            Assert.AreEqual(position, world.Position);
            world.Gesture.Move(20, 30, regrab: false);
            Assert.AreEqual(2, world.Applications);
        }

        [DataTestMethod]
        [DataRow(TestMapType.Utm)]
        [DataRow(TestMapType.Geocentric)]
        public void SuccessfulRegrab_UsesNewCameraAndReferenceWithoutMovingAtBegin(TestMapType mapType)
        {
            var world = new World(mapType);
            world.Gesture.Move(0, 0, regrab: true);
            world.Gesture.Move(10, 20, regrab: false);
            Vec3D position = world.Position;
            world.Surface = new Vec3D(position.x + 100, 75, position.z + 200);
            world.Gesture.Move(100, 200, regrab: true);
            Assert.AreEqual(new Vector2(100, 200), world.LastSurfacePointer);
            Assert.AreEqual(position, world.Position);
            Assert.AreEqual(1, world.Applications);
            world.Gesture.Move(103, 207, regrab: false);
            Assert.AreEqual(new Vec3D(position.x - 3, position.y, position.z - 7), world.Position);
            Assert.AreEqual(2, world.SurfaceQueries);
            Assert.AreEqual(2, world.Applications);
        }

        [DataTestMethod]
        [DataRow(TestMapType.Utm, true, 1)]
        [DataRow(TestMapType.Geocentric, true, 0)]
        [DataRow(TestMapType.Utm, false, 0)]
        [DataRow(TestMapType.Geocentric, false, 1)]
        public void PlaneIntersection_RetainsGeometrySpecificZeroDistanceAndGrazingPolicies(
            TestMapType mapType, bool zeroDistance, int applications)
        {
            var world = new World(mapType);
            if (zeroDistance) world.Surface = world.Position;
            world.Gesture.Move(0, 0, regrab: true);
            if (!zeroDistance) world.RayDirection = new Vec3(1, -5e-13f, 0);
            world.Gesture.Move(10, 20, regrab: false);
            Assert.AreEqual(applications, world.Applications);
            if (applications == 0)
                Assert.AreEqual(world.InitialPosition, world.Position);
            else
                Assert.AreNotEqual(world.InitialPosition, world.Position);
        }

        [DataTestMethod]
        [DataRow(TestMapType.Utm)]
        [DataRow(TestMapType.Geocentric)]
        public void DragFromBelowReference_UsesSignedIncidenceWithoutRejectingOppositeSide(
            TestMapType mapType)
        {
            var world = new World(mapType);
            world.Surface = new Vec3D(world.Position.x, 150, world.Position.z);
            world.RayDirection = new Vec3(0, 1, 0);
            world.Gesture.Move(0, 0, regrab: true);
            world.Gesture.Move(10, 20, regrab: false);
            Assert.AreEqual(1, world.Applications);
            Assert.AreEqual(new Vec3D(999990, 100, 2999980), world.Position);
        }

        [DataTestMethod]
        [DataRow(TestMapType.Utm, 1)]
        [DataRow(TestMapType.Geocentric, 1)]
        [DataRow(TestMapType.Unknown, 0)]
        [DataRow(TestMapType.Plain, 0)]
        public void Movement_RequiresBeginAndSupportedCoordinateModel(
            TestMapType mapType, int applications)
        {
            var world = new World(mapType);
            world.Gesture.Move(10, 20, regrab: false);
            Assert.AreEqual(0, world.Applications);
            Assert.AreEqual(0, world.SurfaceQueries);
            world.Gesture.Move(0, 0, regrab: true);
            world.Gesture.Move(10, 20, regrab: false);
            Assert.AreEqual(applications, world.Applications);
            Assert.AreEqual(1, world.SurfaceQueries);
        }

        private sealed class World
        {
            internal readonly GroundDragGesture Gesture;
            internal readonly Vec3D InitialPosition = new Vec3D(1000000, 100, 3000000);
            internal Vec3D Position;
            internal Vec3D Surface = new Vec3D(1000000, 50, 3000000);
            internal TestMapType MapType;
            internal int SurfaceQueries, FrameQueries, Applications;
            internal readonly Vec3D InitialEcef = new Vec3D(0, 6378237, 0);
            internal Vec3D AppliedOrigin;
            internal Vector2 AppliedOffset;
            internal Vector2 LastSurfacePointer;
            private Vec3D _cameraEcef;
            internal Vector3 East = UnitX, North = UnitZ, Up = UnitY;
            internal bool MapAvailable = true, CurrentMapAvailable = true;
            internal bool SurfaceAvailable = true, FrameAvailable = true;
            internal bool RayAvailable = true, ApplicationAllowed = true;
            internal Vec3 RayDirection = new Vec3(0, -1, 0);

            internal World(TestMapType mapType, bool dampen = true)
            {
                MapType = mapType;
                Position = InitialPosition;
                _cameraEcef = InitialEcef;
                Gesture = new GroundDragGesture(
                    ReadMap, QuerySurface, ProjectedRay, ReadFrame, Raycast, Apply, dampen);
            }

            private bool ReadMap(
                bool beginning,
                out ProductionMapType mapType,
                out Vec3D position)
            {
                mapType = (ProductionMapType)(int)MapType;
                position = Position;
                return MapAvailable
                    && (!beginning || CurrentMapAvailable);
            }

            private bool QuerySurface(float x, float y, out Vec3D position)
            {
                SurfaceQueries++;
                LastSurfacePointer = new Vector2(x, y);
                position = Surface;
                return SurfaceAvailable;
            }

            private bool ProjectedRay(float x, float y, out Vec3D origin, out Vec3 direction)
            {
                origin = new Vec3D(Position.x + x, Position.y, Position.z + y);
                direction = RayDirection;
                return RayAvailable;
            }

            private bool Apply(
                ProductionMapType mapType,
                Vec3D position,
                float east,
                float north)
            {
                AppliedOrigin = position;
                AppliedOffset = new Vector2(east, north);
                if (!ApplicationAllowed)
                    return false;
                if (mapType == ProductionMapType.GEOCENTRIC)
                {
                    // Deterministic coordinate/application adapter; production retains the native ellipsoid operation.
                    _cameraEcef = new Vec3D(position.x + east, position.y, position.z + north);
                    Position = new Vec3D(
                        InitialPosition.x + _cameraEcef.x - InitialEcef.x, InitialPosition.y,
                        InitialPosition.z + _cameraEcef.z - InitialEcef.z);
                }
                else
                    Position = position;
                Applications++;
                return true;
            }

            private bool ReadFrame(Vec3D reference, out GroundDragGesture.TangentFrame frame)
            {
                FrameQueries++;
                frame = new GroundDragGesture.TangentFrame
                {
                    East = East, North = North, Up = Up, CameraEcef = _cameraEcef,
                    ReferencePoint = new Vector3(
                        (float)(reference.x - Position.x), (float)(reference.y - Position.y),
                        (float)(reference.z - Position.z))
                };
                return FrameAvailable;
            }

            private bool Raycast(
                float x, float y, Vector3 normal, Vector3 point, out Vector3 intersection, out float incidence)
            {
                Vector3 origin = new Vector3(x, 0, y);
                Vector3 direction = new Vector3(RayDirection.x, RayDirection.y, RayDirection.z);
                normal = Vector3.Normalize(normal);
                incidence = -Vector3.Dot(Vector3.Normalize(direction), normal);
                float distance = Vector3.Dot(point - origin, normal) / Vector3.Dot(direction, normal);
                intersection = origin + direction * distance;
                return RayAvailable && distance > 0;
            }
        }
    }
}
