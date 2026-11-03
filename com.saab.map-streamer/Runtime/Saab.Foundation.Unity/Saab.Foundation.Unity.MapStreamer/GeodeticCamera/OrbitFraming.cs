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
// File         : OrbitFraming.cs
// Module       :
// Description  : Transports orbit framing and manages roll around a surface point.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who  Date    Description
//
// DSA  260901  Created file
//
//******************************************************************************

using System;
using static Saab.Foundation.Unity.MapStreamer.GeodeticCamera.GizmoCameraMath;
using Quaternion = GizmoSDK.GizmoBase.Quaternion;
using Vec3D = GizmoSDK.GizmoBase.Vec3D;
using Vector3 = GizmoSDK.GizmoBase.Vec3;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    internal sealed class OrbitFraming
    {
        private const float MinimumLengthSquared = 1e-12f;
        private const float SuppressedLevelingUpAlignment = 0.7071068f;
        private const float FullLevelingUpAlignment = 0.5f;

        private Vector3 _pivotDirectionInCameraSpace;
        private bool _hasContinuousFraming;
        private Vector3 _previousPivotDirection;
        private Quaternion _continuousOrientation;
        private int _revision;

        /// <summary>
        /// Resolves a proposed Unity-world camera displacement to map-global position
        /// and map up at that position. Queries must not move the camera or framing state.
        /// </summary>
        internal delegate bool PositionResolver(
            Vector3 displacement, out Vec3D globalPosition, out Vector3 upDirection);

        /// <summary>Captures a pivot ray in camera space and the orientation to transport during the gesture.</summary>
        /// <param name="pivotDirection">Camera-to-pivot direction in world coordinates.</param>
        /// <param name="cameraOrientation">Camera-to-world rotation, including any roll.</param>
        /// <returns>False for a zero or near-zero direction or rotation; no gesture remains active.</returns>
        /// <remarks>Inputs must be finite. Every call replaces the previous gesture, including failed attempts.</remarks>
        internal bool TryBegin(
            Vector3 pivotDirection,
            Quaternion cameraOrientation)
        {
            _revision++;
            _hasContinuousFraming = false;
            _pivotDirectionInCameraSpace = default;
            _previousPivotDirection = default;
            _continuousOrientation = default;

            if (!TryNormalize(pivotDirection, out Vector3 normalizedPivotDirection)
                || !TryNormalize(cameraOrientation, out Quaternion normalizedCamera)
                || !TryNormalize(
                    Transform(
                        normalizedPivotDirection,
                        Inverse(normalizedCamera)),
                    out _pivotDirectionInCameraSpace))
            {
                return false;
            }

            _previousPivotDirection = normalizedPivotDirection;
            _continuousOrientation = normalizedCamera;
            _hasContinuousFraming = true;
            return true;
        }

        private bool TryCalculateUnboundedPointerStep(
            Vector3 upDirection,
            float headingDeltaRadians,
            float pitchDeltaRadians,
            OrbitPitchLimits pitchLimits,
            float maxLevelingRadians,
            out PointerStep step)
        {
            step = default;
            if (!_hasContinuousFraming
                || !TryNormalize(upDirection, out upDirection)
                || !IsFinite(maxLevelingRadians)
                || maxLevelingRadians < 0.0f
                || !OrbitMotion.TryCalculatePointerMotion(
                    -_previousPivotDirection,
                    upDirection,
                    Transform(UnitY, _continuousOrientation),
                    headingDeltaRadians,
                    pitchDeltaRadians,
                    pitchLimits,
                    out Vector3 orbitDirection,
                    out _,
                    out Quaternion motion))
            {
                return false;
            }

            Vector3 pivotDirection = -orbitDirection;
            Quaternion candidate = Multiply(motion, _continuousOrientation);
            if (!TryNormalize(candidate, out candidate)
                || !TryApplyLeveling(pivotDirection, upDirection, maxLevelingRadians, ref candidate)
                || Vector3.Dot(Transform(_pivotDirectionInCameraSpace, candidate), pivotDirection) < 0.9999f)
            {
                return false;
            }

            if (pitchLimits.HasBothBounds
                && Vector3.Dot(Transform(UnitY, _continuousOrientation), upDirection) > 0.0f
                && Vector3.Dot(Transform(UnitY, candidate), upDirection) <= 0.0f)
            {
                return false;
            }

            // Derive the accepted position ray from the captured ray and the
            // accepted orientation, so independent float updates cannot accumulate drift.
            if (!TryNormalize(Transform(_pivotDirectionInCameraSpace, candidate), out pivotDirection))
                return false;

            step = new PointerStep(this, _revision, pivotDirection, candidate);
            return true;
        }

        /// <summary>
        /// Proposes an orbit against the coordinate model at every candidate position.
        /// Both finite bounds stop pitch at its first unsafe candidate and let
        /// heading follow a locally reachable boundary without inverting the camera.
        /// A failed position/basis query rejects the whole proposal without advancing state.
        /// </summary>
        /// <param name="orbitPoint">Camera-relative Unity-world offset to the captured pivot.</param>
        /// <param name="resolvePosition">Side-effect-free position and local-up query, including zero displacement.</param>
        /// <param name="headingDeltaRadians">Signed heading travel around the starting map up.</param>
        /// <param name="pitchDeltaRadians">Signed travel along the screen-up orbit tangent.</param>
        /// <param name="pitchLimits">Both bounds enable candidate-frame upright protection.</param>
        /// <param name="maxLevelingRadians">The unchanged per-step horizon correction budget.</param>
        /// <param name="step">Complete, uncommitted map-global position and orientation, or default on failure.</param>
        internal bool TryCalculatePointerStep(
            Vector3 orbitPoint,
            PositionResolver resolvePosition,
            float headingDeltaRadians,
            float pitchDeltaRadians,
            OrbitPitchLimits pitchLimits,
            float maxLevelingRadians,
            out PointerStep step)
        {
            step = default;
            int revision = _revision;
            if (!_hasContinuousFraming || resolvePosition == null
                || !TryNormalize(orbitPoint, out Vector3 pivotDirection)
                || Vector3.Dot(pivotDirection, _previousPivotDirection) < 0.9999f
                || !IsFinite(headingDeltaRadians) || !IsFinite(pitchDeltaRadians)
                || !IsFinite(maxLevelingRadians) || maxLevelingRadians < 0.0f
                || !resolvePosition(Zero3, out Vec3D position, out Vector3 up)
                || !IsFinite(position) || !TryNormalize(up, out up))
            {
                return false;
            }

            float distance = orbitPoint.Length();
            var candidate = new PointerPose(_previousPivotDirection, _continuousOrientation, position, up);
            if (!pitchLimits.HasBothBounds)
            {
                // Omitted bounds explicitly retain the library's original pole-crossing policy.
                if (!TryCalculateUnboundedPointerStep(
                        up, headingDeltaRadians, pitchDeltaRadians, pitchLimits,
                        maxLevelingRadians, out PointerStep unrestricted)
                    || !TryResolvePointerPose(
                        unrestricted.Orientation, orbitPoint, distance, resolvePosition, out candidate))
                    return false;
            }
            else
            {
                if (!TryCalculatePointerHeading(
                        candidate, headingDeltaRadians, orbitPoint, distance,
                        resolvePosition, pitchLimits, out candidate))
                    return false;

                if (pitchDeltaRadians != 0.0f)
                {
                    Vector3 cameraUp = Transform(UnitY, candidate.Orientation);
                    if (!TryNormalize(Vector3.Cross(-candidate.Direction, cameraUp), out Vector3 pitchAxis)
                        || !TryClipPointerArc(
                            candidate, pitchAxis, pitchDeltaRadians, orbitPoint, distance,
                            resolvePosition, pitchLimits, out candidate))
                        return false;
                }

                Quaternion leveled = candidate.Orientation;
                if (!TryApplyLeveling(candidate.Direction, candidate.Up, maxLevelingRadians, ref leveled))
                    return false;

                if (!leveled.Equals(candidate.Orientation))
                {
                    if (!TryResolvePointerPose(
                            leveled, orbitPoint, distance, resolvePosition, out PointerPose final)
                        || !IsPointerPoseAllowed(final, candidate, pitchLimits))
                        return false;

                    candidate = final;
                }
            }

            if (_revision != revision)
                return false;

            step = new PointerStep(
                this, revision, candidate.Direction, candidate.Orientation, candidate.Position);
            return true;
        }

        private bool TryCalculatePointerHeading(
            PointerPose start, float angle, Vector3 orbitPoint, float distance,
            PositionResolver resolvePosition, OrbitPitchLimits limits, out PointerPose accepted)
        {
            accepted = start;
            if (angle == 0.0f)
                return true;

            // Whole turns carry no additional pointer-heading intent. Still scan
            // a complete turn before its remainder so a safe endpoint cannot hide
            // a boundary crossing.
            double travel = Math.Abs((double)angle);
            if (travel > 2.0 * Math.PI)
                travel = 2.0 * Math.PI + travel % (2.0 * Math.PI);
            int intervals = Math.Max(1, (int)Math.Ceiling(travel / (Math.PI / 180.0)));
            float increment = (float)(travel / intervals) * (angle < 0.0f ? -1.0f : 1.0f);
            for (int i = 0; i < intervals; i++)
            {
                if (!TryResolvePointerPose(
                        Multiply(CreateFromAxisAngle(accepted.Up, increment), accepted.Orientation),
                        orbitPoint, distance, resolvePosition, out PointerPose next))
                    return false;

                if (!IsPointerPoseAllowed(next, accepted, limits))
                {
                    if (!TryFollowPointerBoundary(
                            accepted, next, Math.Abs(increment), orbitPoint, distance,
                            resolvePosition, limits, out bool followed, out PointerPose corrected))
                        return false;

                    if (!followed)
                        return TryClipPointerArc(
                            accepted, accepted.Up, increment, orbitPoint, distance,
                            resolvePosition, limits, out accepted);
                    next = corrected;
                }
                else if (!next.Up.Equals(accepted.Up))
                {
                    if (!TryClipPointerArc(
                            accepted, accepted.Up, increment, orbitPoint, distance,
                            resolvePosition, limits, out PointerPose swept))
                        return false;
                    if (!swept.Orientation.Equals(next.Orientation))
                    {
                        accepted = swept;
                        return true;
                    }
                }
                accepted = next;
            }
            return true;
        }

        private bool TryFollowPointerBoundary(
            PointerPose previous, PointerPose headed, float maximumCorrection,
            Vector3 orbitPoint, float distance, PositionResolver resolvePosition,
            OrbitPitchLimits limits, out bool followed, out PointerPose accepted)
        {
            followed = false;
            accepted = previous;
            Vector3 cameraUp = Transform(UnitY, headed.Orientation);
            if (!TryNormalize(Vector3.Cross(-headed.Direction, cameraUp), out Vector3 axis))
                return true;

            // Curvature can make pure heading leave an active boundary. Follow
            // that boundary with the smallest screen-pitch correction, not a roll
            // reset or horizon snap. A local correction cannot exceed the heading
            // travel; otherwise stop instead of jumping to a remote safe pose.
            float bestCorrection = maximumCorrection;
            for (float sign = -1.0f; sign <= 1.0f; sign += 2.0f)
            {
                if (!TryResolvePointerPose(
                        Multiply(CreateFromAxisAngle(axis, sign * bestCorrection), headed.Orientation),
                        orbitPoint, distance, resolvePosition, out PointerPose corrected))
                    return false;
                if (!IsPointerPoseAllowed(corrected, previous, limits))
                    continue;

                float low = 0.0f;
                float high = bestCorrection;
                for (int iteration = 0; iteration < 24 && high - low > 1e-7f; iteration++)
                {
                    float middle = low + (high - low) * 0.5f;
                    if (!TryResolvePointerPose(
                            Multiply(CreateFromAxisAngle(axis, sign * middle), headed.Orientation),
                            orbitPoint, distance, resolvePosition, out PointerPose trial))
                        return false;
                    if (IsPointerPoseAllowed(trial, previous, limits))
                    {
                        high = middle;
                        corrected = trial;
                    }
                    else
                        low = middle;
                }

                bestCorrection = high;
                accepted = corrected;
                followed = true;
            }
            return true;
        }

        private bool TryClipPointerArc(
            PointerPose start, Vector3 axis, float angle,
            Vector3 orbitPoint, float distance, PositionResolver resolvePosition,
            OrbitPitchLimits limits, out PointerPose accepted)
        {
            accepted = start;
            if (angle == 0.0f)
                return true;

            // Position and up may be nonlinear in the active CRS. Scan the actual
            // rotation path before bisecting its first unsafe interval, not just
            // its endpoint (which may be safe again after crossing a pole).
            // One full revolution covers this fixed-axis path even for huge input.
            double travel = Math.Min(Math.Abs((double)angle), 2.0 * Math.PI);
            int intervals = Math.Max(1, (int)Math.Ceiling(travel / (Math.PI / 180.0)));
            float sign = angle < 0.0f ? -1.0f : 1.0f;
            float previousAngle = 0.0f;
            for (int i = 1; i <= intervals; i++)
            {
                float nextAngle = (float)(travel * i / intervals);
                if (!TryResolvePointerPose(
                        Multiply(CreateFromAxisAngle(axis, sign * nextAngle), start.Orientation),
                        orbitPoint, distance, resolvePosition, out PointerPose next))
                    return false;

                float frameTravel = nextAngle - previousAngle + (accepted.Up - next.Up).Length();
                float clearance = GetPointerPoseClearance(next, accepted, limits);
                if (clearance >= 0.0f
                    && Math.Max(clearance, GetPointerPoseClearance(accepted, accepted, limits))
                        <= 0.5f * frameTravel * frameTravel + 1e-6f)
                {
                    if (!TryFindPointerArcMinimum(
                            start, axis, sign, previousAngle, nextAngle, accepted,
                            orbitPoint, distance, resolvePosition, limits,
                            out float minimumAngle, out PointerPose minimum))
                        return false;
                    if (!IsPointerPoseAllowed(minimum, accepted, limits))
                    {
                        nextAngle = minimumAngle;
                        next = minimum;
                    }
                }

                if (!IsPointerPoseAllowed(next, accepted, limits))
                {
                    float low = previousAngle;
                    float high = nextAngle;
                    for (int iteration = 0; iteration < 24 && high - low > 1e-7f; iteration++)
                    {
                        float middle = low + (high - low) * 0.5f;
                        if (middle == low || middle == high)
                            break;

                        if (!TryResolvePointerPose(
                                Multiply(CreateFromAxisAngle(axis, sign * middle), start.Orientation),
                                orbitPoint, distance, resolvePosition, out next))
                            return false;

                        if (IsPointerPoseAllowed(next, accepted, limits))
                        {
                            low = middle;
                            accepted = next;
                        }
                        else
                        {
                            high = middle;
                        }
                    }
                    return true;
                }

                accepted = next;
                previousAngle = nextAngle;
            }

            if (Math.Abs((double)angle) > travel)
            {
                if (!TryResolvePointerPose(
                        Multiply(CreateFromAxisAngle(axis, angle), start.Orientation),
                        orbitPoint, distance, resolvePosition, out PointerPose final)
                    || !IsPointerPoseAllowed(final, accepted, limits))
                    return false;
                accepted = final;
            }
            return true;
        }

        private bool TryFindPointerArcMinimum(
            PointerPose start, Vector3 axis, float sign, float low, float high, PointerPose reference,
            Vector3 orbitPoint, float distance, PositionResolver resolvePosition, OrbitPitchLimits limits,
            out float minimumAngle, out PointerPose minimum)
        {
            minimumAngle = low;
            minimum = reference;
            // Endpoint sampling alone misses a narrow forbidden interval near
            // tangency. Refine the clearance minimum on near-boundary intervals
            // using the queried CRS, rather than assuming its up is constant.
            const float ratio = 0.618034f;
            float left = high - (high - low) * ratio;
            float right = low + (high - low) * ratio;
            if (!Resolve(left, out PointerPose leftPose) || !Resolve(right, out PointerPose rightPose))
                return false;
            for (int i = 0; i < 24 && high - low > 1e-7f; i++)
            {
                if (GetPointerPoseClearance(leftPose, reference, limits)
                    <= GetPointerPoseClearance(rightPose, reference, limits))
                {
                    high = right;
                    right = left;
                    rightPose = leftPose;
                    left = high - (high - low) * ratio;
                    if (!Resolve(left, out leftPose))
                        return false;
                }
                else
                {
                    low = left;
                    left = right;
                    leftPose = rightPose;
                    right = low + (high - low) * ratio;
                    if (!Resolve(right, out rightPose))
                        return false;
                }
            }
            bool useLeft = GetPointerPoseClearance(leftPose, reference, limits)
                <= GetPointerPoseClearance(rightPose, reference, limits);
            minimumAngle = useLeft ? left : right;
            minimum = useLeft ? leftPose : rightPose;
            return true;

            bool Resolve(float angle, out PointerPose pose)
            {
                return TryResolvePointerPose(
                    Multiply(CreateFromAxisAngle(axis, sign * angle), start.Orientation),
                    orbitPoint, distance, resolvePosition, out pose);
            }
        }

        private bool TryResolvePointerPose(
            Quaternion orientation, Vector3 orbitPoint, float distance,
            PositionResolver resolvePosition, out PointerPose pose)
        {
            pose = default;
            if (!TryNormalize(orientation, out orientation)
                || !TryNormalize(
                    Transform(_pivotDirectionInCameraSpace, orientation), out Vector3 direction)
                || !resolvePosition(
                    orbitPoint - direction * distance, out Vec3D position, out Vector3 up)
                || !IsFinite(position) || !TryNormalize(up, out up))
                return false;

            pose = new PointerPose(direction, orientation, position, up);
            return true;
        }

        private static bool IsPointerPoseAllowed(
            PointerPose candidate, PointerPose previous, OrbitPitchLimits limits)
        {
            return GetPointerPoseClearance(candidate, previous, limits) >= 0.0f;
        }

        private static float GetPointerPoseClearance(
            PointerPose candidate, PointerPose previous, OrbitPitchLimits limits)
        {
            const float minimumUprightAlignment = 1e-5f;
            float minimumAlignment = Math.Min(minimumUprightAlignment, previous.UprightDot);
            float minimumElevation = (float)Math.Sin(limits.ClampRadians(-(float)Math.PI / 2.0f));
            float maximumElevation = (float)Math.Sin(limits.ClampRadians((float)Math.PI / 2.0f));
            return Math.Min(candidate.UprightDot - minimumAlignment, Math.Min(
                candidate.ElevationDot - Math.Min(minimumElevation, previous.ElevationDot),
                Math.Max(maximumElevation, previous.ElevationDot) - candidate.ElevationDot));
        }

        private readonly struct PointerPose
        {
            internal readonly Vector3 Direction;
            internal readonly Quaternion Orientation;
            internal readonly Vec3D Position;
            internal readonly Vector3 Up;
            internal float ElevationDot => -Vector3.Dot(Direction, Up);
            internal float UprightDot => Vector3.Dot(Transform(UnitY, Orientation), Up);

            internal PointerPose(Vector3 direction, Quaternion orientation, Vec3D position, Vector3 up)
            {
                Direction = direction;
                Orientation = orientation;
                Position = position;
                Up = up;
            }
        }

        /// <summary>A validated pose proposal; committing a stale or already committed proposal fails.</summary>
        internal readonly struct PointerStep
        {
            private readonly OrbitFraming _owner;
            private readonly int _revision;
            internal Vector3 PivotDirection { get; }
            internal Quaternion Orientation { get; }
            internal Vec3D GlobalPosition { get; }

            internal PointerStep(
                OrbitFraming owner, int revision, Vector3 pivotDirection,
                Quaternion orientation, Vec3D globalPosition = default)
            {
                _owner = owner;
                _revision = revision;
                PivotDirection = pivotDirection;
                Orientation = orientation;
                GlobalPosition = globalPosition;
            }

            internal bool TryCommit()
            {
                if (_owner == null || !_owner._hasContinuousFraming || _owner._revision != _revision)
                    return false;

                _owner._previousPivotDirection = PivotDirection;
                _owner._continuousOrientation = Orientation;
                _owner._revision++;
                return true;
            }
        }

        private bool TryApplyLeveling(
            Vector3 pivotDirection,
            Vector3 upDirection,
            float maxLevelingRadians,
            ref Quaternion orientation)
        {
            if (!TryNormalize(
                    Transform(
                        upDirection,
                        Inverse(orientation)),
                    out Vector3 currentCameraSpaceUp))
            {
                return false;
            }

            float upAlignment = Math.Abs(Vector3.Dot(pivotDirection, upDirection));
            if (maxLevelingRadians <= 0.0f
                || upAlignment >= SuppressedLevelingUpAlignment
                || !TryResolveCameraSpaceUp(
                    _pivotDirectionInCameraSpace,
                    Vector3.Dot(pivotDirection, upDirection),
                    currentCameraSpaceUp,
                    out Vector3 targetCameraSpaceUp))
            {
                return true;
            }

            if (upAlignment > FullLevelingUpAlignment)
            {
                float levelingBlend =
                    (SuppressedLevelingUpAlignment - upAlignment)
                    / (SuppressedLevelingUpAlignment - FullLevelingUpAlignment);
                levelingBlend =
                    levelingBlend * levelingBlend * (3.0f - 2.0f * levelingBlend);
                maxLevelingRadians *= levelingBlend;
            }

            Vector3 currentProjectedUp =
                currentCameraSpaceUp
                - Vector3.Dot(currentCameraSpaceUp, _pivotDirectionInCameraSpace)
                * _pivotDirectionInCameraSpace;
            Vector3 targetProjectedUp =
                targetCameraSpaceUp
                - Vector3.Dot(targetCameraSpaceUp, _pivotDirectionInCameraSpace)
                * _pivotDirectionInCameraSpace;
            if (!TryNormalize(currentProjectedUp, out currentProjectedUp)
                || !TryNormalize(targetProjectedUp, out targetProjectedUp))
            {
                return true;
            }

            float levelingAngle = (float)Math.Atan2(
                Vector3.Dot(
                    _pivotDirectionInCameraSpace,
                    Vector3.Cross(currentProjectedUp, targetProjectedUp)),
                Vector3.Dot(currentProjectedUp, targetProjectedUp));
            float appliedAngle = Math.Max(
                -maxLevelingRadians,
                Math.Min(maxLevelingRadians, levelingAngle));
            if (Math.Abs(appliedAngle) <= 1e-6f)
                return true;

            Quaternion worldCorrection = CreateFromAxisAngle(
                pivotDirection,
                -appliedAngle);
            // Rotation around the world pivot direction changes only internal
            // roll; the pointer ray remains fixed in camera space.
            Quaternion candidate = Multiply(worldCorrection, orientation);
            if (!TryNormalize(candidate, out orientation))
                return false;

            return true;
        }

        /// <summary>Discards the captured gesture. Repeated calls are safe; advancing requires a new TryBegin.</summary>
        internal void Clear()
        {
            _revision++;
            _pivotDirectionInCameraSpace = default;
            _previousPivotDirection = default;
            _continuousOrientation = default;
            _hasContinuousFraming = false;
        }

        private bool TryResolveCameraSpaceUp(
            Vector3 cameraSpacePivotDirection,
            float pivotUpDot,
            Vector3 currentCameraSpaceUp,
            out Vector3 cameraSpaceUp)
        {
            cameraSpaceUp = default;

            float projectionCoefficient = cameraSpacePivotDirection.y;
            float depthCoefficient = cameraSpacePivotDirection.z;
            float coefficientLengthSquared =
                projectionCoefficient * projectionCoefficient
                + depthCoefficient * depthCoefficient;
            if (coefficientLengthSquared <= MinimumLengthSquared)
                return false;

            float coefficientLength = (float)Math.Sqrt(coefficientLengthSquared);
            if (Math.Abs(pivotUpDot) > coefficientLength + 0.00001f)
                return false;

            float reachableDot = Math.Max(
                -coefficientLength,
                Math.Min(coefficientLength, pivotUpDot));
            float baseProjection =
                projectionCoefficient * reachableDot / coefficientLengthSquared;
            float baseDepth =
                depthCoefficient * reachableDot / coefficientLengthSquared;
            float solutionScale = (float)Math.Sqrt(
                Math.Max(
                    0.0f,
                    1.0f - reachableDot * reachableDot / coefficientLengthSquared));
            float perpendicularProjection = -depthCoefficient / coefficientLength;
            float perpendicularDepth = projectionCoefficient / coefficientLength;

            Vector3 first = CreateCameraSpaceUp(
                baseProjection + solutionScale * perpendicularProjection,
                baseDepth + solutionScale * perpendicularDepth);
            Vector3 second = CreateCameraSpaceUp(
                baseProjection - solutionScale * perpendicularProjection,
                baseDepth - solutionScale * perpendicularDepth);

            bool firstValid =
                TryNormalize(first, out first) && IsValidCameraSpaceUp(first);
            bool secondValid =
                TryNormalize(second, out second) && IsValidCameraSpaceUp(second);
            if (!firstValid && !secondValid)
                return false;

            cameraSpaceUp = firstValid
                && (!secondValid
                    || Vector3.Dot(first, currentCameraSpaceUp)
                    >= Vector3.Dot(second, currentCameraSpaceUp))
                ? first
                : second;
            return true;
        }

        private Vector3 CreateCameraSpaceUp(float projectionLength, float depth)
        {
            return new Vector3(
                0.0f,
                projectionLength,
                depth);
        }

        private bool IsValidCameraSpaceUp(Vector3 cameraSpaceUp)
        {
            return cameraSpaceUp.x * cameraSpaceUp.x
                       + cameraSpaceUp.y * cameraSpaceUp.y > MinimumLengthSquared
                   && cameraSpaceUp.y > 0.0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vec3D value)
        {
            return !double.IsNaN(value.x) && !double.IsInfinity(value.x)
                && !double.IsNaN(value.y) && !double.IsInfinity(value.y)
                && !double.IsNaN(value.z) && !double.IsInfinity(value.z);
        }
    }
}
