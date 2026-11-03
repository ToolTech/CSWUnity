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
// File         : Wgs84Ellipsoid.cs
// Module       :
// Description  : Intersects rays with the WGS84 reference ellipsoid.
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
using GizmoSDK.GizmoBase;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    internal static class Wgs84Ellipsoid
    {
        private const double SemiMajorAxis = 6378137.0;
        private const double InverseFlattening = 298.257223563;

        internal static bool TryIntersectRay(
            Vec3D rayOrigin,
            Vec3D rayDirection,
            out Vec3D result)
        {
            result = default;

            double semiMinorAxis = SemiMajorAxis * (1.0 - 1.0 / InverseFlattening);
            double scaledDirectionX = rayDirection.x / SemiMajorAxis;
            double scaledDirectionY = rayDirection.y / SemiMajorAxis;
            double scaledDirectionZ = rayDirection.z / semiMinorAxis;
            double scaledDirectionLength = Math.Sqrt(
                scaledDirectionX * scaledDirectionX
                + scaledDirectionY * scaledDirectionY
                + scaledDirectionZ * scaledDirectionZ);

            if (scaledDirectionLength <= double.Epsilon)
                return false;

            double directionX = scaledDirectionX / scaledDirectionLength;
            double directionY = scaledDirectionY / scaledDirectionLength;
            double directionZ = scaledDirectionZ / scaledDirectionLength;
            double originX = rayOrigin.x / SemiMajorAxis;
            double originY = rayOrigin.y / SemiMajorAxis;
            double originZ = rayOrigin.z / semiMinorAxis;
            double originDistanceSquared =
                originX * originX
                + originY * originY
                + originZ * originZ;
            const double surfaceTolerance = 1e-12;
            if (originDistanceSquared < 1.0 - surfaceTolerance)
                return false;

            double projection =
                originX * directionX
                + originY * directionY
                + originZ * directionZ;
            double closestDistanceSquared =
                originDistanceSquared - projection * projection;

            if (closestDistanceSquared > 1.0 + surfaceTolerance)
                return false;

            double halfChord = Math.Sqrt(Math.Max(0.0, 1.0 - closestDistanceSquared));
            double nearDistance = -projection - halfChord;
            double farDistance = -projection + halfChord;

            double scaledDistance = nearDistance >= 0.0 ? nearDistance : farDistance;
            if (scaledDistance < 0.0)
                return false;

            double distance = scaledDistance / scaledDirectionLength;
            result = new Vec3D(
                rayOrigin.x + distance * rayDirection.x,
                rayOrigin.y + distance * rayDirection.y,
                rayOrigin.z + distance * rayDirection.z);
            return true;
        }
    }
}
