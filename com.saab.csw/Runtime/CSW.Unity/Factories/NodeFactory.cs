//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: NodeFactory.cs
// Description	: Built-in factories of group-like GizmoSDK nodes
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261104	Created the gzNode, gzGroup, gzLod, gzCrossboard and gzDynamicLoader factories.	(2.12.327)
//
//*****************************************************************************

using CSW.Unity.StreamingMap;

namespace CSW.Unity
{
	/// <summary>Realizes <c>gzNode</c> and <c>gzGroup</c> occurrences as Unity group objects.</summary>
	public class NodeFactory : Factory
	{
	}

	/// <summary>Realizes <c>gzLod</c> occurrences as group objects reported through OnNewLod.</summary>
	public class LodFactory : NodeFactory
	{
		internal override void CaptureNode(FactoryCapture capture)
		{
			base.CaptureNode(capture);
			capture.Data.Kind = SceneNodeKind.Lod;
		}
	}

	/// <summary>Realizes <c>gzCrossboard</c> occurrences as group objects reported through OnNewCrossboard.</summary>
	public class CrossboardFactory : NodeFactory
	{
		internal override void CaptureNode(FactoryCapture capture)
		{
			base.CaptureNode(capture);
			capture.Data.Kind = SceneNodeKind.Crossboard;
		}
	}

	/// <summary>Realizes <c>gzDynamicLoader</c> occurrences as group objects reported through OnNewLoader.</summary>
	public class DynamicLoaderFactory : NodeFactory
	{
		internal override void CaptureNode(FactoryCapture capture)
		{
			base.CaptureNode(capture);
			capture.Data.Kind = SceneNodeKind.Loader;
		}
	}
}
