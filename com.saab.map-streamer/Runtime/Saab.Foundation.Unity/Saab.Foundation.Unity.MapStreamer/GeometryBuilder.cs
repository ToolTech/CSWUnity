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
// File			: GeometryBuilder.cs
// Module		:
// Description	: Special class for geometry builder
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
// ZJP	200625	Created file                                        (2.10.6)
// AMO  230113  Added Feature Maps from texture 1                   (2.12.43)
// AMO	261022	Added command-driven material construction from copied scene State data.	(2.12.327)
// AMO	261028	Removed legacy node building; kept the command-driven builder contract with traversal and pool feature flags.	(2.12.327)
//
//******************************************************************************

// Framework
using System;

// Unity
using UnityEngine;

// GizmoSDK
using GizmoSDK.Gizmo3D;

namespace Saab.Foundation.Unity.MapStreamer
{
    /// <summary>
    /// Flags used during traversal to keep track of state
    /// </summary>
    [Flags]
    public enum TraversalState
    {
        None,
        /// <summary>
        /// Set when traversing an asset subgraph i.e. /Resources
        /// </summary>
        Asset = 0x01,

        /// <summary>
        /// Set when traversing a gzRefNode subgraph
        /// </summary>
        AssetInstance = 0x02,
    }

    /// <summary>
    /// Presentation feature of a scene object built by a geometry builder
    /// </summary>
    [Flags]
    public enum PoolObjectFeature : byte
    {
        None = 0,
        Terrain = 1 << 0,
        StaticMesh = 1 << 1,
        Crossboard = 1 << 2,
    }

    /// <summary>
    /// Selects the nodes a command-driven scene builds and supplies their Unity materials and layer
    /// </summary>
    public abstract class GeometryBuilderBase : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Material to use when state is missing")]
        protected Material _fallbackMaterial;

        [SerializeField]
        [Tooltip("Material to use when building geometry")]
        protected Material _material;

        [SerializeField]
        [Tooltip("Only nodes with a matching intersection mask will be processed by this builder")]
        protected IntersectMaskValue _mask;

        [SerializeField]
        [Tooltip("Ignore state information and always use the fallback material")]
        private bool _forceFallbackMaterial;

        [SerializeField]
        [Tooltip("Unity layer for node")]
        private int _layer = 0;    // 0 = Unity "Default" layer

        public abstract PoolObjectFeature Feature { get; }

        internal int SceneLayer => _layer;

        internal Material CreateSceneMaterial(StateBuildOutput state, Color uniformColor)
        {
            var template = !_forceFallbackMaterial && state.Texture
                ? _material
                : _fallbackMaterial;
            if (!template)
                throw new System.InvalidOperationException(
                    $"Builder '{name}' has no material for the command-driven scene.");

            var material = Instantiate(template);
            if (!_forceFallbackMaterial && state.Texture)
            {
                material.mainTexture = state.Texture;
                if (state.Feature && material.HasProperty("_FeatureMap"))
                    material.SetTexture("_FeatureMap", state.Feature);
            }
            if (material.HasProperty("_Color"))
                material.color = uniformColor;
            return material;
        }

        public virtual bool CanBuild(Node node, TraversalState traversalState, IntersectMaskValue intersectMask)
        {
            return (intersectMask & _mask) != IntersectMaskValue.NOTHING && node is Geometry;
        }
    }
}
