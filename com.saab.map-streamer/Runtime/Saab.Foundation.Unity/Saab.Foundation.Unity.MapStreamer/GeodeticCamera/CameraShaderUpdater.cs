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
// File			: CameraShaderUpdater.cs
// Module		:
// Description	: Feeds camera data to shaders
// Author		: Mats Edvinsson
//
// Revision History...
//
// Who	    Date	Description
//
// u068671	260616	Created file
//
//******************************************************************************

using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;
using Saab.Foundation.Map;
using Saab.Unity.Extensions;
using System;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.GeodeticCamera
{
    public class CameraShaderUpdater : MonoBehaviour
    {
        GeodeticCameraControl _cameraControl;
        private MapPos _mapPos;

        private void Awake()
        {
            _cameraControl = GetComponent<GeodeticCameraControl>();
            _cameraControl.OnPreTraverse += smc_OnPreTraverse;
        }

        private void smc_OnPreTraverse(bool locked)
        {
            UpdateShaderPos();
            UpdateShaderOrientation();
        }

        private void UpdateShaderPos()
        {
            var pos = _cameraControl.GlobalPosition;
            float max = 5000; // Needs to match ShaderUtils.PositionTiling
            var cameraHeight = (float)Math.Clamp(pos.y, -float.MaxValue, float.MaxValue);
            var worldOffset = new Vector3((float)(pos.x % max), cameraHeight, -(float)(pos.z % max));
            Shader.SetGlobalVector("_WorldOffset", worldOffset);
        }

        private void UpdateShaderOrientation()
        {
            if (MapControl.SystemMap?.CurrentMap == null)
                return;

            if (!MapControl.SystemMap.GlobalToWorld(new Vec3D(_cameraControl.X, _cameraControl.Y, _cameraControl.Z), out LatPos latPos))
                return;

            if (_mapPos == null)
            {
                _mapPos = new MapPos();
            }

            _mapPos.SetLatPos(latPos.Latitude, latPos.Longitude, latPos.Altitude);

            var enu = _mapPos.EnuToLocal();

            var east = enu * new Vec3(1, 0, 0);
            var north = enu * new Vec3(0, 1, 0);
            var up = enu * new Vec3(0, 0, 1);

            var localToEun = MapUtil.FromBasis(east.ToVector3(), up.ToVector3(), north.ToVector3());

            Shader.SetGlobalMatrix("_LocalToEUN", localToEun);
        }
    }
}