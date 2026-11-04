//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: Conversion.cs
// Description	: Engine step of the conversions between GizmoSDK and Unity values
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	180607	Created file as UnityGizmoExtensions.	(2.9.1)
// AMO	261104	Renamed to CSW.Unity.Conversion with the axis mapping of GizmoUnityAxisMapping and the COORD-046 names.	(2.12.327)
//
//*****************************************************************************

using GizmoSDK.GizmoBase;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;

namespace CSW.Unity
{
	/// <summary>Converts values between GizmoSDK and Unity; the engine step of REQ-CSW-COORD-046.</summary>
	/// <remarks>
	/// The ToUnity and ToGizmo methods apply the Viewer Coordinate Mapping of Unity, the negation of Z between the
	/// native Gizmo3D scene and the Unity scene. Positions are positions in a Local 3D Frame; translation to and from
	/// the frame origin belongs to the localization step. The Copy methods copy components without any axis,
	/// handedness or unit change. No method allocates managed memory.
	/// </remarks>
	public static class Conversion
	{
		/// <summary>Returns the Viewer Coordinate Mapping as a Unity matrix; it is its own inverse.</summary>
		public static Matrix4x4 UnityBasis => Matrix4x4.Scale(UnityRootScale);

		/// <summary>Returns the scale of the presentation root that maps copied native transforms to Unity.</summary>
		public static Vector3 UnityRootScale => new Vector3(1, 1, -1);

		#region ----- Engine step --------------------------

		/// <summary>Returns the Unity position of a position in a Local 3D Frame, narrowed to float last.</summary>
		public static Vector3 ToUnityPosition(Vec3D local)
		{
			return new Vector3((float)local.x, (float)local.y, (float)-local.z);
		}

		/// <summary>Returns the position in a Local 3D Frame of a Unity position.</summary>
		public static Vec3D ToGizmoPosition(Vector3 position)
		{
			return new Vec3D(position.x, position.y, -(double)position.z);
		}

		public static Vector3 ToUnityDirection(Vec3 direction)
		{
			return new Vector3(direction.x, direction.y, -direction.z);
		}

		public static Vec3 ToGizmoDirection(Vector3 direction)
		{
			return new Vec3(direction.x, direction.y, -direction.z);
		}

		/// <summary>Returns the unit Unity normal of a GizmoSDK normal.</summary>
		public static Vector3 ToUnityNormal(Vec3 normal)
		{
			return ToUnityDirection(normal).normalized;
		}

		/// <summary>Returns the unit GizmoSDK normal of a Unity normal.</summary>
		public static Vec3 ToGizmoNormal(Vector3 normal)
		{
			return ToGizmoDirection(normal.normalized);
		}

		/// <summary>Returns the Unity scale of a GizmoSDK scale.</summary>
		/// <remarks>The Z negation commutes with an axis-aligned scale, so the components are unchanged.</remarks>
		public static Vector3 ToUnityScale(Vec3 scale)
		{
			return new Vector3(scale.x, scale.y, scale.z);
		}

		public static Vec3 ToGizmoScale(Vector3 scale)
		{
			return new Vec3(scale.x, scale.y, scale.z);
		}

		/// <summary>Returns the Unity rotation of a GizmoSDK heading, pitch and roll in degrees.</summary>
		/// <remarks>
		/// The rotation R is mapped as M R M, with M the Z negation, so its determinant stays +1. GizmoSDK looks along
		/// -Z and Unity along +Z, so a GizmoSDK camera and its Unity camera look in the same direction.
		/// </remarks>
		public static Quaternion ToUnityRotation(Vec3 hpr)
		{
			var rotation = Matrix3.CreateFrom_Euler_YXZ(
				hpr.x * Mathf.Deg2Rad,
				hpr.y * Mathf.Deg2Rad,
				-hpr.z * Mathf.Deg2Rad);
			return Quaternion.LookRotation(
				ToUnityDirection(rotation * new Vec3(0, 0, -1)),
				ToUnityDirection(rotation * new Vec3(0, 1, 0)));
		}

		/// <summary>Returns the GizmoSDK heading, pitch and roll in degrees of a Unity rotation.</summary>
		public static Vec3 ToGizmoRotation(Quaternion rotation)
		{
			var right = ToGizmoDirection(rotation * Vector3.right);
			var up = ToGizmoDirection(rotation * Vector3.up);
			var backward = ToGizmoDirection(-(rotation * Vector3.forward));
			var matrix = new Matrix3(right, up, backward);
			matrix.Decompose_Euler_YXZ(out var heading, out var pitch, out var roll);
			return new Vec3(
				heading * Mathf.Rad2Deg,
				pitch * Mathf.Rad2Deg,
				-roll * Mathf.Rad2Deg);
		}

		/// <summary>Returns the Unity matrix M A M of a GizmoSDK matrix A, with M the Z negation.</summary>
		public static Matrix4x4 ToUnityMatrix(Matrix4 matrix)
		{
			return NegateZ(matrix.CopyMatrix4x4());
		}

		/// <summary>Returns the GizmoSDK matrix M A M of a Unity matrix A, with M the Z negation.</summary>
		public static Matrix4 ToGizmoMatrix(Matrix4x4 matrix)
		{
			return NegateZ(matrix).CopyMatrix4();
		}

		private static Matrix4x4 NegateZ(Matrix4x4 matrix)
		{
			matrix.m02 = -matrix.m02;
			matrix.m12 = -matrix.m12;
			matrix.m32 = -matrix.m32;
			matrix.m20 = -matrix.m20;
			matrix.m21 = -matrix.m21;
			matrix.m23 = -matrix.m23;
			return matrix;
		}

		#endregion

		#region ----- Copy --------------------------

		public static Vec3D CopyVec3D(this Vector3 vec)
		{
			return new Vec3D(vec.x, vec.y, vec.z);
		}

		public static Vec3 CopyVec3(this Vector3 vec)
		{
			return new Vec3(vec.x, vec.y, vec.z);
		}

		public static Vector3 CopyVector3(this Vec3D vec)
		{
			return new Vector3((float)vec.x, (float)vec.y, (float)vec.z);
		}

		public static Vector3 CopyVector3(this Vec3 vec)
		{
			return new Vector3(vec.x, vec.y, vec.z);
		}

		public static Matrix4 CopyMatrix4(this Matrix4x4 matrix)
		{
			return new Matrix4
			{
				v11 = matrix.m00,
				v12 = matrix.m01,
				v13 = matrix.m02,
				v14 = matrix.m03,

				v21 = matrix.m10,
				v22 = matrix.m11,
				v23 = matrix.m12,
				v24 = matrix.m13,

				v31 = matrix.m20,
				v32 = matrix.m21,
				v33 = matrix.m22,
				v34 = matrix.m23,

				v41 = matrix.m30,
				v42 = matrix.m31,
				v43 = matrix.m32,
				v44 = matrix.m33,
			};
		}

		public static Matrix4x4 CopyMatrix4x4(this Matrix4 matrix)
		{
			return new Matrix4x4
			{
				m00 = matrix.v11,
				m01 = matrix.v12,
				m02 = matrix.v13,
				m03 = matrix.v14,

				m10 = matrix.v21,
				m11 = matrix.v22,
				m12 = matrix.v23,
				m13 = matrix.v24,

				m20 = matrix.v31,
				m21 = matrix.v32,
				m22 = matrix.v33,
				m23 = matrix.v34,

				m30 = matrix.v41,
				m31 = matrix.v42,
				m32 = matrix.v43,
				m33 = matrix.v44
			};
		}

		#endregion
	}
}
