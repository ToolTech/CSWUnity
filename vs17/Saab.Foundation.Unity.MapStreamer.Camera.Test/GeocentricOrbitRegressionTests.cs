/*
 * Copyright (C) SAAB AB
 *
 * All rights, including the copyright, to the computer program(s)
 * herein belong to Saab AB. The program(s) may be used and/or
 * copied only with the written permission of Saab AB, or in
 * accordance with the terms and conditions stipulated in the
 * agreement/contract under which the program(s) have been supplied.
 *
 * Information Class: COMPANY RESTRICTED
 * Defence Secrecy: UNCLASSIFIED
 * Export Control: NOT EXPORT CONTROLLED
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Saab.Foundation.Unity.MapStreamer;
using Saab.Foundation.Unity.MapStreamer.GeodeticCamera;
using static Saab.Foundation.Unity.MapStreamer.GeodeticCamera.GizmoCameraMath;
using Quaternion = GizmoSDK.GizmoBase.Quaternion;
using Vec3D = GizmoSDK.GizmoBase.Vec3D;
using Vector3 = GizmoSDK.GizmoBase.Vec3;

namespace Saab.Foundation.Unity.MapStreamer.Camera.Test
{
    [TestClass]
    public sealed class GeocentricOrbitRegressionTests
    {
        private const float Radius = 1000000.0f;
        private static readonly OrbitPitchLimits Limits = new OrbitPitchLimits(-89.0f, 89.0f);
        private static readonly Vector3 Ray = Vector3.Normalize(new Vector3(0.4f, -0.2f, 1.0f));

        [DataTestMethod]
        [DataRow(1, "failure")]
        [DataRow(2, "failure")]
        [DataRow(30, "failure")]
        [DataRow(2, "zero-up")]
        [DataRow(2, "nan-up")]
        [DataRow(2, "nan-position")]
        public void FailedCandidateQuery_RejectsEntireProposalWithoutAdvancingState(int failAt, string failure)
        {
            var orbit = new OrbitSession();
            var control = new OrbitSession();
            Vec3D position = orbit.Position;
            Quaternion orientation = orbit.Orientation;
            int queries = 0;
            Assert.IsFalse(orbit.Framing.TryCalculatePointerStep(
                orbit.OrbitPoint, Resolve, 0.0f, Radians(-400.0f), Limits, out var rejected));
            Assert.IsFalse(rejected.TryCommit());
            Assert.AreEqual(failAt, queries);
            Assert.AreEqual(position, orbit.Position);
            Assert.AreEqual(orientation, orbit.Orientation);
            var continued = orbit.Advance(1.0f, 1.0f);
            var expected = control.Advance(1.0f, 1.0f);
            Assert.AreEqual(expected.Orientation, continued.Orientation);
            Assert.AreEqual(expected.GlobalPosition, continued.GlobalPosition);

            bool Resolve(Vector3 displacement, out Vec3D candidate, out Vector3 up)
            {
                orbit.Resolve(displacement, out candidate, out up);
                if (++queries != failAt)
                    return true;
                if (failure == "zero-up") up = Zero3;
                if (failure == "nan-up") up = new Vector3(float.NaN, 0.0f, 0.0f);
                if (failure == "nan-position") candidate = new Vec3D(double.NaN, 0.0, 0.0);
                return failure != "failure";
            }
        }

        [TestMethod]
        public void DiscardedGeocentricProposal_AndStaleRevision_CannotChangeAcceptedPose()
        {
            var orbit = new OrbitSession();
            var control = new OrbitSession();
            Assert.IsTrue(orbit.Framing.TryCalculatePointerStep(
                orbit.OrbitPoint, orbit.Resolve, Radians(30.0f), Radians(-400.0f), Limits, out var discarded));
            Assert.AreEqual(control.Position, orbit.Position);
            Assert.AreEqual(control.Orientation, orbit.Orientation);
            var accepted = orbit.Advance(-1.0f, 1.0f);
            var expected = control.Advance(-1.0f, 1.0f);
            Assert.AreEqual(expected.GlobalPosition, accepted.GlobalPosition);
            Assert.AreEqual(expected.Orientation, accepted.Orientation);
            Assert.IsFalse(discarded.TryCommit());
            Assert.IsFalse(accepted.TryCommit());

            Assert.IsFalse(orbit.Framing.TryCalculatePointerStep(
                orbit.OrbitPoint, RestartDuringQuery, 0.0f, Radians(1.0f), Limits, out var stale));
            Assert.IsFalse(stale.TryCommit());
            Assert.AreEqual(accepted.GlobalPosition, orbit.Position);
            Assert.AreEqual(accepted.Orientation, orbit.Orientation);

            bool RestartDuringQuery(Vector3 displacement, out Vec3D position, out Vector3 up)
            {
                Assert.IsTrue(orbit.Framing.TryBegin(orbit.OrbitPoint, orbit.Orientation));
                return orbit.Resolve(displacement, out position, out up);
            }
        }

        [TestMethod]
        public void ExternallyInvertedGeocentricPose_CanRecoverWithoutSnap()
        {
            var orbit = new OrbitSession();
            Quaternion initial = CreateFromAxisAngle(UnitZ, MathF.PI);
            orbit.Orientation = initial;
            orbit.OrbitPoint = Transform(Ray, initial) * Radius;
            Assert.IsTrue(orbit.Framing.TryBegin(orbit.OrbitPoint, initial));
            Assert.IsTrue(orbit.Framing.TryCalculatePointerStep(
                orbit.OrbitPoint, orbit.Resolve, 0.0f, 0.0f, Limits, out var unchanged));
            Assert.IsTrue(Math.Abs(Dot(initial, unchanged.Orientation)) > 0.999999f);
            Assert.AreEqual(orbit.Position, unchanged.GlobalPosition);
            Assert.IsTrue(orbit.Framing.TryCalculatePointerStep(
                orbit.OrbitPoint, orbit.Resolve, 0.0f, Radians(1.0f), Limits, out var recovered));
            float alignment = Vector3.Dot(
                Transform(UnitY, recovered.Orientation), Wgs84Up(recovered.GlobalPosition));
            Assert.IsTrue(alignment > -1.0f && alignment < -0.99f);
        }

        [DataTestMethod]
        [DataRow(0.0f, 90.0f, Radius)]
        [DataRow(60.0f, 40.0f, Radius)]
        [DataRow(-45.0f, -150.0f, 10000000.0f)]
        public void MixedInput_WithChangingGeocentricUp_PreservesEveryAcceptedPose(
            float latitude, float longitude, float radius)
        {
            var orbit = new OrbitSession(latitude, longitude, Ray, radius);
            var random = new Random(372896);
            for (int i = 0; i < 1000; i++)
            {
                if (i % 17 == 0)
                    Assert.IsTrue(orbit.Framing.TryBegin(orbit.OrbitPoint, orbit.Orientation));
                float heading = (float)(random.NextDouble() - 0.5) * (i % 11 == 0 ? 800.0f : 3.0f);
                float pitch = (float)(random.NextDouble() - 0.5) * (i % 13 == 0 ? 800.0f : 3.0f);
                orbit.Advance(heading, pitch);
            }
        }

        [TestMethod]
        public void NearlyTangentElevationCrossing_StopsBeforeReturningToSafeEndpoint()
        {
            float tilt = MathF.Asin(MathF.Cos(Radians(89.001f)) / MathF.Cos(Radians(30.0f)));
            Quaternion initial = Multiply(
                CreateFromAxisAngle(UnitX, Radians(30.0f)),
                CreateFromAxisAngle(UnitZ, tilt));
            Vector3 ray = Vector3.Normalize(new Vector3(0.0f, -0.2f, 1.0f));
            Vector3 pivot = Transform(ray, initial);
            var framing = new OrbitFraming();
            Assert.IsTrue(framing.TryBegin(pivot, initial));
            Assert.IsTrue(framing.TryCalculatePointerStep(
                pivot,
                (Vector3 _, out Vec3D position, out Vector3 up) =>
                {
                    position = default;
                    up = UnitY;
                    return true;
                },
                0.0f, Radians(400.0f), Limits, out var step));
            float elevation = MathF.Asin(-Vector3.Dot(step.PivotDirection, UnitY));
            Assert.IsTrue(elevation >= Radians(88.99f),
                $"Skipped the first narrow elevation boundary and stopped at {elevation * 180.0f / MathF.PI} degrees.");
        }

        private sealed class OrbitSession
        {
            internal readonly OrbitFraming Framing = new OrbitFraming();
            private readonly Vector3 _ray;
            internal Vec3D Position;
            internal Vector3 OrbitPoint;
            internal Quaternion Orientation;

            internal OrbitSession(
                float latitude = 0.0f, float longitude = 90.0f, Vector3? ray = null, float radius = Radius)
            {
                _ray = ray ?? Ray;
                Position = Wgs84Position(Radians(latitude), Radians(longitude), radius);
                Vector3 up = Wgs84Up(Position);
                Vector3 cross = Vector3.Cross(UnitY, up);
                Quaternion frame = Normalize(new Quaternion(
                    1.0f + Vector3.Dot(UnitY, up), cross.x, cross.y, cross.z));
                Orientation = Multiply(frame, CreateFromAxisAngle(UnitX, Radians(30.0f)));
                OrbitPoint = Transform(_ray, Orientation) * radius;
                Assert.IsTrue(Framing.TryBegin(OrbitPoint, Orientation));
            }

            internal bool Resolve(Vector3 displacement, out Vec3D position, out Vector3 up)
            {
                position = AddUnityOffset(Position, displacement);
                up = Wgs84Up(position);
                return true;
            }

            internal OrbitFraming.PointerStep Advance(float heading, float pitch)
            {
                Assert.IsTrue(Framing.TryCalculatePointerStep(
                    OrbitPoint, Resolve, Radians(heading), Radians(pitch), Limits, out var step),
                    $"Rejected valid input {heading}, {pitch}.");
                Assert.IsTrue(step.TryCommit());
                Position = step.GlobalPosition;
                OrbitPoint = step.PivotDirection * OrbitPoint.Length();
                Orientation = step.Orientation;
                float alignment = Vector3.Dot(Transform(UnitY, Orientation), Wgs84Up(Position));
                Assert.IsTrue(alignment > 0.0f, $"Inverted against actual map up: {alignment}.");
                float elevation = MathF.Asin(-Vector3.Dot(step.PivotDirection, Wgs84Up(Position)));
                Assert.IsTrue(MathF.Abs(elevation) <= Radians(89.001f));
                Assert.IsTrue(
                    (_ray - Transform(step.PivotDirection, Inverse(Orientation))).Length() < 0.00001f);
                return step;
            }
        }

        private static Vec3D Wgs84Position(double latitude, double longitude, float height)
        {
            double sinLatitude = Math.Sin(latitude);
            double n = 6378137.0 / Math.Sqrt(1.0 - 6.6943799901413165e-3 * sinLatitude * sinLatitude);
            return new Vec3D(
                (n + height) * Math.Cos(latitude) * Math.Cos(longitude),
                (n + height) * Math.Cos(latitude) * Math.Sin(longitude),
                (n * (1.0 - 6.6943799901413165e-3) + height) * sinLatitude);
        }

        private static Vec3D AddUnityOffset(Vec3D position, Vector3 offset)
        {
            return new Vec3D(position.x + offset.x, position.y + offset.y, position.z - offset.z);
        }

        private static Vector3 Wgs84Up(Vec3D position)
        {
            // Recover geodetic latitude, not geocentric latitude or a spherical normal.
            const double a = 6378137.0;
            const double e2 = 6.6943799901413165e-3;
            double horizontal = Math.Sqrt(position.x * position.x + position.y * position.y);
            double latitude = Math.Atan2(position.z, horizontal * (1.0 - e2));
            for (int i = 0; i < 12; i++)
            {
                double sinLatitude = Math.Sin(latitude);
                double n = a / Math.Sqrt(1.0 - e2 * sinLatitude * sinLatitude);
                latitude = Math.Atan2(position.z + e2 * n * sinLatitude, horizontal);
            }

            double longitude = Math.Atan2(position.y, position.x);
            return Vector3.Normalize(new Vector3(
                (float)(Math.Cos(latitude) * Math.Cos(longitude)),
                (float)(Math.Cos(latitude) * Math.Sin(longitude)),
                (float)-Math.Sin(latitude)));
        }

        private static float Radians(float degrees) => degrees * MathF.PI / 180.0f;
    }
}
