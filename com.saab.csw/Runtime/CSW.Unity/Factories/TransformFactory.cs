//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: TransformFactory.cs
// Description	: Built-in factories of transformed GizmoSDK groups
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261104	Created the gzTransform and gzRoiNode factories.	(2.12.327)
//
//*****************************************************************************

namespace CSW.Unity
{
	/// <summary>Realizes <c>gzTransform</c> occurrences as group objects with the local transform of the node.</summary>
	public class TransformFactory : NodeFactory
	{
		internal override void CaptureNode(FactoryCapture capture)
		{
			base.CaptureNode(capture);
			if (capture.Node is GizmoSDK.Gizmo3D.Transform transform)
			{
				transform.GetTransform(out var matrix);
				capture.Data.LocalTransform = matrix.CopyMatrix4x4();
			}
		}
	}

	/// <summary>Realizes <c>gzRoiNode</c> occurrences; their frame position follows through transform-only updates.</summary>
	public class RoiNodeFactory : TransformFactory
	{
	}
}
