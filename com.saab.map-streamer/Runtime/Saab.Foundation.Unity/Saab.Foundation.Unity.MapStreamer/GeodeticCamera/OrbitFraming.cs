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
// Description  : Keeps the orbit point fixed on screen and preserves camera alignment with the local horizon.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who  Date    Description
//
// DSA  260901  Created file
// DSA  261020  Fixed unwanted roll during orbit and pitch stalling near vertical; simplified constraints
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
        private const float HorizonAlignmentTolerance = 1e-6f;
        private const float ConstraintProbeAngle = 0.001f;
        private const float MaximumConstraintCorrection = 0.25f;
        private const int MaximumConstraintIterations = 16;
        private const int MaximumConstraintBacktracks = 12;

        private Vector3 _pivotDirectionInCameraSpace;
        private bool _hasContinuousFraming;
        private Vector3 _previousPivotDirection;
        private Quaternion _continuousOrientation;
        private float? _rightUpAlignment;
        private int _revision;

        /// <summary>
        /// Resolves camera-world displacement to map-global position and local up without changing state.
        /// </summary>
        internal delegate bool PositionResolver(
            Vector3 displacement, out Vec3D globalPosition, out Vector3 upDirection);

        /// <summary>
        /// Captures a pivot ray in camera space and the orientation to transport during the gesture.
        /// </summary>
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
            _rightUpAlignment = null;

            if (!TryNormalize(pivotDirection, out Vector3 normalizedPivotDirection)
                || !TryNormalize(cameraOrientation, out Quaternion normalizedCamera)
                || !TryNormalize(
                    Transform(
                        normalizedPivotDirection,
                        Inverse(normalizedCamera)),
                    out _pivotDirectionInCameraSpace))
                return false;

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
            out PointerStep step)
        {
            step = default;
            if (!_hasContinuousFraming
                || !TryNormalize(upDirection, out upDirection)
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
                return false;

            Vector3 pivotDirection = -orbitDirection;
            Quaternion candidate = Multiply(motion, _continuousOrientation);
            if (!TryNormalize(candidate, out candidate)
                || Vector3.Dot(Transform(_pivotDirectionInCameraSpace, candidate), pivotDirection) < 0.9999f)
                return false;

            // Derive the accepted position ray from the captured ray and the
            // accepted orientation, so independent float updates cannot accumulate drift.
            if (!TryNormalize(Transform(_pivotDirectionInCameraSpace, candidate), out pivotDirection))
                return false;

            step = new PointerStep(this, _revision, pivotDirection, candidate);
            return true;
        }

        /// <summary>
        /// Calculates an orbit proposal without advancing accepted state.
        /// </summary>
        /// <remarks>
        /// Checks local up at each candidate. With both bounds, pitch stops at the first
        /// unsafe point while heading can follow the boundary. Failed coordinate queries reject the whole proposal.
        /// </remarks>
        /// <param name="orbitPoint">Camera-relative Unity-world offset to the captured pivot.</param>
        /// <param name="resolvePosition">Side-effect-free position and local-up query, including zero displacement.</param>
        /// <param name="headingDeltaRadians">Signed heading travel around the starting map up.</param>
        /// <param name="pitchDeltaRadians">Signed camera-right pitch when bounded; screen-up orbit-tangent travel otherwise.</param>
        /// <param name="pitchLimits">Both bounds enable candidate-frame upright protection.</param>
        /// <param name="step">Complete, uncommitted map-global position and orientation, or default on failure.</param>
        internal bool TryCalculatePointerStep(
            Vector3 orbitPoint,
            PositionResolver resolvePosition,
            float headingDeltaRadians,
            float pitchDeltaRadians,
            OrbitPitchLimits pitchLimits,
            out PointerStep step)
        {
            step = default;
            int revision = _revision;
            if (!_hasContinuousFraming || resolvePosition == null
                || !TryNormalize(orbitPoint, out Vector3 pivotDirection)
                || Vector3.Dot(pivotDirection, _previousPivotDirection) < 0.9999f
                || !IsFinite(headingDeltaRadians) || !IsFinite(pitchDeltaRadians)
                || !resolvePosition(Zero3, out Vec3D position, out Vector3 up)
                || !IsFinite(position) || !TryNormalize(up, out up))
                return false;

            var context = new PointerContext(orbitPoint, resolvePosition, pitchLimits);
            float rightUp = _rightUpAlignment
                ?? Vector3.Dot(Transform(UnitX, _continuousOrientation), up);
            rightUp = Math.Max(-1.0f, Math.Min(1.0f, rightUp));
            if (Math.Abs(rightUp) <= HorizonAlignmentTolerance)
                rightUp = 0.0f;
            var candidate = new PointerPose(_previousPivotDirection, _continuousOrientation, position, up, rightUp);
            if (!pitchLimits.HasBothBounds)
            {
                // Omitted bounds explicitly retain the library's original pole-crossing policy.
                if (!TryCalculateUnboundedPointerStep(
                        up, headingDeltaRadians, pitchDeltaRadians, pitchLimits, out PointerStep unrestricted)
                    || !TryResolvePointerPose(
                        unrestricted.Orientation, context, out candidate))
                    return false;
            }
            else if (!TryCalculateBoundedPointerStep(
                    candidate, headingDeltaRadians, pitchDeltaRadians, context, out candidate))
                return false;

            if (_revision != revision)
                return false;

            step = new PointerStep(
                this, revision, candidate.Direction, candidate.Orientation, candidate.Position,
                pitchLimits.HasBothBounds ? candidate.RightUpAlignment : (float?)null);
            return true;
        }

        private bool TryCalculateBoundedPointerStep(
            PointerPose start, float heading, float pitch, in PointerContext context, out PointerPose accepted)
        {
            if (!TryCalculatePointerHeading(start, heading, context, out accepted))
                return false;

            if (Math.Abs(pitch) > 0.0f
                && (!TryNormalize(Transform(UnitX, accepted.Orientation), out Vector3 pitchAxis)
                    || !TryClipPointerArc(accepted, pitchAxis, pitch, context, out accepted)))
                return false;
            return true;
        }

        private bool TryCalculatePointerHeading(
            PointerPose start, float angle, in PointerContext context, out PointerPose accepted)
        {
            accepted = start;
            if (Math.Abs(angle) <= 0.0f)
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
                if (!TryResolveHeadingPose(
                        Multiply(CreateFromAxisAngle(accepted.Up, increment), accepted.Orientation), accepted.Up,
                        accepted.RightUpAlignment,
                        context, out PointerPose next))
                    return false;

                if (!IsPointerPoseAllowed(next, accepted, context.Limits))
                {
                    if (!TryFollowPointerBoundary(
                            accepted, next, Math.Abs(increment), context,
                            out bool followed, out PointerPose corrected))
                        return false;

                    if (!followed)
                        return TryClipPointerArc(
                            accepted, accepted.Up, increment, context, out accepted);
                    next = corrected;
                }
                accepted = next;
            }
            return true;
        }

        private bool TryFollowPointerBoundary(
            PointerPose previous, PointerPose headed, float maximumCorrection,
            in PointerContext context, out bool followed, out PointerPose accepted)
        {
            followed = false;
            accepted = previous;
            if (!TryNormalize(Transform(UnitX, headed.Orientation), out Vector3 axis))
                return true;

            // Curvature can make pure heading leave an active boundary. Follow
            // that boundary with the smallest screen-pitch correction, not a roll
            // reset or horizon snap. A local correction cannot exceed the heading
            // travel; otherwise stop instead of jumping to a remote safe pose.
            float bestCorrection = maximumCorrection;
            for (float sign = -1.0f; sign <= 1.0f; sign += 2.0f)
            {
                var arc = new PointerArc(headed, axis, sign, context);
                if (!TryResolvePointerArcPose(arc, bestCorrection, out PointerPose corrected))
                    return false;
                if (!IsPointerPoseAllowed(corrected, previous, context.Limits))
                    continue;

                if (!TryRefinePointerBoundary(
                        arc, previous, bestCorrection, corrected, out bestCorrection, out accepted))
                    return false;
                followed = true;
            }
            return true;
        }

        private bool TryRefinePointerBoundary(
            in PointerArc arc, PointerPose previous, float maximumCorrection, PointerPose endpoint,
            out float correction, out PointerPose accepted)
        {
            correction = maximumCorrection;
            accepted = endpoint;
            float low = 0.0f;
            float high = maximumCorrection;
            for (int iteration = 0; iteration < 24 && high - low > 1e-7f; iteration++)
            {
                float middle = low + (high - low) * 0.5f;
                if (!TryResolvePointerArcPose(arc, middle, out PointerPose trial))
                    return false;
                if (IsPointerPoseAllowed(trial, previous, arc.Context.Limits))
                {
                    high = middle;
                    accepted = trial;
                }
                else
                    low = middle;
            }
            correction = high;
            return true;
        }

        private bool TryClipPointerArc(
            PointerPose start, Vector3 axis, float angle,
            in PointerContext context, out PointerPose accepted)
        {
            accepted = start;
            if (Math.Abs(angle) <= 0.0f)
                return true;

            // Position and up may be nonlinear in the active CRS. Scan the actual
            // rotation path before bisecting its first unsafe interval, not just
            // its endpoint (which may be safe again after crossing a pole).
            // One full revolution covers this fixed-axis path even for huge input.
            double travel = Math.Min(Math.Abs((double)angle), 2.0 * Math.PI);
            int intervals = Math.Max(1, (int)Math.Ceiling(travel / (Math.PI / 180.0)));
            float sign = angle < 0.0f ? -1.0f : 1.0f;
            var arc = new PointerArc(start, axis, sign, context);
            float previousAngle = 0.0f;
            for (int i = 1; i <= intervals; i++)
            {
                float nextAngle = (float)(travel * i / intervals);
                if (!TryResolvePointerArcPose(arc, nextAngle, out PointerPose next)
                    || !TryRefinePointerArcInterval(
                        arc, previousAngle, nextAngle, accepted, next, out var sample))
                    return false;

                if (!IsPointerPoseAllowed(sample.Pose, accepted, context.Limits))
                    return TryClipPointerArcBoundary(arc, previousAngle, sample.Angle, accepted, out accepted);

                accepted = sample.Pose;
                previousAngle = sample.Angle;
            }

            if (Math.Abs((double)angle) > travel)
            {
                if (!TryResolveHorizonPose(
                        Multiply(CreateFromAxisAngle(axis, angle), start.Orientation), start.RightUpAlignment,
                        context, out PointerPose final)
                    || !IsPointerPoseAllowed(final, accepted, context.Limits))
                    return false;
                accepted = final;
            }
            return true;
        }

        private bool TryRefinePointerArcInterval(
            in PointerArc arc, float low, float high, PointerPose reference, PointerPose endpoint,
            out (float Angle, PointerPose Pose) sample)
        {
            sample = (high, endpoint);
            float frameTravel = high - low + (reference.Up - endpoint.Up).Length();
            double clearance = GetPointerPoseClearance(endpoint, reference, arc.Context.Limits);
            if (clearance >= 0.0f
                && Math.Max(clearance, GetPointerPoseClearance(reference, reference, arc.Context.Limits))
                    <= 0.5f * frameTravel * frameTravel + 1e-6f)
            {
                if (!TryFindPointerArcMinimum(
                        arc, low, high, reference, out float minimumAngle, out PointerPose minimum))
                    return false;
                if (!IsPointerPoseAllowed(minimum, reference, arc.Context.Limits))
                    sample = (minimumAngle, minimum);
            }
            return true;
        }

        private bool TryClipPointerArcBoundary(
            in PointerArc arc, float low, float high, PointerPose start, out PointerPose accepted)
        {
            accepted = start;
            for (int iteration = 0; iteration < 24 && high - low > 1e-7f; iteration++)
            {
                float middle = low + (high - low) * 0.5f;
                if (middle <= low || middle >= high)
                    break;

                if (!TryResolvePointerArcPose(arc, middle, out PointerPose next))
                    return false;

                if (IsPointerPoseAllowed(next, accepted, arc.Context.Limits))
                {
                    low = middle;
                    accepted = next;
                }
                else
                    high = middle;
            }
            return true;
        }

        private bool TryResolvePointerArcPose(in PointerArc arc, float angle, out PointerPose pose)
        {
            return TryResolveHorizonPose(
                Multiply(CreateFromAxisAngle(arc.Axis, arc.Sign * angle), arc.Start.Orientation),
                arc.Start.RightUpAlignment, arc.Context, out pose);
        }

        private bool TryFindPointerArcMinimum(
            in PointerArc arc, float low, float high, PointerPose reference,
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
            if (!TryResolvePointerArcPose(arc, left, out PointerPose leftPose)
                || !TryResolvePointerArcPose(arc, right, out PointerPose rightPose))
                return false;
            for (int i = 0; i < 24 && high - low > 1e-7f; i++)
            {
                if (GetPointerPoseClearance(leftPose, reference, arc.Context.Limits)
                    <= GetPointerPoseClearance(rightPose, reference, arc.Context.Limits))
                {
                    high = right;
                    right = left;
                    rightPose = leftPose;
                    left = high - (high - low) * ratio;
                    if (!TryResolvePointerArcPose(arc, left, out leftPose))
                        return false;
                }
                else
                {
                    low = left;
                    left = right;
                    leftPose = rightPose;
                    right = low + (high - low) * ratio;
                    if (!TryResolvePointerArcPose(arc, right, out rightPose))
                        return false;
                }
            }
            bool useLeft = GetPointerPoseClearance(leftPose, reference, arc.Context.Limits)
                <= GetPointerPoseClearance(rightPose, reference, arc.Context.Limits);
            minimumAngle = useLeft ? left : right;
            minimum = useLeft ? leftPose : rightPose;
            return true;
        }

        private bool TryResolveHeadingPose(
            Quaternion orientation, Vector3 sourceUp, float rightUp,
            in PointerContext context, out PointerPose pose)
        {
            pose = default;
            if (!TryNormalize(orientation, out orientation)
                || !TryNormalize(Transform(sourceUp, Inverse(orientation)), out Vector3 targetUp))
                return false;
            float verticalLength = (float)Math.Sqrt(targetUp.y * targetUp.y + targetUp.z * targetUp.z);
            float scale = verticalLength > 0.0f
                ? (float)Math.Sqrt(Math.Max(0.0f, 1.0f - rightUp * rightUp)) / verticalLength : 0.0f;
            targetUp = new Vector3(rightUp, targetUp.y * scale, targetUp.z * scale);
            if (!TryNormalize(Vector3.Cross(targetUp, UnitX), out Vector3 first)
                && !TryNormalize(Vector3.Cross(targetUp, UnitZ), out first))
                return false;
            Vector3 second = Vector3.Cross(targetUp, first);
            Quaternion candidate = orientation;
            // Heading retains local pitch and banking, so it follows an active
            // elevation boundary rather than crossing it and stopping there.
            for (int iteration = 0; iteration < MaximumConstraintIterations; iteration++)
            {
                if (!TryResolveHeadingError(candidate, targetUp, rightUp, context, out pose, out Vector3 error))
                    return false;
                if (error.LengthSq2() <= HorizonAlignmentTolerance * HorizonAlignmentTolerance)
                    return true;

                if (!TryCalculateHeadingCorrection(
                        pose, error, targetUp, first, second, context, out Vector3 correction)
                    || !TryImproveHeadingPose(pose, error, targetUp, correction, context, out candidate))
                    return false;
            }
            return false;
        }

        private bool TryCalculateHeadingCorrection(
            PointerPose pose, Vector3 error, Vector3 targetUp, Vector3 first, Vector3 second,
            in PointerContext context, out Vector3 correction)
        {
            correction = default;
            Vector3 firstAxis = Transform(first, pose.Orientation);
            Vector3 secondAxis = Transform(second, pose.Orientation);
            if (!TryResolveHeadingError(
                    Multiply(CreateFromAxisAngle(firstAxis, ConstraintProbeAngle), pose.Orientation),
                    targetUp, pose.RightUpAlignment, context, out _, out Vector3 firstError)
                || !TryResolveHeadingError(
                    Multiply(CreateFromAxisAngle(secondAxis, ConstraintProbeAngle), pose.Orientation),
                    targetUp, pose.RightUpAlignment, context, out _, out Vector3 secondError))
                return false;
            Vector3 firstGradient = (firstError - error) / ConstraintProbeAngle;
            Vector3 secondGradient = (secondError - error) / ConstraintProbeAngle;
            float aa = Vector3.Dot(firstGradient, first), ab = Vector3.Dot(secondGradient, first);
            float ba = Vector3.Dot(firstGradient, second), bb = Vector3.Dot(secondGradient, second);
            float determinant = aa * bb - ab * ba;
            if (!IsFinite(determinant) || Math.Abs(determinant) <= 1e-12f)
                return false;
            float a = Vector3.Dot(error, first), b = Vector3.Dot(error, second);
            correction = firstAxis * ((ab * b - bb * a) / determinant)
                + secondAxis * ((ba * a - aa * b) / determinant);
            return true;
        }

        private bool TryImproveHeadingPose(
            PointerPose pose, Vector3 error, Vector3 targetUp, Vector3 correction,
            in PointerContext context, out Quaternion candidate)
        {
            candidate = default;
            float length = correction.Length();
            if (!IsFinite(length) || length <= 0.0f)
                return false;
            Vector3 axis = correction / length;
            float angle = Math.Min(MaximumConstraintCorrection, length);
            for (int reduction = 0; reduction < MaximumConstraintBacktracks; reduction++, angle *= 0.5f)
            {
                if (!TryResolveHeadingError(
                        Multiply(CreateFromAxisAngle(axis, angle), pose.Orientation),
                        targetUp, pose.RightUpAlignment, context, out PointerPose trial, out Vector3 nextError))
                    return false;
                if (nextError.LengthSq2() < error.LengthSq2())
                {
                    candidate = trial.Orientation;
                    return true;
                }
            }
            return false;
        }

        private bool TryResolveHeadingError(
            Quaternion rotation, Vector3 targetUp, float rightUp, in PointerContext context,
            out PointerPose pose, out Vector3 error)
        {
            error = default;
            if (!TryResolvePointerPose(rotation, context, out pose, rightUp))
                return false;
            error = Transform(pose.Up, Inverse(pose.Orientation)) - targetUp;
            return true;
        }

        private bool TryResolveHorizonPose(
            Quaternion orientation, float rightUp, in PointerContext context, out PointerPose pose)
        {
            pose = default;
            if (!TryNormalize(orientation, out orientation))
                return false;
            Quaternion candidate = orientation;
            // Position depends on orientation through the captured ray. Resolve
            // its horizontal right axis and the new CRS up together. Use the
            // actual coordinate-model response, including large geocentric orbits.
            for (int iteration = 0; iteration < MaximumConstraintIterations; iteration++)
            {
                if (!TryResolvePointerPose(candidate, context, out pose, rightUp))
                    return false;
                float error = Vector3.Dot(Transform(UnitX, pose.Orientation), pose.Up) - rightUp;
                if (Math.Abs(error) <= HorizonAlignmentTolerance)
                    return true;

                if (!TryCalculateHorizonCorrection(
                        candidate, pose, error, context, out Vector3 axis, out float angle)
                    || !TryImproveHorizonPose(pose, error, axis, angle, context, out candidate))
                    return false;
            }
            return false;
        }

        private bool TryCalculateHorizonCorrection(
            Quaternion candidate, PointerPose pose, float error, in PointerContext context,
            out Vector3 axis, out float angle)
        {
            axis = default;
            angle = default;
            Vector3 cameraUp = Transform(UnitY, pose.Orientation);
            Vector3 forward = Transform(UnitZ, pose.Orientation);
            if (!TryProbeHorizonGradient(candidate, cameraUp, pose.RightUpAlignment, error, context, out float upGradient)
                || !TryProbeHorizonGradient(candidate, forward, pose.RightUpAlignment, error, context, out float forwardGradient))
                return false;
            Vector3 gradient = cameraUp * upGradient + forward * forwardGradient;
            float length = gradient.Length();
            if (!TryNormalize(gradient, out axis))
                return false;
            angle = Math.Max(-MaximumConstraintCorrection, Math.Min(MaximumConstraintCorrection, -error / length));
            return true;
        }

        private bool TryImproveHorizonPose(
            PointerPose pose, float error, Vector3 axis, float angle,
            in PointerContext context, out Quaternion candidate)
        {
            candidate = default;
            for (int reduction = 0; reduction < MaximumConstraintBacktracks; reduction++, angle *= 0.5f)
            {
                Quaternion next = Multiply(CreateFromAxisAngle(axis, angle), pose.Orientation);
                if (!TryResolvePointerPose(next, context, out PointerPose trial, pose.RightUpAlignment))
                    return false;
                float nextError = Vector3.Dot(Transform(UnitX, trial.Orientation), trial.Up) - pose.RightUpAlignment;
                if (Math.Abs(nextError) < Math.Abs(error))
                {
                    candidate = trial.Orientation;
                    return true;
                }
            }
            return false;
        }

        private bool TryProbeHorizonGradient(
            Quaternion candidate, Vector3 axis, float rightUp, float error,
            in PointerContext context, out float derivative)
        {
            derivative = 0;
            if (!TryResolvePointerPose(
                    Multiply(CreateFromAxisAngle(axis, ConstraintProbeAngle), candidate),
                    context, out PointerPose probe, rightUp))
                return false;
            derivative = (Vector3.Dot(Transform(UnitX, probe.Orientation), probe.Up) - rightUp - error) / ConstraintProbeAngle;
            return IsFinite(derivative);
        }

        private bool TryResolvePointerPose(
            Quaternion orientation, in PointerContext context, out PointerPose pose, float rightUp = 0.0f)
        {
            pose = default;
            if (!TryNormalize(orientation, out orientation)
                || !TryNormalize(
                    Transform(_pivotDirectionInCameraSpace, orientation), out Vector3 direction)
                || !context.ResolvePosition(
                    context.OrbitPoint - direction * context.Distance, out Vec3D position, out Vector3 up)
                || !IsFinite(position) || !TryNormalize(up, out up))
                return false;

            pose = new PointerPose(direction, orientation, position, up, rightUp);
            return true;
        }

        private static bool IsPointerPoseAllowed(
            PointerPose candidate, PointerPose previous, OrbitPitchLimits limits)
        {
            return GetPointerPoseClearance(candidate, previous, limits) >= 0.0f;
        }

        private static double GetPointerPoseClearance(
            PointerPose candidate, PointerPose previous, OrbitPitchLimits limits)
        {
            const float minimumUprightAlignment = 1e-5f;
            const double elevationProjectionTolerance = 2e-8;
            const double uprightProjectionTolerance = 1e-7;
            double minimumAlignment = Math.Min(minimumUprightAlignment, previous.UprightDot);
            double uprightTolerance = previous.UprightDot > 0.0
                ? Math.Min(uprightProjectionTolerance, minimumAlignment * 0.5)
                : uprightProjectionTolerance;
            if (previous.UprightDot >= minimumUprightAlignment - 2 * uprightProjectionTolerance)
                minimumAlignment = minimumUprightAlignment;
            float minimumElevation = (float)Math.Sin(limits.ClampRadians(-(float)Math.PI / 2.0f));
            float maximumElevation = (float)Math.Sin(limits.ClampRadians((float)Math.PI / 2.0f));
            // Float poses retain tiny projection noise after double normalization.
            // Tolerance must not accumulate by treating roundoff as an external
            // out-of-bounds pose on the next step.
            double elevationClearance = Math.Min(
                candidate.ElevationDot - (previous.ElevationDot < minimumElevation - 2 * elevationProjectionTolerance
                    ? previous.ElevationDot : minimumElevation),
                (previous.ElevationDot > maximumElevation + 2 * elevationProjectionTolerance
                    ? previous.ElevationDot : maximumElevation) - candidate.ElevationDot);
            // Keep a strictly positive upright margin when the accepted pose is upright.
            return Math.Min(candidate.UprightDot - minimumAlignment + uprightTolerance,
                elevationClearance + elevationProjectionTolerance);
        }

        private readonly struct PointerContext
        {
            internal readonly Vector3 OrbitPoint;
            internal readonly float Distance;
            internal readonly PositionResolver ResolvePosition;
            internal readonly OrbitPitchLimits Limits;

            internal PointerContext(
                Vector3 orbitPoint, PositionResolver resolvePosition, OrbitPitchLimits limits)
            {
                OrbitPoint = orbitPoint;
                Distance = orbitPoint.Length();
                ResolvePosition = resolvePosition;
                Limits = limits;
            }
        }

        private readonly struct PointerArc
        {
            internal readonly PointerPose Start;
            internal readonly Vector3 Axis;
            internal readonly float Sign;
            internal readonly PointerContext Context;

            internal PointerArc(PointerPose start, Vector3 axis, float sign, in PointerContext context)
            {
                Start = start;
                Axis = axis;
                Sign = sign;
                Context = context;
            }
        }

        private readonly struct PointerPose
        {
            internal readonly Vector3 Direction;
            internal readonly Quaternion Orientation;
            internal readonly Vec3D Position;
            internal readonly Vector3 Up;
            internal readonly double ElevationDot;
            internal readonly double UprightDot;
            internal readonly float RightUpAlignment;

            internal PointerPose(Vector3 direction, Quaternion orientation, Vec3D position, Vector3 up, float rightUp)
            {
                Direction = direction;
                Orientation = orientation;
                Position = position;
                Up = up;
                RightUpAlignment = rightUp;
                ElevationDot = -NormalizedDot(direction, up);
                UprightDot = NormalizedDot(Transform(UnitY, orientation), up);
            }

            private static double NormalizedDot(Vector3 left, Vector3 right)
            {
                // Float-normalized directions can still project beyond one near a pole.
                double dot = (double)left.x * right.x + (double)left.y * right.y + (double)left.z * right.z;
                double leftSquared = (double)left.x * left.x + (double)left.y * left.y + (double)left.z * left.z;
                double rightSquared = (double)right.x * right.x + (double)right.y * right.y + (double)right.z * right.z;
                return Math.Max(-1.0, Math.Min(1.0, dot / Math.Sqrt(leftSquared * rightSquared)));
            }
        }

        /// <summary>
        /// A validated pose proposal; committing a stale or already committed proposal fails.
        /// </summary>
        internal readonly struct PointerStep
        {
            private readonly OrbitFraming _owner;
            private readonly int _revision;
            internal Vector3 PivotDirection { get; }
            internal Quaternion Orientation { get; }
            internal Vec3D GlobalPosition { get; }
            private float? RightUpAlignment { get; }

            internal PointerStep(
                OrbitFraming owner, int revision, Vector3 pivotDirection,
                Quaternion orientation, Vec3D globalPosition = default, float? rightUpAlignment = null)
            {
                _owner = owner;
                _revision = revision;
                PivotDirection = pivotDirection;
                Orientation = orientation;
                GlobalPosition = globalPosition;
                RightUpAlignment = rightUpAlignment;
            }

            internal bool TryCommit()
            {
                if (_owner == null || !_owner._hasContinuousFraming || _owner._revision != _revision)
                    return false;

                _owner._previousPivotDirection = PivotDirection;
                _owner._continuousOrientation = Orientation;
                _owner._rightUpAlignment = RightUpAlignment;
                _owner._revision++;
                return true;
            }
        }

        /// <summary>
        /// Discards the captured gesture. Repeated calls are safe; advancing requires a new TryBegin.
        /// </summary>
        internal void Clear()
        {
            _revision++;
            _pivotDirectionInCameraSpace = default;
            _previousPivotDirection = default;
            _continuousOrientation = default;
            _rightUpAlignment = null;
            _hasContinuousFraming = false;
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
