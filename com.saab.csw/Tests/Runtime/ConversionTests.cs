//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: ConversionTests.cs
// Description	: Tests of the engine step and the copies of CSW.Unity.Conversion
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261104	Created file from the axis mapping test of SceneCommandTests.	(2.12.327)
//
//*****************************************************************************

using GizmoSDK.GizmoBase;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Quaternion = UnityEngine.Quaternion;

namespace CSW.Unity.Tests
{
	public sealed class ConversionTests
	{
		private const float Tolerance = 1e-5f;

		[Test]
		public void PositionsNegateZAndKeepDoublePrecisionUntilTheFinalNarrowing()
		{
			var local = new Vec3D(1000000000.25, 2000000000.5, 3000000000.75) -
				new Vec3D(1000000000, 2000000000, 3000000000);
			Assert.That(Conversion.ToUnityPosition(local), Is.EqualTo(new Vector3(0.25f, 0.5f, -0.75f)));
			var gizmo = Conversion.ToGizmoPosition(new Vector3(1.25f, -2.5f, 3.75f));
			Assert.That(gizmo.x, Is.EqualTo(1.25));
			Assert.That(gizmo.y, Is.EqualTo(-2.5));
			Assert.That(gizmo.z, Is.EqualTo(-3.75));
			Assert.That(Conversion.ToUnityPosition(gizmo), Is.EqualTo(new Vector3(1.25f, -2.5f, 3.75f)));
		}

		[Test]
		public void DirectionsNormalsAndScalesFollowTheZNegation()
		{
			Assert.That(Conversion.ToUnityDirection(new Vec3(1, 0, 0)), Is.EqualTo(Vector3.right));
			Assert.That(Conversion.ToUnityDirection(new Vec3(0, 1, 0)), Is.EqualTo(Vector3.up));
			Assert.That(Conversion.ToUnityDirection(new Vec3(0, 0, 1)), Is.EqualTo(Vector3.back));
			var direction = new Vector3(1.25f, -2.5f, 3.75f);
			Assert.That(Conversion.ToUnityDirection(Conversion.ToGizmoDirection(direction)), Is.EqualTo(direction));

			Assert.That(Conversion.ToUnityNormal(new Vec3(0, 0, 4)), Is.EqualTo(Vector3.back));
			var normal = Conversion.ToGizmoNormal(new Vector3(0, 3, 4));
			Assert.That(normal.y, Is.EqualTo(0.6f).Within(Tolerance));
			Assert.That(normal.z, Is.EqualTo(-0.8f).Within(Tolerance));

			Assert.That(Conversion.ToUnityScale(new Vec3(2, 3, 4)), Is.EqualTo(new Vector3(2, 3, 4)));
			Assert.That(Conversion.ToUnityScale(Conversion.ToGizmoScale(new Vector3(5, 6, 7))), Is.EqualTo(new Vector3(5, 6, 7)));
		}

		[Test]
		public void RotationsRoundTripAndLookAlongTheSameDirection()
		{
			Assert.That(Quaternion.Angle(Conversion.ToUnityRotation(new Vec3(0, 0, 0)), Quaternion.identity),
				Is.LessThan(0.01f), "A GizmoSDK camera at zero looks along -Z, the Unity camera along +Z.");
			var rotation = Quaternion.Euler(21, 37, -13);
			Assert.That(Quaternion.Angle(rotation, Conversion.ToUnityRotation(Conversion.ToGizmoRotation(rotation))),
				Is.LessThan(0.02f));
			Assert.That(Matrix4x4.Rotate(Conversion.ToUnityRotation(new Vec3(30, -20, 10))).determinant,
				Is.EqualTo(1).Within(Tolerance));
		}

		[Test]
		public void MatricesMapGizmoTransformsToTheSameUnityTransforms()
		{
			var gizmo = Matrix4x4.TRS(new Vector3(10, 20, 30), Quaternion.Euler(21, 37, -13), Vector3.one).CopyMatrix4();
			var unity = Conversion.ToUnityMatrix(gizmo);
			Assert.That(unity.determinant, Is.EqualTo(1).Within(Tolerance), "The rotation keeps determinant +1.");

			var point = new Vec3D(1.5, -2, 3);
			var transformed = gizmo.CopyMatrix4x4().MultiplyPoint3x4(point.CopyVector3()).CopyVec3D();
			var expected = Conversion.ToUnityPosition(transformed);
			var actual = unity.MultiplyPoint3x4(Conversion.ToUnityPosition(point));
			Assert.That(Vector3.Distance(expected, actual), Is.LessThan(Tolerance));

			Assert.That(Conversion.ToGizmoMatrix(unity).CopyMatrix4x4(), Is.EqualTo(gizmo.CopyMatrix4x4()));
			Assert.That(Conversion.UnityBasis * gizmo.CopyMatrix4x4() * Conversion.UnityBasis, Is.EqualTo(unity));
		}

		[Test]
		public void CopiesKeepTheComponentsAndTheTranslationColumn()
		{
			Assert.That(new Vec3D(1, 2, 3).CopyVector3(), Is.EqualTo(new Vector3(1, 2, 3)));
			Assert.That(new Vec3(4, 5, 6).CopyVector3(), Is.EqualTo(new Vector3(4, 5, 6)));
			Assert.That(new Vector3(7, 8, 9).CopyVec3D().CopyVector3(), Is.EqualTo(new Vector3(7, 8, 9)));
			Assert.That(new Vector3(7, 8, 9).CopyVec3().CopyVector3(), Is.EqualTo(new Vector3(7, 8, 9)));
			var matrix = Matrix4x4.Translate(new Vector3(10, 20, 30)).CopyMatrix4();
			Assert.That(matrix.v14, Is.EqualTo(10));
			Assert.That(matrix.v24, Is.EqualTo(20));
			Assert.That(matrix.v34, Is.EqualTo(30));
			Assert.That(Conversion.UnityRootScale, Is.EqualTo(new Vector3(1, 1, -1)));
		}

		[Test]
		public void ConversionsAllocateNoManagedMemory()
		{
			var position = new Vec3D(1, 2, 3);
			var direction = new Vec3(0, 1, 0);
			var hpr = new Vec3(30, -20, 10);
			var rotation = Quaternion.Euler(21, 37, -13);
			var matrix = Matrix4x4.TRS(new Vector3(10, 20, 30), rotation, Vector3.one);
			TestDelegate convert = () =>
			{
				Conversion.ToGizmoPosition(Conversion.ToUnityPosition(position));
				Conversion.ToGizmoDirection(Conversion.ToUnityDirection(direction));
				Conversion.ToGizmoNormal(Conversion.ToUnityNormal(direction));
				Conversion.ToGizmoScale(Conversion.ToUnityScale(direction));
				Conversion.ToGizmoRotation(Conversion.ToUnityRotation(hpr));
				Conversion.ToGizmoMatrix(Conversion.ToUnityMatrix(matrix.CopyMatrix4()));
			};
			convert();
			Assert.That(() => { _ = new int[16]; }, Is.AllocatingGCMemory(),
				"The GC allocation recorder must observe a known allocation for this test to be meaningful.");
			Assert.That(convert, Is.Not.AllocatingGCMemory());
		}
	}
}
