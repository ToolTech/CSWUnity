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
using Saab.Foundation.Map;
using static Saab.Foundation.Unity.MapStreamer.GeodeticCamera.GizmoCameraMath;
using Vector3 = GizmoSDK.GizmoBase.Vec3;
using Quaternion = GizmoSDK.GizmoBase.Quaternion;

namespace UnityEngine
{
    public static class Screen
    {
        public static int width => 800;
        public static int height => 600;
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float sqrMagnitude => x * x + y * y + z * z;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float scale) => new(a.x * scale, a.y * scale, a.z * scale);
    }
    public sealed class Transform
    {
        public Func<Vector3, Vector3> Project = value => value;
        public Vector3 forward = new(0, -0.6f, 0.8f);
        public Vector3 TransformPoint(Vector3 value) => Project(value);
    }
    public sealed class Camera
    {
        public Transform transform = new();
    }
}

namespace Saab.Utility.Unity.NodeUtils
{
    public static class NodeUtils
    {
        public static Func<IntPtr, UnityEngine.Transform?> FindTransform = _ => null;
        public static UnityEngine.Transform? FindFirstGameObjectTransform(IntPtr reference) => FindTransform(reference);
    }
}

namespace Saab.Unity.Extensions
{
    public static class OrbitNativeConversions
    {
        public static Vec3D ToVec3D(this UnityEngine.Vector3 value) => new(value.x, value.y, value.z);
    }
}

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    // Queries and failure guards are compiled from GeodeticCameraControl. Only frame/pose boundaries are substituted.
    public partial class GeodeticCameraControl
    {
        private MapPos _mapPos = null!;
        private readonly NavigationSurfaceQuery NavigationSurfaces = new(() => (800, 600));
        internal Vec3D Position = new(1000, 2000, 3000);
        internal Quaternion Orientation = Identity;
        internal UnityEngine.Camera Camera = new();
        internal int Preparations, Applications;
        private double X => Position.x;
        private double Y => Position.y;
        private double Z => Position.z;

        internal OrbitGesture CreateGesture(OrbitPivotMode mode = OrbitPivotMode.ViewportCenter)
        {
            var gesture = new OrbitGesture(ReadFrame, QueryOrbitTerrain, TryPointAlongView, Resolve,
                CreateLookRotation, Prepare, faceCenter: true);
            gesture.SetPivotMode(mode);
            return gesture;
        }

        private bool ReadFrame(out OrbitGesture.Frame frame)
        {
            frame = new OrbitGesture.Frame
            {
                WorldPosition = new Vec3D(X, Y, -Z), Orientation = Orientation,
                East = UnitX, North = UnitZ, Up = UnitY
            };
            return NavigationSurfaces.IsAvailable;
        }

        private bool Resolve(Vector3 offset, out Vec3D position, out Vector3 up)
        {
            position = new Vec3D(X + offset.x, Y + offset.y, Z - offset.z);
            up = UnitY;
            return true;
        }

        private bool Prepare(Vec3D? resolved, Vector3 offset, Quaternion orientation, out Action apply)
        {
            Preparations++;
            Resolve(offset, out Vec3D candidate, out _);
            apply = () => { Position = resolved ?? candidate; Orientation = orientation; Applications++; };
            return true;
        }
    }
}
