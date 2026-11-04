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
using Saab.Foundation.Unity.MapStreamer.GeodeticCamera;
using static Saab.Foundation.Unity.MapStreamer.GeodeticCamera.GizmoCameraMath;
using Quaternion = GizmoSDK.GizmoBase.Quaternion;
using Vec3D = GizmoSDK.GizmoBase.Vec3D;
using Vector2 = GizmoSDK.GizmoBase.Vec2;
using Vector3 = GizmoSDK.GizmoBase.Vec3;

namespace Saab.Foundation.Unity.MapStreamer.Camera.Test
{
    internal sealed class OrbitTestWorld
    {
        internal readonly OrbitGesture Gesture;
        internal readonly Vector3 Ray;
        private readonly bool _geocentric;
        private Vector2 _previousPointer;
        internal Vec3D Position = new Vec3D(1000, 2000, 3000);
        internal Quaternion Orientation = CreateFromAxisAngle(UnitX, MathF.PI / 6);
        internal Vec3D Terrain;
        internal Vector2 LastPick;
        internal int Applications;
        internal bool TerrainAvailable = true;
        internal bool FrameAvailable = true;
        internal float FallbackDistance;
        internal bool PreparationAllowed = true;
        internal Action? BeforePreparation { get; set; }
        internal int FailQueryAt { get; set; }
        internal OrbitGesture.Frame? FixedFrame { get; set; }
        private int _queries;

        internal OrbitTestWorld(
            bool geocentric = false, bool centered = false, bool pointerMode = true,
            bool constrained = true)
        {
            _geocentric = geocentric;
            Ray = centered ? UnitZ : Vector3.Normalize(new Vector3(0.4f, -0.2f, 1));
            if (geocentric)
            {
                Position = Wgs84Position(Math.PI / 3, Math.PI * 2 / 9, 1000000);
                Vector3 up = UpAt(Position);
                Vector3 cross = Vector3.Cross(UnitY, up);
                Quaternion frame = Normalize(new Quaternion(
                    1 + Vector3.Dot(UnitY, up), cross.x, cross.y, cross.z));
                Orientation = Multiply(frame, Orientation);
            }

            Terrain = Add(Position, Transform(Ray, Orientation) * (geocentric ? 1000000 : 1000));
            Gesture = new OrbitGesture(
                ReadFrame, Pick, PointAlongView, Resolve, FacePivot, Prepare,
                faceCenter: constrained);
            if (constrained)
                Gesture.SetPitchLimits(1, 89);

            if (pointerMode)
                Gesture.SetPivotMode(OrbitPivotMode.PointerSurface);
        }

        internal void Move(Vector2 pointer, bool begin = false)
        {
            Vector2 delta = pointer - _previousPointer;
            _previousPointer = pointer;
            if (begin)
            {
                Gesture.Begin(pointer, 800, 600);
                return;
            }

            const float radiansPerStep = 0.125f * (float)Math.PI / 180;
            Gesture.Rotate(delta.x * radiansPerStep, -delta.y * radiansPerStep);
        }

        internal void AssertFraming()
        {
            Vector3 ray = Transform(Vector3.Normalize(Offset(Terrain, Position)), Inverse(Orientation));
            float drift = (Ray - ray).Length();
            Assert.IsTrue(drift < 0.00001f, $"Captured ray drifted by {drift}.");
        }

        internal void AssertBoundedPose()
        {
            AssertFraming();
            Vector3 up = UpAt(Position);
            Assert.IsTrue(Vector3.Dot(Transform(UnitY, Orientation), up) > 0,
                "The applied pose must be upright against up at the applied position.");
            float elevation = -Vector3.Dot(Vector3.Normalize(Offset(Terrain, Position)), up);
            // Compare projections: asin magnifies float roundoff near either pole.
            Assert.IsTrue(MathF.Abs(elevation) <= MathF.Sin(89 * MathF.PI / 180) + 0.000001f,
                $"Applied elevation projection {elevation}, applications {Applications}.");
        }

        internal Vector3 UpAt(Vec3D position) => _geocentric ? Wgs84Up(position) : UnitY;

        private bool ReadFrame(out OrbitGesture.Frame frame)
        {
            Vector3 up = UpAt(Position);
            Vector3 east = Vector3.Normalize(Vector3.Cross(up, UnitZ));
            frame = FixedFrame ?? new OrbitGesture.Frame
            {
                East = east, North = Vector3.Cross(east, up), Up = up
            };
            frame.WorldPosition = new Vec3D(Position.x, Position.y, -Position.z);
            frame.Orientation = Orientation;
            return FrameAvailable;
        }

        private NavigationQueryResult Pick(OrbitPivotMode mode, Vector2 screenPosition, out Vec3D position)
        {
            LastPick = screenPosition;
            Vector3 local = Offset(Terrain, Position);
            position = mode == OrbitPivotMode.PointerSurface
                ? new Vec3D(Terrain.x, Terrain.y, -Terrain.z)
                : new Vec3D(local.x, local.y, local.z);
            return TerrainAvailable ? NavigationQueryResult.Hit : NavigationQueryResult.Miss;
        }

        private bool PointAlongView(float distance, out Vector3 point)
        {
            FallbackDistance = distance;
            point = Transform(UnitZ, Orientation) * distance;
            return true;
        }

        private bool Resolve(Vector3 displacement, out Vec3D position, out Vector3 up)
        {
            position = Add(Position, displacement);
            up = UpAt(position);
            return ++_queries != FailQueryAt;
        }

        private static Quaternion FacePivot(Vector3 forward, Vector3 up)
        {
            return CreateLookRotation(forward, up);
        }

        private bool Prepare(Vec3D? resolvedPosition, Vector3 displacement, Quaternion orientation, out Action apply)
        {
            BeforePreparation?.Invoke();
            var position = resolvedPosition ?? Add(Position, displacement);
            apply = () =>
            {
                Position = position;
                Orientation = orientation;
                Applications++;
            };
            return PreparationAllowed
                && double.IsFinite(position.x) && double.IsFinite(position.y) && double.IsFinite(position.z)
                && float.IsFinite(Dot(orientation, orientation)) && Dot(orientation, orientation) > 1e-12f;
        }

        internal static Vec3D Add(Vec3D position, Vector3 offset)
            => new Vec3D(position.x + offset.x, position.y + offset.y, position.z - offset.z);

        internal static Vector3 Offset(Vec3D point, Vec3D origin)
            => new Vector3((float)(point.x - origin.x), (float)(point.y - origin.y), (float)(origin.z - point.z));

        private static Vec3D Wgs84Position(double latitude, double longitude, double height)
        {
            double sinLatitude = Math.Sin(latitude);
            double n = 6378137.0 / Math.Sqrt(1 - 6.6943799901413165e-3 * sinLatitude * sinLatitude);
            return new Vec3D(
                (n + height) * Math.Cos(latitude) * Math.Cos(longitude),
                (n + height) * Math.Cos(latitude) * Math.Sin(longitude),
                (n * (1 - 6.6943799901413165e-3) + height) * sinLatitude);
        }

        private static Vector3 Wgs84Up(Vec3D position)
        {
            const double eccentricitySquared = 6.6943799901413165e-3;
            double horizontal = Math.Sqrt(position.x * position.x + position.y * position.y);
            double latitude = Math.Atan2(position.z, horizontal * (1 - eccentricitySquared));
            for (int i = 0; i < 12; i++)
            {
                double sinLatitude = Math.Sin(latitude);
                double n = 6378137.0 / Math.Sqrt(1 - eccentricitySquared * sinLatitude * sinLatitude);
                latitude = Math.Atan2(position.z + eccentricitySquared * n * sinLatitude, horizontal);
            }

            double longitude = Math.Atan2(position.y, position.x);
            return Vector3.Normalize(new Vector3(
                (float)(Math.Cos(latitude) * Math.Cos(longitude)),
                (float)(Math.Cos(latitude) * Math.Sin(longitude)),
                (float)-Math.Sin(latitude)));
        }
    }
}
