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
using static Saab.Foundation.Unity.MapStreamer.GeodeticCamera.GizmoCameraMath;
using static Saab.Foundation.Unity.MapStreamer.Camera.Test.OrbitTestWorld;
using Quaternion = GizmoSDK.GizmoBase.Quaternion;
using Vec3D = GizmoSDK.GizmoBase.Vec3D;
using Vector2 = GizmoSDK.GizmoBase.Vec2;
using Vector3 = GizmoSDK.GizmoBase.Vec3;
using World = Saab.Foundation.Unity.MapStreamer.Camera.Test.OrbitTestWorld;

namespace Saab.Foundation.Unity.MapStreamer.Camera.Test
{
    [TestClass]
    public sealed class OrbitGestureTests
    {
        [TestMethod]
        public void PointerGesture_CapturesWithoutMoving_ThenPreservesOffCenterFraming()
        {
            var world = new World();
            var position = world.Position;
            var orientation = world.Orientation;
            world.Move(new Vector2(123, 456), begin: true);

            Assert.AreEqual(new Vector2(123, 456), world.LastPick);
            Assert.AreEqual(position, world.Position);
            Assert.AreEqual(orientation, world.Orientation);
            Assert.AreEqual(0, world.Applications);

            world.Move(new Vector2(124, 455));
            Assert.AreEqual(1, world.Applications);
            Assert.AreNotEqual(position, world.Position);
            world.AssertFraming();
        }

        [TestMethod]
        public void ViewportCenterGesture_PicksCenterFacesPivot_ThenUsesPixelDeltas()
        {
            var world = new World();
            world.Gesture.SetPivotMode(OrbitPivotMode.ViewportCenter);
            var position = world.Position;
            world.Move(new Vector2(123, 456), begin: true);

            Assert.AreEqual(new Vector2(400, 300), world.LastPick);
            Assert.IsTrue(Offset(world.Position, position).Length() < 0.001f);
            Assert.AreEqual(1, world.Applications);
            Vector3 forward = Vector3.Normalize(Offset(world.Terrain, world.Position));
            Assert.IsTrue((forward - Transform(UnitZ, world.Orientation)).Length() < 0.00001f);

            world.Move(new Vector2(131, 456));
            Assert.AreEqual(2, world.Applications);
            Vector3 expected = Transform(forward, CreateFromAxisAngle(UnitY, MathF.PI / 180));
            Assert.IsTrue((expected - Transform(UnitZ, world.Orientation)).Length() < 0.00001f);
        }

        [TestMethod]
        public void ViewportCenterGeocentricGesture_StationaryPointerDoesNotMoveInChangingEnuFrame()
        {
            var world = new World(geocentric: true, pointerMode: false);
            world.Move(Zero2, begin: true);
            var pointer = new Vector2(80, -40);
            world.Move(pointer);
            Vec3D position = world.Position;
            Quaternion orientation = world.Orientation;

            world.Move(pointer);

            Assert.IsTrue(Offset(position, world.Position).Length() < 0.001f);
            Assert.IsTrue(MathF.Abs(Dot(orientation, world.Orientation)) > 0.999999f);
        }

        [TestMethod]
        public void ViewportCenterGeocentricGesture_NextDeltaUsesLatestEnuFrame()
        {
            var continuous = new World(geocentric: true, pointerMode: false);
            continuous.Move(Zero2, begin: true);
            var pointer = new Vector2(80, -40);
            continuous.Move(pointer);

            var restarted = new World(geocentric: true, pointerMode: false)
            {
                Position = continuous.Position,
                Orientation = continuous.Orientation,
                Terrain = continuous.Terrain
            };
            restarted.Move(pointer, begin: true);
            var nextPointer = pointer + new Vector2(8, -4);

            continuous.Move(nextPointer);
            restarted.Move(nextPointer);

            float positionDifference = Offset(restarted.Position, continuous.Position).Length();
            float orientationAlignment = MathF.Abs(
                Dot(restarted.Orientation, continuous.Orientation));
            Assert.IsTrue(positionDifference < 1.0f, $"Position differed by {positionDifference} metres.");
            Assert.IsTrue(orientationAlignment > 0.999999f, $"Orientation alignment was {orientationAlignment}.");
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ViewportCenterWithoutUsableTerrain_OrbitsPoint500MetersAlongView(bool zeroOffset)
        {
            var world = new World { TerrainAvailable = zeroOffset };
            world.Terrain = world.Position;
            world.Gesture.SetPivotMode(OrbitPivotMode.ViewportCenter);
            var pivot = Add(world.Position, Transform(UnitZ, world.Orientation) * 500);
            world.Move(Zero2, begin: true);
            Assert.AreEqual(500f, world.FallbackDistance);
            world.Move(new Vector2(80, -80));
            Assert.AreEqual(2, world.Applications);
            Assert.AreEqual(500f, Offset(pivot, world.Position).Length(), 0.001f);
            Assert.IsTrue(
                (Vector3.Normalize(Offset(pivot, world.Position))
                    - Transform(UnitZ, world.Orientation)).Length() < 0.00001f);
        }

        [DataTestMethod]
        [DataRow(OrbitPivotMode.PointerSurface)]
        [DataRow(OrbitPivotMode.ViewportCenter)]
        public void SettingMode_EndsGestureButRetainsLastPointerPivot(OrbitPivotMode mode)
        {
            var world = new World();
            world.Move(Zero2, begin: true);
            world.Move(new Vector2(8, -8));
            world.TerrainAvailable = false;
            var position = world.Position;
            var orientation = world.Orientation;
            world.Gesture.SetPivotMode(mode);
            world.Move(new Vector2(100, 100));
            Assert.AreEqual(position, world.Position);
            Assert.AreEqual(orientation, world.Orientation);
            world.Gesture.SetPivotMode(OrbitPivotMode.PointerSurface);
            world.Move(new Vector2(100, 100), begin: true);
            world.Move(new Vector2(108, 92));
            Assert.AreNotEqual(position, world.Position);
            Assert.AreEqual(0f, world.FallbackDistance);
            world.AssertFraming();
        }

        [TestMethod]
        public void Reset_DiscardsActiveGestureAndLastPivot_UntilNewTerrainIsAcquired()
        {
            var world = new World();
            world.Move(Zero2, begin: true);
            world.Gesture.Reset();
            world.Gesture.Reset();
            world.TerrainAvailable = false;
            world.Move(new Vector2(8, -8));
            world.Move(new Vector2(8, -8), begin: true);
            world.Move(new Vector2(16, -16));
            Assert.AreEqual(0, world.Applications);
            Assert.AreEqual(0f, world.FallbackDistance);

            world.TerrainAvailable = true;
            world.Move(new Vector2(16, -16));
            Assert.AreEqual(0, world.Applications, "Returning terrain alone must not restart orbit.");
            world.Move(new Vector2(16, -16), begin: true);
            world.Move(new Vector2(24, -24));
            Assert.AreEqual(1, world.Applications);
            world.AssertFraming();
        }

        [DataTestMethod]
        [DataRow(OrbitPivotMode.PointerSurface, "frame")]
        [DataRow(OrbitPivotMode.PointerSurface, "nan")]
        [DataRow(OrbitPivotMode.PointerSurface, "infinity")]
        [DataRow(OrbitPivotMode.PointerSurface, "overflow")]
        [DataRow(OrbitPivotMode.ViewportCenter, "overflow")]
        public void InvalidInputOrMissingFrame_RequiresNewBegin(OrbitPivotMode mode, string failure)
        {
            var world = new World();
            world.Gesture.SetPivotMode(mode);
            Vector2 start = failure == "overflow" ? new Vector2(float.MaxValue, 0) : Zero2;
            world.Move(start, begin: true);
            int applications = world.Applications;
            world.FrameAvailable = failure != "frame";
            Vector2 invalid = failure switch
            {
                "nan" => new Vector2(float.NaN, 0),
                "infinity" => new Vector2(0, float.PositiveInfinity),
                "overflow" => new Vector2(-float.MaxValue, 0),
                _ => new Vector2(8, -8)
            };
            world.Move(invalid);
            Assert.AreEqual(applications, world.Applications);
            world.FrameAvailable = true;
            world.Move(new Vector2(16, -16));
            world.Move(new Vector2(24, -24));
            Assert.AreEqual(applications, world.Applications);

            world.Move(Zero2, begin: true);
            world.Move(new Vector2(8, -8));
            Assert.IsTrue(world.Applications > applications);
        }

        [DataTestMethod]
        [DataRow(OrbitPivotMode.PointerSurface)]
        [DataRow(OrbitPivotMode.ViewportCenter)]
        public void ChangingBounds_DoesNotSnapOrEndGesture_AndReverseLeavesStopImmediately(OrbitPivotMode mode)
        {
            var world = new World();
            world.Gesture.SetPivotMode(mode);
            world.Move(Zero2, begin: true);
            var position = world.Position;
            var orientation = world.Orientation;
            world.Gesture.SetPitchLimits(-45, 45);
            Assert.AreEqual(position, world.Position);
            Assert.AreEqual(orientation, world.Orientation);

            world.Move(new Vector2(0, -3200));
            float elevation = MathF.Asin(-Vector3.Normalize(Offset(world.Terrain, world.Position)).y)
                * 180 / MathF.PI;
            Assert.AreEqual(45, elevation, 0.001f);
            position = world.Position;
            world.Move(new Vector2(0, -3201));
            Assert.IsTrue(Offset(position, world.Position).Length() < 0.001f);
            world.Move(new Vector2(0, -3200));
            Assert.IsTrue(Offset(position, world.Position).Length() > 1f);
        }

        [DataTestMethod]
        [DataRow(OrbitPivotMode.PointerSurface, true)]
        [DataRow(OrbitPivotMode.PointerSurface, false)]
        [DataRow(OrbitPivotMode.ViewportCenter, false)]
        public void RejectedStep_ConsumesInputButKeepsAcceptedPose_AndReverseContinues(
            OrbitPivotMode mode, bool failQuery)
        {
            var world = new World();
            var control = new World();
            world.Gesture.SetPivotMode(mode);
            control.Gesture.SetPivotMode(mode);
            world.Move(Zero2, begin: true);
            control.Move(Zero2, begin: true);
            var position = world.Position;
            var orientation = world.Orientation;
            int applications = world.Applications;
            world.FailQueryAt = failQuery ? 2 : 0;
            world.PreparationAllowed = failQuery;
            world.Move(new Vector2(80, -80));
            Assert.AreEqual(position, world.Position);
            Assert.AreEqual(orientation, world.Orientation);
            Assert.AreEqual(applications, world.Applications);

            world.FailQueryAt = 0;
            world.PreparationAllowed = true;
            world.Move(new Vector2(72, -72));
            control.Move(new Vector2(-8, 8));
            Assert.AreEqual(control.Position, world.Position);
            Assert.AreEqual(control.Orientation, world.Orientation);
            Assert.AreEqual(applications + 1, world.Applications);
            if (mode == OrbitPivotMode.PointerSurface) world.AssertFraming();
        }

        [DataTestMethod]
        [DataRow(false, 1f, false, 120f)]
        [DataRow(false, -1f, false, 120f)]
        [DataRow(true, 1f, false, 120f)]
        [DataRow(true, -1f, false, 120f)]
        [DataRow(false, 1f, true, 120f)]
        [DataRow(false, -1f, true, 120f)]
        [DataRow(true, 1f, true, 120f)]
        [DataRow(true, -1f, true, 120f)]
        [DataRow(true, 1f, false, 179f)]
        [DataRow(true, -1f, false, 179f)]
        [DataRow(true, 1f, false, -400f)]
        [DataRow(true, -1f, false, -400f)]
        public void BoundedSequence_MatchesLargeStop_RestartsHeadsAndReversesWithSafeFraming(
            bool geocentric, float sign, bool centered, float heading)
        {
            var large = new World(geocentric, centered);
            var small = new World(geocentric, centered);
            large.Gesture.SetPitchLimits(-89, 89);
            small.Gesture.SetPitchLimits(-89, 89);
            large.Move(Zero2, begin: true);
            small.Move(Zero2, begin: true);
            large.Move(new Vector2(0, -sign * 3200));
            large.AssertBoundedPose();
            for (int i = 1; i <= 1440; i++)
            {
                small.Move(new Vector2(0, -sign * i));
                small.AssertBoundedPose();
            }
            Assert.IsTrue(
                (Vector3.Normalize(Offset(large.Terrain, large.Position))
                    - Vector3.Normalize(Offset(small.Terrain, small.Position))).Length() < 0.0001f);
            Assert.IsTrue(MathF.Abs(Dot(large.Orientation, small.Orientation)) > 0.999999f);

            Quaternion stopped = large.Orientation;
            Vector2 pointer = new Vector2(0, -sign * 3200);
            for (int i = 0; i < 20; i++)
            {
                large.Move(pointer, begin: true);
                pointer.y -= sign;
                large.Move(pointer);
                large.AssertBoundedPose();
                Assert.IsTrue(MathF.Abs(Dot(stopped, large.Orientation)) > 0.999999f);
            }
            pointer.x += heading;
            large.Move(pointer);
            large.AssertBoundedPose();
            Assert.IsTrue(MathF.Abs(Dot(stopped, large.Orientation)) < 0.99999f);
            Vector3 headed = Vector3.Normalize(Offset(large.Terrain, large.Position));
            pointer.y += sign;
            large.Move(pointer);
            large.AssertBoundedPose();
            Assert.IsTrue(
                (headed - Vector3.Normalize(Offset(large.Terrain, large.Position))).Length() > 0.001f);
        }

        [DataTestMethod]
        [DataRow(1.0f, false)]
        [DataRow(-1.0f, false)]
        [DataRow(1.0f, true)]
        [DataRow(-1.0f, true)]
        public void PointerGesture_SmallHeadingStepsAtStop_DoNotFreezeOrInvert(
            float direction, bool centered)
        {
            var world = new World(centered: centered);
            world.Gesture.SetPitchLimits(-89.0f, 89.0f);
            world.Move(Zero2, begin: true);
            var pointer = new Vector2(0.0f, -direction * 3200.0f);
            world.Move(pointer);
            world.AssertBoundedPose();
            Vector3 stoppedDirection = Vector3.Normalize(Offset(world.Terrain, world.Position));
            Quaternion stoppedOrientation = world.Orientation;

            for (int i = 0; i < 1440; i++)
            {
                pointer += new Vector2(1.0f, -direction);
                int previousApplications = world.Applications;
                world.Move(pointer);
                Assert.AreEqual(
                    previousApplications + 1,
                    world.Applications,
                    $"Heading stopped at step {i}.");
                world.AssertBoundedPose();
            }

            Quaternion expectedHeading =
                CreateFromAxisAngle(UnitY, MathF.PI);
            Assert.IsTrue(
                (Transform(stoppedDirection, expectedHeading)
                    - Vector3.Normalize(Offset(world.Terrain, world.Position))).Length() < 0.00003f);
            Assert.IsTrue(MathF.Abs(Dot(
                Multiply(expectedHeading, stoppedOrientation), world.Orientation)) > 0.999999f);

            Vec3D headedPosition = world.Position;
            pointer.y += direction;
            world.Move(pointer);
            world.AssertBoundedPose();
            Assert.IsTrue(Offset(headedPosition, world.Position).Length() > 0.001f);
        }

        [DataTestMethod]
        [DataRow(89.0f, 90.0f)]
        [DataRow(89.0f, -90.0f)]
        [DataRow(-89.0f, 90.0f)]
        [DataRow(-89.0f, -90.0f)]
        public void BoundedPointerGesture_QuarterRolledBoundary_OnlyUprightDirectionMayLeave(
            float pitch, float roll)
        {
            var inward = CreateQuarterRolledWorld(pitch, roll);
            var outward = CreateQuarterRolledWorld(pitch, roll);
            float pointerDirection = MathF.Sign(pitch) * 8.0f;

            inward.Move(Zero2, begin: true);
            inward.Move(new Vector2(0.0f, pointerDirection));
            inward.AssertBoundedPose();

            Vec3D position = outward.Position;
            Quaternion orientation = outward.Orientation;
            outward.Move(Zero2, begin: true);
            outward.Move(new Vector2(0.0f, -pointerDirection));

            double movement = Math.Sqrt(
                Math.Pow(position.x - outward.Position.x, 2)
                + Math.Pow(position.y - outward.Position.y, 2)
                + Math.Pow(position.z - outward.Position.z, 2));
            Assert.IsTrue(movement == 0.0, $"Rejected boundary movement was {movement} metres.");
            Assert.IsTrue(MathF.Abs(Dot(orientation, outward.Orientation)) > 0.999999f);
        }

        [DataTestMethod]
        [DataRow(false, 1f)]
        [DataRow(false, -1f)]
        [DataRow(true, 1f)]
        [DataRow(true, -1f)]
        public void OmittedBound_ContinuesThroughItsPoleWithoutLosingFraming(bool geocentric, float sign)
        {
            var world = new World(geocentric);
            world.Gesture.SetPitchLimits(sign > 0 ? -89 : null, sign < 0 ? 89 : null);
            world.Move(Zero2, begin: true);
            world.Move(new Vector2(0, -sign * 1280));
            Assert.AreEqual(1, world.Applications);
            Assert.IsTrue(
                Vector3.Dot(Transform(UnitY, world.Orientation), world.UpAt(world.Position)) < 0);
            world.AssertFraming();
        }

        [DataTestMethod]
        [DataRow("miss")]
        [DataRow("near-zero")]
        [DataRow("orientation")]
        [DataRow("frame")]
        public void FailedPointerBegin_RemainsInactiveWithoutFallbackUntilNewBegin(string failure)
        {
            var world = new World();
            Vec3D terrain = world.Terrain;
            Quaternion orientation = world.Orientation;
            world.TerrainAvailable = failure != "miss";
            world.FrameAvailable = failure != "frame";
            if (failure == "near-zero") world.Terrain = Add(world.Position, UnitX * 0.000005f);
            if (failure == "orientation") world.Orientation = default;
            world.Move(Zero2, begin: true);
            world.Move(new Vector2(8, -8));
            world.TerrainAvailable = world.FrameAvailable = true;
            world.Terrain = terrain;
            world.Orientation = orientation;
            world.Move(new Vector2(16, -16));
            Assert.AreEqual(0, world.Applications);
            Assert.AreEqual(0f, world.FallbackDistance);
            world.Move(new Vector2(16, -16), begin: true);
            world.Move(new Vector2(24, -24));
            Assert.AreEqual(1, world.Applications);
            world.AssertFraming();
        }

        [TestMethod]
        public void ResetDuringPreparation_DiscardsStalePoseBeforeApplication()
        {
            var world = new World();
            world.Move(Zero2, begin: true);
            var position = world.Position;
            var orientation = world.Orientation;
            world.BeforePreparation = world.Gesture.Reset;
            world.Move(new Vector2(8, -8));
            Assert.AreEqual(0, world.Applications);
            Assert.AreEqual(position, world.Position);
            Assert.AreEqual(orientation, world.Orientation);
            world.BeforePreparation = null;
            world.TerrainAvailable = false;
            world.Move(Zero2, begin: true);
            world.Move(new Vector2(8, -8));
            Assert.AreEqual(0, world.Applications);
        }

        [DataTestMethod]
        [DataRow(15f)]
        [DataRow(30f)]
        [DataRow(37.5f)]
        [DataRow(45f)]
        [DataRow(60f)]
        public void StationaryPointer_RetainsPositionRotationAndCapturedFraming(float elevation)
        {
            var world = new World(centered: true);
            world.Orientation = CreateFromAxisAngle(UnitX, elevation * MathF.PI / 180)
                * CreateFromAxisAngle(UnitZ, 20 * MathF.PI / 180);
            world.Terrain = Add(world.Position, Transform(world.Ray, world.Orientation) * 1000);
            var before = world.Orientation;
            var position = world.Position;
            world.Move(Zero2, begin: true);
            for (int i = 0; i < 5; i++)
            {
                world.Move(Zero2);
                Assert.AreEqual(position, world.Position);
                Assert.AreEqual(before, world.Orientation);
                Assert.AreEqual(0, world.Applications);
                world.AssertFraming();
            }

            world.Move(new Vector2(1, 0));
            Assert.AreEqual(1, world.Applications, "Idle input must not end the acquired gesture.");
            world.AssertFraming();
        }

        [TestMethod]
        public void ExternallyInvertedPose_CanRecoverWithoutSnappingAtBegin()
        {
            var world = new World();
            world.Gesture.SetPitchLimits(-89, 89);
            world.Orientation = CreateFromAxisAngle(UnitZ, MathF.PI);
            world.Terrain = Add(world.Position, Transform(world.Ray, world.Orientation) * 1000);
            var position = world.Position;
            var orientation = world.Orientation;
            world.Move(Zero2, begin: true);
            Assert.AreEqual(position, world.Position);
            Assert.AreEqual(orientation, world.Orientation);
            world.Move(Zero2);
            Assert.IsTrue(MathF.Abs(Dot(orientation, world.Orientation)) > 0.999999f);
            world.Move(new Vector2(0, -8));
            float alignment = Vector3.Dot(Transform(UnitY, world.Orientation), UnitY);
            Assert.IsTrue(alignment > -1 && alignment < -0.99f);
            world.AssertFraming();
        }

        [TestMethod]
        public void Defaults_AcquireViewportCenterAndLimitElevationToOneThrough89Degrees()
        {
            var world = new World(pointerMode: false);
            world.Move(new Vector2(123, 456), begin: true);
            Assert.AreEqual(new Vector2(400, 300), world.LastPick);
            Assert.AreEqual(1, world.Applications);
            world.Move(new Vector2(123, 2056));
            float elevation = MathF.Asin(-Vector3.Normalize(Offset(world.Terrain, world.Position)).y)
                * 180 / MathF.PI;
            Assert.AreEqual(1f, elevation, 0.001f);
            world.Move(new Vector2(123, -3200));
            elevation = MathF.Asin(-Vector3.Normalize(Offset(world.Terrain, world.Position)).y)
                * 180 / MathF.PI;
            Assert.AreEqual(89f, elevation, 0.001f);
        }

        [TestMethod]
        public void InvalidSettings_ThrowWithoutEndingOrChangingActiveGesture()
        {
            var world = new World();
            var control = new World();
            world.Move(Zero2, begin: true);
            control.Move(Zero2, begin: true);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => world.Gesture.SetPivotMode((OrbitPivotMode)99));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => world.Gesture.SetPitchLimits(float.NaN, 89));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => world.Gesture.SetPitchLimits(-89, float.PositiveInfinity));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => world.Gesture.SetPitchLimits(-90, 89));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => world.Gesture.SetPitchLimits(-89, 90));
            Assert.ThrowsException<ArgumentException>(() => world.Gesture.SetPitchLimits(2, 1));
            world.Move(new Vector2(8, -8));
            control.Move(new Vector2(8, -8));
            Assert.AreEqual(1, world.Applications);
            Assert.AreEqual(control.Position, world.Position);
            Assert.AreEqual(control.Orientation, world.Orientation);
        }

        [TestMethod]
        public void Reset_EndsCenterGesture_ButNewBeginCanAcquireForwardFallback()
        {
            var world = new World(pointerMode: false);
            world.Move(Zero2, begin: true);
            world.Move(new Vector2(8, -8));
            var position = world.Position;
            var orientation = world.Orientation;
            world.Gesture.Reset();
            world.TerrainAvailable = false;
            world.Move(new Vector2(16, -16));
            Assert.AreEqual(2, world.Applications);
            Assert.AreEqual(position, world.Position);
            Assert.AreEqual(orientation, world.Orientation);
            world.Move(new Vector2(16, -16), begin: true);
            Assert.AreEqual(500f, world.FallbackDistance);
            world.Move(new Vector2(24, -24));
            Assert.AreEqual(4, world.Applications);
            Assert.AreNotEqual(position, world.Position);
        }

        [TestMethod]
        public void CenterPivotBelowUnityNormalizationThreshold_RetainsIdentityFacingFallback()
        {
            var world = new World(pointerMode: false);
            world.Terrain = Add(world.Position, UnitX * 0.000005f);
            world.Move(Zero2, begin: true);
            Assert.AreEqual(1, world.Applications);
            Assert.AreEqual(Identity, world.Orientation);
            Assert.AreEqual(0f, world.FallbackDistance);
        }

        [TestMethod]
        public void UnboundedCenterOrbit_ParallelToMapUp_AppliesPoseAndContinuesThroughPole()
        {
            double latitude = -55 * Math.PI / 180;
            double longitude = -135 * Math.PI / 180;
            var up = new Vector3(
                (float)(Math.Cos(latitude) * Math.Cos(longitude)),
                (float)(Math.Cos(latitude) * Math.Sin(longitude)),
                (float)-Math.Sin(latitude));
            var north = new Vector3(
                (float)(-Math.Sin(latitude) * Math.Cos(longitude)),
                (float)(-Math.Sin(latitude) * Math.Sin(longitude)),
                (float)-Math.Cos(latitude));
            var world = new World(pointerMode: false)
            {
                FixedFrame = new OrbitGesture.Frame
                {
                    East = new Vector3((float)-Math.Sin(longitude), (float)Math.Cos(longitude), 0),
                    North = north,
                    Up = up
                }
            };
            world.Terrain = Add(world.Position, -(north * MathF.Cos(MathF.PI / 6) + up * 0.5f) * 1000);
            world.Gesture.SetPitchLimits(null, null);
            world.Move(Zero2, begin: true);
            world.Move(new Vector2(0, -480));

            Assert.AreEqual(2, world.Applications, "The pole-facing pose must not be discarded.");
            Assert.IsTrue(float.IsFinite(Dot(world.Orientation, world.Orientation)));
            Assert.IsTrue((-up - Transform(UnitZ, world.Orientation)).Length() < 0.00001f);

            var polePosition = world.Position;
            world.Move(new Vector2(0, -488));
            Assert.AreEqual(3, world.Applications);
            Assert.AreNotEqual(polePosition, world.Position);
            Assert.IsTrue(
                (Vector3.Normalize(Offset(world.Terrain, world.Position))
                    - Transform(UnitZ, world.Orientation)).Length() < 0.00001f);
        }

        [TestMethod]
        public void FreePointerOrbit_CrossesPoleWithoutImplicitPitchOrUprightPolicy()
        {
            var world = new World(centered: true, constrained: false);
            world.Move(Zero2, begin: true);
            world.Move(new Vector2(0, -800));

            Assert.AreEqual(1, world.Applications);
            Assert.IsTrue(Vector3.Dot(Transform(UnitY, world.Orientation), UnitY) < 0);
            world.AssertFraming();
        }

        [TestMethod]
        public void FreeCenterReference_DoesNotFacePivotOrLevelRollOnAcquisition()
        {
            var world = new World(pointerMode: false, constrained: false);
            world.Orientation = Multiply(
                world.Orientation, CreateFromAxisAngle(UnitZ, MathF.PI / 4));
            Quaternion accepted = world.Orientation;
            Vec3D position = world.Position;

            world.Move(new Vector2(123, 456), begin: true);

            Assert.AreEqual(0, world.Applications);
            Assert.AreEqual(position, world.Position);
            Assert.AreEqual(accepted, world.Orientation);
        }

        private static World CreateQuarterRolledWorld(float pitch, float roll)
        {
            var world = new World();
            world.Gesture.SetPitchLimits(-89.0f, 89.0f);
            world.Orientation = Multiply(
                CreateFromAxisAngle(UnitX, pitch * MathF.PI / 180.0f),
                CreateFromAxisAngle(UnitZ, roll * MathF.PI / 180.0f));
            world.Terrain = Add(
                world.Position,
                Transform(world.Ray, world.Orientation) * 1000.0f);
            return world;
        }

    }
}
