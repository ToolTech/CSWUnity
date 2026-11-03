/*
 * Copyright (C) SAAB AB
 *
 * All rights, including the copyright, to the computer program(s)
 * herein belong to Saab AB. The program(s) may be used and/or
 * copied only with the written permission of Saab AB, or in
 * accordance with the terms and conditions stipulated in the
 * agreement/contract under which the program(s) have been
 * supplied.
 *
 * Information Class:          COMPANY RESTRICTED
 * Defence Secrecy:            UNCLASSIFIED
 * Export Control:             NOT EXPORT CONTROLLED
 */

using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using GizmoSDK.Coordinate;

namespace GizmoSDK.Coordinate
{
    public readonly struct CartPos
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public CartPos(double x, double y, double z) { X = x; Y = y; Z = z; }
    }
}

namespace GizmoSDK.Gizmo3D
{
    public enum IntersectQuery { NULL, INTERSECT }
    public sealed class Node
    {
        public IntPtr Reference = new IntPtr(1);
        public IntPtr GetNativeReference() => Reference;
    }
}

namespace Saab.Foundation.Map
{
    [Flags]
    public enum GroundClampType { NONE, GROUND = 1, BUILDING = 2 }
    [Flags]
    public enum ClampFlags { NONE, FRUSTRUM_CULL = 1 << 3, DEFAULT = FRUSTRUM_CULL }

    // Only the native query surface is substituted; navigation policy and geometry are production code.
    public sealed class MapControl
    {
        public static MapControl SystemMap = null!;
        public MapType MapType = MapType.UTM;
        public Func<Vec3D, Vec3, GroundClampType, ClampFlags, (bool, MapPos)> Terrain = null!;
        public Func<int, int, uint, uint, (bool, Vec3D, Vec3)> Ray = null!;
        public Func<Vec3D, (bool, CartPos)> ToWorld = null!;
        public Func<MapPos, CartPos, bool> PositionFromWorld = null!;

        public bool GetScreenGroundPosition(
            int x, int y, uint width, uint height, out MapPos result,
            GroundClampType clampType, ClampFlags flags = ClampFlags.DEFAULT)
        {
            if (!GetScreenVectors(x, y, width, height, out Vec3D origin, out Vec3 direction))
            {
                result = null!;
                return false;
            }
            return GetGroundPosition(origin, direction, out result, clampType, flags);
        }

        public bool GetGroundPosition(
            Vec3D origin, Vec3 direction, out MapPos result, GroundClampType clampType,
            ClampFlags flags = ClampFlags.DEFAULT)
        {
            (bool success, result) = Terrain(origin, direction, clampType, flags);
            return success;
        }

        public bool GlobalToWorld(Vec3D position, out CartPos result)
        {
            (bool success, result) = ToWorld(position);
            return success;
        }

        public bool SetPosition(MapPos position, CartPos cart, GroundClampType type, ClampFlags flags)
            => PositionFromWorld(position, cart);

        public bool GetScreenVectors(int x, int y, uint width, uint height, out Vec3D origin, out Vec3 direction)
        {
            (bool success, origin, direction) = Ray(x, y, width, height);
            return success;
        }
    }

    public sealed class MapPos
    {
        public Vec3D position;
        public Node? node;
        public IntersectQuery clampResult;
        public Func<Vec3D>? ToGlobal;
        public Vec3D GlobalPosition() => ToGlobal?.Invoke() ?? position;
        public bool SetCartPos(double x, double y, double z)
            => MapControl.SystemMap != null
                && MapControl.SystemMap.SetPosition(this, new CartPos(x, y, z), GroundClampType.NONE, ClampFlags.NONE);
    }
}
