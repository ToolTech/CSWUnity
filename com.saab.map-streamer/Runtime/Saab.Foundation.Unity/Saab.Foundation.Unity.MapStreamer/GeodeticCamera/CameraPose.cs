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
// File         : CameraPose.cs
// Module       :
// Description  : Validates, prepares and atomically applies camera poses.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who      Date    Description
//
// DSA      261023  Created file
//
//******************************************************************************

using System;
using GizmoSDK.GizmoBase;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    /// <summary>
    /// Validates and atomically applies unrestricted poses, including roll, on the owning thread.
    /// </summary>
    public sealed class CameraPose
    {
        /// <summary>
        /// Receives a finite position and normalized rotation; must not change accepted state.
        /// Success supplies a synchronous, infallible commit; failure rejects the whole pose.
        /// </summary>
        public delegate bool PosePreparation(Vec3D position, Quaternion orientation, out Action apply);

        private readonly PosePreparation _prepare;

        public CameraPose(PosePreparation prepare)
        {
            _prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
        }

        /// <returns>False for invalid values or failed coordinate/basis conversion; nothing is committed.</returns>
        public bool TrySetPose(Vec3D position, Quaternion orientation)
        {
            double magnitude = (double)orientation.x * orientation.x
                + (double)orientation.y * orientation.y
                + (double)orientation.z * orientation.z
                + (double)orientation.w * orientation.w;
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z)
                || !IsFinite(magnitude) || magnitude <= 0)
                return false;

            double inverseLength = 1.0 / Math.Sqrt(magnitude);
            var normalized = new Quaternion(
                (float)(orientation.w * inverseLength),
                (float)(orientation.x * inverseLength),
                (float)(orientation.y * inverseLength),
                (float)(orientation.z * inverseLength));
            if (!_prepare(position, normalized, out Action apply))
                return false;

            apply();
            return true;
        }

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
