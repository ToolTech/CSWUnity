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
// File         : GroundDragPositionAdapter.cs
// Module       :
// Description  : Applies projected and geocentric ground-drag positions.
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
using Saab.Foundation.Map;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    /// <summary>Adapts accepted Ground-Drag Gesture movement to the camera position.</summary>
    internal sealed class GroundDragPositionAdapter
    {
        private readonly Func<Vec3D, double, double, Vec3D> _moveEcef;
        private readonly Func<Vec3D, bool> _trySetEcefPosition;
        private readonly Func<Vec3D> _readGlobalPosition;
        private readonly Action<Vec3D> _applyGlobalPosition;
        private readonly Action _updateOrientation;

        internal GroundDragPositionAdapter(
            Func<Vec3D, double, double, Vec3D> moveEcef,
            Func<Vec3D, bool> trySetEcefPosition,
            Func<Vec3D> readGlobalPosition,
            Action<Vec3D> applyGlobalPosition,
            Action updateOrientation)
        {
            _moveEcef = moveEcef ?? throw new ArgumentNullException(nameof(moveEcef));
            _trySetEcefPosition = trySetEcefPosition
                ?? throw new ArgumentNullException(nameof(trySetEcefPosition));
            _readGlobalPosition = readGlobalPosition
                ?? throw new ArgumentNullException(nameof(readGlobalPosition));
            _applyGlobalPosition = applyGlobalPosition
                ?? throw new ArgumentNullException(nameof(applyGlobalPosition));
            _updateOrientation = updateOrientation
                ?? throw new ArgumentNullException(nameof(updateOrientation));
        }

        internal bool Apply(MapType mapType, Vec3D position, float east, float north)
        {
            if (mapType != MapType.GEOCENTRIC)
            {
                _applyGlobalPosition(position);
                return true;
            }

            Vec3D moved = _moveEcef(position, east, north);
            if (!_trySetEcefPosition(moved))
                return false;

            _applyGlobalPosition(_readGlobalPosition());
            _updateOrientation();
            return true;
        }
    }
}
