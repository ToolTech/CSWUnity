//******************************************************************************
//
// Copyright (C) SAAB AB
//
// All rights, including the copyright, to the computer program(s)
// herein belong to Saab AB. The program(s) may be used and/or
// copied only with the written permission of Saab AB, or in
// accordance with the terms and conditions stipulated in the
// agreement/contract under which the program(s) have been
// supplied.
//
//
// Information Class:  COMPANY UNCLASSIFIED
// Defence Secrecy:     NOT CLASSIFIED
// Export Control:      NOT EXPORT CONTROLLED
//
//
// File         : UnityCameraNavigationAdapter.cs
// Module       :
// Description  : Adapts Unity rays and rotations to camera-navigation values.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who      Date    Description
//
// DSA  260909  Created file
//
//******************************************************************************

using GizmoSDK.GizmoBase;
using UnityEngine;
using GizmoQuaternion = GizmoSDK.GizmoBase.Quaternion;
using GizmoVector3 = GizmoSDK.GizmoBase.Vec3;
using Quaternion = UnityEngine.Quaternion;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    internal static class UnityCameraNavigationAdapter
    {
        internal static OrbitGesture.Frame CreateOrbitFrame(
            Vec3D worldPosition,
            Quaternion orientation,
            Vector3 east,
            Vector3 north,
            Vector3 up)
        {
            return new OrbitGesture.Frame
            {
                WorldPosition = worldPosition,
                Orientation = new GizmoQuaternion(
                    orientation.w, orientation.x, orientation.y, orientation.z),
                East = ToGizmo(east),
                North = ToGizmo(north),
                Up = ToGizmo(up)
            };
        }

        internal static GizmoQuaternion FaceOrbitPivot(
            GizmoVector3 forward,
            GizmoVector3 up)
        {
            return GizmoCameraMath.CreateLookRotation(forward, up);
        }

        internal static bool TryIntersectGroundDragPlane(
            Ray ray,
            GizmoVector3 normal,
            GizmoVector3 point,
            out GizmoVector3 intersection,
            out float incidence)
        {
            Vector3 unityNormal = ToUnity(normal);
            incidence = -Vector3.Dot(ray.direction.normalized, unityNormal.normalized);
            var plane = new Plane(unityNormal, ToUnity(point));
            intersection = default;
            if (!plane.Raycast(ray, out float enter))
                return false;

            intersection = ToGizmo(ray.origin + ray.direction * enter);
            return true;
        }

        internal static bool IsValidOrientation(Quaternion orientation)
        {
            if (!IsFinite(orientation))
                return false;

            float magnitude = Quaternion.Dot(orientation, orientation);
            return IsFinite(magnitude) && magnitude > 0.0f;
        }

        private static bool IsFinite(Quaternion orientation)
        {
            return IsFinite(orientation.x)
                && IsFinite(orientation.y)
                && IsFinite(orientation.z)
                && IsFinite(orientation.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static GizmoVector3 ToGizmo(Vector3 value)
            => new GizmoVector3(value.x, value.y, value.z);

        private static Vector3 ToUnity(GizmoVector3 value)
            => new Vector3(value.x, value.y, value.z);
    }
}
