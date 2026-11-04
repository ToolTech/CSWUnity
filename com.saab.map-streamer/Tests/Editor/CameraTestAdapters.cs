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
using System.Collections.Generic;
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

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Saab.Application.Unity.MapEditor.Tests.EditMode")]

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
    // Deterministic coordinate Adapter; inherited pose and notification lifecycles
    // are production code. Native conversion is not claimed.
    public sealed class NavigationProbeCamera : ConstrainedGeodeticCamera
    {
        internal bool Available = true;
        internal Vec3D OrbitPivot;
        internal int Applications;
        internal Action BeforePreparation;
        internal readonly List<string> Trace = new();

        private protected override bool TryGetZoomReference(float x, float y, out Vec3D reference)
        {
            reference = default;
            return Available;
        }

        private protected override NavigationQueryResult QueryOrbitTerrain(
            OrbitPivotMode mode, Vec2 screenPosition, out Vec3D position)
        {
            position = mode == OrbitPivotMode.PointerSurface
                ? new Vec3D(OrbitPivot.x, OrbitPivot.y, -OrbitPivot.z)
                : new Vec3D(0, -100, 100);
            return Available ? NavigationQueryResult.Hit : NavigationQueryResult.Invalid;
        }

        private protected override bool ReadOrbitFrame(out OrbitGesture.Frame frame)
        {
            OrbitTestCoordinates.ReadFrame(this, out frame);
            return Available;
        }

        private protected override bool TryResolvePointerPosition(
            GizmoVector3 displacement, out Vec3D globalPosition, out GizmoVector3 upDirection)
        {
            globalPosition = OrbitTestCoordinates.Resolve(this, displacement);
            upDirection = new GizmoVector3(0, 1, 0);
            return Available;
        }

        private protected override bool PrepareOrbitPose(
            Vec3D? resolvedPosition, GizmoVector3 displacement, GizmoQuaternion rotation, out Action apply)
        {
            return PreparePose(
                resolvedPosition ?? OrbitTestCoordinates.Resolve(this, displacement), rotation, out apply);
        }

        protected override bool PreparePose(Vec3D position, GizmoQuaternion orientation, out Action apply)
        {
            BeforePreparation?.Invoke();
            apply = null;
            if (!Available)
                return false;

            apply = () =>
            {
                GlobalPosition = position;
                SetOrientation(new UnityQuaternion(orientation.x, orientation.y, orientation.z, orientation.w));
                Applications++;
                Trace.Add("commit");
            };
            return true;
        }

        protected override void OnPoseAccepted()
        {
            base.OnPoseAccepted();
            Trace.Add("pose-invalidated");
        }

        protected override void OnMapInvalidated()
        {
            base.OnMapInvalidated();
            Trace.Add("map-invalidated");
        }
    }

    public sealed class BarePoseProbeCamera : FreeGeodeticCamera
    {
        internal Vec3D OrbitPivot;

        private protected override bool TryGetZoomReference(float x, float y, out Vec3D reference)
        {
            reference = default;
            return true;
        }

        private protected override NavigationQueryResult QueryOrbitTerrain(
            OrbitPivotMode mode, Vec2 screenPosition, out Vec3D position)
        {
            position = mode == OrbitPivotMode.PointerSurface
                ? new Vec3D(OrbitPivot.x, OrbitPivot.y, -OrbitPivot.z)
                : new Vec3D(0, -100, 100);
            return NavigationQueryResult.Hit;
        }

        private protected override bool ReadOrbitFrame(out OrbitGesture.Frame frame)
        {
            OrbitTestCoordinates.ReadFrame(this, out frame);
            return true;
        }

        private protected override bool TryResolvePointerPosition(
            GizmoVector3 displacement, out Vec3D globalPosition, out GizmoVector3 upDirection)
        {
            globalPosition = OrbitTestCoordinates.Resolve(this, displacement);
            upDirection = new GizmoVector3(0, 1, 0);
            return true;
        }

        private protected override bool PrepareOrbitPose(
            Vec3D? resolvedPosition, GizmoVector3 displacement, GizmoQuaternion rotation, out Action apply)
        {
            return PreparePose(
                resolvedPosition ?? OrbitTestCoordinates.Resolve(this, displacement), rotation, out apply);
        }

        protected override bool PreparePose(Vec3D position, GizmoQuaternion orientation, out Action apply)
        {
            apply = () =>
            {
                GlobalPosition = position;
                SetOrientation(new UnityQuaternion(orientation.x, orientation.y, orientation.z, orientation.w));
            };
            return true;
        }
    }

    // Native-coordinate boundary only; movement, policy and transactional framing
    // are production code. No native conversion or Unity lifecycle pass is claimed.
    internal static class OrbitTestCoordinates
    {
        internal static double Distance(Vec3D first, Vec3D second)
        {
            Vec3D delta = first - second;
            return Math.Sqrt(delta.x * delta.x + delta.y * delta.y + delta.z * delta.z);
        }

        internal static Vector3 CameraRay(FreeGeodeticCamera camera, Vec3D pivot)
        {
            Vec3D offset = pivot - camera.GlobalPosition;
            return UnityQuaternion.Inverse(camera.Orientation)
                * new Vector3((float)offset.x, (float)offset.y, (float)-offset.z).normalized;
        }

        internal static Vec3D Resolve(FreeGeodeticCamera camera, GizmoVector3 displacement)
        {
            Vec3D position = camera.GlobalPosition;
            return new Vec3D(
                position.x + displacement.x, position.y + displacement.y, position.z - displacement.z);
        }

        internal static void ReadFrame(FreeGeodeticCamera camera, out OrbitGesture.Frame frame)
        {
            Vec3D position = camera.GlobalPosition;
            UnityQuaternion rotation = camera.Orientation;
            frame = new OrbitGesture.Frame
            {
                WorldPosition = new Vec3D(position.x, position.y, -position.z),
                Orientation = new GizmoQuaternion(rotation.w, rotation.x, rotation.y, rotation.z),
                East = new GizmoVector3(1, 0, 0),
                North = new GizmoVector3(0, 0, 1),
                Up = new GizmoVector3(0, 1, 0)
            };
        }

        internal static Vec3D Pivot(FreeGeodeticCamera camera, Vector3 ray, float distance)
        {
            Vector3 offset = camera.Orientation * ray * distance;
            return Resolve(camera, new GizmoVector3(offset.x, offset.y, offset.z));
        }

        internal static void AssertFraming(FreeGeodeticCamera camera, Vec3D pivot, Vector3 capturedRay)
        {
            Assert.That((CameraRay(camera, pivot) - capturedRay).magnitude, Is.LessThan(1e-5f));
        }
    }
}
