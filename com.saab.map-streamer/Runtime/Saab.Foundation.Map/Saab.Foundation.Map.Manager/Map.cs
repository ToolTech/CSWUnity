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
// File			: Manager.cs
// Module		: Saab.Foundation.Map.Manager
// Description	: Map Manager of maps in BTA
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
// AMO  191023  Fixed issue in double position in screenvectors
// AMO  240412  Added META info about LOD, Size and Extent            (2.12.143)
//
//******************************************************************************

using System;

using GizmoSDK.Gizmo3D;
using GizmoSDK.GizmoBase;
using GizmoSDK.Coordinate;

namespace Saab.Foundation.Map
{
    public enum MapType
    {
        UNKNOWN,
        PLAIN,
        UTM,
        GEOCENTRIC,
        PROJ_SWEREF99,
    }

    [Flags]
    public enum GroundClampType
    {
        NONE,
        GROUND      = 1<<0,
        BUILDING    = 1<<1,
    }

    [Flags]
    public enum ClampFlags
    {
        NONE,               
        ALIGN_NORMAL_TO_SURFACE = 1<<0,
        WAIT_FOR_DATA           = 1<<1,
        ISECT_LOD_QUALITY       = 1<<2,
        FRUSTRUM_CULL           = 1<<3,    
        UPDATE_DATA             = 1<<4,
        CONSTRAIN_SURFACE       = 1<<5,

        DEFAULT = FRUSTRUM_CULL,
    }

    public class MapControl
    {
        // Constants for GZ_DB_INFO_PROJECTION ---------------------------------------------------------------------------------------------

        const string GZ_DB_INFO_PROJECTION_FLAT           = "Flat Earth";
        const string GZ_DB_INFO_PROJECTION_SPHERE         = "Sphere";
        const string GZ_DB_INFO_PROJECTION_TRAPEZODIAL    = "Trapezoidal";
        const string GZ_DB_INFO_PROJECTION_LAMBERT        = "Lambert";
        const string GZ_DB_INFO_PROJECTION_UTM            = "UTM";
        const string GZ_DB_INFO_PROJECTION_RT90           = "RT90";
        const string GZ_DB_INFO_PROJECTION_SWEREF99       = "SWEREF99";
        const string GZ_DB_INFO_PROJECTION_PROJECTED      = "Projected";

        // Attribute names -----------------------------------------------------------------------------------------------------------------

        const string GZ_DB_INFO_METER_SCALE     = "DbI-MeterScale";             // Number to scale model to meters approx
        const string GZ_DB_INFO_PROJECTION      = "DbI-Projection";             // GZ_DB_INFO_PROJECTION_xx
        const string GZ_DB_INFO_ELLIPSOID       = "DbI-Ellipsoid";
        const string GZ_DB_INFO_COORD_SYS       = "DbI-CoordSystem";			// CoordSystem String
              
        const string GZ_DB_INFO_DB_ORIGIN_POS   = "DbI-Database Origin";        // Depends on GZ_DB_INFO_COORD_SYS
        const string GZ_DB_INFO_DB_SW_POS       = "DbI-Database SWpos";         // gzAttribute_LatPos in radians
        const string GZ_DB_INFO_DB_NE_POS       = "DbI-Database NEpos";         // gzAttribute_LatPos in radians

        const string GZ_DB_INFO_DB_SIZE         = "DbI-SZ";				        // gzAttribute_DBSize

        // Max Lod distance from loaded db

        const string GZ_DB_INFO_DB_MAX_LOD_RANGE = "DbI-LR";				// Meters


        const double MAX_TRIANGLE_CACHE_AGE = 3.0;


        public delegate void EventHandler_MapInfo(string url,MapType type,Node root);
        public event EventHandler_MapInfo OnMapInfo;

        private readonly Coordinate _converter = new Coordinate();

        public MapControl()
        {
            Reset();
        }

        public void Reset()
        {
            try
            {
                NodeLock.WaitLockEdit();        // All change of map parameters shall be done in locked edit mode

                _mapType = MapType.UNKNOWN;

                _topRoi = null;
                _currentMap = null;
                _nodeURL = null;

                _origin = new Vec3D(0, 0, 0);
                _metaData = new CoordinateSystemMetaData();

                _coordSystem = new CoordinateSystem();

            }
            finally
            {
                NodeLock.UnLock();
            }
        }

        #region ------------------------ Screen Functions --------------------------------------

        /// <summary>
        /// Get Global position and global direction for a screen coordinate
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="size_x"></param>
        /// <param name="size_y"></param>
        /// <param name="position"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public bool GetScreenVectors(int x,int y,uint size_x,uint size_y,out Vec3D position,out Vec3 direction)
        {
            position = new Vec3D();
            direction = new Vec3();

            if (_camera== null || !_camera.IsValid())
                return false;

            Camera.GetScreenVectors(x, y, size_x, size_y, out position, out direction);

            return true;
        }

        /// <summary>
        /// Get local ground position using intersector in screen coordinates
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="size_x"></param>
        /// <param name="size_y"></param>
        /// <param name="result"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public bool GetScreenGroundPosition(int x, int y, uint size_x, uint size_y, out MapPos result, 
            GroundClampType clampType, ClampFlags flags = ClampFlags.DEFAULT)
        {
            Vec3D position;
            Vec3 direction;


            if (!GetScreenVectors(x, y, size_x, size_y, out position, out direction))
            {
                result = null;
                return false;
            }

            return GetGroundPosition(position, direction, out result, clampType, flags);
        }

        #endregion

        #region ------------------------ Local/Global ------------------------------------------

        /// <summary>
        /// Convert a local mappos to a global 3D position with ENU offset
        /// </summary>
        /// <param name="mappos"></param>
        /// <param name="enu_offset"></param>
        /// <returns></returns>
        public Vec3D LocalToGlobal(MapPos mappos, Vec3 enu_offset = default)
        {
            return mappos.GlobalPosition(enu_offset);
        }

        public MapPos GlobalToLocal(Vec3D global_position)
        {
            MapPos result = new MapPos();

            result.position = global_position;

            result.local_orientation = GetLocalOrientation(global_position);

            ToLocal(result);

            return result;
        }

        #endregion

        #region ------------------------ Global/World ------------------------------------------

        public bool GlobalToWorld(Vec3D global_position, out LatPos result)
        {
            // TODO:Coordinate.GetGlobalCoordinate(global_position+_origin, _coordSystem, _metaData, out LatPos lp);

            if (!GlobalToWorld(global_position))
            {
                result = default;
                return false;
            }

            return _converter.GetLatPos(out result);
        }

        public bool GlobalToWorld(Vec3D global_position, out CartPos result)
        {
            if (!GlobalToWorld(global_position))
            {
                result = default;
                return false;
            }

            return _converter.GetCartPos(out result);
        }

        private bool WorldToGlobal(ref Vec3D pos, ref Matrix3 orientationMatrix)
        {

            // Convert to global 3D coordinate in appropriate target system

            switch (_mapType)
            {
                case MapType.UTM:
                    {
                        UTMPos utmpos;

                        if (!_converter.GetUTMPos(out utmpos))
                        {
                            Message.Send("Controller", MessageLevel.WARNING, "Failed to convert to UTM");
                            return false;
                        }

                        pos = new Vec3D(utmpos.Easting, utmpos.H, -utmpos.Northing) - _origin;

                    }
                    break;

                case MapType.GEOCENTRIC:
                    {
                        CartPos cartpos;

                        if (!_converter.GetCartPos(out cartpos))
                        {
                            Message.Send("Controller", MessageLevel.WARNING, "Failed to convert to Geocentric");
                            return false;
                        }

                        pos = new Vec3D(cartpos.X, cartpos.Y, cartpos.Z) - _origin;
                    }
                    break;

                case MapType.PROJ_SWEREF99:
                    {
                        ProjPos projpos;

                        if (!_converter.GetProjPos(out projpos, FlatGaussProjection.SWEREF99))
                        {
                            Message.Send("Controller", MessageLevel.WARNING, "Failed to convert to Projected");
                            return false;
                        }

                        pos = new Vec3D(projpos.Y, projpos.H, -projpos.X) - _origin;
                    }
                    break;

                default:
                    return false;
            }

            // Set up orientation

            orientationMatrix = GetLocalOrientation(pos);

            return true;
        }

        public bool GlobalToWorld(Vec3D position)
        {
            switch (_mapType)
            {
                case MapType.UNKNOWN:
                    {
                        return false;
                    }

                case MapType.UTM:

                    UTMPos utmpos = new UTMPos(_metaData.Zone(),_metaData.North(), -(position.z + _origin.z), position.x + _origin.x, position.y + _origin.y);

                    _converter.SetUTMPos(utmpos);

                    break;

                case MapType.GEOCENTRIC:

                    CartPos cartpos = new CartPos(position.x + _origin.x, position.y + _origin.y, position.z + _origin.z);

                    _converter.SetCartPos(cartpos);

                    break;

                case MapType.PROJ_SWEREF99:

                    ProjPos projpos = new ProjPos(-(position.z + _origin.z), position.x + _origin.x, position.y + _origin.y);

                    _converter.SetProjPos(projpos, FlatGaussProjection.SWEREF99);

                    break;
                
                default:
                    return false;
            }

            return true;
        }

        #endregion

        #region ------------------------ Local/World -------------------------------------------

        public bool LocalToWorld(MapPos pos, out LatPos result)
        {
            Vec3D position = pos.position;

            RoiNode roi = pos.node as RoiNode;

            if (roi != null && roi.IsValid())   // Convert to a global position
                position += roi.Position;

            return GlobalToWorld(position, out result);
        }

        public bool LocalToWorld(MapPos pos, out CartPos result)
        {
            Vec3D position = pos.position;

            RoiNode roi = pos.node as RoiNode;

            if (roi != null && roi.IsValid())   // Convert to a global position
                position += roi.Position;

            return GlobalToWorld(position, out result);
        }

        #endregion

        #region ------------------------ Intersector Queries -----------------------------------

        /// <summary>
        /// Get a Local mappos in Global ray direction
        /// </summary>
        /// <param name="global_position"></param>
        /// <param name="direction"></param>
        /// <param name="result"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public bool GetGroundPosition(Vec3D global_position, Vec3 direction, out MapPos result,
            GroundClampType clampType, ClampFlags flags = ClampFlags.DEFAULT)
        {
            result = new MapPos();

            if (_currentMap == null)
                return false;

            // Coordinate is now in world Cartesian coordinates (Roi Position)

            Vec3D origo = new Vec3D(0, 0, 0);       // Set used origo for clamp operation

            Intersector isect = new Intersector();

            var mask = GetMask(clampType);
            isect.IntersectMask=mask;  // Lets hit the ground

            // Check camera frustrum -----------------------------------------------------------

            if (_camera != null && _camera.IsValid())
            {
                origo = _camera.Position;

                if ((flags & ClampFlags.FRUSTRUM_CULL) != 0)
                    isect.SetCamera(_camera);
            }

            // Adjust intersector to use origo as center
            global_position = global_position - origo;

            isect.StartPosition=(Vec3)global_position;
            isect.Direction=direction;

            if (isect.Intersect(_currentMap,  IntersectQuery.ABC_TRI | 
                                                    IntersectQuery.NEAREST_POINT |
                                                     (flags.HasFlag(ClampFlags.ALIGN_NORMAL_TO_SURFACE) ? IntersectQuery.NORMAL : 0) | //IntersectQuery.NORMAL | 
                                                    (flags.HasFlag(ClampFlags.WAIT_FOR_DATA) ? IntersectQuery.WAIT_FOR_DYNAMIC_DATA : 0),
                                                    LodFactor, true, origo))
            {
                IntersectorResult res = isect.GetResult();

                IntersectorData data = res.GetData(0);

                

                result.position = data.coordinate + origo;

                if ((data.resultMask & IntersectQuery.NORMAL) != 0)
                    result.normal = data.normal;

                if (data.resultMask.HasFlag(IntersectQuery.ABC_TRI))
                {
                    result.a = data.a + origo;
                    result.b = data.b + origo;
                    result.c = data.c + origo;
                    result.t = Time.SystemSeconds + MAX_TRIANGLE_CACHE_AGE;
                }

                result.clampResult = data.resultMask;
            }
            else
                result.clampResult = IntersectQuery.NULL;

            isect.Dispose();   // Drop handle and ignore GC

            result.local_orientation = GetLocalOrientation(result.position);

            if (result.clampResult == IntersectQuery.NULL)
                result.normal = result.local_orientation.GetCol(2);

            return ToLocal(result);
        }

        public double GetAltitude(LatPos pos, ClampFlags flags = ClampFlags.DEFAULT)
        {
            MapPos mapPos;

            if (!GetPosition(pos, out mapPos, GroundClampType.GROUND, flags))
                return 0;

            LatPos updatedPos;

            if (!LocalToWorld(mapPos, out updatedPos))
                return 0;

            return updatedPos.Altitude;
        }

        public static bool Intersect(Vec3D origin, Vec3D direction, Vec3D V0, Vec3D V1, Vec3D V2, out Vec3D p)
        {
            // Möller-Trumbore algorithm

            const double EPSILON = 1e-8;

            Vec3D E1 = V1 - V0;
            Vec3D E2 = V2 - V0;

            Vec3D P = direction.Cross(E2);

            double det = P.Dot(E1);

            // ray and triangle are parallel if det is close to 0
            if (det > -EPSILON && det < EPSILON)
            {
                p = default;
                return false;
            }

            double invDet = 1.0 / det;

            Vec3D T = origin - V0;

            double u = P.Dot(T) * invDet;

            if (u < 0 || u > 1)
            {
                p = default;
                return false;
            }

            Vec3D Q = T.Cross(E1);

            double v = Q.Dot(direction) * invDet;

            if (v < 0 || (u + v) > 1)
            {
                p = default;
                return false;
            }

            double t = Q.Dot(E2) * invDet;

            p = origin + t * direction;
            return true;
        }

        public static bool IntersectPlane(Vec3D origin, Vec3D direction, Vec3D V0, Vec3D V1, Vec3D V2, out Vec3D p)
        {
            const double EPSILON = 1e-8;

            Vec3D E1 = V1 - V0;
            Vec3D E2 = V2 - V0;

            Vec3D N = E1.Cross(E2);
            N.Normalize();

            double d = -Vec3D.Dot(N, V0);

            double denom = Vec3D.Dot(N, direction);

            // ray and plane are parallel if denom is close to 0
            if (denom > -EPSILON && denom < EPSILON)
            {
                p = default;
                return false;
            }

            double t = -(Vec3D.Dot(N, origin) + d) / denom;

            p = origin + t * direction;
            return true;
        }

        public bool UpdatePosition(MapPos result, GroundClampType groundClamp = GroundClampType.NONE, ClampFlags flags = ClampFlags.DEFAULT)
        {
            if (_currentMap == null)    // No map
                return false;

            var normal = result.normal;
            
            result.normal = result.local_orientation.GetCol(2);

            if (groundClamp != GroundClampType.NONE)
            {
                

                // Add ROINode position as offset   - Go to global 3D coordinate system as we need to clamp in global 3D

                RoiNode roi = result.node as RoiNode;

                if (roi != null && roi.IsValid())
                    result.position += roi.Position;

                // store current global position
                var globalPos = result.position;
                

                // The defined down vector

                Vec3 down = -result.normal;

                // Check triangel ground

                if (result.clampResult.HasFlag(IntersectQuery.ABC_TRI))
                {
                    // todo: temporary fix to force update after 3 seconds, actual fix will need map to notify objects
                    // that LOD has been changed!
                    var timeout = Time.SystemSeconds > result.t;

                    if (!timeout && Intersect(result.position, down, result.a, result.b, result.c, out Vec3D p))
                    {
                        result.position = p;
                        result.normal = normal;
                        ToLocal(result);

                        return true;
                    }
                }

                // Check new intersector


                Intersector isect = new Intersector();

                Vec3D origo = new Vec3D(0, 0, 0);

                // Check camera frustrum -----------------------------------------------------------

                if (_camera != null && _camera.IsValid())
                {
                    origo = _camera.Position;

                    if ((flags & ClampFlags.FRUSTRUM_CULL) != 0)
                        isect.SetCamera(_camera);
                }

                if ((flags & ClampFlags.ISECT_LOD_QUALITY) != 0)                // Lets stand in the ray to get highest quality
                    origo = result.position;


                isect.StartPosition=(Vec3)(result.position - origo) - 10000.0f * down;  // Move backwards
                isect.Direction=down;
                isect.IntersectMask=GetMask(groundClamp);

                if (isect.Intersect(_currentMap, IntersectQuery.NEAREST_POINT | IntersectQuery.ABC_TRI |
                                                    (flags.HasFlag(ClampFlags.ALIGN_NORMAL_TO_SURFACE) ? IntersectQuery.NORMAL : 0) |
                                                    (flags.HasFlag(ClampFlags.WAIT_FOR_DATA) ? IntersectQuery.WAIT_FOR_DYNAMIC_DATA : 0) |
                                                    (flags.HasFlag(ClampFlags.UPDATE_DATA) ? IntersectQuery.UPDATE_DYNAMIC_DATA : 0)
                                                    , LodFactor, true, origo))
                {
                    IntersectorResult res = isect.GetResult();

                    IntersectorData data = res.GetData(0);

                    result.position = data.coordinate + origo;

                    if ((data.resultMask & IntersectQuery.NORMAL) != 0)
                        result.normal = data.normal;

                    if (data.resultMask.HasFlag(IntersectQuery.ABC_TRI))
                    {
                        result.a = data.a + origo;
                        result.b = data.b + origo;
                        result.c = data.c + origo;
                        result.t = Time.SystemSeconds + MAX_TRIANGLE_CACHE_AGE;
                    }

                    result.clampResult = data.resultMask;
                }
                else
                {
                    // we have failed to clamp, use earlier triangle as ground-plane
                    if (result.clampResult.HasFlag(IntersectQuery.ABC_TRI))
                    {
                        IntersectPlane(globalPos, down, result.a, result.b, result.c, out result.position);
                        result.clampResult = IntersectQuery.ABC_TRI | IntersectQuery.NORMAL;
                        result.t = Time.SystemSeconds + MAX_TRIANGLE_CACHE_AGE;
                    }
                    else
                        result.clampResult = IntersectQuery.NULL;
                }

                //if (groundClamp == GroundClampType.GROUND)
                //{
                //    result.normal = result.local_orientation.GetCol(2);
                //}


                isect.Dispose();    // Drop handle and ignore GC

                // Remove ROINode position as offset - Go to local coordinate system under ROI Node

                ToLocal(result);
            }

            result.clampFlags = flags;
            result.clampType = groundClamp;

            return true;
        }

        #endregion

        private static IntersectMaskValue GetMask(GroundClampType clampType)
        {
            var mask = IntersectMaskValue.NOTHING;

            if ((clampType & GroundClampType.GROUND) != GroundClampType.NONE)
                mask |= IntersectMaskValue.GROUND;
            
            if ((clampType & GroundClampType.BUILDING) != GroundClampType.NONE)
                mask |= IntersectMaskValue.BUILDING;

            return mask;
        }

        /// <summary>
        /// Get local ENU orientation matrix for Global Position Vec3D
        /// </summary>
        /// <param name="global_position"></param>
        /// <returns></returns>
        public Matrix3 GetLocalOrientation(Vec3D global_position)
        {
            switch (_mapType)
            {
                default:
                    {
                        return new Matrix3();
                    }

                case MapType.UTM:
                    {
                        UTMPos utmpos = new UTMPos(
                            _metaData.Zone(),
                            _metaData.North(),
                            -(global_position.z + _origin.z),
                            global_position.x + _origin.x,
                            global_position.y + _origin.y);

                        return Coordinate.GetOrientationMatrix(utmpos);
                    }

                case MapType.PROJ_SWEREF99:
                    {
                        ProjPos projpos = new ProjPos(
                            -(global_position.z + _origin.z),
                            global_position.x + _origin.x,
                            global_position.y + _origin.y);

                        return Coordinate.GetOrientationMatrix(projpos, FlatGaussProjection.SWEREF99);
                    }

                case MapType.GEOCENTRIC:

                    return Coordinate.GetOrientationMatrix(new CartPos(global_position.x+_origin.x,global_position.y+_origin.y, global_position.z+_origin.z));
            }
        }

        /// <summary>
        /// Get local ENU orientation matrix for LatPos
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public Matrix3 GetLocalOrientation(LatPos pos)
        {
            _converter.SetLatPos(pos);

            switch (_mapType)
            {
                default:
                    {
                        return new Matrix3();
                    }

                case MapType.UTM:
                    {
                        UTMPos utmpos;

                        if (!_converter.GetUTMPos(out utmpos))
                        {
                            Message.Send("Controller", MessageLevel.WARNING, "Failed to convert to UTM");
                            return new Matrix3();
                        }

                        return Coordinate.GetOrientationMatrix(utmpos);
                    }

                case MapType.PROJ_SWEREF99:
                    {
                        ProjPos projpos;

                        if (!_converter.GetProjPos(out projpos, FlatGaussProjection.SWEREF99))
                        {
                            Message.Send("Controller", MessageLevel.WARNING, "Failed to convert to Projected");
                            return new Matrix3();
                        }

                        return Coordinate.GetOrientationMatrix(projpos, FlatGaussProjection.SWEREF99);
                    }

                case MapType.GEOCENTRIC:

                    CartPos cartpos;

                    if (!_converter.GetCartPos(out cartpos))
                    {
                        Message.Send("Controller", MessageLevel.WARNING, "Failed to convert to Geocentric");
                        return new Matrix3();
                    }

                    return  Coordinate.GetOrientationMatrix(cartpos);
            }
        }

        /// <summary>
        /// Converts a global mappos to a local under a roi
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool ToLocal(MapPos result)
        {

            if (result.node != null)        // Already local
                return true;

            if (_topRoi == null)
                return false;
            
            RoiNode roi = _topRoi.GetClosestRoiNode(result.position);
            result.node = roi;

            // Remove roiNode position as offset - Go to local RoiNode based coordinate system

            if (roi != null && roi.IsValid())
            {
                result.position -= roi.Position;
                
                return true;
            }
            return false;   // We failed to convert
        }

        /// <summary>
        /// Converts a local mappos under a roi to a global position
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool ToGlobal(MapPos result)
        {
            if (result.node == null)    // Already global
                return true;

            // Remove roiNode position as offset - Go to local RoiNode based coordinate system

            RoiNode roi = result.node as RoiNode;

            if (roi != null && roi.IsValid())
            {
                result.position += roi.Position;

                result.node = null;

                return true;
            }

            return false;   // We failed to convert
        }


        public bool SetPosition(MapPos result, LatPos pos, GroundClampType groundClamp = GroundClampType.NONE, ClampFlags flags = ClampFlags.DEFAULT)
        {
            _converter.SetLatPos(pos);

            return SetPositionInternal(result, groundClamp, flags);
        }

        public bool SetPosition(MapPos result, CartPos pos, GroundClampType groundClamp = GroundClampType.NONE, ClampFlags flags = ClampFlags.DEFAULT)
        {
            _converter.SetCartPos(pos);

            return SetPositionInternal(result, groundClamp, flags);
        }

        private bool SetPositionInternal(MapPos result, GroundClampType groundClamp, ClampFlags flags)
        {
            if (!WorldToGlobal(ref result.position, ref result.local_orientation))
                return false;

            // optimized path when not handling surface constraint
            if (!flags.HasFlag(ClampFlags.CONSTRAIN_SURFACE))
            {
                ToLocal(result);

                return UpdatePosition(result, groundClamp, flags);
            }

            // store unclamped position
            Vec3D oldPos = result.position;

            ToLocal(result);

            if (!UpdatePosition(result, groundClamp, flags))
                return false;

            // get vector between unclamped and clamped position
            var delta = (Vec3)(oldPos - result.position);

            // if clamped position was below unclamped position we discard the clamped position
            if (Vec3.Dot(delta, result.normal) > 0)
                result.position = oldPos;

            return true;
        }
             

        public bool GetPosition(LatPos pos, out MapPos result,GroundClampType groundClamp= GroundClampType.NONE, ClampFlags flags = ClampFlags.DEFAULT)
        {
            result = new MapPos
            {
                local_orientation = new Matrix3(new Vec3(1, 0, 0), new Vec3(0, 0, -1), new Vec3(0, 1, 0))    // East North Up vectors
            };

            return SetPosition(result, pos, groundClamp, flags);                 
        }

        public bool GetPosition(CartPos pos, out MapPos result, GroundClampType groundClamp = GroundClampType.NONE, ClampFlags flags = ClampFlags.DEFAULT)
        {
            result = new MapPos
            {
                local_orientation = new Matrix3(new Vec3(1, 0, 0), new Vec3(0, 0, -1), new Vec3(0, 1, 0))    // East North Up vectors
            };

            return SetPosition(result, pos, groundClamp, flags);
        }


        public Vec3D Origin
        {
            get
            {
                try
                {
                    NodeLock.WaitLockEdit();

                    return _origin;
                }
                finally
                {
                    NodeLock.UnLock();
                }
            }
        }

        public Node CurrentMap
        {
            get
            {
                try
                {
                    NodeLock.WaitLockEdit();
                    return _currentMap;
                }
                finally
                {
                    NodeLock.UnLock();
                }
            }

            set
            {
                try
                {
                    NodeLock.WaitLockEdit();

                    _currentMap = value;

                    // reset to defaults
                    _converter.PrefUTMZone = -1;
                    _converter.PrefUTMHemisphere = 0;

                    if (value != null && value.IsValid() && value.HasDbInfo())
                    {
                        var projection = _currentMap.GetDbInfo(GZ_DB_INFO_PROJECTION);

                        if (projection == GZ_DB_INFO_PROJECTION_UTM)
                        {
                            _mapType = MapType.UTM;

                            UTMPos utmOrigin = _currentMap.GetDbInfo( GZ_DB_INFO_DB_ORIGIN_POS);

                            _origin = new Vec3D(utmOrigin.Easting, utmOrigin.H, -utmOrigin.Northing);

                            _metaData = new CoordinateSystemMetaData(utmOrigin.Zone, utmOrigin.North);

                            _coordSystem = new CoordinateSystem(Datum.WGS84_ELLIPSOID, FlatGaussProjection.UTM, CoordinateType.UTM);

                            // we need to set utm zone converter settings according to map settings
                            _converter.PrefUTMZone = utmOrigin.Zone;
                            _converter.PrefUTMHemisphere = utmOrigin.North ? 1 : -1;
                        }
                        else if (projection == GZ_DB_INFO_PROJECTION_FLAT)   // To run a 3D world without world coordinates
                        {
                            _mapType = MapType.PLAIN;
                            _origin = (Vec3D)_currentMap.GetDbInfo( GZ_DB_INFO_DB_ORIGIN_POS).GetVec3();

                            _metaData.value1 = 0;
                            _metaData.value2 = 0;

                            _coordSystem = new CoordinateSystem();
                        }
                        else if (projection == GZ_DB_INFO_PROJECTION_SPHERE)
                        {
                            _mapType = MapType.GEOCENTRIC;
                            CartPos cartOrigin = _currentMap.GetDbInfo( GZ_DB_INFO_DB_ORIGIN_POS);
                            _origin = new Vec3D(cartOrigin.X, cartOrigin.Y, cartOrigin.Z);

                            _metaData.value1 = 0;
                            _metaData.value2 = 0;

                            _coordSystem = new CoordinateSystem(Datum.WGS84_ELLIPSOID, FlatGaussProjection.NOT_DEFINED, CoordinateType.GEOCENTRIC);
                        }
                        else if (projection == GZ_DB_INFO_PROJECTION_SWEREF99)
                        {
                            _mapType = MapType.PROJ_SWEREF99;
                            ProjPos projOrigin = _currentMap.GetDbInfo(GZ_DB_INFO_DB_ORIGIN_POS);

                            _origin = new Vec3D(projOrigin.Y, projOrigin.H, -projOrigin.X);

                            _metaData.value1 = 0;
                            _metaData.value2 = 0;

                            // replaced if GZ_DB_INFO_COORD_SYS exists
                            _coordSystem = new CoordinateSystem(Datum.GRS80_ELLIPSOID, FlatGaussProjection.SWEREF99, CoordinateType.PROJECTED);
                        }
                        else
                        {
                            _mapType = MapType.UNKNOWN;
                            _origin = new Vec3D(0, 0, 0);
                        
                            _metaData.value1 = 0;
                            _metaData.value2 = 0;
                        
                            _coordSystem = new CoordinateSystem();
                        }

                        if(_currentMap.HasDbInfo(GZ_DB_INFO_COORD_SYS))
                            _coordSystem = new CoordinateSystem(_currentMap.GetDbInfo( GZ_DB_INFO_COORD_SYS));

                        if (_currentMap.HasDbInfo(GZ_DB_INFO_DB_MAX_LOD_RANGE))
                            _maxLODDistance = _currentMap.GetDbInfo( GZ_DB_INFO_DB_MAX_LOD_RANGE).GetNumber();
                        else
                            _maxLODDistance = 0;

                        if (_currentMap.HasDbInfo(GZ_DB_INFO_DB_NE_POS))
                            _ne_extent = _currentMap.GetDbInfo( GZ_DB_INFO_DB_NE_POS);
                        else
                            _ne_extent = new LatPos(0,0,0);

                        if (_currentMap.HasDbInfo(GZ_DB_INFO_DB_SW_POS))
                            _sw_extent = _currentMap.GetDbInfo( GZ_DB_INFO_DB_SW_POS);
                        else
                            _sw_extent = new LatPos(0, 0, 0);

                        _db_size= _currentMap.GetDbInfo( GZ_DB_INFO_DB_SIZE).AsString();

                        _topRoi = FindTopRoi(value);

                        if (_topRoi == null)   // We have no roi. We must add one
                        {
                            _topRoi = new Roi();

                            RoiNode roiNode = new RoiNode
                            {
                                LoadDistance = 8 * _maxLODDistance,
                                PurgeDistance = 10 * _maxLODDistance
                            };

                            if (_nodeURL != null)  // We have an URL
                            {
                                DynamicLoader loader = new DynamicLoader
                                {
                                    NodeURL = _nodeURL
                                };

                                roiNode.AddNode(loader);
                            }
                            else
                            {
                                roiNode.AddNode(value);
                            }


                            _topRoi.AddNode(roiNode);

                            _currentMap = _topRoi;

                            
                        }
                    }
                    else
                    {
                        _mapType = MapType.UNKNOWN;
                        _topRoi = null;
                    }

                    OnMapInfo?.Invoke(_nodeURL, _mapType, _currentMap);
                }
                finally
                {
                    NodeLock.UnLock();
                }
            }
        }

        public MapType MapType
        {
            get
            {
                try
                {
                    NodeLock.WaitLockEdit();
                    return _mapType;
                }
                finally
                {
                    NodeLock.UnLock();
                }
            }
        }

        public Camera Camera
        {
            get { return _camera; }
            set { _camera = value; }
        }

        
        public string NodeURL
        {
            get
            {
                try
                {
                    NodeLock.WaitLockEdit();
                    return _nodeURL;
                }
                finally
                {
                    NodeLock.UnLock();
                }
            }

            set
            {
                try
                {
                    NodeLock.WaitLockEdit();
                    _nodeURL = value;
                }
                finally
                {
                    NodeLock.UnLock();
                }
            }
        }

        public CoordinateSystem CoordinateSystem
        {
            get
            {
                try
                {
                    NodeLock.WaitLockEdit();
                    return _coordSystem;
                }
                finally
                {
                    NodeLock.UnLock();
                }
            }
        }

        public float LodFactor
        {
            get { return _lodFactor; }
            set { _lodFactor = value; }
        }

        public static MapControl SystemMap=new MapControl();

        public LatPos SWExtent
        {
            get
            {
                try
                {
                    NodeLock.WaitLockEdit();
                    return _sw_extent;
                }
                finally
                {
                    NodeLock.UnLock();
                }
            }
        }

        public LatPos NEExtent
        {
            get
            {
                try
                {
                    NodeLock.WaitLockEdit();
                    return _ne_extent;
                }
                finally
                {
                    NodeLock.UnLock();
                }
            }
        }

        public double MaxLODDistance
        {
            get
            {
                try
                {
                    NodeLock.WaitLockEdit();
                    return _maxLODDistance;
                }
                finally
                {
                    NodeLock.UnLock();
                }
            }
        }

        public string DBSize
        {
            get
            {
                try
                {
                    NodeLock.WaitLockEdit();
                    return _db_size;
                }
                finally
                {
                    NodeLock.UnLock();
                }
            }
        }

        #region ----- Private methods ------------------

        private Roi FindTopRoi(Node map)
        {
            // Must be called node locked

            if (map == null)
                return null;

            Roi roi = map as Roi;

            if (roi != null)
                return roi;

            Group grp = map as Group;

            if (grp != null)
            {
                foreach (Node child in grp)
                {
                    Roi sub = FindTopRoi(child);

                    if (sub != null)
                        return sub;
                }
            }

            return null;
        }

        #endregion

        #region ----- Private variables ----------------

        private MapType                     _mapType;
        private Node                        _currentMap;
        private CoordinateSystem            _coordSystem;
        
        private CoordinateSystemMetaData    _metaData;

        private Vec3D                       _origin;
        private Roi                         _topRoi;

        private double                      _maxLODDistance;
        private LatPos                      _ne_extent;
        private LatPos                      _sw_extent;

        private string                      _db_size;

        private Camera  _camera;
        private string  _nodeURL;

        private float _lodFactor = 1f;
        

        #endregion
    }
}

//// Check attributes for map

//var projection = node.GetAttribute("UserDataDbInfo", "DbI-Projectionx");
//var origin = node.GetAttribute("UserDataDbInfo", "DbI-Database Origin");
//UTMPos utm_origin = origin;
