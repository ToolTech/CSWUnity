//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: CameraFrame.cs
// Description	: Immutable native camera pose and projection of one refresh
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Added double global camera snapshots.	(2.12.327)
// AMO	261103	Added gzPerspCamera's finite and infinite far plane projection and the LOD factor.	(2.12.327)
// AMO	261104	Renamed CameraRelativeFrame to CSW.Unity.CameraFrame in its own file.	(2.12.327)
//
//*****************************************************************************

using System;
using GizmoSDK.GizmoBase;
using UnityEngine;

namespace CSW.Unity
{
	/// <summary>Immutable native camera pose and projection captured for one refresh.</summary>
	/// <remarks>Position remains double precision; this value owns no native references.</remarks>
	public readonly struct CameraFrame
	{
		public uint RefreshId { get; }
		public double RenderTime { get; }
		public Vec3D GlobalPosition { get; }
		public Vec3 Hpr { get; }
		public float VerticalFieldOfView { get; }
		public float HorizontalFieldOfView { get; }
		public float Aspect { get; }
		public float NearDistance { get; }
		public float FarDistance { get; }
		public uint Width { get; }
		public uint Height { get; }
		public float LodFactor { get; }
		/// <summary>Selects gzPerspCamera's infinite far plane; FarDistance is still submitted.</summary>
		public bool InfiniteFar { get; }
		/// <summary>gzPerspCamera's projection for this frustum, in Unity's camera convention.</summary>
		public Matrix4x4 Projection => Camera.NativeProjection(
			VerticalFieldOfView, Aspect, NearDistance, FarDistance, InfiniteFar);

		public CameraFrame(
			Vec3D globalPosition,
			Vec3 hpr,
			double renderTime,
			float verticalFieldOfView,
			float aspect,
			float nearDistance,
			float farDistance,
			uint width,
			uint height,
			float lodFactor = 1,
			uint refreshId = 0,
			bool infiniteFar = false)
		{
			if (!Finite(globalPosition.x) || !Finite(globalPosition.y) || !Finite(globalPosition.z) ||
				!Finite(hpr.x) || !Finite(hpr.y) || !Finite(hpr.z) || !Finite(renderTime) ||
				!Finite(verticalFieldOfView) || verticalFieldOfView <= 0 || verticalFieldOfView >= 180 ||
				!Finite(aspect) || aspect <= 0 || !Finite(nearDistance) || nearDistance <= 0 ||
				!Finite(farDistance) || farDistance <= nearDistance ||
				width == 0 || height == 0 || !Finite(lodFactor) || lodFactor <= 0)
				throw new ArgumentException("A camera frame requires a finite pose, valid projection and nonempty viewport.");
			RefreshId = refreshId;
			RenderTime = renderTime;
			GlobalPosition = globalPosition;
			Hpr = hpr;
			VerticalFieldOfView = verticalFieldOfView;
			Aspect = aspect;
			HorizontalFieldOfView = UnityEngine.Camera.VerticalToHorizontalFieldOfView(verticalFieldOfView, aspect);
			NearDistance = nearDistance;
			FarDistance = farDistance;
			Width = width;
			Height = height;
			LodFactor = lodFactor;
			InfiniteFar = infiniteFar;
		}

		internal CameraFrame WithNativePose(uint refreshId, double time, Vec3D position, Vec3 hpr)
		{
			return new CameraFrame(
				position, hpr, time, VerticalFieldOfView, Aspect,
				NearDistance, FarDistance, Width, Height, LodFactor, refreshId, InfiniteFar);
		}

		private static bool Finite(double value)
		{
			return !double.IsNaN(value) && !double.IsInfinity(value);
		}
	}
}
