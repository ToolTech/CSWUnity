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
// File         : GeodeticCameraControl.Movement.cs
// Module       :
// Description  : Camera navigation gesture entry points.
// Author       : Daniel Sahlin
//
// Revision History...
//
// Who      Date    Description
//
// DSA      261023  Created file
//
//******************************************************************************

using GizmoSDK.GizmoBase;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    public partial class GeodeticCameraControl
    {
        /// <summary>
        /// Uses bottom-left screen pixels; regrab acquires a ground reference without moving.
        /// Later calls retain the reference. Failure ends the gesture.
        /// </summary>
        public void TranslateByGroundDrag(Vector3 screenPosition, bool regrab)
        {
            GroundDragGesture.Move(screenPosition.x, screenPosition.y, regrab);
        }

        /// <summary>
        /// Zooms toward terrain or a surface at the given bottom-left screen position.
        /// </summary>
        /// <remarks>
        /// Ordinary misses fall back to viewport center; invalid queries do not.
        /// Does not end held pan or orbit gestures. Positive amounts move inward,
        /// scaling reference distance by 0.8 raised to amount.
        /// </remarks>
        public void ZoomAtScreenPoint(Vector3 screenPosition, float amount)
        {
            ZoomGesture.Move(screenPosition.x, screenPosition.y, amount);
        }

        /// <summary>
        /// Starts an orbit from bottom-left screen pixels, replacing the previous gesture.
        /// </summary>
        /// <remarks>
        /// Pointer mode does not move the camera. An ordinary miss retains the last valid
        /// pivot; invalid queries end the gesture. Center mode can fall back along the
        /// view direction and face the pivot using map up on constrained cameras.
        /// </remarks>
        public void BeginOrbitAtScreenPoint(Vector3 screenPosition, OrbitPivotMode mode)
        {
            OrbitGesture.SetPivotMode(mode);
            OrbitGesture.Begin(
                new Vec2(screenPosition.x, screenPosition.y), Screen.width, Screen.height);
        }

        /// <summary>
        /// Continues the orbit with signed heading and pitch in radians, without pixel scaling.
        /// </summary>
        /// <remarks>
        /// Zero input leaves the pose unchanged; rejected input does not accumulate.
        /// Invalid input or a lost coordinate frame ends the gesture.
        /// </remarks>
        public bool ContinueOrbit(float headingRadians, float pitchRadians)
        {
            return OrbitGesture.Rotate(headingRadians, pitchRadians);
        }

        private void ClearNavigation()
        {
            _groundDragGesture?.Clear();
            _orbitGesture?.Reset();
        }
    }
}
