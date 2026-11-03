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
// File         : ConstrainedGeodeticCamera.cs
// Module       :
// Description  : Adds optional orbit, zoom and pan policies to geodetic camera movement.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who      Date    Description
//
// DSA      261023  Created file
//
//******************************************************************************

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    /// <summary>
    /// Adds optional orbit and zoom limits, plus shallow-angle pan damping.
    /// </summary>
    /// <remarks>
    /// Defaults: minimum zoom distance 1 m, no maximum, orbit elevation 1..89 degrees.
    /// Direct poses bypass these policies.
    /// </remarks>
    public class ConstrainedGeodeticCamera : GeodeticCameraControl
    {
        protected override bool DampenPan => true;
        protected override bool FaceCenterOrbit => true;

        private protected override void ConfigureZoom(CameraZoomGesture zoom)
        {
            zoom.SetDistanceLimits(1, null);
        }

        private protected override void ConfigureOrbit(OrbitGesture orbit)
        {
            orbit.SetPitchLimits(1, 89);
        }

        /// <inheritdoc cref="ConfigureZoomDistanceLimits"/>
        public void SetZoomDistanceLimits(double? minimumDistance, double? maximumDistance)
        {
            ConfigureZoomDistanceLimits(minimumDistance, maximumDistance);
        }

        /// <inheritdoc cref="ConfigureOrbitPitchLimits"/>
        public void SetOrbitPitchLimits(float? minimumPitchDegrees, float? maximumPitchDegrees)
        {
            ConfigureOrbitPitchLimits(minimumPitchDegrees, maximumPitchDegrees);
        }
    }
}
