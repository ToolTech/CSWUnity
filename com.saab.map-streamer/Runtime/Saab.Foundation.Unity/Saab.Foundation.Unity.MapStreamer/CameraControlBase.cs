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
// File			: CameraControl.cs
// Module		:
// Description	: Manages camera updates with large coordinates
// Author		: Mats Edvinsson
// Product		: Gizmo3D 2.12.326
//
// NOTE:	Gizmo3D is a high performance 3D Scene Graph and effect visualisation 
//			C++ toolkit for Linux, Mac OS X, Windows, Android, iOS and HoloLens for  
//			usage in Game or VisSim development.
//
//
// Revision History...
//
// Who      Date    Description
//
// u068671	260813	Created file
//
//******************************************************************************

using GizmoSDK.GizmoBase;
using Saab.Foundation.Map;
using Saab.Unity.Extensions;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer
{

    public abstract class CameraControlBase : MonoBehaviour, ISceneManagerCamera
    {
        public event Traverse OnPreTraverse;
        public event Traverse OnPostTraverse;

        public double X;
        public double Y;
        public double Z;

        public float LodFactor => 1f;

        public Camera Camera
        {
            get
            {
                return GetComponent<Camera>();
            }
        }

        public Vec3D GlobalPosition
        {
            get { return new Vec3D(X, Y, Z); }

            set
            {
                X = value.x;
                Y = value.y;
                Z = value.z;
            }
        }

        protected Matrix4x4 _localToEun;
        private MapPos _mapPos;

        protected Vector3 _unityEast;
        protected Vector3 _unityNorth;
        protected Vector3 _unityUp;

        public Vector3 East
        {
            get { return MapControl.SystemMap.GetLocalOrientation(GlobalPosition).GetCol(0).ToVector3(); }
        }

        public Vector3 North
        {
            get { return MapControl.SystemMap.GetLocalOrientation(GlobalPosition).GetCol(1).ToVector3(); }
        }

        public Vector3 Up
        {
            get { return MapControl.SystemMap.GetLocalOrientation(GlobalPosition).GetCol(2).ToVector3(); }
        }

        public virtual void PreTraverse(bool locked)
        {
            OnPreTraverse?.Invoke(locked);
        }

        public virtual void PostTraverse(bool locked)
        {
            OnPostTraverse?.Invoke(locked);
        }

        public abstract double UpdateCamera(double renderTime);

        public abstract void MapChanged();
    }
}