//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: SceneCoordinateContext.cs
// Description	: Typed map coordinates and the single Gizmo to Unity basis
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Added owned coordinate context and typed terrain and camera presentation conversions.	(2.12.327)
//
//*****************************************************************************

using System;
using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;
using Saab.Unity.Extensions;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;

namespace Saab.Foundation.Unity.MapStreamer.Csw
{
	/// <summary>Defines the single Gizmo-to-Unity presentation basis used by copied scene data.</summary>
	public sealed class GizmoUnityAxisMapping
	{
		// Coordinate's default 3D conversion already swizzles geographic components.
		// Keep native mesh/local matrices intact and reflect only the presentation root.
		public Matrix4x4 Basis { get; } = Matrix4x4.Scale(new Vector3(1, 1, -1));
		public bool ReversesHandedness => true;
		public Vector3 RootScale => new Vector3(1, 1, -1);

		public Vector3 ToUnityDirection(Vec3 native)
		{
			return Basis.MultiplyVector(native.ToVector3());
		}

		public Vec3 ToNativeDirection(Vector3 unity)
		{
			return Basis.MultiplyVector(unity).ToVec3();
		}

		public Vector3 ToUnityPosition(Vec3D global, Vec3D camera)
		{
			return ToUnityDirection((Vec3)(global - camera));
		}

		public Vector3 ToUnityNormal(Vec3 normal)
		{
			return Basis.inverse.transpose.MultiplyVector(normal.ToVector3()).normalized;
		}

		public Quaternion CameraRotation(Vec3 hpr)
		{
			var rotation = Matrix3.CreateFrom_Euler_YXZ(
				hpr.x * Mathf.Deg2Rad,
				hpr.y * Mathf.Deg2Rad,
				-hpr.z * Mathf.Deg2Rad);
			return Quaternion.LookRotation(
				ToUnityDirection(rotation * new Vec3(0, 0, -1)),
				ToUnityDirection(rotation * new Vec3(0, 1, 0)));
		}

		public Vec3 CameraHpr(Quaternion unityRotation)
		{
			var right = ToNativeDirection(unityRotation * Vector3.right);
			var up = ToNativeDirection(unityRotation * Vector3.up);
			var backward = ToNativeDirection(-(unityRotation * Vector3.forward));
			var rotation = new Matrix3(right, up, backward);
			rotation.Decompose_Euler_YXZ(out var heading, out var pitch, out var roll);
			return new Vec3(
				heading * Mathf.Rad2Deg,
				pitch * Mathf.Rad2Deg,
				-roll * Mathf.Rad2Deg);
		}
	}

	/// <summary>Owns the parsed native map coordinate system used by presentation conversions.</summary>
	/// <remarks>
	/// Use on the Unity main thread. The host owns Coordinate platform lifetime;
	/// disposing this context releases its coordinate-system reference only.
	/// </remarks>
	public sealed class SceneCoordinateContext : IDisposable
	{
		private CoordinateSystem _system;

		public GizmoUnityAxisMapping Axes { get; } = new GizmoUnityAxisMapping();
		public string Definition { get; private set; } = "";
		public Vec3D Origin { get; private set; }
		public bool IsConfigured => _system != null;
		public CoordinateType Type => _system != null ? _system.Type : CoordinateType.NOT_DEFINED;

		internal void Configure(string definition, Vec3D origin)
		{
			CoordinateSystem replacement = null;
			if (!string.IsNullOrEmpty(definition) &&
				!CoordinateSystem.TryParse(definition, out replacement))
			{
				throw new ArgumentException($"Unsupported map coordinate system '{definition}'.");
			}

			_system?.Dispose();
			_system = replacement;
			Definition = definition ?? "";
			Origin = origin;
		}

		/// <summary>Returns a copied double global map position for a geodetic position.</summary>
		public Vec3D FromGeodetic(LatPos position, Datum datum = Datum.WGS84_ELLIPSOID)
		{
			RequireConfigured();
			if (!Coordinate.Get3DCoordinate(position, _system, out var result, datum))
				throw new InvalidOperationException("Coordinate could not convert the geodetic camera position.");
			return result;
		}

		/// <summary>Returns a copied geodetic position in the requested datum.</summary>
		public LatPos ToGeodetic(Vec3D position, Datum datum = Datum.WGS84_ELLIPSOID)
		{
			RequireConfigured();
			if (!Coordinate.GetGlobalCoordinate(position, _system, out LatPos result, datum))
				throw new InvalidOperationException("Coordinate could not convert the global map position.");
			return result;
		}

		/// <summary>Returns a copied geocentric position in the requested datum.</summary>
		public CartPos ToGeocentric(Vec3D position, Datum datum = Datum.WGS84_ELLIPSOID)
		{
			RequireConfigured();
			if (!Coordinate.GetGlobalCoordinate(position, _system, out CartPos result, datum))
				throw new InvalidOperationException("Coordinate could not convert the global map position to ECEF.");
			return result;
		}

		/// <summary>Returns the copied local east, north and up basis at a global map position.</summary>
		public Matrix3 Orientation(Vec3D position)
		{
			var geodetic = ToGeodetic(position);
			if (!_system.Get3DOrientation(geodetic, out var orientation))
				throw new InvalidOperationException("Coordinate could not determine the map's local ENU basis.");
			return orientation;
		}

		internal TerrainPresentationData CreateTerrainData(
			StateBuildOutput state,
			Vector3 center,
			Vec3D globalCenter,
			Matrix4x4 nativeLocalToCamera)
		{
			if (!state.Feature)
				return new TerrainPresentationData(state, center, Matrix4x4.identity, Vector3.zero);

			var orientation = Orientation(globalCenter);
			var local = nativeLocalToCamera.inverse;
			var basis = Matrix4x4.identity;
			basis.SetColumn(0, local.MultiplyVector(orientation.GetCol(0).ToVector3()));
			basis.SetColumn(1, local.MultiplyVector(orientation.GetCol(2).ToVector3()));
			basis.SetColumn(2, local.MultiplyVector(-orientation.GetCol(1).ToVector3()));

			using (var coordinate = new Coordinate())
			{
				coordinate.SetCartPos(ToGeocentric(globalCenter));
				if (_system.TryGetUTM(out var zone, out var north))
				{
					coordinate.PrefUTMZone = zone;
					coordinate.PrefUTMHemisphere = north ? 1 : -1;
				}
				if (!coordinate.GetUTMPos(out var utm))
					throw new InvalidOperationException("Coordinate could not locate the terrain feature image.");

				var topLeft = state.Feature_homography * new Vec3D(0, 0, 1);
				var offset = new Vector3(
					(float)(topLeft.x - utm.Easting), 0,
					(float)(topLeft.y - utm.Northing));
				return new TerrainPresentationData(state, center, basis, offset);
			}
		}

		internal Matrix4x4 ShaderBasis(Vec3D globalCamera)
		{
			var orientation = Orientation(globalCamera);
			var basis = Matrix4x4.identity;
			basis.SetColumn(0, orientation.GetCol(0).ToVector3());
			basis.SetColumn(1, orientation.GetCol(2).ToVector3());
			basis.SetColumn(2, orientation.GetCol(1).ToVector3());
			return basis;
		}

		private void RequireConfigured()
		{
			if (_system == null)
				throw new InvalidOperationException("No map coordinate system has been received.");
		}

		/// <summary>Releases the owned coordinate-system wrapper without stopping its platform.</summary>
		public void Dispose()
		{
			_system?.Dispose();
			_system = null;
			Definition = "";
			Origin = default;
		}
	}
}
