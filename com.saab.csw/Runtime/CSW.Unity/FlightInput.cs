//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: FlightInput.cs
// Description	: Keyboard flight of the camera of a CSW.Unity.MapControl
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261104	Created from the flight input of MapStreamerHost.	(2.12.327)
//
//*****************************************************************************

using UnityEngine;

namespace CSW.Unity
{
	/// <summary>Flies the camera of a CSW.Unity.MapControl with the keyboard.</summary>
	/// <remarks>
	/// W, A, S and D move, Space and Left Ctrl climb and descend, Left Shift moves ten times faster, and the arrow
	/// keys turn. Runs before the map control samples the camera. CSW.MotionModel and CSW.Input replace it later.
	/// </remarks>
	[DefaultExecutionOrder(-2100)]
	public sealed class FlightInput : MonoBehaviour
	{
		[Tooltip("Map control whose camera is flown. When empty, the map control on this GameObject is used.")]
		public MapControl MapControl;
		[Tooltip("Speed in metres per second.")]
		[Min(0)]
		public float Speed = 100;
		[Tooltip("Turn rate in degrees per second.")]
		[Min(0)]
		public float TurnSpeed = 60;

		private void Awake()
		{
			if (!MapControl)
				MapControl = GetComponent<MapControl>();
		}

		private void Update()
		{
			var camera = MapControl ? MapControl.Camera : null;
			if (camera == null || !camera.HasPose)
				return;
			var movement = new Vector3(
				(Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
				(Input.GetKey(KeyCode.Space) ? 1 : 0) - (Input.GetKey(KeyCode.LeftControl) ? 1 : 0),
				(Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
			var speed = Input.GetKey(KeyCode.LeftShift) ? Speed * 10 : Speed;
			if (movement.sqrMagnitude > 0)
				camera.Move(movement.normalized * (speed * Time.unscaledDeltaTime));
			var heading = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
			var pitch = (Input.GetKey(KeyCode.DownArrow) ? 1 : 0) - (Input.GetKey(KeyCode.UpArrow) ? 1 : 0);
			if (heading != 0 || pitch != 0)
				camera.Rotate(heading * TurnSpeed * Time.unscaledDeltaTime, pitch * TurnSpeed * Time.unscaledDeltaTime);
		}
	}
}