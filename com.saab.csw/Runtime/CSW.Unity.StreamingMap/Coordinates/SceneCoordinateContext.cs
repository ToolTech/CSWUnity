//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: SceneCoordinateContext.cs
// Description	: Typed map coordinates of the streamed map
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Added owned coordinate context and typed terrain and camera presentation conversions.	(2.12.327)
// AMO	261104	Moved to namespace CSW.Unity.StreamingMap.	(2.12.327)
// AMO	261104	Moved the axis mapping of GizmoUnityAxisMapping to CSW.Unity.Conversion.	(2.12.327)
// AMO	261104	Names CSW.Unity.Platform as the Coordinate platform owner.	(2.12.327)
//
//*****************************************************************************

using System;
using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;
using UnityEngine;

namespace CSW.Unity.StreamingMap
{
	/// <summary>Owns the parsed native map coordinate system used by presentation conversions.</summary>
	/// <remarks>
	/// Use on the Unity main thread. CSW.Unity.Platform owns Coordinate platform lifetime;
	/// disposing this context releases its coordinate-system reference only.
	/// </remarks>
	public sealed class SceneCoordinateContext : IDisposable
	{
		private CoordinateSystem _system;

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
			basis.SetColumn(0, local.MultiplyVector(orientation.GetCol(0).CopyVector3()));
			basis.SetColumn(1, local.MultiplyVector(orientation.GetCol(2).CopyVector3()));
			basis.SetColumn(2, local.MultiplyVector(-orientation.GetCol(1).CopyVector3()));

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
			basis.SetColumn(0, orientation.GetCol(0).CopyVector3());
			basis.SetColumn(1, orientation.GetCol(2).CopyVector3());
			basis.SetColumn(2, orientation.GetCol(1).CopyVector3());
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
