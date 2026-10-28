//******************************************************************************
//
// Copyright (C) SAAB AB
//
// All rights, including the copyright, to the computer program(s) 
// herein belong to Saab AB. The program(s) may be used and/or
// copied only with the written permission of Saab AB, or in
// accordance with the terms and conditions stipulated in the
// agreement/contract under which the program(s) have been
// supplied. 
//
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
//
// File			: TerrainNodeBuilder.cs
// Module		:
// Description	: Helper class for vertice updates
// Author		: Anders Modén
// Product		: Gizmo3D 2.12.326
//
// NOTE:	Gizmo3D is a high performance 3D Scene Graph and effect visualisation 
//			C++ toolkit for Linux, Mac OS X, Windows, Android, iOS and HoloLens for  
//			usage in Game or VisSim development.
//
//
// Revision History...
//
// Who	Date	Description
//
// ZJP	240821	Created file                                        (2.12.171)
// AMO	261028	Removed legacy state resource building; kept command-driven terrain selection.	(2.12.327)
//
//******************************************************************************

using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer
{
    public class TerrainNodeBuilder : GeometryBuilderBase
    {
        public override PoolObjectFeature Feature => PoolObjectFeature.Terrain;

        public override bool CanBuild(Node node, TraversalState traversalState, IntersectMaskValue intersectMask)
        {
            return (traversalState == TraversalState.None) &&
                base.CanBuild(node, traversalState, intersectMask);
        }
    }
}
