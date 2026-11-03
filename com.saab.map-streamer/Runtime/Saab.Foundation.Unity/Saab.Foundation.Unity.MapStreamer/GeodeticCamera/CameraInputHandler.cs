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
// File			: CameraInputHandler.cs
// Module		:
// Description	: Handles input and forwards it onto the camera controller.
// Author		: Mats Edvinsson
//
// Revision History...
//
// Who	    Date	Description
//
// u068671	260616	Created file
//
//******************************************************************************

using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    public class CameraInputHandler : MonoBehaviour
    {
        GeodeticCameraControl _cameraControl;

        private void Awake()
        {
            _cameraControl = GetComponent<GeodeticCameraControl>();
        }

        void Update()
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                if (Input.GetMouseButton(0))
                {
                    bool wasJustPressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.LeftShift);
                    _cameraControl.Orbit(Input.mousePosition, wasJustPressed);
                }
            }
            else
            {
                if (Input.GetMouseButton(0))
                {
                    bool wasJustPressed = Input.GetMouseButtonDown(0) || Input.GetKeyUp(KeyCode.LeftShift);
                    _cameraControl.TranslateByGroundDrag(Input.mousePosition, wasJustPressed);
                }
            }

            if (Input.mouseScrollDelta.y != 0)
            {
                _cameraControl.Zoom(Input.mousePosition, Input.mouseScrollDelta.y);
            }
        }
    }
}