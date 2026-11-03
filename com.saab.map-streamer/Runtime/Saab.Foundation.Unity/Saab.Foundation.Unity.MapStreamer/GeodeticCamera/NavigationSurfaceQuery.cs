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
// File         : NavigationSurfaceQuery.cs
// Module       :
// Description  : Resolves navigation references against the active map.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who      Date    Description
//
// DSA  260908  Created file
//
//******************************************************************************

#nullable enable
using System;
using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using Saab.Foundation.Map;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    internal enum NavigationQueryResult
    {
        // A successful query without an intersection; only this state permits fallback.
        Miss,
        Hit,
        // Invalid data must not fall through to another surface or a remembered pivot.
        Invalid
    }

    /// <summary>Owns navigation surface policies and native screen/map queries, not gesture movement.</summary>
    internal sealed class NavigationSurfaceQuery
    {
        private readonly Func<(int Width, int Height)> _viewport;
        private MapPos? _ellipsoidPosition;

        internal NavigationSurfaceQuery(Func<(int Width, int Height)> viewport)
        {
            _viewport = viewport;
        }

        internal bool IsAvailable => IsValidContext(MapControl.SystemMap, _viewport(), 0, 0);

        internal bool TryGetGroundDragReference(float x, float y, out Vec3D position)
        {
            return QuerySurface(MapControl.SystemMap, _viewport(), x, y, allowMathematical: true, out _, out position)
                == NavigationQueryResult.Hit;
        }

        internal bool TryGetZoomReference(float x, float y, out Vec3D position)
        {
            var map = MapControl.SystemMap;
            var viewport = _viewport();
            NavigationQueryResult pointer = QuerySurface(map, viewport, x, y, allowMathematical: true, out _, out position);
            if (pointer != NavigationQueryResult.Miss)
                return pointer == NavigationQueryResult.Hit;
            return QuerySurface(map, viewport, viewport.Width * 0.5f, viewport.Height * 0.5f,
                allowMathematical: true, out _, out position)
                == NavigationQueryResult.Hit;
        }

        internal NavigationQueryResult GetPointerOrbitPivot(float x, float y, out Vec3D position)
        {
            return QuerySurface(MapControl.SystemMap, _viewport(), x, y, allowMathematical: false, out _, out position);
        }

        internal NavigationQueryResult GetViewportCenterOrbitTerrain(float x, float y, out MapPos terrain)
        {
            return QuerySurface(MapControl.SystemMap, _viewport(), x, y, allowMathematical: false, out terrain, out _);
        }

        internal bool TryGetMapRay(float x, float y, out Vec3D origin, out Vec3 direction)
        {
            return QueryRay(MapControl.SystemMap, _viewport(), x, y, out origin, out direction)
                == NavigationQueryResult.Hit;
        }

        private NavigationQueryResult QuerySurface(
            MapControl map, (int Width, int Height) viewport, float x, float y, bool allowMathematical,
            out MapPos terrainPosition, out Vec3D position)
        {
            terrainPosition = null!;
            position = default;
            NavigationQueryResult ray = QueryRay(map, viewport, x, y, out Vec3D origin, out Vec3 direction);
            if (ray != NavigationQueryResult.Hit)
                return ray;
            NavigationQueryResult terrain = QueryTerrain(map, origin, direction, out terrainPosition, out position);
            if (!allowMathematical || terrain != NavigationQueryResult.Miss)
                return terrain;
            bool projected = map.MapType == MapType.UTM || map.MapType == MapType.PROJ_SWEREF99;
            if (!projected && map.MapType != MapType.GEOCENTRIC)
                return NavigationQueryResult.Miss;
            Vec3D candidate;
            if (projected)
            {
                if (!ProjectedNavigationPlane.TryIntersectRay(
                        origin, new Vec3D(direction.x, direction.y, direction.z), 0.0, out candidate, out _))
                    return NavigationQueryResult.Miss;
            }
            else
            {
                var end = new Vec3D(origin.x + direction.x, origin.y + direction.y, origin.z + direction.z);
                if (!map.GlobalToWorld(origin, out CartPos ecefOrigin))
                    return NavigationQueryResult.Invalid;
                var worldOrigin = new Vec3D(ecefOrigin.X, ecefOrigin.Y, ecefOrigin.Z);
                if (!IsFinite(worldOrigin))
                    return NavigationQueryResult.Invalid;
                if (!map.GlobalToWorld(end, out CartPos ecefEnd))
                    return NavigationQueryResult.Invalid;
                var worldEnd = new Vec3D(ecefEnd.X, ecefEnd.Y, ecefEnd.Z);
                var worldDirection = new Vec3D(ecefEnd.X - ecefOrigin.X, ecefEnd.Y - ecefOrigin.Y, ecefEnd.Z - ecefOrigin.Z);
                if (!IsFinite(worldEnd) || !IsUsableDirection(worldDirection))
                    return NavigationQueryResult.Invalid;
                if (!Wgs84Ellipsoid.TryIntersectRay(worldOrigin, worldDirection, out Vec3D intersection))
                    return NavigationQueryResult.Miss;
                if (!IsFinite(intersection))
                    return NavigationQueryResult.Invalid;
                if (_ellipsoidPosition == null)
                    _ellipsoidPosition = new MapPos();
                _ellipsoidPosition.node = null;
                if (!_ellipsoidPosition.SetCartPos(intersection.x, intersection.y, intersection.z))
                    return NavigationQueryResult.Invalid;
                candidate = _ellipsoidPosition.GlobalPosition();
            }
            if (!IsFinite(candidate))
                return NavigationQueryResult.Invalid;
            position = candidate;
            return NavigationQueryResult.Hit;
        }

        private static NavigationQueryResult QueryRay(
            MapControl map, (int Width, int Height) viewport, float x, float y,
            out Vec3D origin, out Vec3 direction)
        {
            origin = default;
            direction = default;
            if (!IsValidContext(map, viewport, x, y))
                return NavigationQueryResult.Invalid;
            if (!map.GetScreenVectors(
                    (int)x, (int)(viewport.Height - y), (uint)viewport.Width, (uint)viewport.Height,
                    out Vec3D candidateOrigin, out Vec3 candidateDirection))
                return NavigationQueryResult.Invalid;
            if (!IsFinite(candidateOrigin)
                || !IsUsableDirection(new Vec3D(candidateDirection.x, candidateDirection.y, candidateDirection.z)))
                return NavigationQueryResult.Invalid;
            origin = candidateOrigin;
            direction = candidateDirection;
            return NavigationQueryResult.Hit;
        }

        private static NavigationQueryResult QueryTerrain(
            MapControl map, Vec3D origin, Vec3 direction,
            out MapPos terrain, out Vec3D position)
        {
            position = default;
            terrain = null!;
            if (!map.GetGroundPosition(origin, direction, out MapPos candidate,
                    GroundClampType.GROUND, ClampFlags.FRUSTRUM_CULL))
                return NavigationQueryResult.Invalid;
            if (candidate == null)
                return NavigationQueryResult.Invalid;
            if (candidate.clampResult == IntersectQuery.NULL)
                return NavigationQueryResult.Miss;

            Vec3D global = candidate.GlobalPosition();
            if (!IsFinite(global))
                return NavigationQueryResult.Invalid;
            terrain = candidate;
            position = global;
            return NavigationQueryResult.Hit;
        }

        private static bool IsUsableDirection(Vec3D direction)
        {
            double squaredLength = direction.x * direction.x + direction.y * direction.y + direction.z * direction.z;
            return !double.IsNaN(squaredLength) && !double.IsInfinity(squaredLength) && squaredLength > 1e-12;
        }

        private static bool IsFinite(Vec3D value)
            => !double.IsNaN(value.x) && !double.IsInfinity(value.x)
                && !double.IsNaN(value.y) && !double.IsInfinity(value.y)
                && !double.IsNaN(value.z) && !double.IsInfinity(value.z);

        private static bool IsValidContext(MapControl map, (int Width, int Height) viewport, float x, float y)
            => map != null && viewport.Width > 0 && viewport.Height > 0
                && !float.IsNaN(x) && !float.IsInfinity(x) && !float.IsNaN(y) && !float.IsInfinity(y);
    }
}
