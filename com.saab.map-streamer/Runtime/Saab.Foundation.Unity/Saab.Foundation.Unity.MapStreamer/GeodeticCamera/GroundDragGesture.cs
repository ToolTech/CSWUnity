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
// File         : GroundDragGesture.cs
// Module       :
// Description  : Owns ground-drag acquisition, references, movement and reset.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who  Date    Description
//
// DSA  260908  Created file
//
//******************************************************************************

#nullable enable
using GizmoSDK.GizmoBase;
using Saab.Foundation.Map;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    /// <summary>Owns ground-drag acquisition, retained references, movement and reset.</summary>
    internal sealed class GroundDragGesture
    {
        private readonly MapQuery _readMap;
        private readonly SurfaceQuery _querySurface;
        private readonly ProjectedRayQuery _queryProjectedRay;
        private readonly PositionApplication _applyPosition;
        private readonly FrameQuery _readFrame;
        private readonly PlaneQuery _raycast;
        private MapType? _mapType;
        private ProjectedNavigationPlane.PanGesture? _projected;
        private TangentFrame _tangent;
        private Vec2 _initialEnu;

        /// <summary>Captured ECEF camera and Unity-world axes; Up and ReferencePoint define the retained plane.</summary>
        internal struct TangentFrame
        {
            internal Vec3 East, North, Up, ReferencePoint;
            internal Vec3D CameraEcef;
        }

        internal delegate bool MapQuery(bool beginning, out MapType mapType, out Vec3D cameraPosition);
        /// <summary>The existing terrain-first, coordinate-model surface query, used only when beginning.</summary>
        internal delegate bool SurfaceQuery(float x, float y, out Vec3D position);
        internal delegate bool ProjectedRayQuery(float x, float y, out Vec3D origin, out Vec3 direction);
        /// <summary>
        /// Applies a projected map-global position, or moves from captured ECEF by
        /// signed east/north meters. False leaves the camera unchanged; success is synchronous.
        /// </summary>
        internal delegate bool PositionApplication(MapType mapType, Vec3D position, float east, float north);
        internal delegate bool FrameQuery(Vec3D reference, out TangentFrame frame);
        /// <summary>Uses native Unity normalization and plane/ray intersection, returning signed incidence.</summary>
        internal delegate bool PlaneQuery(
            float x, float y, Vec3 normal, Vec3 point, out Vec3 intersection, out float incidence);

        internal GroundDragGesture(
            MapQuery readMap, SurfaceQuery querySurface, ProjectedRayQuery queryProjectedRay,
            FrameQuery readFrame, PlaneQuery raycast, PositionApplication applyPosition)
        {
            _readMap = readMap;
            _querySurface = querySurface;
            _queryProjectedRay = queryProjectedRay;
            _readFrame = readFrame;
            _raycast = raycast;
            _applyPosition = applyPosition;
        }

        /// <summary>Consumes bottom-left-origin screen pixels; regrab acquires without moving. Failure ends the gesture.</summary>
        internal void Move(float x, float y, bool regrab)
        {
            if (regrab)
                Clear();
            if (!_readMap(regrab, out MapType mapType, out Vec3D cameraPosition)
                || (!regrab && _mapType != mapType))
            {
                Clear();
                return;
            }

            if (regrab)
            {
                if (!_querySurface(x, y, out Vec3D reference))
                    return;
                if (mapType == MapType.UTM || mapType == MapType.PROJ_SWEREF99)
                {
                    _projected = ProjectedNavigationPlane.BeginPan(cameraPosition, reference);
                    _mapType = mapType;
                }
                else if (mapType == MapType.GEOCENTRIC && _readFrame(reference, out _tangent))
                {
                    _initialEnu = new Vec2(
                        Vec3.Dot(_tangent.East, _tangent.ReferencePoint),
                        Vec3.Dot(_tangent.North, _tangent.ReferencePoint));
                    _mapType = mapType;
                }
                return;
            }

            if (_mapType == MapType.GEOCENTRIC)
            {
                if (_raycast(x, y, _tangent.Up, _tangent.ReferencePoint, out Vec3 hit, out float incidence)
                    && GroundDragMath.TryCalculatePanScale(incidence, out float scale))
                {
                    Vec2 currentEnu = new Vec2(
                        Vec3.Dot(_tangent.East, hit), Vec3.Dot(_tangent.North, hit));
                    Vec2 offset = scale * (currentEnu - _initialEnu);
                    if (_applyPosition(mapType, _tangent.CameraEcef, -offset.x, -offset.y))
                        return;
                }
            }
            else if (_projected != null
                && _queryProjectedRay(x, y, out Vec3D origin, out Vec3 direction)
                && _projected.TryCalculateNextPosition(
                    origin, new Vec3D(direction.x, direction.y, direction.z), out Vec3D position))
            {
                if (_applyPosition(mapType, position, 0, 0))
                    return;
            }
            Clear();
        }

        internal void Clear()
        {
            _mapType = null;
            _projected = null;
        }
    }
}
