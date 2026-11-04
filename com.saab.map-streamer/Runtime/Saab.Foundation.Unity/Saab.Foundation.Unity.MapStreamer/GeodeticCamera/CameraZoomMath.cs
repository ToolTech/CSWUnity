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
// File         : CameraZoomMath.cs
// Module       :
// Description  : Calculates safe geodetic camera zoom distances.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who  Date    Description
//
// DSA  260901  Created file
// DSA  261020  Removed the zoom-out limit and rejected invalid zoom calculations
//
//******************************************************************************

using System;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    internal static class CameraZoomMath
    {
        private const double ZoomFactor = 0.2;

        internal static bool TryCalculateDistanceScale(
            double distance,
            float steps,
            double? minimumDistance,
            double? maximumDistance,
            out double scale)
        {
            scale = default;

            if (double.IsNaN(distance)
                || double.IsInfinity(distance)
                || float.IsNaN(steps)
                || float.IsInfinity(steps)
                || distance <= 0.0)
                return false;

            if (steps > 0.0f && minimumDistance.HasValue && distance <= minimumDistance.Value
                || steps < 0.0f && maximumDistance.HasValue && distance >= maximumDistance.Value)
            {
                scale = 1.0;
                return true;
            }

            double adjustedDistance = distance * Math.Pow(1.0 - ZoomFactor, steps);

            if (steps > 0.0f && minimumDistance.HasValue && adjustedDistance < minimumDistance.Value)
                adjustedDistance = minimumDistance.Value;
            else if (steps < 0.0f && maximumDistance.HasValue && adjustedDistance > maximumDistance.Value)
                adjustedDistance = maximumDistance.Value;

            double candidateScale = adjustedDistance / distance;
            if (double.IsNaN(adjustedDistance)
                || double.IsInfinity(adjustedDistance)
                || double.IsNaN(candidateScale)
                || double.IsInfinity(candidateScale)
                || candidateScale <= 0.0)
                return false;

            scale = candidateScale;
            return true;
        }
    }
}
