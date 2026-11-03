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
// File			: ExampleCameraInput.cs
// Module		:
// Description	: Example application's physical camera input bindings.
// Author		: Daniel Sahlin
//
// Revision History...
//
// Who	    Date	Description
//
// DSA	    261023	Created file
//
//******************************************************************************

using System;
using Saab.Foundation.Unity.MapStreamer.GeodeticCamera;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Example
{
    public sealed class ExampleCameraInput : MonoBehaviour
    {
        private Bindings _bindings;

        private void Awake()
        {
            // Accepts the free or constrained camera; bindings only use shared movement.
            if (!TryGetComponent(out GeodeticCameraControl camera))
            {
                throw new MissingComponentException(
                    $"{nameof(ExampleCameraInput)} on '{name}' requires a "
                    + $"{nameof(GeodeticCameraControl)} component, such as "
                    + $"{nameof(ConstrainedGeodeticCamera)}.");
            }

            _bindings = new Bindings(
                (x, y, begin) => camera.TranslateByGroundDrag(new Vector3(x, y, 0), begin),
                (x, y) => camera.BeginOrbitAtScreenPoint(
                    new Vector3(x, y, 0), OrbitPivotMode.ViewportCenter),
                (heading, pitch) => camera.ContinueOrbit(heading, pitch),
                (x, y, steps) => camera.ZoomAtScreenPoint(new Vector3(x, y, 0), steps));
        }

        private void Update()
        {
            Vector2 pointer = Input.mousePosition;
            _bindings.Update(
                Input.GetMouseButton(2)
                    || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
                Input.GetMouseButton(2)
                    || Input.GetMouseButton(0)
                        && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)),
                Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(2)
                    || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift),
                Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl),
                Input.GetKeyUp(KeyCode.LeftControl) || Input.GetKeyUp(KeyCode.RightControl)
                    || Input.GetMouseButtonUp(2),
                pointer,
                Input.mouseScrollDelta.y);
        }

        // The example owns the bindings and sensitivity; callbacks only
        // acquire geometric references or request meaningful movement amounts.
        internal sealed class Bindings
        {
            private const float RadiansPerPixel = 0.0625f * (float)Math.PI / 180;
            private readonly Action<float, float, bool> _pan;
            private readonly Action<float, float> _beginOrbit;
            private readonly Action<float, float> _rotate;
            private readonly Action<float, float, float> _zoom;
            private float _previousOrbitX;
            private float _previousOrbitY;

            internal Bindings(
                Action<float, float, bool> pan,
                Action<float, float> beginOrbit,
                Action<float, float> rotate,
                Action<float, float, float> zoom)
            {
                _pan = pan;
                _beginOrbit = beginOrbit;
                _rotate = rotate;
                _zoom = zoom;
            }

            internal void Update(
                bool shiftPressed,
                bool leftPressed,
                bool leftJustPressed,
                bool shiftJustPressed,
                bool shiftJustReleased,
                Vector2 pointer,
                float wheelSteps)
            {
                if (leftPressed)
                {
                    if (shiftPressed)
                    {
                        float heading = (pointer.x - _previousOrbitX) * RadiansPerPixel;
                        float pitch = -(pointer.y - _previousOrbitY) * RadiansPerPixel;
                        _previousOrbitX = pointer.x;
                        _previousOrbitY = pointer.y;

                        if (leftJustPressed || shiftJustPressed)
                        {
                            _beginOrbit(pointer.x, pointer.y);
                        }
                        else
                        {
                            _rotate(heading, pitch);
                        }
                    }
                    else
                    {
                        _pan(pointer.x, pointer.y, leftJustPressed || shiftJustReleased);
                    }
                }

                if (Mathf.Abs(wheelSteps) >= float.Epsilon)
                {
                    _zoom(pointer.x, pointer.y, wheelSteps);
                }
            }
        }
    }
}
