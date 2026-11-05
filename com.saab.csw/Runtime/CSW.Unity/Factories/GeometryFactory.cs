//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: GeometryFactory.cs
// Description	: Built-in factory of GizmoSDK geometry
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261104	Created the gzGeometry factory with the geometry builder selection of SceneCommandPump.	(2.12.327)
// AMO	261105	Passed loader-thread mesh preparations to the resource capture.	(2.12.327)
//
//*****************************************************************************

using System;
using CSW.Unity.StreamingMap;
using GizmoSDK.Gizmo3D;

namespace CSW.Unity
{
	/// <summary>Realizes <c>gzGeometry</c> occurrences with the mesh and material of the first accepting builder.</summary>
	/// <remarks>Geometry outside every intersection mask of the session is realized as a group object.</remarks>
	public class GeometryFactory : NodeFactory
	{
		internal override void CaptureNode(FactoryCapture capture)
		{
			base.CaptureNode(capture);
			var data = capture.Data;
			data.Kind = SceneNodeKind.Geometry;
			if (!(capture.Node is Geometry geometry) || data.IntersectMask == IntersectMaskValue.NOTHING)
				return;
			var traversal = data.IsStatic ? TraversalState.AssetInstance : TraversalState.None;
			foreach (var builder in capture.Builders)
			{
				if (builder && builder.CanBuild(geometry, traversal, data.IntersectMask))
				{
					data.Builder = builder;
					break;
				}
			}
			if (!data.Builder)
				throw new InvalidOperationException($"No geometry builder accepts '{data.Name}' ({data.IntersectMask}).");
			data.Prepared = capture.Resources.Capture(geometry, capture.State, capture.IsUpdate, capture.PreparedMeshes);
		}
	}
}
