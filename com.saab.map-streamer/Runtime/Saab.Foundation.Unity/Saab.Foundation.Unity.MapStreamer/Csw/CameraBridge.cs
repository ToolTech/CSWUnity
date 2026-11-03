//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: CameraBridge.cs
// Description	: Double global flight pose and live presentation in committed reference frames
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Added double global camera snapshots and camera-relative frame publication.	(2.12.327)
// AMO	261023	Presented live rotation and double-precision translation across deferred native reference rebases.	(2.12.327)
// AMO	261026	Added committed-frame pose and projection presentation with reversible synchronization.	(2.12.327)
// AMO	261103	Matched gzPerspCamera's finite and infinite far plane projection and culling, scaled the LOD factor by zoom and kept host projection settings.	(2.12.327)
//
//*****************************************************************************

using System;
using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;
using UnityEngine;
using Camera = UnityEngine.Camera;
using Quaternion = UnityEngine.Quaternion;

namespace Saab.Foundation.Unity.MapStreamer.Csw
{
	/// <summary>Immutable native camera pose and projection captured for one refresh.</summary>
	/// <remarks>Position remains double precision; this value owns no native references.</remarks>
	public readonly struct CameraRelativeFrame
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
		public Matrix4x4 Projection => CameraBridge.NativeProjection(
			VerticalFieldOfView, Aspect, NearDistance, FarDistance, InfiniteFar);

		public CameraRelativeFrame(
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
			HorizontalFieldOfView = Camera.VerticalToHorizontalFieldOfView(verticalFieldOfView, aspect);
			NearDistance = nearDistance;
			FarDistance = farDistance;
			Width = width;
			Height = height;
			LodFactor = lodFactor;
			InfiniteFar = infiniteFar;
		}

		internal CameraRelativeFrame WithNativePose(uint refreshId, double time, Vec3D position, Vec3 hpr)
		{
			return new CameraRelativeFrame(
				position, hpr, time, VerticalFieldOfView, Aspect,
				NearDistance, FarDistance, Width, Height, LodFactor, refreshId, InfiniteFar);
		}

		private static bool Finite(double value)
		{
			return !double.IsNaN(value) && !double.IsInfinity(value);
		}
	}

	/// <summary>Presents a live double global pose or its synchronized committed camera frame.</summary>
	/// <remarks>
	/// Use on the Unity main thread. The camera and coordinate context are borrowed.
	/// The camera's field of view, aspect, near and far planes define the submitted frustum and are never written.
	/// </remarks>
	public sealed class CameraBridge
	{
		/// <summary>Depth nudge of gzPerspCamera's infinite far plane projection.</summary>
		public const float InfiniteFarNudge = 0.995f;

		private readonly Camera _camera;
		private readonly SceneCoordinateContext _coordinates;
		private Quaternion _rotation = Quaternion.identity;
		private Vec3D _referencePosition;
		private bool _hasReference;
		private CameraRelativeFrame _committedFrame;
		private bool _synchronizeFrames;
		private bool _projectionOverridden;

		public Vec3D GlobalPosition { get; private set; }
		public bool HasPose { get; private set; }
		public Quaternion Rotation => _rotation;
		/// <summary>Submits and presents gzPerspCamera's infinite far plane; the camera's far plane stays finite.</summary>
		public bool InfiniteFarPlane { get; set; }
		/// <summary>Vertical field of view in degrees at which a sampled LOD factor applies unscaled.</summary>
		/// <remarks>A value outside (0, 180) disables zoom scaling.</remarks>
		public float LodReferenceFieldOfView { get; set; }
		/// <summary>Presents only the committed pose/projection; desired input remains independently writable.</summary>
		public bool SynchronizeFrames
		{
			get => _synchronizeFrames;
			set
			{
				_synchronizeFrames = value;
				if (!value)
					ReleaseProjectionOverride();
				else
					PresentPose();
			}
		}

		public CameraBridge(Camera camera, SceneCoordinateContext coordinates)
		{
			_camera = camera ? camera : throw new ArgumentNullException(nameof(camera));
			_coordinates = coordinates ?? throw new ArgumentNullException(nameof(coordinates));
		}

		/// <summary>Clears pose initialization without taking ownership of the Unity camera.</summary>
		public void ResetPose()
		{
			HasPose = false;
			_hasReference = false;
			ReleaseProjectionOverride();
		}

		/// <summary>Copies a finite native global position and Unity camera orientation.</summary>
		public void SetPose(Vec3D position, Quaternion rotation)
		{
			if (double.IsNaN(position.x) || double.IsInfinity(position.x) ||
				double.IsNaN(position.y) || double.IsInfinity(position.y) ||
				double.IsNaN(position.z) || double.IsInfinity(position.z) ||
				!float.IsFinite(rotation.x) || !float.IsFinite(rotation.y) ||
				!float.IsFinite(rotation.z) || !float.IsFinite(rotation.w) ||
				Quaternion.Dot(rotation, rotation) < 1e-12f)
				throw new ArgumentException("The global camera pose must be finite with a nonzero rotation.");
			GlobalPosition = position;
			_rotation = rotation.normalized;
			HasPose = true;
		}

		/// <summary>Converts a geodetic position through the borrowed map coordinate context.</summary>
		public void SetGeodeticPose(LatPos position, Quaternion rotation)
		{
			SetPose(_coordinates.FromGeodetic(position), rotation);
		}

		internal void InitializeFromMap(double altitude)
		{
			if (HasPose)
				return;
			var basis = _coordinates.Orientation(_coordinates.Origin);
			var up = basis.GetCol(2);
			var north = basis.GetCol(1);
			var direction = north * 0.70710678f - up * 0.70710678f;
			SetPose(
				_coordinates.Origin + (Vec3D)(up * (float)altitude),
				Quaternion.LookRotation(
					_coordinates.Axes.ToUnityDirection(direction),
					_coordinates.Axes.ToUnityDirection(up)));
		}

		/// <summary>Moves the stored global pose by a camera-local displacement in metres.</summary>
		public void Move(Vector3 cameraLocalMetres)
		{
			if (!HasPose)
				throw new InvalidOperationException("The camera has no global pose.");
			GlobalPosition += (Vec3D)_coordinates.Axes.ToNativeDirection(_rotation * cameraLocalMetres);
		}

		/// <summary>Rotates the stored pose around local map up and camera right.</summary>
		public void Rotate(float headingDegrees, float pitchDegrees)
		{
			if (!HasPose)
				return;
			var up = _coordinates.Axes.ToUnityDirection(_coordinates.Orientation(GlobalPosition).GetCol(2));
			_rotation = Quaternion.AngleAxis(headingDegrees, up) * _rotation *
				Quaternion.AngleAxis(pitchDegrees, Vector3.right);
		}

		/// <summary>Copies the current pose and projection without moving the Unity camera.</summary>
		/// <remarks>The frame's LOD factor is lodFactor scaled by zoom against LodReferenceFieldOfView.</remarks>
		public CameraRelativeFrame Sample(double time, float lodFactor = 1)
		{
			if (!HasPose)
				throw new InvalidOperationException("The camera has no global pose.");
			var fieldOfView = _camera.fieldOfView;
			return new CameraRelativeFrame(
				GlobalPosition, _coordinates.Axes.CameraHpr(_rotation), time,
				fieldOfView, _camera.aspect,
				_camera.nearClipPlane, _camera.farClipPlane,
				(uint)Math.Max(1, _camera.pixelWidth), (uint)Math.Max(1, _camera.pixelHeight),
				EffectiveLodFactor(lodFactor, fieldOfView, LodReferenceFieldOfView), infiniteFar: InfiniteFarPlane);
		}

		/// <summary>Returns gzPerspCamera's projection for a vertical field of view in degrees.</summary>
		/// <remarks>
		/// The finite projection equals Matrix4x4.Perspective. The infinite projection maps near to
		/// -InfiniteFarNudge and infinity toward +InfiniteFarNudge, so nothing beyond near is clipped or culled.
		/// </remarks>
		public static Matrix4x4 NativeProjection(
			float verticalFieldOfView, float aspect, float nearDistance, float farDistance, bool infiniteFar)
		{
			var projection = Matrix4x4.Perspective(verticalFieldOfView, aspect, nearDistance, farDistance);
			if (infiniteFar)
			{
				projection.m22 = -InfiniteFarNudge;
				projection.m23 = -2 * nearDistance * InfiniteFarNudge;
			}
			return projection;
		}

		/// <summary>Returns GizmoSDK's effective LOD factor, lodFactor times the zoom of a reference field of view.</summary>
		/// <remarks>Zoom is tan(reference/2)/tan(fov/2); a reference outside (0, 180) degrees returns lodFactor.</remarks>
		public static float EffectiveLodFactor(float lodFactor, float verticalFieldOfView, float referenceFieldOfView)
		{
			if (!(referenceFieldOfView > 0 && referenceFieldOfView < 180))
				return lodFactor;
			const double halfAngle = Math.PI / 360;
			return (float)(lodFactor * Math.Tan(referenceFieldOfView * halfAngle) /
				Math.Tan(verticalFieldOfView * halfAngle));
		}

		internal void PresentPose()
		{
			if (_synchronizeFrames)
			{
				if (!_hasReference)
					return;
				_camera.transform.SetPositionAndRotation(Vector3.zero,
					_coordinates.Axes.CameraRotation(_committedFrame.Hpr));
				OverrideProjection(_committedFrame.Projection);
				return;
			}
			if (HasPose && _hasReference)
				_camera.transform.SetPositionAndRotation(
					_coordinates.Axes.ToUnityPosition(GlobalPosition, _referencePosition), _rotation);
			else if (HasPose)
				_camera.transform.rotation = _rotation;
			if (InfiniteFarPlane)
				OverrideProjection(NativeProjection(_camera.fieldOfView, _camera.aspect,
					_camera.nearClipPlane, _camera.farClipPlane, true));
			else
				ReleaseProjectionOverride();
		}

		/// <summary>Renders and culls with one projection at the camera's current pose.</summary>
		private void OverrideProjection(Matrix4x4 projection)
		{
			_camera.projectionMatrix = projection;
			_camera.cullingMatrix = projection * _camera.worldToCameraMatrix;
			_projectionOverridden = true;
		}

		private void ReleaseProjectionOverride()
		{
			if (_projectionOverridden && _camera)
			{
				_camera.ResetProjectionMatrix();
				_camera.ResetCullingMatrix();
			}
			_projectionOverridden = false;
		}

		/// <summary>Rebases presentation with the committed scene and shader origin.</summary>
		/// <remarks>Synchronized presentation also shows the committed frame's projection.</remarks>
		public void Commit(CameraRelativeFrame frame)
		{
			_referencePosition = frame.GlobalPosition;
			_committedFrame = frame;
			_hasReference = true;
			if (HasPose || _synchronizeFrames)
				PresentPose();
			else
				_camera.transform.SetPositionAndRotation(
					Vector3.zero, _coordinates.Axes.CameraRotation(frame.Hpr));

			const double tiling = 5000;
			Shader.SetGlobalVector("_WorldOffset", new Vector3(
				(float)(frame.GlobalPosition.x % tiling),
				(float)Math.Max(-float.MaxValue, Math.Min(float.MaxValue, frame.GlobalPosition.y)),
				-(float)(frame.GlobalPosition.z % tiling)));
			Shader.SetGlobalMatrix("_LocalToEUN", _coordinates.ShaderBasis(frame.GlobalPosition));
		}
	}
}
