//******************************************************************************
//
// Copyright (C) SAAB AB
//
// All rights, including the copyright, to the computer program(s) 
// herein belong to SAAB AB. The program(s) may be used and/or
// copied only with the written permission of SAAB AB, or in
// accordance with the terms and conditions stipulated in the
// agreement/contract under which the program(s) have been
// supplied. 
//
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
//
// File			: MapPos.cs
// Module		: Saab.Foundation.Map.Manager
// Description	: Definition of the MapPos map position structure
// Author		: Anders Modén		
// Product		: Gizmo3D 2.12.326
//
// NOTE:	Gizmo3D is a high performance 3D Scene Graph and effect visualisation 
//			C++ toolkit for Linux, Mac OS X, Windows, Android, iOS and HoloLens for  
//			usage in Game or VisSim development.
//
//
// Revision History...						
//									
// Who	Date	Description						
//									
// AMO	180301	Created file 	
//
//******************************************************************************


using GizmoSDK.Coordinate;
using GizmoSDK.Gizmo3D;
using GizmoSDK.GizmoBase;
using System;

namespace Saab.Foundation.Map
{
    //------------------ Coordinates -------------------------------------------------------------------------------------------------------------
    //
    // The map exposes three coordinate systems
    //
    // 1.   The World Coordinate System. All coodinates are in LatPos or CartPos double precision   (LatPos, CartPos)
    //      Always a geocentric system
    //
    // 2.   The Global coordinate system. All coordnates are in X,Y,Z RightON Handed                (MapPos Global)
    //      The interior database is stored in this level or transformed here                      
    //      Can be flat UTM, Flat Projected or Spherical Cartesian (or other) (uses _origin)
    //
    // 3.   The Local coordnate system.                                                             (MapPos Local)
    //      Has a local origin and local normals ENU (East, North,Up)
    //      In UTM its aligned with X to the east, Y up and Z to the south
    //      In Sperical its aligned with XYZ to Cartesian Coordnates
    //
    // 4.   The local ENU (East-North-Up) coordinate system.
    //      A MapPos local coordnate is given in a fixed 3D frame aligned with Unitys coordnate system
    //      but with flipped Z axis. In this the east,north,up vectors might be aligned in another direction
    //      A ENU transform can be performed on a local MapPos to get it to ENU MapPos              (MapPos ENU)
    //
    //  In Unity you have a Left ON system and with a Z into the screen so be careful using Unitys quarternios etc.
    //  Positioning is made under the Top RoiNode and it has a fliped Z transform so in under this node you dont need to take the flip into account
    //
    //-----------------------------------------------------------------------------------------------------------------------------------------------

    [Flags]
    public enum MapPositionProperty
    {
        Normal,
        Position,
        Rotation
    }
       

    public interface IMapPosition<TContext>
    {
        bool IsUpdated(MapPositionProperty prop, bool clearUpdate = true);
        void ClearUpdate(MapPositionProperty prop);

        Vec3 LocalPosition { get; }         // local position

        Quaternion Rotation { get; }        // orientation transform

        Vec3 Orientation { get; set; }      // ENU orientation (Yaw,Pitch,Roll)

        Vec3 Normal { get; set; }           // External or ground based normal

        TContext Context { get; }
    }


    public class MapPos : IMapPosition<Node>
    {
        public Node                 node;                   // Local Context, Can be null for Global context
        public Vec3D                position;               // Relative position to context in double precision

        // Internal variables

        
        public Vec3                  normal;                // Normal in local coordinate system
        public Matrix3               local_orientation;     // East North Up base matrix
        protected Vec3               euler_enu;             // Yaw, Pitch, Roll - euler angles around ENU in (yaw-U),(pitch-E),(roll-N) right ON
        public Vec3D                 a, b, c;             // Place on triangle
        public double t;                                    // Time triangle was detected

        public IntersectQuery       clampResult;          // non zero if this position is clamped
        public GroundClampType      clampType;
        public ClampFlags           clampFlags;

        public MapPositionProperty  updated;

        public readonly static Matrix3 BodyToENU = new Matrix3(new Vec3(1, 0, 0), new Vec3(0, 0, 1), new Vec3(0, -1, 0));

        public bool IsLocal()
        {
            return node != null;
        }

        public bool IsGlobal()
        {
            return node == null;
        }

        public Node Context
        {
            get
            {
                return node;
            }
        }

        public Vec3 LocalPosition
        {
            get
            {
                if (IsLocal())
                    return (Vec3)position;
                else
                    throw new SystemException("Not a local position");
            }
        }

        public Vec3D GlobalPosition(Vec3 enu_offset = default)
        {
            Vec3D result = position;

            RoiNode roi = node as RoiNode;

            // If we are a roi node based position we add roi position as local origin

            if (roi != null && roi.IsValid())
            {
                result += roi.Position;
            }

            result += local_orientation * enu_offset;

            return result;
        }

        public Quaternion Rotation
        {
            get { return (EnuToLocal() * Matrix3.CreateFrom_Euler_ZXY(euler_enu.x, euler_enu.y, euler_enu.z)* BodyToENU).Quaternion(); }
        }

        public Vec3 Orientation
        {
            get { return euler_enu; }
            set { euler_enu = value; }
        }

        public Vec3 Normal
        {
            get { return normal; }
            set { normal = value; }
        }

        public Matrix3 EnuToLocal()
        {
            Vec3 up;                                    // up in local coordinate system

            if (normal.LengthSq2() != 0)                // Use normal as up
                up = normal;
            else 
                up = local_orientation.GetCol(2);       // If no normal use 
           
            Vec3 east = local_orientation.GetCol(0);

            east = Vec3.Orthogonal(east,up);            // East will be orthogonal to up in east direction
            Vec3 north = up.Cross(east);                     // North will be orthogonal to east and up
  
            return new Matrix3(east, north, up);
        }

        public Matrix3 LocalToEnu()
        {
            return EnuToLocal().Transpose();
        }

        public bool SetLatPos(double lat, double lon, double alt)
        {
            var mapControl = MapControl.SystemMap;

            if (mapControl == null)
            {
                return false;
            }

            return mapControl.SetPosition(this, new LatPos(lat,lon, alt), clampType, clampFlags);
        }

        public bool SetCartPos(double x, double y, double z)
        {
            var mapControl = MapControl.SystemMap;

            if (mapControl == null)
            {
                return false;
            }

            return mapControl.SetPosition(this, new CartPos(x, y, z),clampType, clampFlags);

        }

        public LatPos MoveAlongEllipsoid(LatPos start, double metersEast, double metersNorth)
        {
            // WGS84 radii of curvature
            const double a = 6378137.0;
            const double e2 = 6.69437999014e-3;

            double sinLat = Math.Sin(start.Latitude);
            double cosLat = Math.Cos(start.Latitude);

            double denom = Math.Sqrt(1.0 - e2 * sinLat * sinLat);
            double N = a / denom;
            double M = a * (1.0 - e2) / (denom * denom * denom);

            // Apply local ENU motion while preserving altitude
            double dLat = metersNorth / (M + start.Altitude);
            double dLon = metersEast / ((N + start.Altitude) * Math.Max(1e-12, cosLat));

            return new LatPos(start.Latitude + dLat, start.Longitude + dLon, start.Altitude);
        }

        public (Vec3 e, Vec3 n, Vec3 u) GetEnuBasisVectors(bool unityCS = false)
        {
            var enu = EnuToLocal();

            var east = enu * new Vec3(1, 0, 0);
            var north = enu * new Vec3(0, 1, 0);
            var up = enu * new Vec3(0, 0, 1);

            if (unityCS)
            {
                east.z = -east.z;
                north.z = -north.z;
                up.z = -up.z;
            }

            return (east, north, up);
        }

        public bool UpdatePosition()
        {
            var mapControl = MapControl.SystemMap;

            if (mapControl == null)
            {
                return false;
            }

            if (clampFlags == ClampFlags.NONE)
            {
                return false;
            }

            mapControl.UpdatePosition(this, clampType, clampFlags);

            return true;
        }

        public bool IsUpdated(MapPositionProperty prop, bool clearUpdate = true)
        {
            bool result = updated.HasFlag(prop);

            if (clearUpdate)
                updated &= ~prop;

            return result;
        }

        public void ClearUpdate(MapPositionProperty prop)
        {
            updated &= ~prop;
        }
    }
}

