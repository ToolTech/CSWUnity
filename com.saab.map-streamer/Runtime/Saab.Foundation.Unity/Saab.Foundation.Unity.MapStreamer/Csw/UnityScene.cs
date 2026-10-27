//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: UnityScene.cs
// Description	: Main-thread hierarchy, scope gates and render resource leases
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Added command-driven hierarchy ownership, pooling and camera-relative frame publication.	(2.12.327)
// AMO	261023	Indexed shared resource users through rebind and pooling; profiled publication and frame commits.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Collections.Generic;
using Thread = System.Threading.Thread;
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using Saab.Unity.Extensions;
using UnityEngine;
using Camera = UnityEngine.Camera;
using Quaternion = UnityEngine.Quaternion;
using Transform = UnityEngine.Transform;
using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;
using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;

namespace Saab.Foundation.Unity.MapStreamer.Csw
{
	/// <summary>Owns the Unity occurrence hierarchy, pooling and render-resource leases.</summary>
	/// <remarks>
	/// Create, access, clear and dispose on the Unity main thread. Camera and Coordinates
	/// are borrowed; returned handles, Unity objects and textures remain presentation-owned.
	/// </remarks>
	public sealed class UnityScene : IScenePresentation, IStreamingDiagnosticsProvider, IDisposable
	{
		private static readonly ProfilerMarker _publishResourcesMarker =
			new ProfilerMarker(ProfilerCategory.Render, "CSW-PublishResources");
		private static readonly ProfilerMarker _commitMarker =
			new ProfilerMarker(ProfilerCategory.Render, "CSW-CommitFrame");
		private readonly int _threadId = Thread.CurrentThread.ManagedThreadId;
		private readonly Dictionary<SceneInstanceKey, SceneInstanceHandle> _instances =
			new Dictionary<SceneInstanceKey, SceneInstanceHandle>();
		private readonly Dictionary<IntPtr, HashSet<SceneInstanceHandle>> _meshUsers =
			new Dictionary<IntPtr, HashSet<SceneInstanceHandle>>();
		private readonly Dictionary<IntPtr, HashSet<SceneInstanceHandle>> _stateUsers =
			new Dictionary<IntPtr, HashSet<SceneInstanceHandle>>();
		private readonly Stack<SceneInstanceHandle> _pool = new Stack<SceneInstanceHandle>();
		private readonly HashSet<SceneInstanceHandle> _pendingTerrain = new HashSet<SceneInstanceHandle>();
		private readonly HashSet<SceneInstanceHandle> _notified = new HashSet<SceneInstanceHandle>();
		private readonly HashSet<SceneInstanceHandle> _pendingPresentation = new HashSet<SceneInstanceHandle>();
		private readonly HashSet<SceneInstanceHandle> _pendingVisibility = new HashSet<SceneInstanceHandle>();
		private readonly int _maximumPooledObjects;
		private readonly GameObject _poolRoot;
		private bool _disposed;
		private ulong _nextLifetime;

		internal SceneResources Resources { get; } = new SceneResources();
		public SceneCoordinateContext Coordinates { get; }
		public GameObject Root { get; }
		public Camera Camera { get; }
		public ulong Generation { get; private set; }
		public int InstanceCount => _instances.Count - 1;
		public int PooledObjectCount => _pool.Count;
		public int MeshCount => Resources.MeshCount;
		public int MaterialCount => Resources.MaterialCount;
		internal long ResourcePublicationVisits { get; private set; }
		public IReadOnlyCollection<SceneInstanceHandle> Instances => _instances.Values;
		public bool HasCommittedFrame { get; private set; }
		public CameraRelativeFrame CommittedFrame { get; private set; }
		public TextureManagerDiagnostics TextureDiagnostics => Resources.TextureDiagnostics;
		public MeshBuildDiagnostics MeshFrameDiagnostics => SceneManager.MeshFrameDiagnostics;
		public TextureBuildDiagnostics TextureFrameDiagnostics => SceneManager.TextureFrameDiagnostics;
		public MeshBuildDiagnostics MeshTotalDiagnostics => SceneManager.MeshTotalDiagnostics;
		public TextureBuildDiagnostics TextureTotalDiagnostics => SceneManager.TextureTotalDiagnostics;
		public long BuildDiagnosticsResetVersion => SceneManager.BuildDiagnosticsResetVersion;

		public event SceneManager.EventHandler_OnGameObject OnNewTerrain;
		public event SceneManager.EventHandler_OnGameObject OnNewGeometry;
		public event SceneManager.EventHandler_OnGameObject OnNewCrossboard;
		public event SceneManager.EventHandler_OnGameObject OnNewLod;
		public event SceneManager.EventHandler_OnGameObject OnNewLoader;
		public event SceneManager.EventHandler_OnGameObjectFree OnEnterPool;
		public event SceneManager.EventHandler_OnGameObjectFree OnRemoveGeometry;
		public event SceneManager.EventHandler_OnGameObjectFree OnRemoveTerrain;
		public event SceneManager.EventHandler_Traverse OnPreTraverse;
		public event SceneManager.EventHandler_Traverse OnPostTraverse;
		public event Action OnMapChanged;
		public event Action<string> OnMapLoadError;
		public event SceneManager.EventHandler_OnUpdateCamera OnUpdateCamera;
		public event Action<CameraRelativeFrame> FrameCommitting;

		/// <summary>Creates an owned scene attachment beneath a borrowed parent.</summary>
		/// <remarks>The supplied camera and coordinate context remain caller-owned.</remarks>
		public UnityScene(Transform parent, Camera camera, SceneCoordinateContext coordinates,
			int maximumPooledObjects = 2048, IntersectMaskValue intersectMask = IntersectMaskValue.ALL)
		{
			if (!parent)
				throw new ArgumentNullException(nameof(parent));
			if (!camera)
				throw new ArgumentNullException(nameof(camera));
			ValidateAttachment(parent);
			Coordinates = coordinates ?? throw new ArgumentNullException(nameof(coordinates));
			Camera = camera;
			_maximumPooledObjects = Math.Max(0, maximumPooledObjects);
			Root = new GameObject("CSW Scene");
			Root.transform.SetParent(parent, false);
			Root.transform.localScale = Coordinates.Axes.RootScale;
			var root = Root.AddComponent<SceneInstanceHandle>();
			root.IsReady = true;
			root.IsLocallyActive = true;
			root.IsPresented = true;
			root.IntersectMask = intersectMask;
			_instances.Add(SceneInstanceKey.Root, root);
			_poolRoot = new GameObject("CSW Pool");
			_poolRoot.transform.SetParent(Root.transform, false);
			_poolRoot.SetActive(false);
		}

		internal void RequireMainThread()
		{
			if (Thread.CurrentThread.ManagedThreadId != _threadId)
				throw new InvalidOperationException("CSW presentation must run on its Unity main thread.");
			if (_disposed)
				throw new ObjectDisposedException(nameof(UnityScene));
		}

		/// <summary>Returns a borrowed, potentially pooled handle for the current occurrence.</summary>
		public bool TryGetInstance(SceneInstanceKey key, out SceneInstanceHandle handle)
		{
			RequireMainThread();
			return _instances.TryGetValue(key, out handle);
		}

		/// <summary>Copies current terrain metadata while retaining presentation ownership of its textures.</summary>
		public bool TryGetTerrain(GameObject gameObject, out TerrainPresentationData terrain)
		{
			terrain = default;
			if (!gameObject || !gameObject.TryGetComponent<SceneInstanceHandle>(out var handle) ||
				handle.Generation != Generation || handle.Feature != PoolObjectFeature.Terrain ||
				!handle.IsReady || !_instances.TryGetValue(handle.Key, out var registered) || registered != handle)
				return false;
			terrain = handle.Terrain;
			return true;
		}

		internal SceneInstanceHandle Parent(SceneInstanceKey key)
		{
			if (!_instances.TryGetValue(key, out var parent))
				throw new InvalidOperationException($"SceneManager referenced missing parent {key}.");
			return parent;
		}

		internal void Create(SceneNodeData data)
		{
			RequireMainThread();
			ValidateLocalTransform(data.LocalTransform);
			if (data.Key.Equals(SceneInstanceKey.Root) || _instances.ContainsKey(data.Key))
				throw new InvalidOperationException($"Duplicate scene occurrence {data.Key}.");
			var parent = Parent(data.Parent);
			var handle = _pool.Count > 0 ? _pool.Pop() : new GameObject().AddComponent<SceneInstanceHandle>();
			handle.gameObject.SetActive(false);
			handle.Key = data.Key;
			handle.ParentKey = data.Parent;
			handle.Generation = Generation;
			handle.Version++;
			handle.Lifetime = checked(++_nextLifetime);
			handle.ParentVersion = 1;
			handle.IsPresented = false;
			handle.IsLocallyActive = !data.Parent.Equals(SceneInstanceKey.Root);
			handle.IsStatic = data.IsStatic;
			handle.IsAsset = false;
			handle.HasFrameTransform = false;
			handle.IntersectMask = data.IntersectMask;
			handle.BoundaryCenter = data.BoundaryCenter;
			handle.Builder = data.Builder;
			handle.Feature = data.Builder ? data.Builder.Feature : PoolObjectFeature.None;
			handle.gameObject.name = data.Parent.Equals(SceneInstanceKey.Root)
				? $"MapInstanceRoot {data.Name}"
				: data.Name;
			handle.transform.SetParent(parent.transform, false);
			ApplyLocalTransform(handle.transform, data.LocalTransform);
			_instances.Add(data.Key, handle);
			parent.Children.Add(data.Key);
			_pendingPresentation.Add(handle);
			if (data.Prepared != null)
				PublishResources(handle, data);
			handle.IsReady = true;
			NotifyCreated(handle, data.Kind);
		}

		internal void Update(SceneNodeData data)
		{
			RequireMainThread();
			ValidateLocalTransform(data.LocalTransform);
			if (!_instances.TryGetValue(data.Key, out var handle))
				throw new InvalidOperationException($"Update referenced missing occurrence {data.Key}.");
			if (!handle.ParentKey.Equals(data.Parent))
			{
				var parent = Parent(data.Parent);
				var ancestor = parent;
				while (!ancestor.Key.Equals(SceneInstanceKey.Root))
				{
					if (ancestor.Key.Equals(handle.Key))
						throw new InvalidOperationException("SceneManager attempted to create a hierarchy cycle.");
					ancestor = Parent(ancestor.ParentKey);
				}
				Parent(handle.ParentKey).Children.Remove(handle.Key);
				parent.Children.Add(handle.Key);
				handle.ParentKey = data.Parent;
				handle.ParentVersion++;
				handle.transform.SetParent(parent.transform, false);
			}
			handle.Version++;
			if (data.Kind == SceneNodeKind.Geometry && !data.Builder && handle.Resources != null)
			{
				NotifyRemoved(handle);
				DetachResources(handle);
				handle.Feature = PoolObjectFeature.None;
			}
			handle.BoundaryCenter = data.BoundaryCenter;
			handle.IntersectMask = data.IntersectMask;
			handle.IsStatic = data.IsStatic;
			handle.Builder = data.Builder;
			ApplyLocalTransform(handle.transform, data.LocalTransform);
			if (data.Prepared != null)
				PublishResources(handle, data);
		}

		private void PublishResources(SceneInstanceHandle target, SceneNodeData data)
		{
			using var sample = _publishResourcesMarker.Auto();
			var prepared = data.Prepared;
			var affected = new HashSet<SceneInstanceHandle>();
			if (prepared.HasMesh)
				CollectResourceUsers(_meshUsers, prepared.MeshSource, affected);
			if (prepared.HasState)
			{
				CollectResourceUsers(_stateUsers, prepared.StateSource, affected);
				foreach (var source in prepared.DependentStates.Keys)
					CollectResourceUsers(_stateUsers, source, affected);
			}
			if (target.Resources != null)
			{
				ResourcePublicationVisits++;
				affected.Add(target);
			}
			foreach (var handle in affected)
				NotifyRemoved(handle);
			using (var retired = Resources.Publish(prepared))
			{
				foreach (var handle in affected)
				{
					var source = handle == target ? prepared.StateSource : handle.Resources.State?.Source ?? IntPtr.Zero;
					BindResources(handle, source);
					NotifyCreated(handle, SceneNodeKind.Geometry);
				}
				if (!affected.Contains(target))
					BindResources(target, prepared.StateSource);
			}
		}

		private void BindResources(SceneInstanceHandle handle, IntPtr stateSource)
		{
			var replacement = Resources.Acquire(handle.Key.Node, stateSource, handle.Builder);
			var previous = handle.Resources;
			RemoveResourceUsers(handle);
			handle.Resources = replacement;
			AddResourceUser(_meshUsers, replacement.Mesh.Source, handle);
			if (replacement.State != null)
				AddResourceUser(_stateUsers, replacement.State.Source, handle);
			if (!handle.TryGetComponent<MeshFilter>(out var filter))
				filter = handle.gameObject.AddComponent<MeshFilter>();
			if (!handle.TryGetComponent<MeshRenderer>(out var renderer))
				renderer = handle.gameObject.AddComponent<MeshRenderer>();
			filter.sharedMesh = replacement.Mesh.Mesh;
			renderer.sharedMaterial = replacement.Material.Material;
			renderer.enabled = true;
			handle.gameObject.layer = handle.Builder.SceneLayer;
			handle.Feature = handle.Builder.Feature;
			previous?.Dispose();
		}

		private void CollectResourceUsers(Dictionary<IntPtr, HashSet<SceneInstanceHandle>> index,
			IntPtr source, HashSet<SceneInstanceHandle> affected)
		{
			if (!index.TryGetValue(source, out var users))
				return;
			foreach (var handle in users)
			{
				ResourcePublicationVisits++;
				affected.Add(handle);
			}
		}

		private static void AddResourceUser(Dictionary<IntPtr, HashSet<SceneInstanceHandle>> index,
			IntPtr source, SceneInstanceHandle handle)
		{
			if (!index.TryGetValue(source, out var users))
			{
				users = new HashSet<SceneInstanceHandle>();
				index.Add(source, users);
			}
			users.Add(handle);
		}

		private void RemoveResourceUsers(SceneInstanceHandle handle)
		{
			var lease = handle.Resources;
			if (lease == null)
				return;
			RemoveResourceUser(_meshUsers, lease.Mesh.Source, handle);
			if (lease.State != null)
				RemoveResourceUser(_stateUsers, lease.State.Source, handle);
		}

		private static void RemoveResourceUser(Dictionary<IntPtr, HashSet<SceneInstanceHandle>> index,
			IntPtr source, SceneInstanceHandle handle)
		{
			if (!index.TryGetValue(source, out var users) || !users.Remove(handle))
				throw new InvalidOperationException("A scene resource lease lost its source-user registration.");
			if (users.Count == 0)
				index.Remove(source);
		}

		private void NotifyCreated(SceneInstanceHandle handle, SceneNodeKind kind)
		{
			if (handle.Feature == PoolObjectFeature.Terrain)
			{
				_pendingTerrain.Add(handle);
			}
			else if (handle.Feature == PoolObjectFeature.StaticMesh && _notified.Add(handle))
				OnNewGeometry?.Invoke(handle.gameObject, handle.IsAsset);
			else if (kind == SceneNodeKind.Crossboard)
				OnNewCrossboard?.Invoke(handle.gameObject, handle.IsAsset);
			else if (kind == SceneNodeKind.Lod)
				OnNewLod?.Invoke(handle.gameObject, handle.IsAsset);
			else if (kind == SceneNodeKind.Loader)
				OnNewLoader?.Invoke(handle.gameObject, handle.IsAsset);
		}

		private void PublishTerrain(SceneInstanceHandle handle)
		{
			if (handle.Resources == null || !Coordinates.IsConfigured)
				return;
			var mapRoot = handle;
			while (!mapRoot.ParentKey.Equals(SceneInstanceKey.Root))
				mapRoot = Parent(mapRoot.ParentKey);
			if (!mapRoot.HasFrameTransform)
				return;
			var local = Root.transform.worldToLocalMatrix * handle.transform.localToWorldMatrix;
			var center = local.MultiplyPoint3x4(handle.BoundaryCenter);
			var global = mapRoot.TransformCamera + center.ToVec3D();
			handle.Terrain = Coordinates.CreateTerrainData(
				handle.Resources.State?.Data ?? default, handle.BoundaryCenter, global, local);
			_pendingTerrain.Remove(handle);
			if (_notified.Add(handle))
				OnNewTerrain?.Invoke(handle.gameObject, handle.IsAsset);
		}

		private void NotifyRemoved(SceneInstanceHandle handle)
		{
			_pendingTerrain.Remove(handle);
			if (!_notified.Remove(handle))
				return;
			if (handle.Feature == PoolObjectFeature.Terrain)
				InvokeCleanupEvent(OnRemoveTerrain, handle.gameObject);
			else if (handle.Feature == PoolObjectFeature.StaticMesh)
				InvokeCleanupEvent(OnRemoveGeometry, handle.gameObject);
		}

		private static void InvokeCleanupEvent(SceneManager.EventHandler_OnGameObjectFree handlers, GameObject value)
		{
			if (handlers == null)
				return;
			var cleanup = new SceneCleanup();
			foreach (SceneManager.EventHandler_OnGameObjectFree handler in handlers.GetInvocationList())
				cleanup.Attempt(() => handler(value));
			cleanup.Complete("A scene cleanup observer failed.");
		}

		internal void SetTransform(SceneInstanceKey key, Matrix4x4 matrix, Vec3D? frameCamera = null)
		{
			RequireMainThread();
			if (!_instances.TryGetValue(key, out var handle))
				throw new InvalidOperationException($"Transform referenced missing occurrence {key}.");
			ApplyLocalTransform(handle.transform, matrix);
			if (frameCamera.HasValue && handle.ParentKey.Equals(SceneInstanceKey.Root))
			{
				handle.TransformCamera = frameCamera.Value;
				handle.HasFrameTransform = true;
			}
		}

		internal void SetActivation(SceneInstanceKey key, CSW.Activation activation, bool applyVisibility = true)
		{
			RequireMainThread();
			if (!_instances.TryGetValue(key, out var handle))
				throw new InvalidOperationException($"Activation referenced missing occurrence {key}.");
			handle.IsLocallyActive = activation == CSW.Activation.ON;
			if (applyVisibility)
				handle.gameObject.SetActive(handle.IsPresented && handle.IsReady && handle.IsLocallyActive);
			else
				_pendingVisibility.Add(handle);
		}

		internal void Delete(SceneInstanceKey key)
		{
			RequireMainThread();
			if (!_instances.TryGetValue(key, out var handle))
				return;
			if (key.Equals(SceneInstanceKey.Root))
				throw new InvalidOperationException("The native stream cannot delete the host-owned scene attachment.");
			var pending = new Stack<(SceneInstanceHandle Handle, bool Visited)>();
			var cleanup = new SceneCleanup();
			pending.Push((handle, false));
			while (pending.Count > 0)
			{
				var item = pending.Pop();
				if (!item.Visited)
				{
					pending.Push((item.Handle, true));
					foreach (var child in item.Handle.Children)
						pending.Push((_instances[child], false));
					continue;
				}
				cleanup.Attempt(() => Recycle(item.Handle));
			}
			cleanup.Complete("Could not recycle every occurrence in the hierarchy.");
		}

		private void Recycle(SceneInstanceHandle handle)
		{
			var cleanup = new SceneCleanup();
			cleanup.Attempt(() => NotifyRemoved(handle));
			handle.gameObject.SetActive(false);
			handle.IsReady = false;
			handle.Version++;
			cleanup.Attempt(() => DetachResources(handle));
			_pendingPresentation.Remove(handle);
			_pendingVisibility.Remove(handle);
			handle.IsPresented = false;
			handle.Builder = null;
			handle.Children.Clear();
			Parent(handle.ParentKey).Children.Remove(handle.Key);
			_instances.Remove(handle.Key);
			cleanup.Attempt(() => InvokeCleanupEvent(OnEnterPool, handle.gameObject));
			handle.transform.SetParent(_poolRoot.transform, false);
			if (_pool.Count < _maximumPooledObjects)
				_pool.Push(handle);
			else
				SceneObjects.Destroy(handle.gameObject);
			cleanup.Complete("Occurrence cleanup failed.");
		}

		private void DetachResources(SceneInstanceHandle handle)
		{
			RemoveResourceUsers(handle);
			if (handle.TryGetComponent<MeshFilter>(out var filter))
				filter.sharedMesh = null;
			if (handle.TryGetComponent<MeshRenderer>(out var renderer))
			{
				renderer.sharedMaterial = null;
				renderer.enabled = false;
			}
			try
			{
				handle.Resources?.Dispose();
			}
			finally
			{
				handle.Resources = null;
				handle.Terrain = default;
			}
		}

		internal void CommitFrame(CameraRelativeFrame frame)
		{
			using var sample = _commitMarker.Auto();
			RequireMainThread();
			ValidateAttachment(Root.transform.parent);
			CommittedFrame = frame;
			HasCommittedFrame = true;
			FrameCommitting?.Invoke(frame);
			foreach (var handle in new List<SceneInstanceHandle>(_pendingTerrain))
				PublishTerrain(handle);
			foreach (var handle in _pendingPresentation)
			{
				handle.IsPresented = true;
				_pendingVisibility.Add(handle);
			}
			_pendingPresentation.Clear();
			foreach (var handle in _pendingVisibility)
				handle.gameObject.SetActive(handle.IsPresented && handle.IsReady && handle.IsLocallyActive);
			_pendingVisibility.Clear();
			OnUpdateCamera?.Invoke(frame.RenderTime);
		}

		internal void BeginFrame()
		{
			OnPreTraverse?.Invoke(false);
		}

		/// <summary>Raises post-presentation notifications on the Unity main thread.</summary>
		public void PostPresent()
		{
			RequireMainThread();
			OnPostTraverse?.Invoke(false);
		}

		internal void MapChanged()
		{
			OnMapChanged?.Invoke();
		}

		internal void MapError(string message)
		{
			OnMapLoadError?.Invoke(message);
		}

		/// <summary>Releases current occurrences and resources, then adopts the supplied generation.</summary>
		/// <remarks>The scene attachment, borrowed camera and coordinate context are retained.</remarks>
		public void Clear(ulong generation)
		{
			RequireMainThread();
			var root = _instances[SceneInstanceKey.Root];
			var cleanup = new SceneCleanup();
			foreach (var child in new List<SceneInstanceKey>(root.Children))
				cleanup.Attempt(() => Delete(child));
			cleanup.Attempt(Resources.Dispose);
			_meshUsers.Clear();
			_stateUsers.Clear();
			_pendingTerrain.Clear();
			_notified.Clear();
			_pendingPresentation.Clear();
			_pendingVisibility.Clear();
			Generation = generation;
			HasCommittedFrame = false;
			CommittedFrame = default;
			root.Generation = generation;
			cleanup.Complete("Scene cleanup failed.");
		}

		/// <summary>Resets build and texture-cache diagnostics for the presentation.</summary>
		public void ResetStreamingDiagnostics()
		{
			SceneManager.ResetTotalBuildDiagnostics();
			Resources.ResetDiagnostics();
		}

		internal static void ApplyLocalTransform(Transform transform, Matrix4x4 matrix)
		{
			ValidateLocalTransform(matrix);
			var scale = new Vector3(
				((Vector3)matrix.GetColumn(0)).magnitude,
				((Vector3)matrix.GetColumn(1)).magnitude,
				((Vector3)matrix.GetColumn(2)).magnitude);
			if (matrix.determinant < 0)
				scale.x = -scale.x;
			var rotation = Quaternion.LookRotation(
				matrix.GetColumn(2) / scale.z,
				matrix.GetColumn(1) / scale.y);
			transform.localPosition = matrix.GetColumn(3);
			transform.localRotation = rotation;
			transform.localScale = scale;
		}

		private static void ValidateLocalTransform(Matrix4x4 matrix)
		{
			for (var index = 0; index < 16; index++)
				if (!float.IsFinite(matrix[index]))
					throw new InvalidOperationException("SceneManager produced a non-finite local transform.");
			if (!matrix.ValidTRS() ||
				((Vector3)matrix.GetColumn(0)).sqrMagnitude == 0 ||
				((Vector3)matrix.GetColumn(1)).sqrMagnitude == 0 ||
				((Vector3)matrix.GetColumn(2)).sqrMagnitude == 0)
				throw new InvalidOperationException("SceneManager produced a non-finite or non-TRS local transform.");
		}

		private static void ValidateAttachment(Transform parent)
		{
			var matrix = parent.localToWorldMatrix;
			for (var index = 0; index < 16; index++)
				if (!float.IsFinite(matrix[index]) ||
					Mathf.Abs(matrix[index] - Matrix4x4.identity[index]) > 0.00001f)
					throw new ArgumentException("Phase 1 requires a world-aligned identity attachment transform.");
		}

		/// <summary>Releases owned presentation resources without disposing the borrowed camera or coordinates.</summary>
		public void Dispose()
		{
			if (_disposed)
				return;
			RequireMainThread();
			var cleanup = new SceneCleanup();
			cleanup.Attempt(() => Clear(Generation + 1));
			_pool.Clear();
			cleanup.Attempt(() => SceneObjects.Destroy(Root));
			_disposed = true;
			cleanup.Complete("Scene disposal failed.");
		}
	}
}
