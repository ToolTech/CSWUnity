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
// Description  : Calculates bounded geodetic camera zoom distances.
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
using GizmoSDK.GizmoBase;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    internal static class CameraZoomMath
    {
        internal const double MinimumDistance = 1.0;

        private const double ZoomFactor = 0.2;

        internal static bool TryCalculateDistanceScale(
            double distance,
            float steps,
            double maximumDistance,
            out double scale)
        {
            scale = default;

            if (double.IsNaN(distance)
                || double.IsInfinity(distance)
                || float.IsNaN(steps)
                || float.IsInfinity(steps)
                || double.IsNaN(maximumDistance)
                || double.IsInfinity(maximumDistance)
                || distance <= 0.0
                || maximumDistance < MinimumDistance)
                return false;

            if (steps > 0.0f && distance <= MinimumDistance
                || steps < 0.0f && distance >= maximumDistance)
            {
                scale = 1.0;
                return true;
            }

            double adjustedDistance = distance * Math.Pow(1.0 - ZoomFactor, steps);

            if (steps > 0.0f && adjustedDistance < MinimumDistance)
                adjustedDistance = MinimumDistance;
            else if (steps < 0.0f && adjustedDistance > maximumDistance)
                adjustedDistance = maximumDistance;

            scale = adjustedDistance / distance;
            return true;
        }
    }
}
