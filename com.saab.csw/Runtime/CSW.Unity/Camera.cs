//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: Camera.cs
// Description	: Unity realization of the CSW camera with overridable properties and behaviors
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
// AMO	261104	Moved to namespace CSW.Unity.	(2.12.327)
// AMO	261104	Converts through CSW.Unity.Conversion.	(2.12.327)
// AMO	261104	Renamed CameraBridge to CSW.Unity.Camera with overridable properties, sampling, presentation and commit.	(2.12.327)
// AMO	261104	Installed through CSW.Unity.MapControl.CameraFactory.	(2.12.327)
//
//*****************************************************************************

using CSW.Unity.StreamingMap;
using System;
using GizmoSDK.Coordinate;
using GizmoSDK.GizmoBase;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;

namespace CSW.Unity
{
	/// <summary>Connects a Unity camera to the SceneManager camera of a streaming map.</summary>
	/// <remarks>
	/// Use on the Unity main thread. The Unity camera and the coordinate context are borrowed.
	/// The camera keeps a double-precision global pose, samples it with its projection into each refresh,
	/// presents the live or the committed pose and projection, and rebases presentation on each committed refresh.
	/// The default camera samples the field of view, aspect, near and far distances of the Unity camera and never
	/// writes them. Derive from this class to override camera properties and behaviors, and install the derived
	/// camera through CSW.Unity.MapControl.CameraFactory.
	/// </remarks>
	public class Camera
	{
		/// <summary>Depth nudge of gzPerspCamera's infinite far plane projection.</summary>
		public const float InfiniteFarNudge = 0.995f;

		private Vec3D _globalPosition;
		private Quaternion _rotation = Quaternion.identity;
		private bool _hasPose;
		private Vec3D _referencePosition;
		private bool _hasReference;
		private CameraFrame _committedFrame;
		private bool _synchronizeFrames;
		private bool _projectionOverridden;

		public Camera(UnityEngine.Camera camera, SceneCoordinateContext coordinates)
		{
			UnityCamera = camera ? camera : throw new ArgumentNullException(nameof(camera));
			Coordinates = coordinates ?? throw new ArgumentNullException(nameof(coordinates));
		}

		/// <summary>The borrowed Unity camera that renders the map.</summary>
		public UnityEngine.Camera UnityCamera { get; }
		/// <summary>The borrowed coordinate context of the map.</summary>
		public SceneCoordinateContext Coordinates { get; }

		/// <summary>Native global position in double precision.</summary>
		public virtual Vec3D GlobalPosition => _globalPosition;
		/// <summary>Unity orientation of the camera.</summary>
		public virtual Quaternion Rotation => _rotation;
		/// <summary>Whether the camera has a global pose to sample and present.</summary>
		public virtual bool HasPose => _hasPose;
		/// <summary>Vertical field of view in degrees.</summary>
		public virtual float FieldOfView => UnityCamera.fieldOfView;
		/// <summary>Width divided by height of the view.</summary>
		public virtual float Aspect => UnityCamera.aspect;
		/// <summary>Near distance in metres.</summary>
		public virtual float NearDistance => UnityCamera.nearClipPlane;
		/// <summary>Far distance in metres; still submitted with an infinite far plane.</summary>
		public virtual float FarDistance => UnityCamera.farClipPlane;
		/// <summary>Submits and presents gzPerspCamera's infinite far plane; the Unity camera's far plane stays finite.</summary>
		public virtual bool InfiniteFarPlane { get; set; }
		/// <summary>Vertical field of view in degrees at which a sampled LOD factor applies unscaled.</summary>
		/// <remarks>A value outside (0, 180) disables zoom scaling.</remarks>
		public float LodReferenceFieldOfView { get; set; }
		/// <summary>Whether presentation writes the projection of the Unity camera and repeats before it renders.</summary>
		/// <remarks>Override to return true when overriding the projection properties, so that the rendered and sampled frustum agree.</remarks>
		public virtual bool OwnsProjection => _synchronizeFrames || InfiniteFarPlane;

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
					Present();
			}
		}

		/// <summary>Whether a refresh has been committed since the last pose reset.</summary>
		protected bool HasCommittedFrame => _hasReference;
		/// <summary>The last committed refresh; valid when HasCommittedFrame is true.</summary>
		protected CameraFrame CommittedFrame => _committedFrame;

		/// <summary>Clears pose initialization without taking ownership of the Unity camera.</summary>
		public void ResetPose()
		{
			_hasPose = false;
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
			_globalPosition = position;
			_rotation = rotation.normalized;
			_hasPose = true;
		}

		/// <summary>Converts a geodetic position through the borrowed map coordinate context.</summary>
		public void SetGeodeticPose(LatPos position, Quaternion rotation)
		{
			SetPose(Coordinates.FromGeodetic(position), rotation);
		}

		internal void InitializeFromMap(double altitude)
		{
			if (HasPose)
				return;
			var basis = Coordinates.Orientation(Coordinates.Origin);
			var up = basis.GetCol(2);
			var north = basis.GetCol(1);
			var direction = north * 0.70710678f - up * 0.70710678f;
			SetPose(
				Coordinates.Origin + (Vec3D)(up * (float)altitude),
				Quaternion.LookRotation(
					Conversion.ToUnityDirection(direction),
					Conversion.ToUnityDirection(up)));
		}

		/// <summary>Moves the stored global pose by a camera-local displacement in metres.</summary>
		public void Move(Vector3 cameraLocalMetres)
		{
			if (!_hasPose)
				throw new InvalidOperationException("The camera has no global pose.");
			_globalPosition += (Vec3D)Conversion.ToGizmoDirection(_rotation * cameraLocalMetres);
		}

		/// <summary>Rotates the stored pose around local map up and camera right.</summary>
		public void Rotate(float headingDegrees, float pitchDegrees)
		{
			if (!_hasPose)
				return;
			var up = Conversion.ToUnityDirection(Coordinates.Orientation(_globalPosition).GetCol(2));
			_rotation = Quaternion.AngleAxis(headingDegrees, up) * _rotation *
				Quaternion.AngleAxis(pitchDegrees, Vector3.right);
		}

		/// <summary>Returns the LOD factor to submit for a requested factor and vertical field of view.</summary>
		/// <remarks>The default scales lodFactor by zoom against LodReferenceFieldOfView.</remarks>
		public virtual float LodFactor(float lodFactor, float verticalFieldOfView)
		{
			return EffectiveLodFactor(lodFactor, verticalFieldOfView, LodReferenceFieldOfView);
		}

		/// <summary>Returns the projection that renders and culls a frustum, in Unity's camera convention.</summary>
		/// <remarks>The default is gzPerspCamera's projection, NativeProjection.</remarks>
		public virtual Matrix4x4 Projection(
			float verticalFieldOfView, float aspect, float nearDistance, float farDistance, bool infiniteFar)
		{
			return NativeProjection(verticalFieldOfView, aspect, nearDistance, farDistance, infiniteFar);
		}

		/// <summary>Copies the current pose and projection into a refresh without moving the Unity camera.</summary>
		/// <remarks>The frame's LOD factor is LodFactor(lodFactor, FieldOfView).</remarks>
		public virtual CameraFrame Sample(double time, float lodFactor = 1)
		{
			if (!HasPose)
				throw new InvalidOperationException("The camera has no global pose.");
			var fieldOfView = FieldOfView;
			return new CameraFrame(
				GlobalPosition, Conversion.ToGizmoRotation(Rotation), time,
				fieldOfView, Aspect, NearDistance, FarDistance,
				(uint)Math.Max(1, UnityCamera.pixelWidth), (uint)Math.Max(1, UnityCamera.pixelHeight),
				LodFactor(lodFactor, fieldOfView), infiniteFar: InfiniteFarPlane);
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

		/// <summary>Presents the live pose, or the committed pose and projection when synchronized, on the Unity camera.</summary>
		/// <remarks>
		/// The live position is the double-precision offset from the committed reference. MapControl calls this in
		/// LateUpdate and, while OwnsProjection is true, again before the Unity camera renders.
		/// </remarks>
		public virtual void Present()
		{
			if (_synchronizeFrames)
			{
				if (!_hasReference)
					return;
				UnityCamera.transform.SetPositionAndRotation(Vector3.zero,
					Conversion.ToUnityRotation(_committedFrame.Hpr));
				OverrideProjection(Projection(_committedFrame.VerticalFieldOfView, _committedFrame.Aspect,
					_committedFrame.NearDistance, _committedFrame.FarDistance, _committedFrame.InfiniteFar));
				return;
			}
			if (HasPose && _hasReference)
				UnityCamera.transform.SetPositionAndRotation(
					Conversion.ToUnityPosition(GlobalPosition - _referencePosition), Rotation);
			else if (HasPose)
				UnityCamera.transform.rotation = Rotation;
			if (OwnsProjection)
				OverrideProjection(Projection(FieldOfView, Aspect, NearDistance, FarDistance, InfiniteFarPlane));
			else
				ReleaseProjectionOverride();
		}

		/// <summary>Renders and culls with one projection at the camera's current pose.</summary>
		protected void OverrideProjection(Matrix4x4 projection)
		{
			UnityCamera.projectionMatrix = projection;
			UnityCamera.cullingMatrix = projection * UnityCamera.worldToCameraMatrix;
			_projectionOverridden = true;
		}

		/// <summary>Returns projection ownership to the Unity camera.</summary>
		protected void ReleaseProjectionOverride()
		{
			if (_projectionOverridden && UnityCamera)
			{
				UnityCamera.ResetProjectionMatrix();
				UnityCamera.ResetCullingMatrix();
			}
			_projectionOverridden = false;
		}

		/// <summary>Rebases presentation with the committed scene and shader origin.</summary>
		/// <remarks>Synchronized presentation also shows the committed frame's projection.</remarks>
		public virtual void Commit(CameraFrame frame)
		{
			_referencePosition = frame.GlobalPosition;
			_committedFrame = frame;
			_hasReference = true;
			if (HasPose || _synchronizeFrames)
				Present();
			else
				UnityCamera.transform.SetPositionAndRotation(
					Vector3.zero, Conversion.ToUnityRotation(frame.Hpr));

			const double tiling = 5000;
			Shader.SetGlobalVector("_WorldOffset", new Vector3(
				(float)(frame.GlobalPosition.x % tiling),
				(float)Math.Max(-float.MaxValue, Math.Min(float.MaxValue, frame.GlobalPosition.y)),
				-(float)(frame.GlobalPosition.z % tiling)));
			Shader.SetGlobalMatrix("_LocalToEUN", Coordinates.ShaderBasis(frame.GlobalPosition));
		}
	}
}
