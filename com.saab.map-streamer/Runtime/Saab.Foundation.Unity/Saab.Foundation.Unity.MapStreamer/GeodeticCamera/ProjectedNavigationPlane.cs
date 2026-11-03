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
// File         : ProjectedNavigationPlane.cs
// Module       :
// Description  : Provides framework-neutral navigation on a projected map plane.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who  Date    Description
//
// DSA  260902  Created file
//
//******************************************************************************

using System;
using GizmoSDK.GizmoBase;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    internal static class ProjectedNavigationPlane
    {
        private const double MinimumIncidence = 1e-12;

        internal static bool TryIntersectRay(
            Vec3D rayOrigin,
            Vec3D rayDirection,
            double planeHeight,
            out Vec3D intersection,
            out float incidence)
        {
            intersection = default;
            incidence = 0.0f;

            double directionLengthSquared =
                rayDirection.x * rayDirection.x
                + rayDirection.y * rayDirection.y
                + rayDirection.z * rayDirection.z;
            if (directionLengthSquared <= 0.0)
                return false;

            double directionLength = Math.Sqrt(
                directionLengthSquared);
            double normalizedVerticalDirection =
                rayDirection.y / directionLength;
            if (Math.Abs(normalizedVerticalDirection) <= MinimumIncidence)
                return false;

            double distance = (planeHeight - rayOrigin.y) / rayDirection.y;
            if (distance < 0.0)
                return false;

            intersection = new Vec3D(
                rayOrigin.x + rayDirection.x * distance,
                planeHeight,
                rayOrigin.z + rayDirection.z * distance);
            incidence = (float)-normalizedVerticalDirection;
            return true;
        }

        internal static Vec3D CalculateNextPanPosition(
            Vec3D currentCameraPosition,
            Vec3D initialAnchor,
            Vec3D currentAnchor,
            float scale)
        {
            return new Vec3D(
                currentCameraPosition.x
                    + (initialAnchor.x - currentAnchor.x) * scale,
                currentCameraPosition.y,
                currentCameraPosition.z
                    + (initialAnchor.z - currentAnchor.z) * scale);
        }

        internal static PanGesture BeginPan(
            Vec3D cameraPosition,
            Vec3D grabbedSurfacePosition)
        {
            return new PanGesture(
                cameraPosition,
                grabbedSurfacePosition);
        }

        internal sealed class PanGesture
        {
            private Vec3D _grabbedSurfacePosition;
            private Vec3D _cameraPosition;

            internal PanGesture(
                Vec3D cameraPosition,
                Vec3D grabbedSurfacePosition)
            {
                _cameraPosition = cameraPosition;
                _grabbedSurfacePosition = grabbedSurfacePosition;
            }

            internal bool TryCalculateNextPosition(
                Vec3D rayOrigin,
                Vec3D rayDirection,
                out Vec3D cameraPosition)
            {
                cameraPosition = default;
                if (!TryIntersectRay(
                        rayOrigin,
                        rayDirection,
                        _grabbedSurfacePosition.y,
                        out Vec3D currentSurfacePosition,
                        out float incidence)
                    || !GroundDragMath.TryCalculatePanScale(
                        incidence,
                        out float scale))
                {
                    return false;
                }

                cameraPosition = CalculateNextPosition(
                    currentSurfacePosition,
                    scale);
                return true;
            }

            internal Vec3D CalculateNextPosition(
                Vec3D currentSurfacePosition,
                float scale)
            {
                Vec3D previousCameraPosition = _cameraPosition;
                Vec3D cameraPosition = CalculateNextPanPosition(
                        _cameraPosition,
                        _grabbedSurfacePosition,
                        currentSurfacePosition,
                        scale);

                _grabbedSurfacePosition = new Vec3D(
                    currentSurfacePosition.x
                        + cameraPosition.x
                        - previousCameraPosition.x,
                    currentSurfacePosition.y
                        + cameraPosition.y
                        - previousCameraPosition.y,
                    currentSurfacePosition.z
                        + cameraPosition.z
                        - previousCameraPosition.z);
                _cameraPosition = cameraPosition;
                return cameraPosition;
            }
        }
    }
}
