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
// File         : OrbitMotion.cs
// Module       :
// Description  : Resolves screen-relative pointer orbit motion.
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
using Vector3 = GizmoSDK.GizmoBase.Vec3;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    internal static class OrbitMotion
    {
        /// <summary>
        /// Calculates position and its full heading/pitch rotation together.
        /// Upright motion clips the pitch arc at the first camera-up or elevation
        /// boundary, rather than recovering an ambiguous rotation from its endpoint.
        /// </summary>
        internal static bool TryCalculatePointerMotion(
            Vector3 orbitDirection,
            Vector3 upDirection,
            Vector3 cameraUpDirection,
            float headingDeltaRadians,
            float pitchDeltaRadians,
            OrbitPitchLimits pitchLimits,
            out Vector3 result,
            out float resultingPitchRadians,
            out Quaternion rotation)
        {
            result = default;
            resultingPitchRadians = default;
            rotation = default;
            if (!GizmoCameraMath.IsFinite(headingDeltaRadians)
                || !GizmoCameraMath.IsFinite(pitchDeltaRadians)
                || !TryNormalize(orbitDirection, out orbitDirection)
                || !TryNormalize(upDirection, out upDirection)
                || !TryNormalize(cameraUpDirection, out cameraUpDirection))
            {
                return false;
            }

            Quaternion headingRotation = CreateFromAxisAngle(
                upDirection,
                headingDeltaRadians);
            Vector3 headingDirection = Transform(
                orbitDirection,
                headingRotation);
            Vector3 headingCameraUp = Transform(
                cameraUpDirection,
                headingRotation);
            if (!TryNormalize(headingDirection, out headingDirection))
                return false;

            Vector3 candidate = headingDirection;
            Quaternion pitchRotation = Identity;
            Vector3 pitchTangent = headingCameraUp
                - Vector3.Dot(headingCameraUp, headingDirection)
                * headingDirection;
            if (pitchDeltaRadians != 0.0f)
            {
                if (!TryNormalize(pitchTangent, out pitchTangent))
                    return false;

                float appliedPitchDelta = pitchLimits.ClampArcRadians(
                    Vector3.Dot(headingDirection, upDirection),
                    Vector3.Dot(pitchTangent, upDirection),
                    pitchDeltaRadians);
                if (!TryNormalize(Vector3.Cross(headingDirection, pitchTangent), out Vector3 pitchAxis))
                    return false;

                pitchRotation = CreateFromAxisAngle(pitchAxis, appliedPitchDelta);
                candidate =
                    headingDirection * (float)Math.Cos(appliedPitchDelta)
                    + pitchTangent * (float)Math.Sin(appliedPitchDelta);
                if (!TryNormalize(candidate, out candidate))
                    return false;
            }

            if (!TryNormalize(candidate, out result))
                return false;

            resultingPitchRadians = (float)Math.Asin(
                Math.Max(-1.0f, Math.Min(1.0f, Vector3.Dot(result, upDirection))));
            rotation = Normalize(Multiply(pitchRotation, headingRotation));
            return true;
        }
    }
}
