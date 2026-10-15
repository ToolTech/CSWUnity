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
// File			: CrossboardBuilder.cs
// Module		:
// Description	: Special class for Crossboard builder
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
//
//******************************************************************************

// Framework
using System.Collections.Generic;

// Unity
using UnityEngine;

// GizmoSDK
using GizmoSDK.Gizmo3D;

// Internal
//using Assets.Crossboard;

namespace Saab.Foundation.Unity.MapStreamer
{ 
    public class CrossboardNodeBuilder : INodeBuilder
    {
        public BuildPriority Priority { get; private set; }

        public PoolObjectFeature Feature => PoolObjectFeature.Crossboard;

        // lifetime data ----------

        // we will cache crossboard textures
        // private readonly TextureCache _textureCache = new TextureCache();
        private TextureManager _textureManager;

        // material to use when rendering
        private readonly Material _crossboardMaterial;

        // compute shader to use when rendering
        private readonly ComputeShader _computeShader;

        // per-frame data ----------

        public CrossboardNodeBuilder(Shader shader, ComputeShader computeShader, BuildPriority priority = BuildPriority.Low)
        {
            Priority = priority;

            _crossboardMaterial = new Material(shader);
            _computeShader = computeShader;
        }

        public bool CanBuild(Node node, TraversalState traversalState, IntersectMaskValue intersectMask)
        {
            return node is Crossboard;
        }

        // TODO: Make functions...
        public bool Build(NodeHandle nodeHandle, NodeHandle activeStateNode)
        {
            var node = nodeHandle.node;

            var cb = node as Crossboard;
            if (cb == null)
                return false;

            float[] position_data;
            if (!cb.GetObjectPositions(out position_data))
                return false;

            float[] object_data;
            if (!cb.GetObjectData(out object_data))
                return false;

            var objects = position_data.Length / 3; // Number of objects


            // NOTE: CrossboardDatasets are copied to compute buffers when assigning the dataset,
            // thus we can reuse same dataset objects to reduce GC pressure if needed.
            //CrossboardDataset dataset = new CrossboardDataset();
            //dataset.POSITION = new Vector3[objects];
            //dataset.UV0ListComp = new List<Vector4>(objects);
            //dataset.UV1ListComp = new List<Vector4>(objects);
            //dataset.COLOR = new Color[objects];

            var float3_index = 0;
            var float4_index = 0;

            for (var i = 0; i < objects; i++)
            {
                //dataset.POSITION[i] = new Vector3(position_data[float3_index], position_data[float3_index + 1], position_data[float3_index + 2]);

                //// size, heading, pitch, roll 
                //dataset.UV0ListComp.Add(new Vector4(object_data[float4_index] * 2f, object_data[float4_index + 1], object_data[float4_index + 2], object_data[float4_index + 3]));

                //// postion offset (x - its up normal), planes offset ( xyz - in there normal direction)
                //dataset.UV1ListComp.Add(new Vector4(-0.02f, 0, 0, 0));

                float3_index += 3;
                float4_index += 4;
            }

            // Shouldnt this be our settings?
            if (cb.UseColors)
            {
                float[] color_data;

                if (!cb.GetColorData(out color_data))
                    return false;

                float4_index = 0;

                var colors = new Color[objects];

                for (int i = 0; i < objects; i++)
                {
                    colors[i] = new Color(color_data[float4_index], color_data[float4_index + 1], color_data[float4_index + 2], color_data[float4_index + 3]);
                    float4_index += 4;
                }

                //dataset.COLOR = colors;
            }

            //var renderer = gameObject.GetComponent<CrossboardRenderer_ComputeShader>();
            //if (renderer == null)
            //{
            //    renderer = gameObject.AddComponent<CrossboardRenderer_ComputeShader>();

            //    renderer.OpaqueCrossboardCompute = true;
            //    renderer._computeShader = GameObject.Instantiate(_computeShader);
            //}


            var material = GameObject.Instantiate(_crossboardMaterial);

            // check if the texture is loaded for this state, otherwise load it
            if (activeStateNode != null)
            {
                if ((activeStateNode.stateLoadInfo & StateLoadInfo.Texture) == StateLoadInfo.None)
                {
                    var state = activeStateNode.node.State;

                    if (!StateHelper.Build(
                            state,
                            out Texture2D buildOutput,
                            _textureManager))
                    {
                        state.ReleaseAlreadyLocked();
                        GameObject.Destroy(material);
                        return false;
                    }

                    activeStateNode.stateLoadInfo |= StateLoadInfo.Texture;
                    activeStateNode.texture = buildOutput;

                    state.ReleaseAlreadyLocked();
                }

                material.mainTexture = activeStateNode.texture;
            }

            //renderer.SetCrossboardDataset(dataset, material);

            return true;
        }

        public void BuiltObjectReturnedToPool(GameObject gameObject, bool sharedAsset)
        {
            //var crossboardRenderer = gameObject.GetComponent<CrossboardRenderer_ComputeShader>();
            //if (!crossboardRenderer)
            //    return;

            //// crossboard renderer uses alot of resources, so we will release it
            //GameObject.Destroy(crossboardRenderer);
        }

        public void InitPoolObject(GameObject gameObject)
        {

        }

        public void SetTextureManager(TextureManager textureManager)
        {
            _textureManager = textureManager;
        }

        public void SetMaterialManager(MaterialManager textureManager)
        {
            // nop
        }


        public void Reset()
        {
            // NOP
        }
    }
}
