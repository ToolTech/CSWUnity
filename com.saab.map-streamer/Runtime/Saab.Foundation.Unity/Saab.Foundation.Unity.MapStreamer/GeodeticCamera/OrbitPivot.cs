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
// File         : OrbitPivot.cs
// Module       :
// Description  : Resolves and retains the geodetic camera orbit pivot.
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
    /// <summary>
    /// Selects the screen position used to acquire an orbit pivot.
    /// </summary>
    public enum OrbitPivotMode
    {
        /// <summary>
        /// Acquires the surface at the center of the viewport.
        /// </summary>
        ViewportCenter,

        /// <summary>
        /// Acquires rendered terrain beneath the pointer, retaining the most
        /// recent pivot when the pointer is outside the loaded map.
        /// </summary>
        PointerSurface
    }

    internal sealed class OrbitPivot
    {
        private bool _hasPosition;
        private Vec3D _position;

        internal bool TryAcquireOffset(
            bool positionResolved,
            Vec3D position,
            Vec3D cameraPosition,
            out Vec3D offset)
        {
            if (positionResolved)
            {
                _position = position;
                _hasPosition = true;
            }

            return TryGetOffset(cameraPosition, out offset);
        }

        private bool TryGetOffset(Vec3D cameraPosition, out Vec3D offset)
        {
            if (!_hasPosition)
            {
                offset = default;
                return false;
            }

            var candidate = new Vec3D(
                _position.x - cameraPosition.x,
                _position.y - cameraPosition.y,
                _position.z - cameraPosition.z);
            double magnitudeSquared =
                candidate.x * candidate.x
                + candidate.y * candidate.y
                + candidate.z * candidate.z;

            if (magnitudeSquared <= 1e-12)
            {
                offset = default;
                return false;
            }

            offset = candidate;
            return true;
        }

        internal void Clear()
        {
            _position = default;
            _hasPosition = false;
        }

        internal static void ResolveScreenPosition(
            OrbitPivotMode mode,
            float pointerX,
            float pointerY,
            int screenWidth,
            int screenHeight,
            out float x,
            out float y)
        {
            ValidateMode(mode);

            if (mode == OrbitPivotMode.ViewportCenter)
            {
                x = screenWidth / 2.0f;
                y = screenHeight / 2.0f;
                return;
            }

            x = pointerX;
            y = pointerY;
        }

        internal static void ValidateMode(OrbitPivotMode mode)
        {
            if (mode != OrbitPivotMode.ViewportCenter
                && mode != OrbitPivotMode.PointerSurface)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported orbit pivot mode.");
            }
        }

    }
}
