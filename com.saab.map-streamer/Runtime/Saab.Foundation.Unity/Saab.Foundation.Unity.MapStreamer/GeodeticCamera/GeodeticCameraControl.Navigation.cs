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
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
//
// File			: GeodeticCameraControl.Navigation.cs
// Module		:
// Description	: Manages camera updates with large coordinates.
//              : Translates and rotates the camera geodetically. (e.g. the camera won't fly off into space as it moves towards the horizon)
// Author		: Daniel Sahlin
//
// Revision History...
//
// Who	    Date	Description
//
// DSA	    261023	Created file
//
//******************************************************************************



using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;
using Saab.Foundation.Map;
using Saab.Unity.Extensions;
using Saab.Utility.Unity.NodeUtils;
using System;
using UnityEngine;
using GizmoQuaternion = GizmoSDK.GizmoBase.Quaternion;
using GizmoVector3 = GizmoSDK.GizmoBase.Vec3;
using Quaternion = UnityEngine.Quaternion;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    public partial class GeodeticCameraControl
    {
        private static readonly Coordinate _converter = new();

        protected virtual bool DampenPan => false;
        protected virtual bool FaceCenterOrbit => false;

        private protected virtual void ConfigureZoom(CameraZoomGesture zoom)
        {
        }

        private protected virtual void ConfigureOrbit(OrbitGesture orbit)
        {
        }

        private bool ApplyMovementPosition(Vec3D position)
        {
            _pose ??= new CameraPose(PreparePose);
            return _pose.TrySetPose(position, ToGizmoQuaternion(GetCameraOrientation()));
        }

        private NavigationSurfaceQuery _navigationSurfaces;
        private NavigationSurfaceQuery NavigationSurfaces =>
            _navigationSurfaces ??= new(() => (Screen.width, Screen.height));
        private CameraZoomGesture _zoomGesture;
        private CameraZoomGesture ZoomGesture
        {
            get
            {
                if (_zoomGesture == null)
                {
                    _zoomGesture = new(
                        TryGetZoomReference,
                        () => GlobalPosition,
                        ApplyMovementPosition);
                    ConfigureZoom(_zoomGesture);
                }

                return _zoomGesture;
            }
        }

        private protected virtual bool TryGetZoomReference(float x, float y, out Vec3D reference)
        {
            return NavigationSurfaces.TryGetZoomReference(x, y, out reference);
        }

        /*
         * Ground drag
         */
        private GroundDragPositionAdapter _groundDragPositionAdapter;
        private GroundDragPositionAdapter GroundDragPositionAdapter =>
            _groundDragPositionAdapter ??= new(
                TryMoveEcefAlongEllipsoid,
                ReadGroundDragOrientation,
                PrepareGroundDragDestination,
                ApplyMovementPosition);
        private GroundDragGesture _groundDragGesture;
        private GroundDragGesture GroundDragGesture => _groundDragGesture ??= new(
            ReadGroundDragMap, NavigationSurfaces.TryGetGroundDragReference, NavigationSurfaces.TryGetMapRay,
            ReadGroundDragFrame, RaycastGroundDragPlane, GroundDragPositionAdapter.Apply, DampenPan);

        /*
         * Orbit
         */
        private OrbitGesture _orbitGesture;
        private OrbitGesture OrbitGesture
        {
            get
            {
                if (_orbitGesture == null)
                {
                    _orbitGesture = new(
                        ReadOrbitFrame, QueryOrbitTerrain, TryPointAlongView, TryResolvePointerPosition,
                        CalculateOrbitFacingRotation, PrepareOrbitPose, FaceCenterOrbit);
                    ConfigureOrbit(_orbitGesture);
                }

                return _orbitGesture;
            }
        }

        private static bool TrySetLocalPosition(MapPos mp, in Vector3 localPosition)
        {
            if (mp?.node == null || mp.node.GetNativeReference() == IntPtr.Zero)
                return false;

            var transform = NodeUtils.FindFirstGameObjectTransform(mp.node.GetNativeReference());
            if (transform == null)
                return false;

            Vec3D position = transform.InverseTransformPoint(localPosition).ToVec3D();
            if (!IsFinite(position))
                return false;

            mp.position = position;
            return true;
        }

        private bool ReadGroundDragMap(
            bool beginning,
            out MapType mapType,
            out Vec3D cameraPosition)
        {
            var map = MapControl.SystemMap;
            mapType = map?.MapType ?? MapType.UNKNOWN;
            cameraPosition = GlobalPosition;
            return map != null && NavigationSurfaces.IsAvailable && (!beginning || map.CurrentMap != null);
        }

        private bool ReadGroundDragFrame(Vec3D referencePosition, out GroundDragGesture.TangentFrame frame)
        {
            frame = default;
            var map = MapControl.SystemMap;
            if (!TryAcquireEnuBasis(
                    out Vec3 east,
                    out Vec3 north,
                    out Vec3 up)
                || !map.GlobalToWorld(
                    GlobalPosition,
                    out CartPos cameraPosition))
                return false;

            frame = new GroundDragGesture.TangentFrame
            {
                East = ToGizmoVector3(east.ToVector3FlipZ()),
                North = ToGizmoVector3(north.ToVector3FlipZ()),
                Up = ToGizmoVector3(up.ToVector3FlipZ()),
                ReferencePoint = ToGizmoVector3((referencePosition - GlobalPosition).ToVector3FlipZ()),
                CameraEcef = new Vec3D(cameraPosition.X, cameraPosition.Y, cameraPosition.Z)
            };
            return true;
        }

        private bool RaycastGroundDragPlane(
            float x, float y, GizmoVector3 normal, GizmoVector3 point,
            out GizmoVector3 intersection, out float incidence)
        {
            var ray = Camera.ScreenPointToRay(new Vector3(x, y, 0));
            return UnityCameraNavigationAdapter.TryIntersectGroundDragPlane(
                ray, normal, point, out intersection, out incidence);
        }

        /// <summary>
        /// Sets optional reference-distance bounds in metres; null disables either bound.
        /// </summary>
        /// <remarks>
        /// Does not move the camera or constrain direct poses. Invalid settings leave the policy unchanged.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">A bound is not positive and finite.</exception>
        /// <exception cref="ArgumentException">Minimum exceeds maximum.</exception>
        protected void ConfigureZoomDistanceLimits(double? minimumDistance, double? maximumDistance)
        {
            ZoomGesture.SetDistanceLimits(minimumDistance, maximumDistance);
        }

        /// <summary>
        /// Sets orbit elevation bounds in degrees relative to local map up.
        /// </summary>
        /// <param name="minimumPitchDegrees">Minimum elevation in degrees, or null.</param>
        /// <param name="maximumPitchDegrees">Maximum elevation in degrees, or null.</param>
        /// <remarks>
        /// Bounds must be within [-89, 89]. With both present, orbit checks local up along
        /// the arc and stops at the first elevation or upright boundary without blocking heading.
        /// Null disables upright protection and permits crossing that pole. Blocked input
        /// does not accumulate. Limits do not move the camera; out-of-range poses can move
        /// toward, but not farther from, the allowed range. Invalid settings leave the policy unchanged.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// The minimum pitch exceeds the maximum pitch.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// A bound is non-finite or outside the supported range.
        /// </exception>
        protected void ConfigureOrbitPitchLimits(
            float? minimumPitchDegrees,
            float? maximumPitchDegrees)
        {
            OrbitGesture.SetPitchLimits(minimumPitchDegrees, maximumPitchDegrees);
        }

        private protected virtual bool ReadOrbitFrame(out OrbitGesture.Frame frame)
        {
            frame = default;
            if (MapControl.SystemMap?.CurrentMap == null || !AcquireCameraEnuFrame(out var enu))
                return false;

            frame = UnityCameraNavigationAdapter.CreateOrbitFrame(
                new Vec3D(X, Y, -Z),
                GetCameraOrientation(),
                enu.e,
                enu.n,
                enu.u);
            return true;
        }

        private static GizmoQuaternion CalculateOrbitFacingRotation(GizmoVector3 forward, GizmoVector3 up)
            => UnityCameraNavigationAdapter.FaceOrbitPivot(forward, up);

        private protected virtual bool PrepareOrbitPose(
            Vec3D? resolvedPosition, GizmoVector3 displacement, GizmoQuaternion rotation, out Action apply)
        {
            apply = null;
            Quaternion orientation = ToUnityQuaternion(rotation);
            if (!UnityCameraNavigationAdapter.IsValidOrientation(orientation))
                return false;

            if (resolvedPosition.HasValue)
                return PreparePose(resolvedPosition.Value, rotation, out apply);

            if (_mapPos?.node == null || MapControl.SystemMap?.CurrentMap == null)
                return false;

            var candidate = new MapPos { node = _mapPos.node, position = _mapPos.position };
            if (!TrySetLocalPosition(candidate, ToUnityVector3(displacement)))
                return false;

            Vec3D position = MapControl.SystemMap.LocalToGlobal(candidate);
            if (!IsFinite(position))
                return false;

            apply = () =>
            {
                _mapPos.position = candidate.position;
                GlobalPosition = position;
                SetOrientation(orientation);
            };
            return true;
        }

        private bool AcquireCameraEnuFrame(out (Vector3 e, Vector3 n, Vector3 u) frame)
        {
            frame = default;

            if (!TryAcquireEnuBasis(out var east, out var north, out var up))
                return false;

            frame = (east.ToVector3FlipZ(), north.ToVector3FlipZ(), up.ToVector3FlipZ());

            return true;
        }

        private protected virtual bool TryResolvePointerPosition(
            GizmoVector3 displacement, out Vec3D globalPosition, out GizmoVector3 upDirection)
        {
            globalPosition = default;
            upDirection = default;
            var map = MapControl.SystemMap;
            if (map?.CurrentMap == null || _mapPos?.node == null)
                return false;

            if (displacement.LengthSq2() <= 0.0f)
                globalPosition = GlobalPosition;
            else
            {
                var nodeTransform = NodeUtils.FindFirstGameObjectTransform(_mapPos.node.GetNativeReference());
                if (nodeTransform == null)
                    return false;

                // Resolve the same local-to-global position used for movement, but
                // never overwrite the accepted MapPos while searching for a safe arc.
                var candidate = new MapPos
                {
                    node = _mapPos.node,
                    position = nodeTransform.InverseTransformPoint(ToUnityVector3(displacement)).ToVec3D()
                };
                globalPosition = map.LocalToGlobal(candidate);
            }
            if (!TryAcquireEnuBasisAt(globalPosition, out _, out _, out Vec3 up))
                return false;

            upDirection = ToGizmoVector3(up.ToVector3FlipZ());
            return true;
        }

        private static GizmoQuaternion ToGizmoQuaternion(Quaternion value)
        {
            return new GizmoQuaternion(value.w, value.x, value.y, value.z);
        }

        private static GizmoVector3 ToGizmoVector3(Vector3 value)
        {
            return new GizmoVector3(value.x, value.y, value.z);
        }

        private static Vector3 ToUnityVector3(GizmoVector3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static Quaternion ToUnityQuaternion(GizmoQuaternion value)
        {
            return new Quaternion(value.x, value.y, value.z, value.w);
        }

        private (GroundDragPositionAdapter.Basis Basis, GizmoQuaternion Orientation) ReadGroundDragOrientation()
            => (new GroundDragPositionAdapter.Basis(
                    ToGizmoVector3(_unityEast), ToGizmoVector3(_unityNorth), ToGizmoVector3(_unityUp)),
                ToGizmoQuaternion(GetCameraOrientation()));

        private bool PrepareGroundDragDestination(
            Vec3D ecefPosition, out GroundDragPositionAdapter.Basis basis, out Action<GizmoQuaternion> apply)
        {
            basis = default;
            apply = null;
            if (MapControl.SystemMap?.CurrentMap == null)
                return false;

            var candidate = new MapPos();
            if (!candidate.SetCartPos(ecefPosition.x, ecefPosition.y, ecefPosition.z))
                return false;

            Vec3D position = candidate.GlobalPosition();
            if (!TryAcquireEnuBasisAt(position, out Vec3 east, out Vec3 north, out Vec3 up))
                return false;

            basis = new GroundDragPositionAdapter.Basis(
                ToGizmoVector3(east.ToVector3FlipZ()),
                ToGizmoVector3(north.ToVector3FlipZ()),
                ToGizmoVector3(up.ToVector3FlipZ()));
            apply = orientation =>
            {
                _mapPos = candidate;
                GlobalPosition = position;
                SetEnuFrame(east, north, up);
                SetOrientation(ToUnityQuaternion(orientation));
            };
            return true;
        }

        /// <summary>
        /// Uses a local WGS84 approximation that preserves altitude, not exact geodesics or terrain clearance.
        /// </summary>
        private bool TryMoveEcefAlongEllipsoid(
            Vec3D ecef, double moveEastMeters, double moveNorthMeters, out Vec3D position)
        {
            position = default;
            if (_mapPos == null || !IsFinite(ecef)
                || !IsFinite(moveEastMeters) || !IsFinite(moveNorthMeters))
                return false;

            _converter.SetCartPos(new CartPos(ecef.x, ecef.y, ecef.z));
            if (!_converter.GetLatPos(out LatPos lp))
                return false;

            var moved = _mapPos.MoveAlongEllipsoid(lp, moveEastMeters, moveNorthMeters);
            _converter.SetLatPos(moved);
            if (!_converter.GetCartPos(out CartPos cp))
                return false;

            position = new Vec3D(cp.X, cp.Y, cp.Z);
            return IsFinite(position);
        }

    }
}