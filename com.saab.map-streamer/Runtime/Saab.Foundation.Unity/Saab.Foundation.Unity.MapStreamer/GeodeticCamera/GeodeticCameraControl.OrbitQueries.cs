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
// File         : GeodeticCameraControl.OrbitQueries.cs
// Module       :
// Description  : Native coordinate adaptation for orbit queries.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who      Date    Description
//
// DSA  260908  Created file
//
//******************************************************************************

using System;
using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;
using Saab.Foundation.Map;
using Saab.Unity.Extensions;
using Saab.Utility.Unity.NodeUtils;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    public partial class GeodeticCameraControl
    {
        private NavigationQueryResult QueryOrbitTerrain(
            OrbitPivotMode mode, Vec2 screenPosition, out Vec3D position)
        {
            position = default;
            if (mode == OrbitPivotMode.PointerSurface)
            {
                NavigationQueryResult result = NavigationSurfaces.GetPointerOrbitPivot(
                    screenPosition.x, screenPosition.y, out Vec3D global);
                if (result != NavigationQueryResult.Hit)
                    return result;
                position = new Vec3D(global.x, global.y, -global.z);
                return NavigationQueryResult.Hit;
            }
            NavigationQueryResult center = NavigationSurfaces.GetViewportCenterOrbitTerrain(
                screenPosition.x, screenPosition.y, out MapPos terrain);
            if (center != NavigationQueryResult.Hit)
                return center;
            if (!TryGetLocalPosition(terrain, out Vector3 local))
                return NavigationQueryResult.Invalid;
            position = local.ToVec3D();
            return NavigationQueryResult.Hit;
        }

        private bool TryPointAlongView(float distance, out Vec3 point)
        {
            point = default;
            var map = MapControl.SystemMap;
            var camera = Camera;
            if (map == null || camera == null || camera.transform == null
                || !map.GlobalToWorld(new Vec3D(X, Y, Z), out CartPos cameraPosition)
                || !TrySetCameraMapPosition(cameraPosition)
                || !TryGetLocalPosition(_mapPos, out Vector3 cameraLocalPosition))
                return false;
            Vector3 forward = camera.transform.forward;
            if (!IsFinite(forward.sqrMagnitude) || forward.sqrMagnitude <= 1e-12f)
                return false;
            Vector3 candidate = cameraLocalPosition + forward * distance;
            if (!IsFinite(candidate))
                return false;
            point = new Vec3(candidate.x, candidate.y, candidate.z);
            return true;
        }

        private bool TryGetLocalPosition(MapPos mp, out Vector3 localPosition)
        {
            localPosition = default;
            if (MapControl.SystemMap == null || mp == null || mp.node == null
                || mp.node.GetNativeReference() == IntPtr.Zero)
                return false;
            var position = new Vector3((float)mp.position.x, (float)mp.position.y, (float)mp.position.z);
            if (!IsFinite(position))
                return false;
            var transform = NodeUtils.FindFirstGameObjectTransform(mp.node.GetNativeReference());
            if (transform == null)
                return false;
            Vector3 candidate = transform.TransformPoint(position);
            if (!IsFinite(candidate))
                return false;
            localPosition = candidate;
            return true;
        }

        private bool TrySetCameraMapPosition(CartPos position)
        {
            if (MapControl.SystemMap == null || !IsFinite(new Vec3D(position.X, position.Y, position.Z)))
                return false;
            if (_mapPos == null)
                _mapPos = new MapPos();
            _mapPos.node = null;
            return _mapPos.SetCartPos(position.X, position.Y, position.Z);
        }

        private static bool IsFinite(Vec3D position)
        {
            return !double.IsNaN(position.x)
                && !double.IsInfinity(position.x)
                && !double.IsNaN(position.y)
                && !double.IsInfinity(position.y)
                && !double.IsNaN(position.z)
                && !double.IsInfinity(position.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}
