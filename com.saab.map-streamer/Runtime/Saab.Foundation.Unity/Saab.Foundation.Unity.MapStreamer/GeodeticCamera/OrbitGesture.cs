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
// File         : OrbitGesture.cs
// Module       :
// Description  : Owns orbit acquisition, input, framing and reset.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who  Date    Description
//
// DSA  260908  Created file
//
//******************************************************************************

using System;
using GizmoSDK.GizmoBase;
using Vector2 = GizmoSDK.GizmoBase.Vec2;
using Vector3 = GizmoSDK.GizmoBase.Vec3;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    /// <summary>Owns orbit acquisition, input and accepted framing for one camera.</summary>
    internal sealed class OrbitGesture
    {
        private const float DegreesPerPixel = 0.125f;
        private readonly FrameQuery _readFrame;
        private readonly TerrainQuery _queryTerrain;
        private readonly ForwardPointQuery _pointAlongView;
        private readonly OrbitFraming.PositionResolver _resolvePosition;
        private readonly Func<Vector3, Vector3, Quaternion> _facePivot;
        private readonly PosePreparation _preparePose;
        private readonly OrbitPivot _pivot = new OrbitPivot();
        private readonly OrbitFraming _framing = new OrbitFraming();
        private OrbitPivotMode _mode = OrbitPivotMode.ViewportCenter;
        private OrbitPitchLimits _limits = OrbitPitchLimits.Default;
        private bool _active;
        private Vector2 _previousPointer;
        private Vector3 _orbitPoint;

        internal struct Frame
        {
            internal Vec3D WorldPosition;
            internal Quaternion Orientation;
            internal Vector3 East, North, Up;
        }

        internal delegate bool FrameQuery(out Frame frame);
        /// <summary>
        /// Queries terrain only. Pointer positions and Frame.WorldPosition use
        /// double-precision Unity-world axes; center positions are camera-relative.
        /// Invalid results end the gesture instead of using a retained or forward pivot.
        /// </summary>
        internal delegate NavigationQueryResult TerrainQuery(OrbitPivotMode mode, Vector2 screenPosition, out Vec3D position);
        /// <summary>Resolves the legacy center fallback without moving the camera; failure ends the gesture.</summary>
        internal delegate bool ForwardPointQuery(float distance, out Vector3 point);
        /// <summary>
        /// Validates without moving the camera. A resolved map-global pointer pose
        /// requires its candidate ENU basis; center displacement retains the current
        /// basis. Success supplies a synchronous, non-failing application to invoke
        /// only after the gesture accepts the proposal.
        /// </summary>
        internal delegate bool PosePreparation(
            Vec3D? resolvedPosition, Vector3 displacement, Quaternion orientation, out Action apply);

        internal OrbitGesture(
            FrameQuery readFrame,
            TerrainQuery queryTerrain,
            ForwardPointQuery pointAlongView,
            OrbitFraming.PositionResolver resolvePosition,
            Func<Vector3, Vector3, Quaternion> facePivot,
            PosePreparation preparePose)
        {
            _readFrame = readFrame;
            _queryTerrain = queryTerrain;
            _pointAlongView = pointAlongView;
            _resolvePosition = resolvePosition;
            _facePivot = facePivot;
            _preparePose = preparePose;
        }

        internal void SetPivotMode(OrbitPivotMode mode)
        {
            OrbitPivot.ValidateMode(mode);
            _mode = mode;
            End();
        }

        internal void Reset()
        {
            End();
            _pivot.Clear();
        }

        internal void SetPitchLimits(float? minimumDegrees, float? maximumDegrees)
        {
            _limits = new OrbitPitchLimits(minimumDegrees, maximumDegrees);
        }

        /// <summary>
        /// Begins or continues from bottom-left-origin screen pixels. Pointer
        /// begin captures without motion; center begin faces its acquired pivot.
        /// Rejected proposals consume input, but invalid input or a missing frame
        /// ends the gesture. Delta time in seconds controls pointer roll leveling.
        /// </summary>
        internal void Move(Vector2 pointer, bool begin, int viewportWidth, int viewportHeight, float deltaTime)
        {
            if (!IsFinite(pointer))
            {
                End();
                return;
            }
            if (!begin && !_active)
                return;
            if (!_readFrame(out Frame frame))
            {
                End();
                return;
            }

            if (begin)
            {
                OrbitPivot.ResolveScreenPosition(
                    _mode, pointer.x, pointer.y, viewportWidth, viewportHeight, out float x, out float y);
                NavigationQueryResult result = _queryTerrain(_mode, new Vec2(x, y), out Vec3D position);
                if (result == NavigationQueryResult.Invalid)
                {
                    End();
                    return;
                }
                bool resolved = result == NavigationQueryResult.Hit;
                Vec3D offset = position;
                _active = _mode == OrbitPivotMode.ViewportCenter
                    || _pivot.TryAcquireOffset(resolved, position, frame.WorldPosition, out offset);
                if (!_active)
                    return;

                _orbitPoint = new Vec3((float)offset.x, (float)offset.y, (float)offset.z);
                if (_mode == OrbitPivotMode.ViewportCenter
                    && (!resolved || _orbitPoint.LengthSq2() < 8 * float.Epsilon))
                {
                    if (!_pointAlongView(500.0f, out _orbitPoint) || !IsUsablePoint(_orbitPoint))
                    {
                        End();
                        return;
                    }
                }
                _previousPointer = pointer;
                if (_mode == OrbitPivotMode.PointerSurface)
                {
                    _active = _framing.TryBegin(NormalizeGestureVector(_orbitPoint), frame.Orientation);
                    return;
                }
            }
            Vec2 delta = pointer - _previousPointer;
            _previousPointer = pointer;
            if (!IsFinite(delta))
            {
                End();
                return;
            }
            if (!begin && _mode == OrbitPivotMode.ViewportCenter && delta.x == 0.0f && delta.y == 0.0f)
                return;

            float headingDelta = delta.x * (float)Math.PI / 180 * DegreesPerPixel;
            float pitchDelta = -delta.y * (float)Math.PI / 180 * DegreesPerPixel;
            if (_mode == OrbitPivotMode.ViewportCenter)
            {
                Vec3 direction = NormalizeGestureVector(new Vec3(
                    Vec3.Dot(frame.East, -_orbitPoint),
                    Vec3.Dot(frame.North, -_orbitPoint),
                    Vec3.Dot(frame.Up, -_orbitPoint)));
                float yaw = (float)Math.Atan2(direction.x, direction.y) + headingDelta;
                if (yaw > 2 * (float)Math.PI) yaw -= 2 * (float)Math.PI;
                if (yaw < 0) yaw += 2 * (float)Math.PI;
                float pitch = _limits.ClampRadians((float)Math.Asin(direction.z) + pitchDelta);
                Vec3 orbitVector = (frame.Up * (float)Math.Sin(pitch)
                    + (frame.North * (float)Math.Cos(yaw) + frame.East * (float)Math.Sin(yaw))
                    * (float)Math.Cos(pitch)) * _orbitPoint.Length();
                Quaternion orientation = _facePivot(NormalizeGestureVector(-orbitVector), frame.Up);
                if (_preparePose(null, _orbitPoint + orbitVector, orientation, out Action apply))
                {
                    apply();
                    _orbitPoint = -orbitVector;
                }
            }
            else if (_framing.TryCalculatePointerStep(
                    _orbitPoint, _resolvePosition,
                    headingDelta, pitchDelta,
                    _limits, 2 * (float)Math.PI * deltaTime, out var step)
                && _preparePose(step.GlobalPosition, default, step.Orientation, out Action apply)
                && step.TryCommit())
            {
                apply();
                _orbitPoint = step.PivotDirection * _orbitPoint.Length();
            }
        }

        private void End()
        {
            _active = false;
            _framing.Clear();
        }

        private static bool IsFinite(Vec2 value)
            => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y);

        private static bool IsUsablePoint(Vec3 value)
        {
            float squaredLength = value.LengthSq2();
            return !float.IsNaN(squaredLength) && !float.IsInfinity(squaredLength) && squaredLength > 1e-12f;
        }

        private static Vec3 NormalizeGestureVector(Vec3 value)
        {
            // Retain Unity's normalization threshold at gesture acquisition.
            float length = value.Length();
            return length > 1e-5f ? value / length : default;
        }
    }
}
