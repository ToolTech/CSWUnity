//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: DemoFlight.cs
// Description	: Demo camera flight in a figure eight over the streamed map
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261104	Added the figure-eight demo flight, started by -csw-demo or by adding the component to the host.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Globalization;
using GizmoSDK.GizmoBase;
using Saab.Foundation.Unity.MapStreamer.Csw;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;

namespace Saab.Foundation.Unity.MapStreamer.Demo
{
	/// <summary>Flies the host camera in a figure eight around the map origin at a constant ground speed.</summary>
	/// <remarks>
	/// The -csw-demo command-line argument adds the flight to the host in the first scene, and the -csw-demo-*
	/// arguments override the fields when the flight starts. The flight replaces the host's flight input and holds
	/// the start view for a few seconds after the first committed frame. Esc quits a player.
	/// </remarks>
	[DefaultExecutionOrder(-3000)]
	[RequireComponent(typeof(MapStreamerHost))]
	public sealed class DemoFlight : MonoBehaviour
	{
		// Hold the start view this long so the first tiles load before flying.
		private const double SettlingSeconds = 3;
		// The second pass over the crossing of the eight lies this much higher than the first.
		private const double ClimbMetres = 150;

		[Tooltip("Ground speed in metres per second (-csw-demo-speed).")]
		public double Speed = 60;
		[Tooltip("Half the east-west extent of the eight in metres; the eight is a third as wide north-south (-csw-demo-size).")]
		public double HalfWidth = 3000;
		[Tooltip("Height above the map origin in metres at the first pass over the crossing (-csw-demo-altitude).")]
		public double Altitude = 200;
		[Tooltip("View angle above the horizon in degrees (-csw-demo-pitch).")]
		public double Pitch = -25;
		[Tooltip("Turns the host's infinite far plane on or off when the flight starts (-csw-demo-infinite 1 or 0).")]
		public bool InfiniteFarPlane = true;

		private MapStreamerHost _host;
		private double _route, _readyAt = -1;
		private Vec3D _origin, _east, _north, _up;
		private Vector3 _unityEast, _unityNorth, _unityUp;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Attach()
		{
			if (Array.IndexOf(Environment.GetCommandLineArgs(), "-csw-demo") < 0)
				return;
			var host = FindFirstObjectByType<MapStreamerHost>();
			if (!host)
				throw new InvalidOperationException("-csw-demo requires a MapStreamerHost in the first scene.");
			if (!host.GetComponent<DemoFlight>())
				host.gameObject.AddComponent<DemoFlight>();
		}

		private static double Argument(string name, double fallback)
		{
			var args = Environment.GetCommandLineArgs();
			var index = Array.LastIndexOf(args, name);
			if (index < 0)
				return fallback;
			if (index + 1 < args.Length &&
				double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
				return value;
			throw new ArgumentException(name + " requires a number.");
		}

		private void Awake()
		{
			Speed = Argument("-csw-demo-speed", Speed);
			HalfWidth = Argument("-csw-demo-size", HalfWidth);
			Altitude = Argument("-csw-demo-altitude", Altitude);
			Pitch = Argument("-csw-demo-pitch", Pitch);
			InfiniteFarPlane = Argument("-csw-demo-infinite", InfiniteFarPlane ? 1 : 0) != 0;
			if (!(Speed >= 0) || !(HalfWidth > 0))
				throw new ArgumentException("The demo flight speed must not be negative and its half width must be positive.");
			_host = GetComponent<MapStreamerHost>();
			_host.FlightInput = false;
			_host.Settings.InfiniteFarPlane = InfiniteFarPlane;
			// The default quality level of a player has no VSync, and the flight moves smoothly at one frame per
			// vertical blank. The Editor keeps the VSync setting of its Game view.
			if (!Application.isEditor)
				QualitySettings.vSyncCount = 1;
			UnityEngine.Debug.Log(string.Format(CultureInfo.InvariantCulture,
				"Demo flight: figure eight at {0} m/s, half width {1} m, altitude {2} m, pitch {3} deg, infinite far plane {4}.",
				Speed, HalfWidth, Altitude, Pitch, InfiniteFarPlane));
		}

		// East and north metres per radian of the route: east = W sin(route) and north = W / 3 sin(2 route).
		private Vector2 Tangent(double route)
		{
			return new Vector2((float)(HalfWidth * Math.Cos(route)), (float)(HalfWidth * 2 / 3 * Math.Cos(2 * route)));
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.Escape))
				Application.Quit();
			if (!_host || _host.Manager == null)
				return;
			var now = UnityEngine.Time.unscaledTimeAsDouble;
			if (_readyAt < 0)
			{
				if (!_host.Camera.HasPose || !_host.Scene.HasCommittedFrame)
					return;
				_readyAt = now;
				_origin = _host.Coordinates.Origin;
				var orientation = _host.Coordinates.Orientation(_origin);
				_east = (Vec3D)orientation.GetCol(0);
				_north = (Vec3D)orientation.GetCol(1);
				_up = (Vec3D)orientation.GetCol(2);
				_unityEast = _host.Coordinates.Axes.ToUnityDirection(orientation.GetCol(0));
				_unityNorth = _host.Coordinates.Axes.ToUnityDirection(orientation.GetCol(1));
				_unityUp = _host.Coordinates.Axes.ToUnityDirection(orientation.GetCol(2));
			}
			// Constant ground speed: the route angle advances by the flown distance over the metres per radian.
			if (now - _readyAt > SettlingSeconds)
				_route = (_route + Speed * UnityEngine.Time.unscaledDeltaTime / Tangent(_route).magnitude) % (2 * Math.PI);
			var tangent = Tangent(_route);
			var position = _origin + _east * (HalfWidth * Math.Sin(_route)) + _north * (HalfWidth / 3 * Math.Sin(2 * _route)) +
				_up * (Altitude + ClimbMetres * (1 - Math.Cos(_route)) / 2);
			// The view looks along the direction of travel; a positive pitch raises it above the horizon.
			_host.Camera.SetPose(position, Quaternion.LookRotation(_unityEast * tangent.x + _unityNorth * tangent.y, _unityUp) *
				Quaternion.AngleAxis(-(float)Pitch, Vector3.right));
		}
	}
}
