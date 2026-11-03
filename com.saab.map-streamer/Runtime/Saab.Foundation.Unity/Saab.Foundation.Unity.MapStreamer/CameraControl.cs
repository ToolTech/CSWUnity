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
// AMO	180607	Created file                                        (2.9.1)
//
//******************************************************************************

//#define TEST_ROTATION   // Just test some default rotation

using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;
using Saab.Foundation.Map;
using Saab.Unity.Extensions;
using Saab.Utility.Unity.NodeUtils;
using System;
using System.Collections.Generic;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Random = UnityEngine.Random;



namespace Saab.Foundation.Unity.MapStreamer
{
    public struct AutoMovement
    {
        public float forward;
        public float right;
        public float up;
        public float pan;
        public float tilt;
    }

    public class CameraControl : CameraControlBase, ISceneManagerCamera
    {
        private bool _initialized = false;

        public float Speed = 20f;
        public float ShiftMultiplier = 2f;
        // Use this for initialization

        public float RotSpeed = 20f;

        private double _lastRenderTime = 0;
        private double _currentRenderTime = 0;
        private bool _inputLocked;
        private AutoMovement _autoMovement = default;

        public float GetDeltaTime()
        {
            if (_lastRenderTime == 0)
                return 0;
            else
                return (float)(_currentRenderTime - _lastRenderTime);
        }

        private float _countDownJump = 4;
        private float _jumpTime = 4;
        private MapPos _mapPos;

        public float JumpInterval
        {
            get
            {
                return _jumpTime;
            }
            set
            {
                _jumpTime = value;
                _countDownJump = _jumpTime;
            }
        }

        public void SetSeed(int seed)
        {
            Random.InitState(seed);
        }

        private void MoveForward(float moveSpeed)
        {
            X = X + moveSpeed * GetDeltaTime() * transform.forward.x;
            Y = Y + moveSpeed * GetDeltaTime() * transform.forward.y;
            // As we have a Right Handed ON system and unitys Z into the screen we apply a negative direction
            Z = Z - moveSpeed * GetDeltaTime() * transform.forward.z;
        }

        private void MoveRight(float moveSpeed)
        {
            X = X + moveSpeed * GetDeltaTime() * transform.right.x;
            Y = Y + moveSpeed * GetDeltaTime() * transform.right.y;

            // As we have a Right Handed ON system and unitys Z points into the screen we apply a negative direction
            Z = Z - moveSpeed * GetDeltaTime() * transform.right.z;
        }

        private void MoveUp(float moveSpeed)
        {
            X = X + moveSpeed * GetDeltaTime() * transform.up.x;
            Y = Y + moveSpeed * GetDeltaTime() * transform.up.y;

            // As we have a Right Handed ON system and unitys Z points into the screen we apply a negative direction
            Z = Z - moveSpeed * GetDeltaTime() * transform.up.z;
        }

        private Quaternion Tilt(float rotationSpeed, Vector3 right)
        {
            float angle = rotationSpeed * GetDeltaTime();
            return Quaternion.AngleAxis(angle, right);
        }

        private Quaternion Pan(float rotationSpeed)
        {
            float angle = rotationSpeed * GetDeltaTime();
            return Quaternion.AngleAxis(angle, _unityUp);
        }

        public void UpdateMoveCamera(float forward, float right, float up, float pan, float tilt, bool lockOtherInput = true)
        {
            _autoMovement.forward = forward;
            _autoMovement.right = right;
            _autoMovement.up = up;
            _autoMovement.pan = pan;
            _autoMovement.tilt = tilt;
            _inputLocked = lockOtherInput;
        }

        private void UpdateShaderPos()
        {
            var pos = GlobalPosition;
            float max = 5000; // Needs to match ShaderUtils.PositionTiling
            var cameraHeight = (float)Math.Clamp(pos.y, -float.MaxValue, float.MaxValue);
            var worldOffset = new Vector3((float)(pos.x % max), cameraHeight, -(float)(pos.z % max));
            Shader.SetGlobalVector("_WorldOffset", worldOffset);
        }

        private void Move(AutoMovement movement)
        {
            MoveForward(movement.forward);
            MoveRight(movement.right);
            MoveUp(movement.up);

            Quaternion rot = transform.rotation;

            rot = Pan(-movement.pan) * rot;

            Vector3 newForward = rot * Vector3.forward;
            Vector3 newRight = Vector3.Cross(_unityUp, newForward).normalized;   // right on tangent plane

            rot = Tilt(movement.tilt, newRight) * rot;

            transform.rotation = rot;
        }

        // Update is called once per frame
        void Update()
        {
            //TODO: Handle shader updates outside of camera controller (Camera controller should only focus on location and orientation of camera, not cater to the needs of shaders)
            UpdateShaderPos();

            if (Input.GetButtonDown("Fire1") && Input.GetKey(KeyCode.LeftShift) && !_inputLocked)
            {
                Map.MapPos mapPos;

                var layerMask = GroundClampType.GROUND;

                if (Map.MapControl.SystemMap.GetScreenGroundPosition((int)Input.mousePosition.x, (int)(Screen.height - Input.mousePosition.y), (uint)Screen.width, (uint)Screen.height, out mapPos, layerMask, Map.ClampFlags.DEFAULT))
                {
                    List<GameObject> list;

                    if (NodeUtils.FindGameObjects(mapPos.node.GetNativeReference(), out list))
                    {
                        foreach (GameObject o in list)
                        {
                            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

                            sphere.transform.parent = o.transform;
                            sphere.transform.transform.localPosition = new Vector3((float)mapPos.position.x, (float)mapPos.position.y, (float)mapPos.position.z);
                            sphere.transform.localScale = new Vector3(10, 10, 10);
                        }
                    }

                    // Just test some update
                    mapPos.position += new Vec3(1, 1, 1);

                    Map.MapControl.SystemMap.UpdatePosition(mapPos, GroundClampType.GROUND);

                    GlobalPosition = mapPos.GlobalPosition(new Vec3(0, 0, 10));
                }


            }

            if (Input.GetButtonDown("Fire2"))
            {
                GizmoSDK.Coordinate.LatPos latpos = new GizmoSDK.Coordinate.LatPos
                {
                    Altitude = 245.52585220821,
                    Latitude = 1.00778345058085,
                    Longitude = 0.251106492463706

                };

                Map.MapPos mappos;

                if (Map.MapControl.SystemMap.GetPosition(latpos, out mappos, Map.GroundClampType.GROUND, Map.ClampFlags.WAIT_FOR_DATA))
                {
                    Debug.Log("Hit Ground ok");
                }
            }

            if (Input.GetKey("b"))
            {
                GizmoSDK.Gizmo3D.DynamicLoaderManager.StopManager();
            }

            if (Input.GetKey("v"))
            {
                GizmoSDK.Gizmo3D.DynamicLoaderManager.StartManager();
            }
        }

        public void RandomJump(float distance, float maxDistance = 3000)
        {
            _countDownJump -= UnityEngine.Time.deltaTime;

            if (_countDownJump <= 0)
            {
                _countDownJump = JumpInterval;
                Quaternion rot = transform.rotation;
                rot *= Quaternion.Euler(0, Random.Range(160, 200), 0);
                transform.rotation = rot;

                X = X + (Random.value * 0.5) + 0.5f * distance;
                Z = Z + (Random.value * 0.5) + 0.5f * distance;

                if (X > maxDistance)
                    X = 0;
                if (Z > maxDistance)
                    Z = 0;
            }
        }

        public void LateUpdate()
        {
            if (MapControl.SystemMap?.CurrentMap != null)
            {
                if (MapControl.SystemMap.GlobalToWorld(new Vec3D(X, Y, Z), out LatPos latPos))
                {
                    if(_mapPos == null)
                    {
                        _mapPos = new MapPos();                   
                    }

                    _mapPos.SetLatPos(latPos.Latitude, latPos.Longitude, latPos.Altitude);
                    var enu = _mapPos.EnuToLocal();

                    var east = enu * new Vec3(1, 0, 0);
                    var north = enu * new Vec3(0, 1, 0);
                    var up = enu * new Vec3(0, 0, 1);

                    _unityEast = east.ToVector3FlipZ();
                    _unityNorth = north.ToVector3FlipZ();
                    _unityUp = up.ToVector3FlipZ();

                    _localToEun = MapUtil.FromBasis((east.ToVector3()), (up.ToVector3()), (north.ToVector3()));

                    Shader.SetGlobalMatrix("_LocalToEUN", _localToEun);
                    if (!_initialized)
                    {
                        // look north
                        var offset = up.ToVector3() * 100f;
                        X += offset.x;
                        Y += offset.y;
                        Z += offset.z;

                        transform.rotation = Quaternion.LookRotation(_unityNorth, _unityUp);
                        //transform.eulerAngles = transform.rotation.eulerAngles
                        _initialized = true;
                    }
                }
            }
        }

        public override double UpdateCamera(double renderTime)
        {
            _lastRenderTime = _currentRenderTime;
            _currentRenderTime = renderTime;

            Move(_autoMovement);


            if (_inputLocked)
                return renderTime;

            var speed = Speed;

            if (Input.GetKey(KeyCode.LeftShift))
                speed *= ShiftMultiplier;

            if (Input.GetKey("w"))
            {
                MoveForward(speed);
            }
            if (Input.GetKey("s"))
            {
                MoveForward(-speed);
            }

            if (Input.GetKey(KeyCode.Space))
            {
                MoveUp(speed / 2);
            }
            if (Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl))
            {
                MoveUp(-speed / 2);
            }

            if (Input.GetKey("d"))
            {
                MoveRight(speed);
            }
            if (Input.GetKey("a"))
            {
                MoveRight(-speed);
            }

            Quaternion rot = transform.rotation;
            Quaternion tilt = Quaternion.identity;
            Quaternion pan = Quaternion.identity;

            if (Input.GetKey(KeyCode.LeftArrow))
            {
                pan = Pan(-RotSpeed);
            }

            if (Input.GetKey(KeyCode.RightArrow))
            {
                pan = Pan(RotSpeed);
            }

            rot = pan * rot;
            Vector3 newForward = rot * Vector3.forward;
            Vector3 newRight = Vector3.Cross(_unityUp, newForward).normalized;   // right on tangent plane

            if (Input.GetKey(KeyCode.UpArrow))
            {
                tilt = Tilt(RotSpeed, newRight);
            }

            if (Input.GetKey(KeyCode.DownArrow))
            {
                tilt = Tilt(-RotSpeed, newRight);
            }

            rot = tilt * rot;

            if (Input.GetKeyDown("p"))
            {
                rot = rot * Quaternion.Euler(0f, 180f, 0f);
            }


#if TEST_ROTATION
                rot = Pan(-rotspeed) * rot;
#endif

            transform.rotation = rot;


            return renderTime;
        }

        public override void MapChanged()
        {
            // Called when global map has changed
        }
    }
}

