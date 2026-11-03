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
// File         : GeodeticCameraInitializationState.cs
// Module       :
// Description  : Selects explicit initial-view and pose-synchronization actions.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who  Date    Description
//
// DSA  260902  Created file
//
//******************************************************************************

using System;
using GizmoSDK.GizmoBase;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    internal enum GeodeticCameraInitializationAction
    {
        None,
        SynchronizeOrientation,
        ApplyInitialView
    }

    internal sealed class GeodeticCameraInitializationState
    {
        private bool _initialViewRequested;
        private bool _orientationInitialized;
        private bool _hasInitialViewPose;
        private Vec3D _initialPosition;
        private Quaternion _initialOrientation;

        /// <summary>Stores a pose already validated by the camera Adapter without consuming a request.</summary>
        internal void SetInitialViewPose(Vec3D position, Quaternion orientation)
        {
            _initialPosition = position;
            _initialOrientation = orientation;
            _hasInitialViewPose = true;
        }

        /// <summary>
        /// Applies a pending registered pose synchronously. A rejected application
        /// leaves the request pending; success consumes it. The Adapter must leave
        /// the current camera unchanged when returning false.
        /// </summary>
        internal bool TryApplyInitialView(Func<Vec3D, Quaternion, bool> applyPose)
        {
            if (!_initialViewRequested || !_hasInitialViewPose)
                return false;

            if (!applyPose(_initialPosition, _initialOrientation))
                return false;

            PoseApplied();
            return true;
        }

        internal void RequestInitialView()
        {
            _initialViewRequested = true;
        }

        internal void MapChanged()
        {
            _orientationInitialized = false;
            _hasInitialViewPose = false;
            _initialPosition = default;
            _initialOrientation = default;
        }

        internal void PoseApplied()
        {
            _initialViewRequested = false;
            _orientationInitialized = true;
        }

        /// <summary>
        /// Selects frame work. Synchronization is consumed immediately; a registered
        /// initial view remains pending until its application succeeds or an explicit
        /// pose cancels it. Missing registration never consumes the pending request.
        /// </summary>
        internal GeodeticCameraInitializationAction TakeNextAction()
        {
            if (_initialViewRequested && _hasInitialViewPose)
            {
                return GeodeticCameraInitializationAction.ApplyInitialView;
            }

            if (_orientationInitialized)
                return GeodeticCameraInitializationAction.None;

            _orientationInitialized = true;
            return GeodeticCameraInitializationAction.SynchronizeOrientation;
        }
    }
}
