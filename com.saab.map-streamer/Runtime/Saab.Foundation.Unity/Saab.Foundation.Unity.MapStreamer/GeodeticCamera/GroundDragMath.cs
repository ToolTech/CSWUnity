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
// File         : GroundDragMath.cs
// Module       :
// Description  : Provides stable ground-drag scaling near the horizon.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who  Date    Description
//
// DSA  260828  Created file
//
//******************************************************************************

using System;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    internal static class GroundDragMath
    {
        // Below ten degrees, scale out the ray/plane amplification instead of disabling pan.
        private const float FullScaleRayPlaneIncidence = 0.17364818f;

        internal static bool TryCalculatePanScale(
            float signedRayPlaneIncidence,
            out float scale)
        {
            scale = 0.0f;

            float incidenceMagnitude = Math.Abs(signedRayPlaneIncidence);
            if (incidenceMagnitude <= 0.0f)
                return false;

            scale = Math.Min(
                1.0f,
                incidenceMagnitude / FullScaleRayPlaneIncidence);
            return true;
        }
    }
}
