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
// File			: GeodeticCameraControl.cs
// Module		:
// Description	: Manages camera updates with large coordinates.
//              : Translates and rotates the camera geodetically. (e.g. the camera won't fly off into space as it moves towards the horizon)
// Author		: Mats Edvinsson
//
// Revision History...
//
// Who	    Date	Description
//
// u068671	260616	Created file
// DSA	    260902	Added map-aware navigation, explicit pose control, safe zoom, and configurable pitch limits
//
//******************************************************************************



using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;
using Saab.Foundation.Map;
using Saab.Unity.Extensions;
using Saab.Utility.Unity.NodeUtils;
using System;
using System.Collections.Generic;
using UnityEngine;
using GizmoQuaternion = GizmoSDK.GizmoBase.Quaternion;
using GizmoVector3 = GizmoSDK.GizmoBase.Vec3;
using Quaternion = UnityEngine.Quaternion;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    [RequireComponent(typeof(CameraShaderUpdater))]
    public partial class GeodeticCameraControl : CameraControlBase, ISceneManagerCamera
    {
        private readonly GeodeticCameraInitializationState _initializationState =
            new GeodeticCameraInitializationState();
        private bool _initialViewFailureReported;

        private static Coordinate _converter = new Coordinate();

        private MapPos _mapPos;
        private NavigationSurfaceQuery _navigationSurfaces;
        private NavigationSurfaceQuery NavigationSurfaces => _navigationSurfaces
            ?? (_navigationSurfaces = new NavigationSurfaceQuery(() => (Screen.width, Screen.height)));
        private CameraZoomGesture _zoomGesture;
        private CameraZoomGesture ZoomGesture => _zoomGesture ?? (_zoomGesture = new CameraZoomGesture(
            NavigationSurfaces.TryGetZoomReference,
            () => GlobalPosition,
            position => GlobalPosition = position));

        /*
         * For synchronizing camera orientation with node transform updates
         */
        private bool needsOrientationSync;
        private Quaternion _orientationSync;

        /*
         * Ground drag
         */
        private GroundDragPositionAdapter _groundDragPositionAdapter;
        private GroundDragPositionAdapter GroundDragPositionAdapter =>
            _groundDragPositionAdapter ?? (_groundDragPositionAdapter = new GroundDragPositionAdapter(
                MoveEcefAlongEllipsoid,
                position => TrySetCameraMapPosition(new CartPos(position.x, position.y, position.z)),
                () => _mapPos.GlobalPosition(),
                position => GlobalPosition = position,
                UpdateOrientation));
        private GroundDragGesture _groundDragGesture;
        private GroundDragGesture GroundDragGesture => _groundDragGesture ?? (_groundDragGesture = new GroundDragGesture(
            ReadGroundDragMap, NavigationSurfaces.TryGetGroundDragReference, NavigationSurfaces.TryGetMapRay,
            ReadGroundDragFrame, RaycastGroundDragPlane, GroundDragPositionAdapter.Apply));

        /* 
         * LatestOrientationChange (loc)
         */
        Vector3 _loc_East;
        Vector3 _loc_North;
        Vector3 _loc_Up;
        Quaternion _loc_Orientation;

        /*
         * Orbit
         */
        private OrbitGesture _orbitGesture;
        private OrbitGesture OrbitGesture => _orbitGesture ?? (_orbitGesture = new OrbitGesture(
            ReadOrbitFrame, QueryOrbitTerrain, TryPointAlongView, TryResolvePointerPosition,
            CalculateOrbitFacingRotation, PrepareOrbitPose));

        /*
         * SETTINGS
         */

        private void Awake()
        {
            OnPreTraverse += smc_OnPreTraverse;
        }

        private void smc_OnPreTraverse(bool locked)
        {
            if (needsOrientationSync)
            {
                transform.rotation = _orientationSync;
                needsOrientationSync = false;
            }
        }

        /// <summary>
        /// Sets the orientation of the camera and updates internal state.
        /// This method should never be called passively (e.g. called every frame because it simplifies logic event when the camera does not need to be oriented differently)
        /// as doing so will prevent the camera controller from orienting the camera geodetically.
        /// </summary>
        /// <param name="orientation">Unity-world camera rotation to retain relative to the current geodetic basis.</param>
        protected void SetOrientation(Quaternion orientation)
        {
            _loc_East = _unityEast;
            _loc_North = _unityNorth;
            _loc_Up = _unityUp;

            _loc_Orientation = orientation;
            needsOrientationSync = true;
            _orientationSync = orientation;
        }

        private void SetLocalPosition(MapPos mp, in Vector3 localPosition)
        {
            var transform = NodeUtils.FindFirstGameObjectTransform(mp.node.GetNativeReference());

            if (transform != null)
            {
                mp.position = transform.InverseTransformPoint(localPosition).ToVec3D();
                return;
            }
        }

        /// <summary>
        /// Initiates or sustains a ground drag camera navigation state and updates the cameras position using the given input.
        /// A new gesture uses rendered terrain beneath the pointer when available,
        /// otherwise the coordinate-model surface, and retains that reference
        /// without switching surfaces while the gesture continues.
        /// </summary>
        /// <param name="mousePosition">The screen coordinate of the mouse in pixels (z-component is ignored)</param>
        /// <param name="regrab">A flag indicating whether to start a new ground drag operation (true) or continue one that was initiated during a different frame (false)</param>
        /// <remarks>
        /// Screen coordinates use Unity's bottom-left origin. Beginning a gesture
        /// acquires its reference without moving the camera; continuing requires
        /// that successful acquisition. Projected maps retain a height plane and
        /// geocentric maps retain a local tangent reference. Shallow-angle motion
        /// is damped. A failed acquisition/continuation or changed map type clears
        /// the gesture; the next movement requires a new grab.
        /// A missing map, invalid viewport or invalid surface query leaves the camera unchanged.
        /// </remarks>
        public void TranslateByGroundDrag(Vector3 mousePosition, bool regrab)
        {
            GroundDragGesture.Move(mousePosition.x, mousePosition.y, regrab);
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
            {
                return false;
            }

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
        /// Moves the camera towards or away from the coordinate-model surface
        /// beneath the mouse cursor, resolving terrain before the mathematical
        /// surface and then using the viewport-center surface on a pointer miss.
        /// The first valid active-map framing establishes the outward bound,
        /// which expands when farther valid framing is subsequently observed.
        /// </summary>
        /// <param name="mousePosition">The mouse position on the screen in pixels (z-component is ignored)</param>
        /// <param name="amount">Dimensionless zoom steps; positive moves in and negative moves out.</param>
        /// <remarks>
        /// Screen coordinates use Unity's bottom-left origin. No resolvable
        /// reference, zero distance, or an invalid calculated movement leaves the
        /// camera unchanged, as do a missing map, invalid viewport or invalid query result.
        /// Opposite equal steps reverse each other only while
        /// the reference stays fixed and distance limits do not clamp the motion.
        /// </remarks>
        public void Zoom(Vector3 mousePosition, float amount)
        {
            ZoomGesture.Move(mousePosition.x, mousePosition.y, amount);
        }

        /// <summary>
        /// Initiates or sustains an orbit camera navigation state and updates the cameras position using the given input.
        /// </summary>
        /// <param name="mousePosition">The screen coordinate of the mouse in pixels (z-component is ignored)</param>
        /// <param name="wasJustPressed">A flag indicating whether to start a new orbit operation (true) or continue one that was initiated during a different frame (false)</param>
        /// <remarks>
        /// Screen coordinates use Unity's bottom-left origin. Pointer mode
        /// acquires rendered terrain, or retains its last valid pivot on a miss;
        /// without either it remains inactive. The first pointer call captures
        /// framing without moving the camera. Subsequent calls apply pixel deltas
        /// from the previous call. A rejected motion/framing step leaves the
        /// accepted pose intact and permits later input to continue the gesture.
        /// Invalid input or loss of the navigation frame requires a new gesture.
        /// Failed or invalid surface/local-coordinate queries end the gesture without moving the camera.
        /// </remarks>
        public void Orbit(Vector3 mousePosition, bool wasJustPressed)
        {
            OrbitGesture.Move(
                new Vec2(mousePosition.x, mousePosition.y),
                wasJustPressed, Screen.width, Screen.height, UnityEngine.Time.deltaTime);
        }

        /// <summary>
        /// Sets whether a new orbit gesture acquires its surface pivot at the
        /// viewport center or from rendered terrain beneath the pointer.
        /// Pointer-surface orbit retains its previous pivot on a terrain miss,
        /// and keeps the pivot fixed in the viewport. Calling this method,
        /// including with the current mode value, ends the active orbit gesture.
        /// </summary>
        /// <remarks>
        /// Pointer orbit transports the full orientation continuously, so an
        /// off-center pivot can introduce roll even without roll input. Separate
        /// horizon leveling rotates about the pivot ray at up to 360 degrees per
        /// second. Correction is fully enabled through 30 degrees of absolute
        /// pivot elevation, fades between 30 and 45 degrees, and is suppressed
        /// at or above 45 degrees to avoid abrupt changes near vertical views.
        /// It is also skipped when no level orientation can preserve the pivot.
        /// Viewport-center orbit instead faces the pivot using map up.
        /// </remarks>
        /// <param name="mode">The orbit pivot acquisition mode.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The mode is not supported.
        /// </exception>
        public void SetOrbitPivotMode(OrbitPivotMode mode)
        {
            OrbitGesture.SetPivotMode(mode);
        }

        /// <summary>
        /// Sets limits on the pivot-to-camera direction's elevation relative to
        /// map up. With both bounds present, pointer orbit also keeps an upright
        /// camera out of the upside-down hemisphere, including off-center pivots.
        /// A null bound allows crossing the corresponding pole.
        /// </summary>
        /// <param name="minimumPitchDegrees">Minimum elevation in degrees, or null; default is 1.</param>
        /// <param name="maximumPitchDegrees">Maximum elevation in degrees, or null; default is 89.</param>
        /// <remarks>
        /// Each finite bound must be within [-89, 89]. Pointer orbit stops at
        /// the first forbidden elevation or upright-orientation boundary along
        /// the requested arc, without accumulating blocked input. Reversal moves
        /// away immediately and heading remains available at either stop.
        /// Supplying a null bound opts out of the upright-orientation constraint.
        /// Changing limits does not move the camera immediately; an out-of-range
        /// pointer-orbit pose may move toward, but not farther from, the allowed range.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// The minimum pitch exceeds the maximum pitch.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// A bound is non-finite or outside the supported range.
        /// </exception>
        public void SetOrbitPitchLimits(
            float? minimumPitchDegrees,
            float? maximumPitchDegrees)
        {
            OrbitGesture.SetPitchLimits(minimumPitchDegrees, maximumPitchDegrees);
        }

        /// <summary>
        /// Requests restoration of the fixed pose registered by
        /// <see cref="TrySetInitialViewPose"/>. This never adds height to the
        /// camera's current position or calculates map framing.
        /// </summary>
        /// <remarks>
        /// Application is deferred to LateUpdate until a registered pose and a
        /// valid map-relative basis are available. Requests coalesce; separately
        /// processed requests restore the same position and orientation.
        /// Map changes discard the old target but retain a pending request.
        /// A successful explicit pose application cancels a pending request.
        /// Failed restoration leaves the camera unchanged, reports a warning
        /// once per request, and retries while the request remains pending.
        /// </remarks>
        public void RequestInitialView()
        {
            _initialViewFailureReported = false;
            _initializationState.RequestInitialView();
        }

        /// <summary>
        /// Applies a host-calculated initial view and registers that exact pose
        /// for subsequent <see cref="RequestInitialView"/> calls.
        /// </summary>
        /// <param name="globalPosition">Position in the active map's global coordinate model, in meters.</param>
        /// <param name="orientation">Unity-world camera rotation; normalized before application and storage.</param>
        /// <returns>
        /// False for the same conditions as <see cref="TrySetPose"/>, preserving
        /// the current camera, any registered target, and any pending request.
        /// True applies and replaces the target and cancels a pending request.
        /// </returns>
        /// <remarks>
        /// The host owns fit-map calculation. Repeated registration replaces the
        /// target; ordinary TrySetPose calls do not replace it. Map changes clear it.
        /// </remarks>
        public bool TrySetInitialViewPose(Vec3D globalPosition, Quaternion orientation)
        {
            if (!TrySetPose(globalPosition, orientation))
                return false;

            _initializationState.SetInitialViewPose(
                globalPosition, ToGizmoQuaternion(GetCameraOrientation()));
            return true;
        }

        /// <summary>
        /// Applies a complete map-global camera pose and synchronizes the
        /// controller's geodetic orientation state.
        /// </summary>
        /// <param name="globalPosition">Position in the active map's global coordinate model, in meters.</param>
        /// <param name="orientation">Unity-world camera rotation; finite and nonzero, including any roll.</param>
        /// <returns>
        /// False if the position or rotation is invalid, or a map-relative ENU
        /// basis cannot be acquired at the target; the previous pose and request
        /// remain unchanged. True applies the position and normalized rotation
        /// and cancels any pending initial-view request.
        /// </returns>
        /// <remarks>
        /// Synchronizes both geodetic orientation state and the rotation used by
        /// navigation before the next node-transform traversal, and ends active
        /// navigation gestures whose retained references belong to the previous
        /// pose. It does not replace the registered initial-view target.
        /// </remarks>
        public bool TrySetPose(Vec3D globalPosition, Quaternion orientation)
        {
            if (!IsFinite(globalPosition)
                || !UnityCameraNavigationAdapter.IsValidOrientation(orientation))
            {
                return false;
            }

            Vec3D previousPosition = GlobalPosition;
            GlobalPosition = globalPosition;
            if (!TryAcquireEnuBasis(
                    out Vec3 east,
                    out Vec3 north,
                    out Vec3 up))
            {
                GlobalPosition = previousPosition;
                return false;
            }

            SetEnuFrame(east, north, up);
            SetOrientation(Quaternion.Normalize(orientation));
            _groundDragGesture?.Clear();
            _orbitGesture?.Reset();
            _initializationState.PoseApplied();
            _initialViewFailureReported = false;
            return true;
        }

        private bool ReadOrbitFrame(out OrbitGesture.Frame frame)
        {
            frame = default;
            if (!NavigationSurfaces.IsAvailable || !AcquireCameraEnuFrame(out var enu))
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

        private bool PrepareOrbitPose(
            Vec3D? resolvedPosition, GizmoVector3 displacement, GizmoQuaternion rotation, out Action apply)
        {
            apply = null;
            Quaternion orientation = ToUnityQuaternion(rotation);
            if (!UnityCameraNavigationAdapter.IsValidOrientation(orientation))
                return false;

            if (resolvedPosition.HasValue)
            {
                if (!TryAcquireEnuBasisAt(resolvedPosition.Value, out Vec3 east, out Vec3 north, out Vec3 up))
                    return false;
                apply = () =>
                {
                    GlobalPosition = resolvedPosition.Value;
                    SetEnuFrame(east, north, up);
                    SetOrientation(orientation);
                };
            }
            else
            {
                var candidate = new MapPos { node = _mapPos.node, position = _mapPos.position };
                SetLocalPosition(candidate, ToUnityVector3(displacement));
                Vec3D position = MapControl.SystemMap.LocalToGlobal(candidate);
                if (!IsFinite(position))
                    return false;
                apply = () =>
                {
                    _mapPos.position = candidate.position;
                    GlobalPosition = position;
                    SetOrientation(orientation);
                };
            }
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

        private bool TryAcquireEnuBasis(out Vec3 east, out Vec3 north, out Vec3 up)
        {
            east = default;
            north = default;
            up = default;

            if (MapControl.SystemMap?.CurrentMap == null)
                return false;

            if (!MapControl.SystemMap.GlobalToWorld(new Vec3D(X, Y, Z), out LatPos latPos))
                return false;

            if (!TrySetCameraMapPosition(latPos))
                return false;

            var enu = _mapPos.EnuToLocal();

            //TODO: Simplify these operations to a single matrix operation. (Create LocalToEnu-matrix)
            east = enu * new Vec3(1, 0, 0);
            north = enu * new Vec3(0, 1, 0);
            up = enu * new Vec3(0, 0, 1);

            return true;
        }

        private bool TryAcquireEnuBasisAt(
            Vec3D globalPosition, out Vec3 east, out Vec3 north, out Vec3 up)
        {
            east = north = up = default;
            var map = MapControl.SystemMap;
            if (map?.CurrentMap == null || !IsFinite(globalPosition)
                || !map.GlobalToWorld(globalPosition, out LatPos latPos))
                return false;

            var candidate = new MapPos();
            if (!candidate.SetLatPos(latPos.Latitude, latPos.Longitude, latPos.Altitude))
                return false;

            var enu = candidate.EnuToLocal();
            east = enu * new Vec3(1, 0, 0);
            north = enu * new Vec3(0, 1, 0);
            up = enu * new Vec3(0, 0, 1);
            return IsFinite(east.ToVector3()) && east.ToVector3().sqrMagnitude > 1e-12f
                && IsFinite(north.ToVector3()) && north.ToVector3().sqrMagnitude > 1e-12f
                && IsFinite(up.ToVector3()) && up.ToVector3().sqrMagnitude > 1e-12f;
        }

        private bool TrySetCameraMapPosition(LatPos position)
        {
            if (_mapPos == null)
                _mapPos = new MapPos();

            _mapPos.node = null;
            return _mapPos.SetLatPos(position.Latitude, position.Longitude, position.Altitude);
        }

        private bool TryResolvePointerPosition(
            GizmoVector3 displacement, out Vec3D globalPosition, out GizmoVector3 upDirection)
        {
            globalPosition = default;
            upDirection = default;
            var map = MapControl.SystemMap;
            if (map?.CurrentMap == null || _mapPos?.node == null)
                return false;

            if (displacement.LengthSq2() == 0.0f)
            {
                globalPosition = GlobalPosition;
            }
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

        private Quaternion GetCameraOrientation()
        {
            return needsOrientationSync ? _orientationSync : transform.rotation;
        }

        private static Quaternion ToUnityQuaternion(GizmoQuaternion value)
        {
            return new Quaternion(value.x, value.y, value.z, value.w);
        }

        private void UpdateOrientation()
        {
            var rotation = FromBasisToBasis(
                _loc_East, _loc_North, _loc_Up,
                _unityEast, _unityNorth, _unityUp
            );

            transform.rotation = rotation * _loc_Orientation;
        }

        /// <summary>Builds the active rotation that maps the source basis directions onto the target basis.</summary>
        /// <param name="e0">Source east direction, expressed in a common Cartesian coordinate frame.</param>
        /// <param name="n0">Source north direction in that frame.</param>
        /// <param name="u0">Source up direction in that frame.</param>
        /// <param name="e1">Target east direction in the same frame.</param>
        /// <param name="n1">Target north direction in the same frame.</param>
        /// <param name="u1">Target up direction in the same frame.</param>
        /// <returns>
        /// Unity rotation R = B1 * transpose(B0), where each basis forms matrix
        /// columns. Left-multiply an orientation by R to transport it.
        /// </returns>
        /// <remarks>
        /// Directions are dimensionless and normalized here. The caller must
        /// supply finite, nonzero, mutually orthogonal bases of equal handedness;
        /// this method does not validate or repair a degenerate basis.
        /// </remarks>
        public static Quaternion FromBasisToBasis(
            Vector3 e0, Vector3 n0, Vector3 u0,
            Vector3 e1, Vector3 n1, Vector3 u1)
        {
            e0.Normalize(); n0.Normalize(); u0.Normalize();
            e1.Normalize(); n1.Normalize(); u1.Normalize();

            Matrix4x4 b0 = Matrix4x4.identity;
            b0.SetColumn(0, new Vector4(e0.x, e0.y, e0.z, 0f));
            b0.SetColumn(1, new Vector4(n0.x, n0.y, n0.z, 0f));
            b0.SetColumn(2, new Vector4(u0.x, u0.y, u0.z, 0f));

            Matrix4x4 b1 = Matrix4x4.identity;
            b1.SetColumn(0, new Vector4(e1.x, e1.y, e1.z, 0f));
            b1.SetColumn(1, new Vector4(n1.x, n1.y, n1.z, 0f));
            b1.SetColumn(2, new Vector4(u1.x, u1.y, u1.z, 0f));

            Matrix4x4 r = b1 * b0.transpose;

            return r.rotation;
        }

        /// <summary>Approximates a local east/north displacement while preserving ellipsoidal altitude.</summary>
        /// <param name="ecef">Starting WGS84 Earth-centered, Earth-fixed position in meters.</param>
        /// <param name="moveEastMeters">Signed local east displacement in meters.</param>
        /// <param name="moveNorthMeters">Signed local north displacement in meters.</param>
        /// <returns>The displaced ECEF position in meters; the camera itself is not moved.</returns>
        /// <remarks>
        /// Requires an initialized map position. Uses local WGS84 curvature,
        /// not an exact long-distance geodesic or terrain-clearance calculation.
        /// Coordinate conversion errors propagate; inputs are not validated here.
        /// </remarks>
        public Vec3D MoveEcefAlongEllipsoid(Vec3D ecef, double moveEastMeters, double moveNorthMeters)
        {
            _converter.SetCartPos(new CartPos(ecef.x, ecef.y, ecef.z));
            _converter.GetLatPos(out LatPos lp);

            var moved = _mapPos.MoveAlongEllipsoid(lp, moveEastMeters, moveNorthMeters);

            _converter.SetLatPos(moved);
            _converter.GetCartPos(out CartPos cp);
            return new Vec3D(cp.X, cp.Y, cp.Z);
        }

        /// <summary>
        /// Unity frame callback that refreshes the map-relative basis and performs
        /// deferred orientation synchronization or registered initial-view restoration.
        /// </summary>
        /// <remarks>Without a valid basis, pending work is retained and the current pose is unchanged.</remarks>
        public void LateUpdate()
        {
            if (!TryAcquireEnuBasis(out var east, out var north, out var up))
                return;

            SetEnuFrame(east, north, up);

            var initializationAction = _initializationState.TakeNextAction();
            if (initializationAction
                == GeodeticCameraInitializationAction.SynchronizeOrientation)
            {
                SetOrientation(GetCameraOrientation());
            }
            else if (initializationAction
                == GeodeticCameraInitializationAction.ApplyInitialView)
            {
                if (!_initializationState.TryApplyInitialView(ApplyInitialViewPose)
                    && !_initialViewFailureReported)
                {
                    Debug.LogWarning("Could not restore the registered initial camera view; the request remains pending.");
                    _initialViewFailureReported = true;
                }
            }
        }

        private bool ApplyInitialViewPose(Vec3D position, GizmoQuaternion orientation)
        {
            return TrySetPose(position, ToUnityQuaternion(orientation));
        }

        private void SetEnuFrame(Vec3 east, Vec3 north, Vec3 up)
        {
            _unityEast = east.ToVector3FlipZ();
            _unityNorth = north.ToVector3FlipZ();
            _unityUp = up.ToVector3FlipZ();

            _localToEun = MapUtil.FromBasis(east.ToVector3(), up.ToVector3(), north.ToVector3());
        }

        /// <summary>Preserves the scene manager's render timestamp; navigation is updated by Unity callbacks.</summary>
        /// <param name="renderTime">Caller-supplied scene render timestamp; not interpreted by this controller.</param>
        /// <returns>The unchanged timestamp.</returns>
        public override double UpdateCamera(double renderTime)
        {
            return renderTime;
        }

        /// <summary>Invalidates navigation references and the registered initial pose for the previous map.</summary>
        /// <remarks>
        /// Preserves any pending initial-view request, and requests synchronization
        /// of the existing orientation when the new map basis becomes available.
        /// Repeated calls leave gestures inactive and reset the learned zoom-out
        /// distance; the host must register the new map's initial pose.
        /// </remarks>
        public override void MapChanged()
        {
            _groundDragGesture?.Clear();
            _orbitGesture?.Reset();
            _zoomGesture = null;
            _initialViewFailureReported = false;
            _initializationState.MapChanged();
        }
    }
}