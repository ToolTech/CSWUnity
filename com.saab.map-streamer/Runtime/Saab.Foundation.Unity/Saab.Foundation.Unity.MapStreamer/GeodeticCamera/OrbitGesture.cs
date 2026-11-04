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
// DSA  261020  Removed idle orbit movement and simplified gesture handling
//
//******************************************************************************

using System;
using GizmoSDK.GizmoBase;
using Vector2 = GizmoSDK.GizmoBase.Vec2;
using Vector3 = GizmoSDK.GizmoBase.Vec3;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    internal sealed class OrbitGesture
    {
        private readonly FrameQuery _readFrame;
        private readonly TerrainQuery _queryTerrain;
        private readonly ForwardPointQuery _pointAlongView;
        private readonly OrbitFraming.PositionResolver _resolvePosition;
        private readonly Func<Vector3, Vector3, Quaternion> _facePivot;
        private readonly PosePreparation _preparePose;
        private readonly bool _faceCenter;
        private readonly OrbitPivot _pivot = new OrbitPivot();
        private readonly OrbitFraming _framing = new OrbitFraming();
        private OrbitPivotMode _mode = OrbitPivotMode.ViewportCenter;
        private OrbitPitchLimits _limits;
        private bool _active;
        private Vector3 _orbitPoint;
        private int _revision;

        internal struct Frame
        {
            internal Vec3D WorldPosition;
            internal Quaternion Orientation;
            internal Vector3 East, North, Up;
        }

        internal delegate bool FrameQuery(out Frame frame);
        /// <summary>
        /// Queries terrain only; invalid results end the gesture.
        /// </summary>
        /// <remarks>
        /// Pointer positions and Frame.WorldPosition use double-precision Unity-world axes;
        /// center positions are camera-relative.
        /// </remarks>
        internal delegate NavigationQueryResult TerrainQuery(OrbitPivotMode mode, Vector2 screenPosition, out Vec3D position);
        /// <summary>
        /// Resolves the center fallback without moving the camera; failure ends the gesture.
        /// </summary>
        internal delegate bool ForwardPointQuery(float distance, out Vector3 point);
        /// <summary>
        /// Prepares a pose without moving; success supplies a synchronous, non-failing commit.
        /// </summary>
        /// <remarks>
        /// Resolved map-global poses need their destination ENU basis; center moves keep the current basis.
        /// Invoke the commit only after the gesture accepts the proposal.
        /// </remarks>
        internal delegate bool PosePreparation(
            Vec3D? resolvedPosition, Vector3 displacement, Quaternion orientation, out Action apply);

        internal OrbitGesture(
            FrameQuery readFrame,
            TerrainQuery queryTerrain,
            ForwardPointQuery pointAlongView,
            OrbitFraming.PositionResolver resolvePosition,
            Func<Vector3, Vector3, Quaternion> facePivot,
            PosePreparation preparePose,
            bool faceCenter = false)
        {
            _readFrame = readFrame;
            _queryTerrain = queryTerrain;
            _pointAlongView = pointAlongView;
            _resolvePosition = resolvePosition;
            _facePivot = facePivot;
            _preparePose = preparePose;
            _faceCenter = faceCenter;
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
        /// Pointer acquisition does not move the camera; center mode may turn to face the pivot.
        /// </summary>
        internal bool Begin(Vector2 pointer, int viewportWidth, int viewportHeight)
        {
            if (!IsFinite(pointer) || !_readFrame(out Frame frame))
            {
                End();
                return false;
            }

            if (!TryBegin(pointer, frame, viewportWidth, viewportHeight))
                return false;

            return _mode != OrbitPivotMode.ViewportCenter || !_faceCenter
                || MoveAroundCenter(frame, 0, 0);
        }

        internal bool Rotate(float headingRadians, float pitchRadians)
        {
            if (!_active)
                return false;

            if (!GizmoCameraMath.IsFinite(headingRadians) || !GizmoCameraMath.IsFinite(pitchRadians)
                || !_readFrame(out Frame frame))
            {
                End();
                return false;
            }

            if (headingRadians == 0 && pitchRadians == 0)
                return true;

            return _mode == OrbitPivotMode.ViewportCenter && _faceCenter
                ? MoveAroundCenter(frame, headingRadians, pitchRadians)
                : MoveAroundPointer(headingRadians, pitchRadians);
        }

        private bool TryBegin(Vector2 pointer, Frame frame, int viewportWidth, int viewportHeight)
        {
            OrbitPivot.ResolveScreenPosition(
                _mode, pointer.x, pointer.y, viewportWidth, viewportHeight, out float x, out float y);
            NavigationQueryResult result = _queryTerrain(_mode, new Vec2(x, y), out Vec3D position);
            if (result == NavigationQueryResult.Invalid)
            {
                End();
                return false;
            }
            bool resolved = result == NavigationQueryResult.Hit;
            Vec3D offset = position;
            _active = _mode == OrbitPivotMode.ViewportCenter
                || _pivot.TryAcquireOffset(resolved, position, frame.WorldPosition, out offset);
            if (!_active)
                return false;

            _orbitPoint = new Vec3((float)offset.x, (float)offset.y, (float)offset.z);
            if (_mode == OrbitPivotMode.ViewportCenter
                && (!resolved || _orbitPoint.LengthSq2() < 8 * float.Epsilon)
                && (!_pointAlongView(500.0f, out _orbitPoint) || !IsUsablePoint(_orbitPoint)))
            {
                End();
                return false;
            }
            if (_mode == OrbitPivotMode.PointerSurface || !_faceCenter)
                _active = _framing.TryBegin(NormalizeGestureVector(_orbitPoint), frame.Orientation);

            _revision++;
            return _active;
        }

        private bool MoveAroundCenter(Frame frame, float headingDelta, float pitchDelta)
        {
            int revision = _revision;
            Vec3 direction = NormalizeGestureVector(new Vec3(
                Vec3.Dot(frame.East, -_orbitPoint),
                Vec3.Dot(frame.North, -_orbitPoint),
                Vec3.Dot(frame.Up, -_orbitPoint)));
            float eastDirection = direction.x;
            float northDirection = direction.y;
            float yaw = (float)Math.Atan2(eastDirection, northDirection) + headingDelta;
            if (yaw > 2 * (float)Math.PI)
                yaw -= 2 * (float)Math.PI;

            if (yaw < 0)
                yaw += 2 * (float)Math.PI;

            float pitch = _limits.ClampRadians((float)Math.Asin(direction.z) + pitchDelta);
            Vec3 orbitVector = (frame.Up * (float)Math.Sin(pitch)
                + (frame.North * (float)Math.Cos(yaw) + frame.East * (float)Math.Sin(yaw))
                * (float)Math.Cos(pitch)) * _orbitPoint.Length();
            Quaternion orientation = _facePivot(NormalizeGestureVector(-orbitVector), frame.Up);
            if (_preparePose(null, _orbitPoint + orbitVector, orientation, out Action apply)
                && revision == _revision)
            {
                apply();
                _orbitPoint = -orbitVector;
                return true;
            }

            return false;
        }

        private bool MoveAroundPointer(float headingDelta, float pitchDelta)
        {
            if (_framing.TryCalculatePointerStep(
                    _orbitPoint, _resolvePosition,
                    headingDelta, pitchDelta,
                    _limits, out var step)
                && _preparePose(step.GlobalPosition, default, step.Orientation, out Action apply)
                && step.TryCommit())
            {
                apply();
                _orbitPoint = step.PivotDirection * _orbitPoint.Length();
                return true;
            }

            return false;
        }

        private void End()
        {
            _active = false;
            _framing.Clear();
            _revision++;
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
