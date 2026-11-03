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
// File         : CameraZoomGesture.cs
// Module       :
// Description  : Owns zoom reference acquisition, distance scaling and application.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who      Date    Description
//
// DSA  260909  Created file
//
//******************************************************************************

using System;
using GizmoSDK.GizmoBase;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    /// <summary>Composes surface acquisition, bounded scaling and camera-position application.</summary>
    internal sealed class CameraZoomGesture
    {
        private readonly ReferenceQuery _queryReference;
        private readonly Func<Vec3D> _readPosition;
        private readonly Action<Vec3D> _applyPosition;
        private double _maximumDistance = CameraZoomMath.MinimumDistance;

        internal delegate bool ReferenceQuery(float x, float y, out Vec3D position);

        internal CameraZoomGesture(
            ReferenceQuery queryReference,
            Func<Vec3D> readPosition,
            Action<Vec3D> applyPosition)
        {
            _queryReference = queryReference ?? throw new ArgumentNullException(nameof(queryReference));
            _readPosition = readPosition ?? throw new ArgumentNullException(nameof(readPosition));
            _applyPosition = applyPosition ?? throw new ArgumentNullException(nameof(applyPosition));
        }

        internal bool Move(float x, float y, float amount)
        {
            if (!_queryReference(x, y, out Vec3D reference))
                return false;

            Vec3D cameraPosition = _readPosition();
            Vec3D offset = reference - cameraPosition;
            double distance = offset.Length();
            double maximumDistance = Math.Max(_maximumDistance, distance);
            if (!CameraZoomMath.TryCalculateDistanceScale(
                    distance,
                    amount,
                    maximumDistance,
                    out double scale))
                return false;

            Vec3D nextPosition = reference - scale * offset;
            if (!IsFinite(nextPosition))
                return false;

            _applyPosition(nextPosition);
            _maximumDistance = maximumDistance;
            return true;
        }

        private static bool IsFinite(Vec3D value)
            => !double.IsNaN(value.x) && !double.IsInfinity(value.x)
                && !double.IsNaN(value.y) && !double.IsInfinity(value.y)
                && !double.IsNaN(value.z) && !double.IsInfinity(value.z);
    }
}
