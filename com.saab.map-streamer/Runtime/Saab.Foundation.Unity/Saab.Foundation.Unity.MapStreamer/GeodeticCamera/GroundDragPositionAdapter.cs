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
// DSA  261020  Validated and applied pan position and rotation together
//
//******************************************************************************

using System;
using GizmoSDK.GizmoBase;
using Saab.Foundation.Map;
using static Saab.Foundation.Unity.MapStreamer.GeodeticCamera.GizmoCameraMath;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    internal sealed class GroundDragPositionAdapter
    {
        internal readonly struct Basis
        {
            internal readonly Vec3 East;
            internal readonly Vec3 North;
            internal readonly Vec3 Up;

            internal Basis(Vec3 east, Vec3 north, Vec3 up)
            {
                East = east;
                North = north;
                Up = up;
            }
        }

        private readonly EllipsoidMovement _moveEcef;
        private readonly Func<(Basis Basis, Quaternion Orientation)> _readOrientation;
        private readonly DestinationPreparation _prepareDestination;
        private readonly Func<Vec3D, bool> _applyGlobalPosition;

        /// <summary>
        /// Resolves the native destination and basis without movement; success supplies its pose application.
        /// </summary>
        internal delegate bool DestinationPreparation(
            Vec3D position, out Basis basis, out Action<Quaternion> apply);

        /// <summary>
        /// Resolves ECEF movement without changing accepted state. A failed native
        /// conversion returns false, even when it supplied a finite output value.
        /// </summary>
        internal delegate bool EllipsoidMovement(
            Vec3D origin, double eastMetres, double northMetres, out Vec3D position);

        internal GroundDragPositionAdapter(
            EllipsoidMovement moveEcef,
            Func<(Basis Basis, Quaternion Orientation)> readOrientation,
            DestinationPreparation prepareDestination,
            Func<Vec3D, bool> applyGlobalPosition)
        {
            _moveEcef = moveEcef ?? throw new ArgumentNullException(nameof(moveEcef));
            _readOrientation = readOrientation ?? throw new ArgumentNullException(nameof(readOrientation));
            _prepareDestination = prepareDestination
                ?? throw new ArgumentNullException(nameof(prepareDestination));
            _applyGlobalPosition = applyGlobalPosition
                ?? throw new ArgumentNullException(nameof(applyGlobalPosition));
        }

        internal bool Apply(MapType mapType, Vec3D position, float east, float north)
        {
            if (mapType != MapType.GEOCENTRIC)
                return _applyGlobalPosition(position);

            var source = _readOrientation();
            if (!IsValid(source.Basis) || !IsValid(source.Orientation))
                return false;

            if (!_moveEcef(position, east, north, out Vec3D moved)
                || !IsFinite(moved.x) || !IsFinite(moved.y) || !IsFinite(moved.z)
                || !_prepareDestination(moved, out Basis destination, out Action<Quaternion> apply)
                || !IsValid(destination))
                return false;

            Quaternion rotation = FromBasisToBasis(
                source.Basis.East, source.Basis.North, source.Basis.Up,
                destination.East, destination.North, destination.Up);
            Quaternion transported = Multiply(rotation, source.Orientation);
            if (!IsValid(transported))
                return false;

            apply(Normalize(transported));
            return true;
        }

        private static bool IsValid(Basis basis)
            => IsValid(basis.East) && IsValid(basis.North) && IsValid(basis.Up);

        private static bool IsValid(Vec3 axis)
        {
            float lengthSquared = axis.LengthSq2();
            return GizmoCameraMath.IsFinite(lengthSquared) && lengthSquared > 1e-12f;
        }

        private static bool IsValid(Quaternion orientation)
        {
            float lengthSquared = Dot(orientation, orientation);
            return GizmoCameraMath.IsFinite(lengthSquared) && lengthSquared > 0.0f;
        }

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
