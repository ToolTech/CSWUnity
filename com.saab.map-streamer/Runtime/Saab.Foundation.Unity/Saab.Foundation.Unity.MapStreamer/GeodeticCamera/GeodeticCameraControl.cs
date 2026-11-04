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
// DSA	    261020	Updated orbit, zoom and pan; simplified navigation helpers
//
//******************************************************************************

using System;
using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;
using Saab.Foundation.Map;
using Saab.Unity.Extensions;
using UnityEngine;
using GizmoQuaternion = GizmoSDK.GizmoBase.Quaternion;
using Quaternion = UnityEngine.Quaternion;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    /// <summary>
    /// Provides unconstrained pan, zoom and orbit without input bindings.
    /// </summary>
    /// <remarks>
    /// Position uses map-global coordinates; rotation uses Unity world axes.
    /// Calls and callbacks require the owning Unity thread.
    /// </remarks>
    [RequireComponent(typeof(Camera), typeof(CameraShaderUpdater))]
    public partial class GeodeticCameraControl : CameraControlBase, ICameraPose
    {
        private CameraPose _pose;
        private bool _orientationInitialized;
        private bool _needsOrientationSync;
        private Quaternion _orientationSync;

        protected MapPos _mapPos;

        /// <inheritdoc cref="ICameraPose.PoseApplied"/>
        public event Action ExplicitPoseApplied;

        /// <inheritdoc cref="ICameraPose.MapInvalidated"/>
        public event Action MapReplaced;

        event Action ICameraPose.PoseApplied
        {
            add => ExplicitPoseApplied += value;
            remove => ExplicitPoseApplied -= value;
        }

        event Action ICameraPose.MapInvalidated
        {
            add => MapReplaced += value;
            remove => MapReplaced -= value;
        }

        bool ICameraPose.TrySetPose(Vec3D position, GizmoQuaternion orientation)
            => TrySetPose(position,
                new Quaternion(orientation.x, orientation.y, orientation.z, orientation.w));

        /// <summary>
        /// Accepted world rotation, including a pose queued for the next traversal.
        /// </summary>
        public Quaternion Orientation => GetCameraOrientation();

        /// <summary>
        /// Applies a pose, including roll; failure leaves the pose and pending reset requests unchanged.
        /// </summary>
        /// <remarks>
        /// Global position updates immediately; transform rotation synchronizes at PreTraverse,
        /// before scene-manager consumers inspect it.
        /// </remarks>
        public virtual bool TrySetPose(Vec3D globalPosition, Quaternion orientation)
        {
            _pose ??= new CameraPose(PreparePose);
            if (!_pose.TrySetPose(globalPosition,
                    new GizmoQuaternion(orientation.w, orientation.x, orientation.y, orientation.z)))
                return false;

            OnPoseAccepted();
            ExplicitPoseApplied?.Invoke();
            return true;
        }

        /// <summary>
        /// Prepares a normalized pose and basis without changing accepted state.
        /// Success supplies a synchronous, infallible commit; failure must leave retained state unchanged.
        /// </summary>
        protected virtual bool PreparePose(Vec3D position, GizmoQuaternion orientation, out Action apply)
        {
            apply = null;
            if (!TryPrepareBasis(position, out MapPos candidate, out Vec3 east, out Vec3 north, out Vec3 up))
                return false;

            apply = () =>
            {
                _mapPos = candidate;
                GlobalPosition = position;
                SetEnuFrame(east, north, up);
                SetOrientation(new Quaternion(orientation.x, orientation.y, orientation.z, orientation.w));
                _orientationInitialized = true;
            };
            return true;
        }

        /// <summary>
        /// Clears movement references after pose commit, before observers are notified.
        /// </summary>
        protected virtual void OnPoseAccepted()
        {
            ClearNavigation();
        }

        /// <summary>
        /// Clears map-relative movement references after basis invalidation, before publishing replacement.
        /// </summary>
        protected virtual void OnMapInvalidated()
        {
            ClearNavigation();
        }

        public override void PreTraverse(bool locked)
        {
            if (_needsOrientationSync)
            {
                transform.rotation = _orientationSync;
                _needsOrientationSync = false;
            }

            base.PreTraverse(locked);
        }

        public virtual void LateUpdate()
        {
            if (!TryAcquireEnuBasis(out Vec3 east, out Vec3 north, out Vec3 up))
                return;

            SetEnuFrame(east, north, up);
            if (!_orientationInitialized)
            {
                SetOrientation(GetCameraOrientation());
                _orientationInitialized = true;
            }
        }

        public override double UpdateCamera(double renderTime)
        {
            return renderTime;
        }

        public override void MapChanged()
        {
            _mapPos = null;
            _orientationInitialized = false;
            OnMapInvalidated();
            MapReplaced?.Invoke();
        }

        protected Quaternion GetCameraOrientation()
        {
            return _needsOrientationSync ? _orientationSync : transform.rotation;
        }

        protected void SetOrientation(Quaternion orientation)
        {
            _needsOrientationSync = true;
            _orientationSync = orientation;
        }

        protected void SetEnuFrame(Vec3 east, Vec3 north, Vec3 up)
        {
            _unityEast = east.ToVector3FlipZ();
            _unityNorth = north.ToVector3FlipZ();
            _unityUp = up.ToVector3FlipZ();
            _localToEun = MapUtil.FromBasis(east.ToVector3(), up.ToVector3(), north.ToVector3());
        }

        protected bool TryAcquireEnuBasis(out Vec3 east, out Vec3 north, out Vec3 up)
        {
            if (!TryPrepareBasis(GlobalPosition, out MapPos candidate, out east, out north, out up))
                return false;

            _mapPos = candidate;
            return true;
        }

        protected static bool TryAcquireEnuBasisAt(
            Vec3D position, out Vec3 east, out Vec3 north, out Vec3 up)
        {
            return TryPrepareBasis(position, out _, out east, out north, out up);
        }

        protected static bool TryPrepareBasis(
            Vec3D position, out MapPos candidate, out Vec3 east, out Vec3 north, out Vec3 up)
        {
            candidate = null;
            east = north = up = default;
            var map = MapControl.SystemMap;
            if (map?.CurrentMap == null
                || !IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z)
                || !map.GlobalToWorld(position, out LatPos latPos))
                return false;

            var resolved = new MapPos();
            if (!resolved.SetLatPos(latPos.Latitude, latPos.Longitude, latPos.Altitude))
                return false;

            var enu = resolved.EnuToLocal();
            east = enu * new Vec3(1, 0, 0);
            north = enu * new Vec3(0, 1, 0);
            up = enu * new Vec3(0, 0, 1);
            if (!IsValidAxis(east) || !IsValidAxis(north) || !IsValidAxis(up))
                return false;

            candidate = resolved;
            return true;
        }

        private static bool IsValidAxis(Vec3 axis)
        {
            return IsFinite(axis.x) && IsFinite(axis.y) && IsFinite(axis.z)
                && axis.LengthSq2() > 1e-12f && IsFinite(axis.LengthSq2());
        }

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
