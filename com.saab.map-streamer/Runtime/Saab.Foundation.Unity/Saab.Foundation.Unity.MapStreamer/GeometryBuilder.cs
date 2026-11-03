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
//
//******************************************************************************

// Unity
using UnityEngine;

// GizmoSDK
using GizmoSDK.Gizmo3D;
using Unity.Profiling;
using System.Collections.Generic;

namespace Saab.Foundation.Unity.MapStreamer
{
    public abstract class NodeBuilderBase : MonoBehaviour, INodeBuilder
    {
        [SerializeField]
        private BuildPriority _mode = BuildPriority.Immediate;

        protected TextureManager _textureManager;
        protected MaterialManager _materialManager;

        public abstract PoolObjectFeature Feature { get; }

        public BuildPriority Priority => _mode;

        public abstract bool Build(NodeHandle nodeHandle, NodeHandle activeStateNode);

        public abstract void BuiltObjectReturnedToPool(GameObject gameObject, bool sharedAsset);
        public abstract void InitPoolObject(GameObject gameObject);

        public abstract bool CanBuild(Node node, TraversalState traversalState, IntersectMaskValue intersectMask);

        public void SetTextureManager(TextureManager textureManager)
        {
            _textureManager = textureManager;
        }

        public void SetMaterialManager(MaterialManager materialManager)
        {
            _materialManager = materialManager;
        }

        public virtual void Reset()
        {
            // NOP
        }
    }

    public abstract class GeometryBuilderBase : NodeBuilderBase
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


        private static readonly ProfilerMarker _profilerBuild = new ProfilerMarker(ProfilerCategory.Render, "SM-Geometry-Build");

        public override bool CanBuild(Node node, TraversalState traversalState, IntersectMaskValue intersectMask)
        {
            return (intersectMask & _mask) != IntersectMaskValue.NOTHING && node is Geometry;
        }

        public override bool Build(NodeHandle nodeHandle, NodeHandle activeStateNode)
        {
            _profilerBuild.Begin();
            var geo = (Geometry)nodeHandle.node;
            
            var go = nodeHandle.gameObject;
            go.layer = _layer;

            // MeshRenderer component
            if (!go.TryGetComponent<MeshRenderer>(out var meshRenderer))
                meshRenderer = go.AddComponent<MeshRenderer>();

            // MeshFilter component
            if (!go.TryGetComponent<MeshFilter>(out var meshFilter))
                meshFilter = go.AddComponent<MeshFilter>();

            var reusableMesh = meshFilter.sharedMesh;
            meshFilter.sharedMesh = null;

            if (!GeometryHelper.Build(
                    geo,
                    reusableMesh,
                    out Mesh mesh,
                    out Color uniformColor))
            {
#if DEBUG
                Debug.LogError("failed to generate mesh, no geometry was built");
#endif

                Destroy(mesh);
                if (reusableMesh && reusableMesh != mesh)
                    Destroy(reusableMesh);
                meshFilter.sharedMesh = null;
                meshRenderer.enabled = false;
                return false;
            }

            if (reusableMesh && reusableMesh != mesh)
                Destroy(reusableMesh);

            Material material;
            if (_forceFallbackMaterial)
            {
                 // Todo: reuse material if texture match
                material = Instantiate(_fallbackMaterial);
            }
            else
            {
                if (activeStateNode != null)
                {
                    if (CreateStateNodeResources(activeStateNode))
                    {
                        material = CreateMaterialFromState(activeStateNode);
                        material.color = uniformColor;
                    }
                    else
                    {
#if DEBUG
                        Debug.LogError("failed to create resources from state, using fallback material");
#endif
                        // Todo: reuse material if texture match
                        material = Instantiate(_fallbackMaterial);
                    }
                }
                else
                {
#if DEBUG
                    Debug.LogError("missing state, using fallback material");
#endif
                    // Todo: reuse material if texture match
                    material = Instantiate(_fallbackMaterial);
                }
            }

            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            meshRenderer.enabled = true;
            _profilerBuild.End();

            return true;
        }

        public override void BuiltObjectReturnedToPool(GameObject gameObject, bool sharedAsset)
        {
            if (sharedAsset)
            {
                if (gameObject.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    renderer.enabled = false;
                    renderer.sharedMaterial = null;
                }
                if (gameObject.TryGetComponent<MeshFilter>(out var meshFilter))
                    meshFilter.sharedMesh = null;
            }
            else
            {
                // Keep the detached mesh on the pooled object for the next native build.
                if (gameObject.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    Material sharedMaterial = renderer.sharedMaterial;
                    if (sharedMaterial)
                        _materialManager.Free(sharedMaterial);

                    renderer.sharedMaterial = null;
                    renderer.enabled = false;
                }
            }
        }

        public override void InitPoolObject(GameObject gameObject)
        {
            gameObject.AddComponent<MeshRenderer>();
            gameObject.AddComponent<MeshFilter>();
        }

        /// <summary>
        /// Loads resources from a state and stores on the node for future reference
        /// </summary>
        /// <param name="stateNode">scenegraph rendering state</param>
        /// <returns>true if resources was loaded from the state correctly</returns>
        protected abstract bool CreateStateNodeResources(NodeHandle stateNode);

        /// <summary>
        /// Creates and setups a material instance from a scenegraph rendering state, uses fallback material if state is not valid
        /// </summary>
        /// <param name="stateNode">scenegraph rendering state</param>
        /// <returns>new material instance</returns>
        protected virtual Material CreateMaterialFromState(NodeHandle stateNode)
        {
            if (!stateNode.texture)
                return Instantiate(_fallbackMaterial);

            var id = stateNode.texture.GetInstanceID();
            Material material = null;

            if (!_materialManager.TryGet(id, out material))
            {
                material = Instantiate(_material);
                material.mainTexture = stateNode.texture;
                _materialManager.TryAdd(id, material);
            }
    
            return material;
        }
    }
}
