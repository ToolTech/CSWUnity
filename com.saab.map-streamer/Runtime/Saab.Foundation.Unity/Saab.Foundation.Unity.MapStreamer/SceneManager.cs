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
// File			: SceneManager.cs
// Module		:
// Description	: Management of dynamic asset loader from GizmoSDK
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
// AMO	180607	Created file        (2.9.1)
// AMO  200304  Updated SceneManager with events for external users     (2.10.1)
// AMO  221130  Updated SM with new locking and camera sync             (2.12.35)
//
//******************************************************************************

//#define DEBUG_CAMERA

// Unity Managed classes
using UnityEngine;

// Gizmo Managed classes
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;

// Fix some conflicts between unity and Gizmo namespaces
using gzCamera = GizmoSDK.Gizmo3D.Camera;
using gzTransform = GizmoSDK.Gizmo3D.Transform;
using gzTexture = GizmoSDK.Gizmo3D.Texture;


// Map utility
using Saab.Foundation.Map;
using Saab.Unity.Extensions;
using Saab.Utility.Unity.NodeUtils;
using Saab.Utility.GfxCaps;

// Fix unity conflicts
using unTransform = UnityEngine.Transform;

// System
using System.Collections.Generic;
using System;
using System.Collections;
using UnityEngine.Networking;
using System.Linq;

using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;
using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;

namespace Saab.Foundation.Unity.MapStreamer
{
    // The SceneManager behaviour takes a unity camera and follows that to populate the current scene with GameObjects in a scenegraph hierarchy

    public interface ISceneManagerCamera
    {
        UnityEngine.Camera Camera { get; }
        Vec3D GlobalPosition { get; set; }           // Position in Global coordinate system

        Vector3 Up { get; }                         // Get up vector in global coordinate system for current position
        Vector3 North { get; }                      // Get north vector in global coordinate system for current position

        void PreTraverse(bool locked);              // Executed before scene is traversed and updated with new transform and new geometry

        void PostTraverse(bool locked);             // Executed after nodes are repositioned with new transforms and correct activations

        double UpdateCamera(double renderTime);     // Executed just before camera transform is used. Update you cam animation in this

        void MapChanged();                          // Executed when map is changed

        float LodFactor { get; }                    // Current lod factor
    }

    /// <summary>
    /// Options for configuring SceneManager runtime behaviour
    /// </summary>
    [Flags]
    public enum SceneManagerOptions
    {
        None = 0,

        /// <summary>
        /// Render during component update, disable to manually control when render is performed
        /// </summary>
        RenderInUpdate = 1 << 0,
        
        /// <summary>
        /// Skip asset loading and ignore RefNodes
        /// </summary>
        DisableInstancing = 1 << 1,

        LazyLoadAssets = 1 << 2,
    }

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


    [Serializable]
    public struct SceneManagerSettings
    {
        public double   MaxBuildTime;                       // Max time to spend in frame to build objects
        public double   MinBuildTime;                       // Min time to spend in frame to build objects
        public byte     DynamicLoaders;
        public IntersectMaskValue IntersectMask;
        public SceneManagerOptions Options;

        public static readonly SceneManagerSettings Default = new SceneManagerSettings
        {
            MaxBuildTime = 0.012,       // 12ms
            MinBuildTime = 0.004,       // 4ms == 16 ms, 60fps
            DynamicLoaders = 4,
            IntersectMask = IntersectMaskValue.ALL,
            Options = SceneManagerOptions.RenderInUpdate,
        };
    }

    public readonly struct MeshBuildDiagnostics
    {
        public readonly long TotalMeshes;
        public readonly long TotalBytes;
        public readonly TimeSpan TotalTime;
        public readonly TimeSpan WorstTime;

        internal MeshBuildDiagnostics(
            long totalMeshes,
            long totalBytes,
            TimeSpan totalTime,
            TimeSpan worstTime)
        {
            TotalMeshes = totalMeshes;
            TotalBytes = totalBytes;
            TotalTime = totalTime;
            WorstTime = worstTime;
        }
    }

    public readonly struct TextureBuildDiagnostics
    {
        public readonly long TotalTextures;
        public readonly long TotalBytes;
        public readonly TimeSpan TotalTime;
        public readonly TimeSpan WorstTime;

        internal TextureBuildDiagnostics(
            long totalTextures,
            long totalBytes,
            TimeSpan totalTime,
            TimeSpan worstTime)
        {
            TotalTextures = totalTextures;
            TotalBytes = totalBytes;
            TotalTime = totalTime;
            WorstTime = worstTime;
        }
    }

    public class SceneManager : MonoBehaviour
    {
        public SceneManagerSettings Settings = SceneManagerSettings.Default;
        public ISceneManagerCamera  SceneManagerCamera;
        public string               MapUrl;
        public NodeBuilderBase[] Builders;

        // Events ----------------------------------------------------------

        public delegate void EventHandler_OnGameObject(GameObject o, bool isAsset);
        public delegate void EventHandler_OnGameObjectFree(GameObject o);
        public delegate void EventHandler_OnNode(Node node);
        public delegate void EventHandler_OnUpdateCamera(double renderTime);
        public delegate void EventHandler_OnMapLoadError(ref string url,string errorString,SerializeAdapter.AdapterError errorType,ref bool retry);

        // Notifications for external users that wants to add components to created game objects. Be swift as we are in edit lock

        public event EventHandler_OnGameObject      OnNewTerrain;       // GameObject with mesh (feature == Terrain)
        public event EventHandler_OnGameObject      OnNewGeometry;      // GameObject with mesh (feature == Static Mesh)
        public event EventHandler_OnGameObject      OnNewCrossboard;    // CrossBoard/tree placement 
        public event EventHandler_OnGameObject      OnNewLod;           // GameObject that toggles on off dep on distance
        public event EventHandler_OnGameObject      OnNewLoader;        // GameObject that works like a dynamic loader
        public event EventHandler_OnGameObjectFree  OnEnterPool;        // Moving GO to pool
        public event EventHandler_OnGameObjectFree  OnRemoveGeometry;   // Removing GO from Scene
        public event EventHandler_OnGameObjectFree  OnRemoveTerrain;   // Removing GO from Scene

        public delegate void EventHandler_Traverse(bool locked);    // Pre and Post traversal in locked or unlocked mode (edit)

        public event EventHandler_Traverse          OnPreTraverse;  // Called before SceneManagerCamera is updated
        public event EventHandler_Traverse          OnPostTraverse; // Called after SceneManagerCamera is updated
        public event EventHandler_OnNode            OnMapChanged;
        public event EventHandler_OnMapLoadError    OnMapLoadError;
        public event EventHandler_OnUpdateCamera    OnUpdateCamera; // Called after SceneManagerCamera is updated

        #region ------------- Privates ----------------

        private Scene _native_scene;
        private gzCamera _native_camera;
        private Context _native_context;
        private CullTraverseAction _native_traverse_action;
        private GameObject _root;

        private NodeAction _actionReceiver;

        private readonly string ID = "Saab.Foundation.Unity.MapStreamer.SceneManager";

        //#pragma warning disable 414
        //private UnityPluginInitializer _plugin_initializer;
        //#pragma warning restore 414

        #endregion


        private static readonly ProfilerMarker _profilerMarkerRender = new ProfilerMarker(ProfilerCategory.Render, "SM-Render");
        private static readonly ProfilerMarker _profilerMarkerCull = new ProfilerMarker(ProfilerCategory.Render, "SM-Cull");
        private static readonly ProfilerMarker _profilerMarkerTraverse = new ProfilerMarker(ProfilerCategory.Render, "SM-Traverse");
        private static readonly ProfilerMarker _profilerFree = new ProfilerMarker(ProfilerCategory.Render, "SM-Free");


        struct NodeLoadInfo
        {
            public NodeLoadInfo(DynamicLoadingState _state, DynamicLoader _loader, Node _node)
            {
                state = _state;
                loader = _loader;
                node = _node;
            }

            public DynamicLoadingState state;
            public DynamicLoader loader;
            public Node node;

        };

        // The activation struct carries information about the state of a native node (loaded/unloaded)
        struct ActivationInfo
        {
            public ActivationInfo(NodeActionEvent _state, Node _node)
            {
                state = _state;
                node = _node;
            }

            public NodeActionEvent state;
            public Node node;
        };

        
        // AssetLoadInfo carries info about loading an asset
        struct AssetLoadInfo
        {
            public AssetLoadInfo(GameObject _parent, string _res_url, string _obj_id)
            {
                parent = _parent;
                resource_url = _res_url;
                object_id = _obj_id;
            }

            public GameObject parent;
            public string resource_url;
            public string object_id;
        };

        // stores information for a pending build job
        struct BuildInfo
        {
            public INodeBuilder Builder;
            public NodeHandle NodeHandle;
            public NodeHandle ActiveStateNode;
            public byte Version;
        }

        private bool _initialized;

        // A queue for new pending loads/unloads
        private readonly List<NodeLoadInfo> pendingLoaders = new List<NodeLoadInfo>(100);
        private readonly Dictionary<IntPtr,NodeLoadInfo> activePendingLoaders = new Dictionary<IntPtr, NodeLoadInfo>();

        // A queue for pending activations/deactivations
        private readonly List<ActivationInfo> pendingActivations = new List<ActivationInfo>(100);
  
        // A queue for post build work
        private readonly Queue<BuildInfo> pendingBuilds = new Queue<BuildInfo>(1000);

        private static long _frameMeshCount;
        private static long _frameMeshBytes;
        private static long _frameMeshTicks;
        private static long _frameMeshWorstTicks;
        private static long _frameTextureCount;
        private static long _frameTextureBytes;
        private static long _frameTextureTicks;
        private static long _frameTextureWorstTicks;
        private static long _totalMeshCount;
        private static long _totalMeshBytes;
        private static long _totalMeshTicks;
        private static long _totalMeshWorstTicks;
        private static long _totalTextureCount;
        private static long _totalTextureBytes;
        private static long _totalTextureTicks;
        private static long _totalTextureWorstTicks;
        private static long _buildDiagnosticsResetVersion;
        private static int _buildDiagnosticsFrame = -1;

        // A queue for AssetLoading
        private readonly Stack<AssetLoadInfo> pendingAssetLoads = new Stack<AssetLoadInfo>(100);

        // The current active asset bundles
        private readonly Dictionary<string, AssetBundle> currentAssetBundles = new Dictionary<string, AssetBundle>();

        // Linked List for nodes that needs updates on update
        private readonly LinkedList<GameObject> updateNodeObjects = new LinkedList<GameObject>();
        
        // List of all registered builds, builders must currently be registered before initialize
        private readonly List<INodeBuilder> _builders = new List<INodeBuilder>();

        // Used during lazy asset loading to defer the traversal of shared assets until first use
        private readonly Dictionary<uint, AssetTraversalFuture> _deferredAssetLoads = new Dictionary<uint, AssetTraversalFuture>();

        // special dedicated builder used when instancing gzRefNodes
        private readonly AssetInstanceBuilder _assetInstanceBuilder = new AssetInstanceBuilder();

        // Used by builders to share and manage texture resources
        private readonly TextureManager _textureManager = new TextureManager();

        public TextureManagerDiagnostics TextureDiagnostics =>
            _textureManager.Diagnostics;

        // Used by builders to share and manage Material resources
        private readonly MaterialManager _materialManager = new MaterialManager();

        // Pools of pre allocated and recycled node objects, used to avoid runtime allocations and instead recycle game objects
        private readonly Stack<NodeHandle>[] _free = new Stack<NodeHandle>[byte.MaxValue];

        // Prefab when allocating node handles for specific pools
        private readonly NodeHandle[] _poolPrefabs = new NodeHandle[byte.MaxValue];

        // Stores objects that have been unloaded but not yet freed, objects will eventually be returned to the free list,
        // this is to reduce the time spent freeing nodes in a single frame
        private readonly Stack<unTransform> _pendingFrees = new Stack<unTransform>();

        // Used during pre allocation to spread allocations evenly across pools
        private readonly Queue<byte> _preAllocationRoundRobinQueue = new Queue<byte>();

        // Stores traversal state data during a traversal pass, passed by reference to reduce parameter passing
        private struct TraversalStateData
        {
            public NodeHandle NodeHandle;
            public NodeHandle ActiveStateNode;
            public TraversalState TraversalStateFlags;
            public IntersectMaskValue IntersectMask;
        }

        // stores traversal information to allow continutation of a traversal at a later time, currently used for lazy load
        private struct AssetTraversalFuture
        {
            public Node AssetNode;
            public TraversalStateData TraversalState;
        }

        public void AddBuilder(INodeBuilder builder)
        {
            if (_initialized)
                throw new InvalidOperationException("builders must be registered before init");
            
            _builders.Add(builder);
        }

        public void RemoveBuilder(INodeBuilder builder)
        {
            _builders.Remove(builder);
        }

        private INodeBuilder GetBuilderForNode(Node node, in TraversalStateData data)
        {
            // performance critical, do not change to foreach
            for (var i = 0; i < _builders.Count; ++i)
            {
                var builder = _builders[i];

                if (!builder.CanBuild(node, data.TraversalStateFlags, data.IntersectMask))
                    continue;

                return builder;
            }

            return null;
        }

        private void BuildNode(INodeBuilder builder, in TraversalStateData data)
        {
            var priority = builder.Priority;
            
            // force immediate mode during asset phase (we might change this later, but for now it makes life simpler)
            //if ((data.TraversalStateFlags & TraversalState.Asset) == TraversalState.Asset)
            //    priority = BuildPriority.Immediate;

            switch (priority)
            {
                case BuildPriority.Immediate:
                    if (builder.Build(data.NodeHandle, data.ActiveStateNode))
                        data.NodeHandle.builder = builder;
                    break;
                case BuildPriority.Low:

                    // defer the build so that we can distribute the build process across multiple frames
                    pendingBuilds.Enqueue(new BuildInfo()
                    {
                        Builder = builder,
                        NodeHandle = data.NodeHandle,
                        ActiveStateNode = data.ActiveStateNode,
                        // when a node is recycled, the version is increased, we store the current version so that
                        // we can detect if the node has already been removed when scheduled for build
                        Version = data.NodeHandle.version,
                    });
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private static void ProcessTransform(gzTransform node, unTransform transform)
        {
            // Check if transform is Active and not unit (1)
            if (!node.IsActive())
                return;

            // todo (opt): GetTransform(out matrix, out active) to reduce P/INVOKE calls by 50%
            node.GetTransform(out Matrix4 mat4);

            transform.localPosition = mat4.Translation().ToVector3();
            transform.localScale = mat4.Scale().ToVector3();
            transform.localRotation = mat4.Quaternion().ToQuaternion();
        }

        private void ProcessDynamicLoader(DynamicLoader node, in TraversalStateData data)
        {
            // earlier impl allowed this and would return the registered node, this seems wrong but I should verify this with AMO
            System.Diagnostics.Debug.Assert(!NodeUtils.HasGameObjectsUnsafe(node.GetNativeReference()));

            //// Possibly we can add action interfaces for dyn load childs as they will get traversable if they have node actons
            //if (NodeUtils.FindGameObjectsUnsafe(node.GetNativeReference(), out List<GameObject> list))
            //{
            //    return list[0];
            //}
            
            // Add to registry
            NodeUtils.AddGameObjectReferenceUnsafe(node.GetNativeReference(), data.NodeHandle.gameObject);
            data.NodeHandle.inNodeUtilsRegistry = true;

            OnNewLoader?.Invoke(data.NodeHandle.gameObject, false);

            // gzDynamicLoader is gzGroup
            // ProcessGroup(node, in data, false);
        }

        private void ProcessLod(Lod node, in TraversalStateData data)
        {
            // gzLod is gzGroup
            ProcessGroup(node, in data, true);

            OnNewLod?.Invoke(data.NodeHandle.gameObject, data.TraversalStateFlags.HasFlag(TraversalState.Asset));
        }

        private void ProcessRoi(Roi node, in TraversalStateData data)
        {
            RegisterNodeForUpdate(data.NodeHandle);

            // gzRoi is gzTransform
            ProcessTransform(node, data.NodeHandle.transform);

            // gzRoi is gzGroup
            ProcessGroup(node, in data, true);
        }

        private void ProcessRoiNode(RoiNode node, in TraversalStateData data)
        {
            RegisterNodeForUpdate(data.NodeHandle);

            // gzRoiNode is gzTransform
            ProcessTransform(node, data.NodeHandle.transform);
            
            // gzRoiNode is gzGroup
            ProcessGroup(node, in data, false);
        }

        private void RegisterNodeForUpdate(NodeHandle nodeHandle)
        {
            nodeHandle.updateTransform = true;
            nodeHandle.inNodeUpdateList = true;
            updateNodeObjects.AddLast(nodeHandle.gameObject);
        }

        private GameObject ProcessRefNode(RefNode refNode, in TraversalStateData data)
        {
            // mostly for debug purposes, will allow us to skip asset instancing
            if (Settings.Options.HasFlag(SceneManagerOptions.DisableInstancing))
                return null;

            // copy the state so that we can write to it
            TraversalStateData state = data;

            // we dont expect RefNodes when traversing asset subtree or during instancing (i.e. nested assets)
            System.Diagnostics.Debug.Assert(!state.TraversalStateFlags.HasFlag(TraversalState.Asset));
            System.Diagnostics.Debug.Assert(!state.TraversalStateFlags.HasFlag(TraversalState.AssetInstance));

            if (Settings.Options.HasFlag(SceneManagerOptions.LazyLoadAssets))
            {
                if (_deferredAssetLoads.TryGetValue(refNode.ReferenceNodeID, out AssetTraversalFuture assetTraverseFuture))
                {
                    _deferredAssetLoads.Remove(refNode.ReferenceNodeID);

                    var assetState = assetTraverseFuture.TraversalState;
                    var asset = TraverseInternal(assetTraverseFuture.AssetNode, ref assetState);
                    asset.transform.SetParent(assetTraverseFuture.TraversalState.NodeHandle.transform);
                }
            }

            // instruct native side to construct the instance
            refNode.AttachNode();

            // create a node handle for the ref node instance
            state.NodeHandle = CreateNodeHandle(refNode, PoolObjectFeature.None);

            // continue traversal with the instance flag set
            state.TraversalStateFlags |= TraversalState.AssetInstance;

            // traverse down the subgraph
            ProcessGroup(refNode, in state, false);

            // return the created gameobject for this instance
            return state.NodeHandle.gameObject;
        }

        private GameObject ProcessGeometry(Geometry geo, in TraversalStateData data)
        {
            TraversalStateData state = data;

            bool isAssetInstance = (data.TraversalStateFlags & TraversalState.AssetInstance) == TraversalState.AssetInstance;
            if (isAssetInstance)
            {
                // allocate node handle from the mesh pool and set node handle instance flag
                state.NodeHandle = CreateNodeHandle(geo, PoolObjectFeature.StaticMesh);
                state.NodeHandle.stateFlags |= NodeStateFlags.AssetInstance;

                // build the geometry using the instance builder that will share mesh/material from prefab asset
                BuildNode(_assetInstanceBuilder, state);
            }
            else
            {
                // find a builder for this type of geometry object
                var builder = GetBuilderForNode(geo, in state);
                if (builder != null)
                {
                    // allocate node handle using the builders pool
                    state.NodeHandle = CreateNodeHandle(geo, builder.Feature);

                    // check for individual state on the geometry node
                    if (geo.HasState())
                        state.ActiveStateNode = state.NodeHandle;

                    // build the geometry, this can either be deferred or immediate
                    BuildNode(builder, state);
                }
                else
                {
                    // we didnt find a geometry builder for this node, simply create an empty placeholder node
                    state.NodeHandle = CreateNodeHandle(geo, PoolObjectFeature.None);
                }

                // if we are currently building an asset prefab, register this node so that the asset builder can
                // use mesh/material when creating new instances
                if ((state.TraversalStateFlags & TraversalState.Asset) == TraversalState.Asset)
                    _assetInstanceBuilder.AddAssetPrefab(geo, state.NodeHandle);
            }

            // notify external systems about the new geometry node
            switch (state.NodeHandle.featureKey)
            {
                case PoolObjectFeature.Terrain:
                    OnNewTerrain?.Invoke(state.NodeHandle.gameObject, state.TraversalStateFlags.HasFlag(TraversalState.Asset));
                    break;
                case PoolObjectFeature.StaticMesh:
                    OnNewGeometry?.Invoke(state.NodeHandle.gameObject, state.TraversalStateFlags.HasFlag(TraversalState.Asset));
                    break;
                default:
                    break;
            }
            
            

            return state.NodeHandle.gameObject;
        }

        private GameObject ProcessCrossboard(Crossboard crossboard, in TraversalStateData data)
        {
            OnNewCrossboard?.Invoke(data.NodeHandle.gameObject, data.TraversalStateFlags.HasFlag(TraversalState.Asset));
            return null;
        }

        // Process and connect a node hierarchy to a GameObject hierarchy
        private void ProcessGroup(Group node, in TraversalStateData data, bool addActionInterfaces = false)
        {
            var parent = data.NodeHandle.transform;

            TraversalStateData state;

            // use addActionInterface if we shall be able to enable/disable part of the tree using action callbacks
            if (addActionInterfaces)        
            {
                foreach (var child in node)
                {
                    state = data;
                    var gameObject = TraverseInternal(child, ref state);

                    if (gameObject == null)
                        continue;

                    var childNodeHandle = gameObject.GetComponent<NodeHandle>();

                    var childPtr = child.GetNativeReference();
                    if (!NodeUtils.HasGameObjectsUnsafe(childPtr))
                    {
                        NodeUtils.AddGameObjectReferenceUnsafe(childPtr, gameObject);

                        childNodeHandle.inNodeUtilsRegistry = true;
                        child.AddActionInterface(_actionReceiver, NodeActionEvent.IS_TRAVERSABLE); 
                        child.AddActionInterface(_actionReceiver, NodeActionEvent.IS_NOT_TRAVERSABLE);
                    }

                    gameObject.transform.SetParent(parent, false);
                }
                
                return;
            }

            foreach (var child in node)
            {
                state = data;
                var gameObject = TraverseInternal(child, ref state);

                if (gameObject == null)
                    continue;

                gameObject.transform.SetParent(parent, false);
            }
        }

        private NodeHandle CreateNodeHandle(Node node, PoolObjectFeature feature)
        {
            var nodeHandle = Allocate(feature, node);
            // we only use the name inside editor to avoid allocations in runtime
#if UNITY_EDITOR
            nodeHandle.name = node.Name;
            
            if (string.IsNullOrEmpty(nodeHandle.name))
                nodeHandle.name = node.GetType().Name;
#endif
            return nodeHandle;
        }

        private GameObject BeginTraverse(Node node,bool dynloaded=false)
        {
            System.Diagnostics.Debug.Assert(node != null && node.IsValid());

            // We must be called in edit lock

            // if dynloaded we should add actions for traverse here as we can be toggled on/off by loader (fancier look)

            var data = new TraversalStateData()
            {
                IntersectMask = node.IntersectMask,
            };

            return TraverseInternal(node, ref data);
        }

        private GameObject TraverseInternal(Node node, ref TraversalStateData data)
        {
            var nodeMask = node.IntersectMask;

            // make sure asset resource nodes does not zero the mask since we rely on it when selecting builders
            if (nodeMask != IntersectMaskValue.NOTHING)
                data.IntersectMask &= nodeMask;

            // mostly for debug purposes, will limit what type of node we traverse
            // (would be much better if this type of culling was a setting on gzCamera so that we didnt even consider the objects)
            if ((data.IntersectMask & Settings.IntersectMask) == IntersectMaskValue.NOTHING)
                return null;

            // Check for asset top node
            if ((data.TraversalStateFlags & (TraversalState.Asset | TraversalState.AssetInstance)) == TraversalState.None
                && node.HasNodeID())
            {
                // mostly for debug purposes, will allow us to skip asset loading
                if (Settings.Options.HasFlag(SceneManagerOptions.DisableInstancing))
                    return null;

                // we do not expect RefNodes during asset traversal
                Debug.Assert(!data.TraversalStateFlags.HasFlag(TraversalState.AssetInstance));

                // Set instancing copy flags so that we share mesh and material between instances
                node.CopyMode = (CopyMode)(CopyModeNode.SHARE_GEOMETRY | CopyModeNode.SHARE_STATE | CopyModeNode.SHARE_TEXTURE);

                // continue traversal with the asset flag set
                data.TraversalStateFlags |= TraversalState.Asset;

                if (Settings.Options.HasFlag(SceneManagerOptions.LazyLoadAssets))
                {
                    // only we dont, we delay this traversal until a refnode references us
                    _deferredAssetLoads.Add(node.NodeID, new AssetTraversalFuture()
                    {
                        AssetNode = node,
                        TraversalState = data,
                    });

                    return null;
                }
            }

            // --------------------------- Add game object ---------------------------------------

            // builder nodes
            switch (node)
            {
                case RefNode refNode:
                    return ProcessRefNode(refNode, in data);
                case Geometry geom:
                    return ProcessGeometry(geom, in data);
                case Crossboard crossboard:
                    return ProcessCrossboard(crossboard, in data);
                default:
                    break;
            }

            data.NodeHandle = CreateNodeHandle(node, PoolObjectFeature.None);

            if (node.HasState())
                data.ActiveStateNode = data.NodeHandle;

            var activeGo = data.NodeHandle.gameObject;

            // -------------- Check if asset objects --------------------------------------------
            if (node.CullMask == CullMaskValue.ALL)     // Not redered or intersected
            {
                activeGo.SetActive(false);            // Lets deactivate the object but build it as ordinary
            }

            switch (node)
            {
                case Roi roi:
                    ProcessRoi(roi, in data);
                    break;
                case RoiNode roiNode:
                    ProcessRoiNode(roiNode, in data);
                    break;
                case gzTransform tr:
                    ProcessTransform(tr, activeGo.transform);
                    ProcessGroup(tr, in data, false);
                    break;
                case DynamicLoader dl:
                    ProcessDynamicLoader(dl, in data);
                    break;
                case Lod ld:
                    ProcessLod(ld, in data);
                    break;
                case Group group:
                    ProcessGroup(group, in data, false);
                    break;
                case ExtRef extRef:
                    var info = new AssetLoadInfo(activeGo, extRef.ResourceURL, extRef.ObjectID);
                    pendingAssetLoads.Push(info);
                    break;
                default:
                    break;
            }

            return activeGo;
        }

        private IEnumerator AssetLoader()
        {
            while (true)
            {
                if (pendingAssetLoads.Count > 0)
                {
                    AssetLoadInfo info = pendingAssetLoads.Pop();

                    AssetBundle assetBundle;

                    if (!currentAssetBundles.TryGetValue(info.resource_url, out assetBundle))
                    {
                        UnityEngine.Networking.UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(info.resource_url, 0);

                        yield return request.SendWebRequest();

                        assetBundle = DownloadHandlerAssetBundle.GetContent(request);

                        if (assetBundle)
                            currentAssetBundles.Add(info.resource_url, assetBundle);
                    }

                    if (assetBundle != null)
                    {

                        GameObject extRefObject = assetBundle.LoadAsset<GameObject>(info.object_id);

                        if (extRefObject != null)
                        {
                            GameObject instance = Instantiate(extRefObject);

                            if (instance != null)
                            {
                                instance.name = info.object_id;
                                instance.transform.SetParent(info.parent.transform, false);
                            }
                        }
                    }
                }

                yield return null;
            }
        }

        // The LoadMap function takes an URL and loads the map into GizmoSDK native db
        public bool LoadMap(string mapURL)
        {
            NodeLock.WaitLockEdit();      // We assume we do all editing from main thread and to allow render we assume we edit in edit mode

            try // We are now locked in edit
            {

                if (!ResetMap())
                    return false;

                Node node = null;

                while (true)
                {
                    if (string.IsNullOrEmpty(mapURL))
                        break;

                    string errorString = "";
                    SerializeAdapter.AdapterError errorType = SerializeAdapter.AdapterError.NO_ERROR;
                    bool retry = false;

                    node = DbManager.LoadDB(mapURL, ref errorString, ref errorType);

                    if (node == null || !node.IsValid())
                    {
                        Message.Send(ID, MessageLevel.WARNING, $"Failed to load map {mapURL}");

                        OnMapLoadError?.Invoke(ref mapURL, errorString, errorType, ref retry);

                        if (retry)
                            continue;

                        return false;
                    }

                    break;
                }

                MapUrl = mapURL;

                MapControl.SystemMap.NodeURL = mapURL;
                MapControl.SystemMap.CurrentMap = node;

                

                if (node != null)
                {
                    _native_scene.AddNode(MapControl.SystemMap.CurrentMap);
#if DEBUG
                    _native_scene.Debug();
#endif

                    _root = new GameObject("root");
                    GameObject scene = BeginTraverse(MapControl.SystemMap.CurrentMap);

                    if (scene != null)
                        scene.transform.SetParent(_root.transform, false);

                    // As GizmoSDK has a flipped Z axis going out of the screen we need a top transform to flip Z
                    _root.transform.localScale = new Vector3(1, 1, -1);
                }
                
                

                //// Add example object under ROI --------------------------------------------------------------

                //MapPos mapPos;

                //GetMapPosition(new LatPos(1.0084718541, 0.24984267815, 300), out mapPos, GroundClampType.GROUND, true);

                //_test = GameObject.CreatePrimitive(PrimitiveType.Sphere);

                //_test.transform.parent = FindFirstGameObjectTransform(mapPos.roiNode);
                //_test.transform.localPosition = mapPos.position.ToVector3();
                //_test.transform.localScale = new Vector3(10, 10, 10);

                if (SceneManagerCamera != null)
                    SceneManagerCamera.MapChanged();

                OnMapChanged?.Invoke(node);
            }
            finally
            {
                NodeLock.UnLock();
            }

            return true;
        }

        public bool ResetMap()
        {
            //MapUrl = null;

            NodeLock.WaitLockEdit();

            try // We are now locked in edit
            {

                foreach (var p in pendingLoaders)
                {
                    p.loader?.Dispose();
                    p.node?.Dispose();
                }

                pendingLoaders.Clear();
                activePendingLoaders.Clear();

                foreach (var p in pendingActivations)
                {
                    p.node?.Dispose();
                }

                pendingActivations.Clear();

                // allow builders to perform custom clean up
                foreach (var builder in _builders)
                    builder.Reset();

                _assetInstanceBuilder.Reset();

                if (_root)
                {
                    UnloadHierarchy(_root.transform);
                    Free(_root.transform);
                    FreeFromPendingQueue(int.MaxValue);
                    _root = null;
                }

                _native_scene?.RemoveAllNodes();

                _textureManager.Clear();
                _materialManager.Clear();

                // clear all pending asset loads
                _deferredAssetLoads.Clear();

                // clear any pending builds
                pendingBuilds.Clear();

                MapControl.SystemMap.Reset();
            }
            finally
            {
                NodeLock.UnLock();
            }

            return true;
        }

        private void AddDefaultBuilders()
        {
            // initialize node builders
            foreach (var builder in Builders)
                _builders.Add(builder);

            if (_builders.Count == 0)
                Message.Send("SceneManager", MessageLevel.WARNING, "no node builder registered");
            

            // ************* [Deprecated] *************
            //if (GfxCaps.CurrentCaps.HasFlag(Capability.UseTreeCrossboards))
            //AddBuilder(new CrossboardNodeBuilder(Settings.CrossboardShader, Settings.ComputeShader));
        }


        public bool InitializeInternal()        
        {
            // Initialize streamer APIs
            if (!GizmoSDK.Gizmo3D.Platform.Initialize())
                return false;

            // Initialize formats
            DbManager.Initialize();

            GizmoSDK.GizmoBase.Message.Send("SceneManager", MessageLevel.DEBUG, "Initialize Graph Streaming");

            // Add builder for registered types
            AddDefaultBuilders();

            // init object pooling
            _free[(byte)PoolObjectFeature.None] = new Stack<NodeHandle>(65000);
            
            // init allocator prefab for logical objects
            _poolPrefabs[0] = CreateAllocatorPrefabForBuilder(null);

            foreach (var builder in _builders)
            {
                var idx = (byte)builder.Feature;
                if (_free[idx] == null)
                {
                    _free[idx] = new Stack<NodeHandle>(65000);
                    _poolPrefabs[idx] = CreateAllocatorPrefabForBuilder(builder);
                }

                builder.SetTextureManager(_textureManager);
                builder.SetMaterialManager(_materialManager);
            }

            var pools = _free.Where(p => p != null).ToArray();
            foreach (byte poolId in pools.Select(p => (byte)Array.IndexOf(_free, p)))
                _preAllocationRoundRobinQueue.Enqueue(poolId);

            if (_poolPrefabs[(int)PoolObjectFeature.StaticMesh] == null)
            {
                Settings.Options |= SceneManagerOptions.DisableInstancing;
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "disabling instancing, no builder for StaticMesh feature");
            }

        


            // Setup internal subscription events
            _actionReceiver = new NodeAction("DynamicLoadManager");
            _actionReceiver.OnAction += ActionReceiver_OnAction;

            DynamicLoader.OnDynamicLoad += DynamicLoader_OnDynamicLoad;

            
            NodeLock.WaitLockEdit();

            try // We are now locked in edit
            {
                // Camera setup
                _native_camera = new PerspCamera("Test");
                _native_camera.RoiPosition = true;
                MapControl.SystemMap.Camera = _native_camera;

                // Top scene
                _native_scene = new Scene("Scene");
                _native_camera.Scene = _native_scene;

                // Top context
                _native_context = new Context();

#if DEBUG_CAMERA

                // If we want to visualize debug 3D
                _native_camera.Debug(_native_context);      // Enable to debug view

#endif // DEBUG_CAMERA

                // Default travrser
                _native_traverse_action = new CullTraverseAction();

                // _native_traverse_action.SetOmniTraverser(true);  // To skip camera cull and use LOD in omni directions


            }
            finally
            {

                NodeLock.UnLock();
            }

            // Set up dynamic loading
            DynamicLoader.UsePreCache(true);                    // Enable use of mipmap creation on dynamic loading
            DynamicLoaderManager.SetNumberOfActiveLoaders(Settings.DynamicLoaders);   // Lets start with 4 parallell threads
            DynamicLoaderManager.StartManager();

            // Start coroutines for asset loading
            StartCoroutine(AssetLoader());

            return true;
        }

        private NodeHandle CreateAllocatorPrefabForBuilder(INodeBuilder builder)
        {
            var feature = builder != null ? builder.Feature : PoolObjectFeature.None;

            var prefab = new GameObject();
            prefab.SetActive(false);
#if UNITY_EDITOR
            prefab.hideFlags = HideFlags.HideInHierarchy;
#endif
            var nh = prefab.AddComponent<NodeHandle>();
            nh.featureKey = feature;
            builder?.InitPoolObject(prefab);

            return nh;
        }

        public bool Uninitialize()
        {
            if (!_initialized)
                   return false;

            // Stop manager
            DynamicLoaderManager.StopManager();

            ResetMap();

            // Remove actions
            DynamicLoader.OnDynamicLoad -= DynamicLoader_OnDynamicLoad;
            _actionReceiver.OnAction -= ActionReceiver_OnAction;

            NodeLock.WaitLockEdit();

            try // We are now locked in edit
            {

                _native_camera.Debug(_native_context, false);
                _native_camera.Dispose();
                _native_camera = null;

                _native_context.Dispose();
                _native_context = null;

                _native_scene.Dispose();
                _native_scene = null;


                _actionReceiver.Dispose();
                _actionReceiver = null;

            }
            finally
            {
                NodeLock.UnLock();
            }

            // Dont do this as Unity wants to keep modules loaded
            //// Drop platform streamer
            //GizmoSDK.Gizmo3D.Platform.Uninitialize();

            _initialized = false;

            return true;
        }

        public bool Init(bool loadMap = true)
        {
            if (_initialized)
                return true;

            _initialized = true;
           
            // Initialize this manager
            if (!InitializeInternal())
                return false;

            // Load the map
            if (loadMap && !LoadMap(MapUrl))
                return false;

            return true;
        }


        private void ActionReceiver_OnAction(NodeAction sender, NodeActionEvent action, Context context, NodeActionProvider trigger, TraverseAction traverser, IntPtr userdata)
        {
            // Locked in edit or render (render) by caller

            if ((action == NodeActionEvent.IS_TRAVERSABLE) || 
                (action == NodeActionEvent.IS_NOT_TRAVERSABLE))
            {
                pendingActivations.Add(new ActivationInfo(action, trigger as Node));
            }
            else
                trigger?.ReleaseNoDelete();      // We are getting ref counts on objects in scene graph and these need to be released immediately

            traverser?.ReleaseNoDelete();
            context?.ReleaseNoDelete();
        }

        private void Start()
        {
            Init();
        }

        private void OnEnable()
        {
            // important that we do not run Init() from OnEable() since that will run during AddComponent(),
            // and in BTA we need to control when the map is loaded.
            if (!_initialized)
                return;

            Init();
        }

        private void OnApplicationQuit()
        {
            Uninitialize();
        }

        private void OnDisable()
        {
            // We add Unitialize and shut down threads here as this routine gets called by an edit in code
            Uninitialize();
        }
               
        private void DynamicLoader_OnDynamicLoad(DynamicLoadingState state, DynamicLoader loader, Node node)
        {
            // Locked in edit or render (render) by caller

            if (state == DynamicLoadingState.LOADED || state == DynamicLoadingState.UNLOADED)
            {
                if (!activePendingLoaders.ContainsKey(loader.GetNativeReference()))
                {
                    NodeLoadInfo info = new NodeLoadInfo(state, loader, node);

                    pendingLoaders.Add(info);       // Sorted order
                    activePendingLoaders.Add(loader.GetNativeReference(), info);    // Lookup
                }
                else
                {
                    // Balanced add/remove that will cancel traversal or delete

                    for (int i = pendingLoaders.Count - 1; i >= 0; i--)
                    {
                        if (pendingLoaders[i].loader.GetNativeReference() == loader.GetNativeReference())
                        {
                            pendingLoaders.RemoveAt(i);
                            break;
                        }
                    }

                    // remove reference
                    activePendingLoaders.Remove(loader.GetNativeReference());
                }

            }
            //else if (state == DynamicLoadingState.REQUEST_LOAD || state == DynamicLoadingState.REQUEST_UNLOAD || state == DynamicLoadingState.REQUEST_LOAD_CANCEL || state == DynamicLoadingState.REQUEST_LOAD_CANCEL)
            //{
            //    loader?.ReleaseNoDelete();      // Same here. We are getting refs to objects in scene graph that we shouldnt release in GC
            //    node?.ReleaseNoDelete();
            //}
            //else if (state == DynamicLoadingState.IN_LOADING)
            //{
            //    loader?.ReleaseNoDelete();      // Same here. We are getting refs to objects in scene graph that we shouldnt release in GC
            //    node?.ReleaseNoDelete();
            //}
            else
            {
                loader?.ReleaseNoDelete();      // Same here. We are getting refs to objects in scene graph that we shouldnt release in GC
                node?.ReleaseNoDelete();
            }
        }

        private void ProcessPendingUpdatesPreTraversal()
        {
            // We must be called in edit lock

            // Process changes of the scenegraph
            _profilerMarkerTraverse.Begin();
            ProcessPendingLoaders();
            _profilerMarkerTraverse.End();
        }

        private void ProcessPendingUpdatesPostTraversal()
        {
            // We must be called in edit lock

            #region Activate/Deactivate GameObjects based on scenegraph -----------------------------------------------------

            ProcessPendingActivations();

            #endregion

            #region Update slow loading assets ------------------------------------------------------------------------------

            // free up to a maximum number of nodes
            FreeFromPendingQueue(1000);

            // make sure we have available nodes in our pools
            PreAllocateNodeHandle(10000, TimeSpan.FromMilliseconds(1));


            var remainingBuildTime = TimeSpan.FromSeconds(Settings.MaxBuildTime) - _renderTimer.Elapsed;
            if (remainingBuildTime < TimeSpan.FromSeconds(Settings.MinBuildTime))
                remainingBuildTime = TimeSpan.FromSeconds(Settings.MinBuildTime);

            ProcessPendingBuilders(remainingBuildTime);

            #endregion
        }

        private void ProcessPendingLoaders()
        {
            foreach (NodeLoadInfo nodeLoadInfo in pendingLoaders)
            {
                if (nodeLoadInfo.state == DynamicLoadingState.LOADED)   // We got a callback from dyn loader that we were loaded or unloaded
                {
                    unTransform transform = NodeUtils.FindFirstGameObjectTransformUnsafe(nodeLoadInfo.loader.GetNativeReference());

                    if (transform == null)              // We have been unloaded or not registered
                        continue;

                    if (transform.childCount != 0)       // We have already a child and our sub graph was loaded
                        continue;

                    // TODO: Active state node can be further up the tree and should be located and passed here
                    GameObject go = BeginTraverse(nodeLoadInfo.node,true);       // Build sub graph as result of dynamic loader

                    if (go != null)
                        go.transform.SetParent(transform, false);               // Connect to our parent

                }
                else if (nodeLoadInfo.state == DynamicLoadingState.UNLOADED)
                {
                    if (NodeUtils.FindGameObjectsUnsafe(
                        nodeLoadInfo.loader.GetNativeReference(), out List<GameObject> list))
                    {
                        foreach (var go in list)
                        {
                            var tr = go.transform;
                            for (var i = tr.childCount - 1; i >= 0; i--)
                            {
                                var child = tr.GetChild(i);
                                //We need to unload all linked go in hierarchy
                                UnloadHierarchy(child);
                                Free(child);
                            }
                        }
                    }
                }
            }

            pendingLoaders.Clear();         // Clear List
            activePendingLoaders.Clear();   // Clear index
        }

        private void ProcessPendingActivations()
        {
            foreach (ActivationInfo activationInfo in pendingActivations)
            {
                List<GameObject> list;  // We need to activate the correct nodes

                if (NodeUtils.FindGameObjectsUnsafe(activationInfo.node.GetNativeReference(), out list))
                {
                    foreach (GameObject obj in list)
                    {
                        if (activationInfo.state == NodeActionEvent.IS_TRAVERSABLE)
                            obj.SetActive(true);
                        else if (activationInfo.state == NodeActionEvent.IS_NOT_TRAVERSABLE)
                            obj.SetActive(false);
                    }
                }
                else 
                {
                    Message.Send(ID, MessageLevel.DEBUG, $"Got Activation {activationInfo.state} for missing node");
                }
            }

            pendingActivations.Clear();
        }

        private void ProcessPendingBuilders(TimeSpan maxBuildTime)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            
            while ((pendingBuilds.Count > 0) && (timer.Elapsed < maxBuildTime))
            {
                var buildInfo = pendingBuilds.Dequeue();

                var nodeHandle = buildInfo.NodeHandle;
                if (buildInfo.Version != nodeHandle.version)
                {
                    continue;
                }

                var activeStateNode = buildInfo.ActiveStateNode;
                if (activeStateNode != null && activeStateNode.node == null)
                {
                    continue;
                }

                if (buildInfo.Builder.Build(nodeHandle, activeStateNode))
                    buildInfo.NodeHandle.builder = buildInfo.Builder;
                else
                {
#if DEBUG
                    Debug.LogError("build failed");
#endif
                }
            }
        }

        public static void ResetTotalBuildDiagnostics()
        {
            _totalMeshCount = 0;
            _totalMeshBytes = 0;
            _totalMeshTicks = 0;
            _totalMeshWorstTicks = 0;
            _totalTextureCount = 0;
            _totalTextureBytes = 0;
            _totalTextureTicks = 0;
            _totalTextureWorstTicks = 0;
            _buildDiagnosticsResetVersion++;
        }

        public void ResetStreamingDiagnostics()
        {
            ResetTotalBuildDiagnostics();
            _textureManager.ResetDiagnostics();
        }

        public static MeshBuildDiagnostics MeshFrameDiagnostics
        {
            get
            {
                RollBuildDiagnosticsFrame();
                return CreateMeshBuildDiagnostics(
                    _frameMeshCount,
                    _frameMeshBytes,
                    _frameMeshTicks,
                    _frameMeshWorstTicks);
            }
        }

        public static TextureBuildDiagnostics TextureFrameDiagnostics
        {
            get
            {
                RollBuildDiagnosticsFrame();
                return CreateTextureBuildDiagnostics(
                    _frameTextureCount,
                    _frameTextureBytes,
                    _frameTextureTicks,
                    _frameTextureWorstTicks);
            }
        }

        public static MeshBuildDiagnostics MeshTotalDiagnostics
        {
            get => CreateMeshBuildDiagnostics(
                _totalMeshCount,
                _totalMeshBytes,
                _totalMeshTicks,
                _totalMeshWorstTicks);
        }

        public static TextureBuildDiagnostics TextureTotalDiagnostics
        {
            get => CreateTextureBuildDiagnostics(
                _totalTextureCount,
                _totalTextureBytes,
                _totalTextureTicks,
                _totalTextureWorstTicks);
        }

        internal static void RecordMeshBuild(
            long totalBytes,
            long elapsedTicks)
        {
            RollBuildDiagnosticsFrame();
            _frameMeshCount++;
            _frameMeshBytes += totalBytes;
            _frameMeshTicks += elapsedTicks;
            _frameMeshWorstTicks =
                Math.Max(_frameMeshWorstTicks, elapsedTicks);
            _totalMeshCount++;
            _totalMeshBytes += totalBytes;
            _totalMeshTicks += elapsedTicks;
            _totalMeshWorstTicks =
                Math.Max(_totalMeshWorstTicks, elapsedTicks);
        }

        internal static void RecordTextureBuild(
            long totalBytes,
            long elapsedTicks)
        {
            RollBuildDiagnosticsFrame();
            _frameTextureCount++;
            _frameTextureBytes += totalBytes;
            _frameTextureTicks += elapsedTicks;
            _frameTextureWorstTicks =
                Math.Max(_frameTextureWorstTicks, elapsedTicks);
            _totalTextureCount++;
            _totalTextureBytes += totalBytes;
            _totalTextureTicks += elapsedTicks;
            _totalTextureWorstTicks =
                Math.Max(_totalTextureWorstTicks, elapsedTicks);
        }

        internal static long BuildDiagnosticsResetVersion =>
            _buildDiagnosticsResetVersion;

        private static MeshBuildDiagnostics CreateMeshBuildDiagnostics(
            long totalMeshes,
            long totalBytes,
            long totalTicks,
            long worstTicks)
        {
            return new MeshBuildDiagnostics(
                totalMeshes,
                totalBytes,
                TimeSpan.FromSeconds(
                    (double)totalTicks /
                    System.Diagnostics.Stopwatch.Frequency),
                TimeSpan.FromSeconds(
                    (double)worstTicks /
                    System.Diagnostics.Stopwatch.Frequency));
        }

        private static TextureBuildDiagnostics CreateTextureBuildDiagnostics(
            long totalTextures,
            long totalBytes,
            long totalTicks,
            long worstTicks)
        {
            return new TextureBuildDiagnostics(
                totalTextures,
                totalBytes,
                TimeSpan.FromSeconds(
                    (double)totalTicks /
                    System.Diagnostics.Stopwatch.Frequency),
                TimeSpan.FromSeconds(
                    (double)worstTicks /
                    System.Diagnostics.Stopwatch.Frequency));
        }

        private static void RollBuildDiagnosticsFrame()
        {
            var frame = UnityEngine.Time.frameCount;
            if (_buildDiagnosticsFrame == frame)
                return;
            _buildDiagnosticsFrame = frame;
            _frameMeshCount = 0;
            _frameMeshBytes = 0;
            _frameMeshTicks = 0;
            _frameMeshWorstTicks = 0;
            _frameTextureCount = 0;
            _frameTextureBytes = 0;
            _frameTextureTicks = 0;
            _frameTextureWorstTicks = 0;
        }

        private void UpdateNodeInternals()
        {
            // Only called if SceneManagerCamera is not null
            foreach (GameObject go in updateNodeObjects)
            {
                NodeHandle h = go.GetComponent<NodeHandle>();

                h.UpdateNodeInternals();
            }
        }

        // Update is called once per frame
        private void Update()
        {
            if (Settings.Options.HasFlag(SceneManagerOptions.RenderInUpdate))
                Render();
        }

        private readonly System.Diagnostics.Stopwatch _renderTimer = new System.Diagnostics.Stopwatch();

        public void Render()
        {
            _renderTimer.Restart();

            // Check if global world camera is present -----------------------
            if (SceneManagerCamera == null)
                return;

            _profilerMarkerRender.Begin();

            RenderInternal();

            _profilerMarkerRender.End();
            
            // -------------------------------------------------------------
            SceneManagerCamera.PostTraverse(false);
            OnPostTraverse?.Invoke(false);
        }

        private void RenderInternal()
        {
            // Check if local unity camera is present ------------------------
            var unityCamera = SceneManagerCamera.Camera;
            if (unityCamera == null)
                return;

            // Check if local native camera is present ------------------------
            if (_native_camera == null)
                return;

            // Lets try to build a scenegraph from pending changes from previous pass
            if (!NodeLock.TryLockEdit(30))      // 30 msek allow latency of other pending editor
            {
                Message.Send(ID, MessageLevel.DEBUG, "Lock contention detected! NodeLock::TryLockEdit() FRAME LOST");

                // We failed to refresh scene in reasonable time but we still need to issue updates;
                SceneManagerCamera.PreTraverse(false);
                OnPreTraverse?.Invoke(false);
                return;
            }

            // Signal the world camera we are in pre traverse locked
            SceneManagerCamera.PreTraverse(true);

            // Signal the SM we are in pre traverse locked
            OnPreTraverse?.Invoke(true);

            // Builds a scenegraph from changes from previous frame
            ProcessPendingUpdatesPreTraversal();

            if (!NodeLock.ChangeToRenderLock())
            {
                NodeLock.UnLock();
                Message.Send(ID, MessageLevel.DEBUG, "Failed to change into RenderLock");
            }

            if (!NodeLock.IsLockedRender())
                return;


            if (activePendingLoaders.Count > 0) // Check if we got a mismatch in updates
            {
                NodeLock.UnLock(); // Unlock render
                Message.Send(ID, MessageLevel.FATAL, "Mismatch in virtual context (loaded/unloaded data)");
                return;
            }

            // We are now locked in Render
            _profilerMarkerCull.Begin();
            RenderInternal(unityCamera);
            _profilerMarkerCull.End();

            if (!NodeLock.ChangeToEditLock())
            {
                NodeLock.UnLock();
                Message.Send(ID, MessageLevel.DEBUG, "Failed to change into EditLock");
            }

            if (!NodeLock.IsLockedByMe())
                return;

            // Builds a scenegraph from changes from previous frame
            ProcessPendingUpdatesPostTraversal();

            NodeLock.UnLock();

            // Unlocked updates
            UpdateNodeInternals();
        }

        private void RenderInternal(UnityEngine.Camera UnityCamera)
        {
            // We are now locked in Render

            // Setup LOD

            // lod bias
            var lodFactor = SceneManagerCamera.LodFactor;
            Lod.SetLODFactor(_native_context, lodFactor);
            MapControl.SystemMap.LodFactor = lodFactor;

            // Transfer camera parameters

            PerspCamera perspCamera = _native_camera as PerspCamera;

            // Right now we use system time as render time but this can be controlled externally in the future
            var renderTime = GizmoSDK.GizmoBase.Time.SystemSeconds;

            // Syncronized update. You should use the rendertime
            renderTime = SceneManagerCamera.UpdateCamera(renderTime);
            OnUpdateCamera?.Invoke(renderTime);

            // Use this time in render
            _native_context.CurrentRenderTime = renderTime;

            if (perspCamera != null)
            {
                perspCamera.VerticalFOV = UnityCamera.fieldOfView;
                perspCamera.HorizontalFOV = 2 * Mathf.Atan(Mathf.Tan(UnityCamera.fieldOfView * Mathf.Deg2Rad / 2) * UnityCamera.aspect) * Mathf.Rad2Deg; ;
                perspCamera.NearClipPlane = UnityCamera.nearClipPlane;
                perspCamera.FarClipPlane = UnityCamera.farClipPlane;
            }

            Matrix4x4 unity_camera_transform = UnityCamera.transform.worldToLocalMatrix;

            _native_camera.Transform = unity_camera_transform.ToZFlippedMatrix4();

            _native_camera.Position = SceneManagerCamera.GlobalPosition;

            _native_camera.Render(_native_context, 1000, 1000, 1000, _native_traverse_action);

#if DEBUG_CAMERA
                     _native_camera.DebugRefresh();
#endif
        }

        private NodeHandle Allocate(PoolObjectFeature featureKey, Node node)
        {
            var idx = (byte)featureKey;
            var pool = _free[idx];               

            if (pool.Count == 0)
                FreeFromPendingQueue(100);
            
            if (pool.Count > 0)
            {
                var res = pool.Pop();
                res.node = node;

                // init
                res.gameObject.SetActive(true);             // <-- stupid slow
#if UNITY_EDITOR
                res.gameObject.hideFlags = HideFlags.None;
#endif

                return res;
            }

            var nh = Instantiate(_poolPrefabs[idx]);
            nh.node = node;
            nh.gameObject.SetActive(true);
            return nh;
        }

        private void Free(unTransform transform)
        {
            transform.parent = null;
            transform.gameObject.SetActive(false);
#if UNITY_EDITOR
            transform.hideFlags = HideFlags.HideInHierarchy;
#endif
            
            _pendingFrees.Push(transform);
        }

        private void UnloadHierarchy(unTransform transform)
        {
            if (transform.TryGetComponent<NodeHandle>(out var nodeHandle))
            {
                // remove from update list
                if (nodeHandle.inNodeUpdateList)
                    updateNodeObjects.Remove(transform.gameObject);

                // remove from registry
                if (nodeHandle.inNodeUtilsRegistry)
                    NodeUtils.RemoveGameObjectReferenceUnsafe(nodeHandle.node.GetNativeReference(), transform.gameObject);

                // invalidate any pending builds for this node handle
                nodeHandle.version++;
            }
            
            // recurse down the hierarchy
            for (var i = 0; i < transform.childCount; ++i)
                UnloadHierarchy(transform.GetChild(i));
        }

        private void FreeFromPendingQueue(int count)
        {
            _profilerFree.Begin();
            while (_pendingFrees.Count > 0 && count > 0)
            {
                var free = _pendingFrees.Pop();
        
                // orphan all children and put them on the free frontier
                for (var i = free.childCount - 1; i >= 0; --i)
                    Free(free.GetChild(i));
        
                FreeInternal(free);
        
                --count;
            }
            _profilerFree.End();
        }

        private void PreAllocateNodeHandle(int count, TimeSpan timeBudget)
        {
            if (_preAllocationRoundRobinQueue.Count == 0)
                return;

            var timer = System.Diagnostics.Stopwatch.StartNew();

            var fullyAllocatedPools = 0;

            while (timer.Elapsed < timeBudget && fullyAllocatedPools < _preAllocationRoundRobinQueue.Count)
            {
                var poolId = _preAllocationRoundRobinQueue.Dequeue();
                _preAllocationRoundRobinQueue.Enqueue(poolId);
                
                var pool = _free[poolId];

                // do in batches of 100
                var remaining = count - pool.Count;
                if (remaining > 100)
                    remaining = 100;

                if (pool.Count < count)
                    AllocateNodeHandleForPool(poolId, remaining);
                else
                    fullyAllocatedPools++;
            }
        }

        private void AllocateNodeHandleForPool(byte poolId, int count)
        {
            for (var i = 0; i < count; ++i)
            {
                var nh = Instantiate(_poolPrefabs[poolId]);
#if UNITY_EDITOR
                nh.gameObject.hideFlags = HideFlags.HideInHierarchy;
#endif
                _free[poolId].Push(nh);
            }
        }
        

        private void FreeInternal(unTransform transform)
        {
            if (transform.TryGetComponent<NodeHandle>(out var nodeHandle))
                FreeHandle(nodeHandle);
            else
                Destroy(transform.gameObject);
        }

        private void FreeHandle(NodeHandle nodeHandle)
        {
            // get pool managing this type of node
            var pool = _free[(byte)nodeHandle.featureKey];

            // return the handle to the pool
            pool.Push(nodeHandle);

            var go = nodeHandle.gameObject;
            
            var tr = go.transform;
            //tr.parent = null;
            tr.localPosition = Vector3.zero;
            tr.localRotation = UnityEngine.Quaternion.identity;
            tr.localScale = Vector3.one;

            var node = nodeHandle.node;

            if (nodeHandle.builder != null)
            {
                bool sharedNode = nodeHandle.stateFlags.HasFlag(NodeStateFlags.AssetInstance);
                nodeHandle.builder.BuiltObjectReturnedToPool(go, sharedNode);
            }

            if (node is Geometry)
            {
                switch (nodeHandle.featureKey)
                {
                    case PoolObjectFeature.Terrain:
                        OnRemoveTerrain?.Invoke(go);
                        break;
                    case PoolObjectFeature.StaticMesh:
                        OnRemoveGeometry?.Invoke(go);
                        break;
                    default:
                        break;
                }
            }

            nodeHandle.Recycle(_textureManager);

            OnEnterPool?.Invoke(go);
        }
    }

    [Flags]
    public enum PoolObjectFeature : byte
    {
        //
        None = 0,

        // Terrain
        Terrain = 1 << 0,

        //
        StaticMesh = 1 << 1,

        //
        Crossboard = 1 << 2,
    }


}
