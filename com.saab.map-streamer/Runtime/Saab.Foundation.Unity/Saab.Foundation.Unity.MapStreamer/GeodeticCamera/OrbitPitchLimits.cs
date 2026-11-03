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
// File         : OrbitPitchLimits.cs
// Module       :
// Description  : Defines geodetic camera orbit pitch limits.
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

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    internal readonly struct OrbitPitchLimits
    {
        private const float MinimumPitchDegrees = -89.0f;
        private const float MaximumPitchDegrees = 89.0f;
        private const float DegreesToRadians = (float)Math.PI / 180.0f;
        // Includes quaternion rotation, normalization, projection and dot-product
        // roundoff in a rotated map frame, not just a single float operation.
        private const double TangentRoundoffTolerance = 1e-6;

        // Null disables the corresponding pitch bound.
        private readonly float? _minimumRadians;
        private readonly float? _maximumRadians;

        internal bool HasBothBounds => _minimumRadians.HasValue && _maximumRadians.HasValue;

        internal static OrbitPitchLimits Default =>
            new OrbitPitchLimits(1.0f, MaximumPitchDegrees);

        internal OrbitPitchLimits(
            float? minimumPitchDegrees,
            float? maximumPitchDegrees)
        {
            ValidatePitch(minimumPitchDegrees, nameof(minimumPitchDegrees));
            ValidatePitch(maximumPitchDegrees, nameof(maximumPitchDegrees));

            if (minimumPitchDegrees.HasValue
                && maximumPitchDegrees.HasValue
                && minimumPitchDegrees.Value > maximumPitchDegrees.Value)
            {
                throw new ArgumentException(
                    "Minimum orbit pitch cannot exceed maximum orbit pitch.");
            }

            _minimumRadians = minimumPitchDegrees.HasValue
                ? minimumPitchDegrees.Value * DegreesToRadians
                : (float?)null;
            _maximumRadians = maximumPitchDegrees.HasValue
                ? maximumPitchDegrees.Value * DegreesToRadians
                : (float?)null;
        }

        internal float ClampRadians(float pitch)
        {
            if (_minimumRadians.HasValue && pitch < _minimumRadians.Value)
                return _minimumRadians.Value;

            if (_maximumRadians.HasValue && pitch > _maximumRadians.Value)
                return _maximumRadians.Value;

            return pitch;
        }

        /// <summary>
        /// Limits a screen-relative great-circle step at its first crossing into
        /// a forbidden elevation, rather than clamping the mouse input angle.
        /// </summary>
        /// <param name="upDot">Map-up dot product of the unit pivot-to-camera direction.</param>
        /// <param name="tangentUpDot">Map-up dot product of its unit pitch tangent.</param>
        /// <param name="deltaRadians">Signed requested travel along that tangent, in radians.</param>
        /// <returns>
        /// The permitted signed travel. An already out-of-range pose may move
        /// toward the allowed range, but not farther away from it.
        /// </returns>
        internal float ClampArcRadians(float upDot, float tangentUpDot, float deltaRadians)
        {
            double travel = Math.Abs((double)deltaRadians);
            double tangent = deltaRadians < 0.0f ? -tangentUpDot : tangentUpDot;
            if (_maximumRadians.HasValue)
            {
                travel = LimitUpperCrossing(
                    upDot, tangent, Math.Sin(_maximumRadians.Value), travel);
            }

            if (_minimumRadians.HasValue)
            {
                travel = LimitUpperCrossing(
                    -upDot, -tangent, -Math.Sin(_minimumRadians.Value), travel);
            }

            return (float)(deltaRadians < 0.0f ? -travel : travel);
        }

        private static double LimitUpperCrossing(
            double upDot, double tangentUpDot, double limit, double travel)
        {
            // Quaternion-derived perpendicular tangents retain float roundoff.
            // At tangency, the second derivative (-upDot) decides whether the
            // arc moves back into range; noise must not lock a rolled camera.
            if (upDot >= limit
                && (tangentUpDot > TangentRoundoffTolerance
                    || (Math.Abs(tangentUpDot) <= TangentRoundoffTolerance && upDot < 0.0)))
                return 0.0;

            // Along the arc, map-up alignment is a*cos(t) + b*sin(t).
            // Find the first rising crossing, including steps that would pass
            // through a forbidden pole and finish back inside the allowed range.
            double amplitude = Math.Sqrt(upDot * upDot + tangentUpDot * tangentUpDot);
            if (amplitude <= limit || amplitude == 0.0)
                return travel;

            double phase = Math.Atan2(tangentUpDot, upDot);
            double crossing = limit <= -amplitude
                ? phase + Math.PI
                : phase - Math.Acos(limit / amplitude);
            crossing %= 2.0 * Math.PI;
            if (crossing < 0.0)
                crossing += 2.0 * Math.PI;

            return Math.Min(travel, crossing);
        }

        private static void ValidatePitch(float? pitchDegrees, string parameterName)
        {
            if (pitchDegrees.HasValue
                && (float.IsNaN(pitchDegrees.Value)
                    || float.IsInfinity(pitchDegrees.Value)
                    || pitchDegrees.Value < MinimumPitchDegrees
                    || pitchDegrees.Value > MaximumPitchDegrees))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Orbit pitch must be between {MinimumPitchDegrees} and "
                    + $"{MaximumPitchDegrees} degrees.");
            }
        }
    }
}
