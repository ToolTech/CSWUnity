//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: SceneInstanceHandle.cs
// Description	: Command-driven Unity occurrence and copied presentation data
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Added occurrence identity and copied presentation data with borrowed resource views.	(2.12.327)
// AMO	261028	Added scene presentation event delegates replacing the removed legacy SceneManager delegates.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Collections.Generic;
using GizmoSDK.GizmoBase;
using GizmoSDK.Gizmo3D;
using UnityEngine;
using Camera = UnityEngine.Camera;

namespace Saab.Foundation.Unity.MapStreamer.Csw
{
	/// <summary>Identifies an occurrence by canonical native node handle and full unsigned 64-bit path.</summary>
	/// <remarks>The handle is identity only; this value does not retain a native reference.</remarks>
	public readonly struct SceneInstanceKey : IEquatable<SceneInstanceKey>
	{
		public static readonly SceneInstanceKey Root = default;

		public IntPtr Node { get; }
		public ulong PathId { get; }

		public SceneInstanceKey(IntPtr node, ulong pathId)
		{
			Node = node;
			PathId = pathId;
		}

		public bool Equals(SceneInstanceKey other)
		{
			return Node == other.Node && PathId == other.PathId;
		}

		public override bool Equals(object obj)
		{
			return obj is SceneInstanceKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (Node.GetHashCode() * 397) ^ PathId.GetHashCode();
			}
		}

		public override string ToString()
		{
			return $"{Node.ToInt64():X}:{PathId:X16}";
		}
	}

	/// <summary>Copied terrain metadata with textures borrowed from the current presentation resources.</summary>
	/// <remarks>Do not destroy the textures or assume they survive occurrence removal or resource replacement.</remarks>
	public readonly struct TerrainPresentationData
	{
		public Texture2D Texture { get; }
		public Texture2D Feature { get; }
		public Texture2D SurfaceHeight { get; }
		public Matrix3D FeatureInfo { get; }
		public Vector3 BoundaryCenter { get; }
		public Matrix4x4 LocalEunBasis { get; }
		public Vector3 FeatureCenterOffset { get; }

		internal TerrainPresentationData(
			StateBuildOutput state,
			Vector3 boundaryCenter,
			Matrix4x4 localEunBasis,
			Vector3 featureCenterOffset)
		{
			Texture = state.Texture;
			Feature = state.Feature;
			SurfaceHeight = state.SurfaceHeight;
			FeatureInfo = state.Feature_homography;
			BoundaryCenter = boundaryCenter;
			LocalEunBasis = localEunBasis;
			FeatureCenterOffset = featureCenterOffset;
		}
	}

	/// <summary>Presentation-owned Unity component for one current node and path occurrence.</summary>
	/// <remarks>Pooled components are reused; revalidate their key, generation and version before use.</remarks>
	public sealed class SceneInstanceHandle : MonoBehaviour
	{
		public SceneInstanceKey Key { get; internal set; }
		public SceneInstanceKey ParentKey { get; internal set; }
		public ulong Generation { get; internal set; }
		public uint Version { get; internal set; }
		public PoolObjectFeature Feature { get; internal set; }
		public bool IsAsset { get; internal set; }
		public bool IsStatic { get; internal set; }
		public bool IsReady { get; internal set; }
		public bool IsLocallyActive { get; internal set; }
		public IntersectMaskValue IntersectMask { get; internal set; }
		public TerrainPresentationData Terrain { get; internal set; }

		internal readonly HashSet<SceneInstanceKey> Children =
			new HashSet<SceneInstanceKey>();
		internal SceneResourceLease Resources;
		internal GeometryBuilderBase Builder;
		internal Vector3 BoundaryCenter;
		internal bool HasFrameTransform;
		internal Vec3D TransformCamera;
		internal ulong Lifetime;
		internal uint ParentVersion;
		internal bool IsPresented;
	}

	/// <summary>Notifies that a presented Unity object was created or activated for a scene occurrence.</summary>
	/// <remarks>Raised on the Unity main thread. The object remains presentation-owned.</remarks>
	public delegate void SceneGameObjectHandler(GameObject gameObject, bool isAsset);

	/// <summary>Notifies that a presented Unity object is being released or returned to its pool.</summary>
	/// <remarks>Raised on the Unity main thread. Drop references to the object before returning.</remarks>
	public delegate void SceneGameObjectReleaseHandler(GameObject gameObject);

	/// <summary>Notifies before or after scene presentation updates, with the native scene lock state.</summary>
	public delegate void SceneTraverseHandler(bool locked);

	/// <summary>Notifies that the presentation camera is updated for the given render time.</summary>
	public delegate void SceneUpdateCameraHandler(double renderTime);

	/// <summary>Borrowed main-thread view of scene notifications and terrain presentation snapshots.</summary>
	/// <remarks>Unity objects and textures exposed by this view remain presentation-owned.</remarks>
	public interface IScenePresentation
	{
		Camera Camera { get; }
		/// <summary>Copies metadata for a current terrain occurrence without transferring texture ownership.</summary>
		bool TryGetTerrain(GameObject gameObject, out TerrainPresentationData terrain);

		event SceneGameObjectHandler OnNewTerrain;
		event SceneGameObjectHandler OnNewGeometry;
		event SceneGameObjectHandler OnNewCrossboard;
		event SceneGameObjectHandler OnNewLod;
		event SceneGameObjectHandler OnNewLoader;
		event SceneGameObjectReleaseHandler OnEnterPool;
		event SceneGameObjectReleaseHandler OnRemoveGeometry;
		event SceneGameObjectReleaseHandler OnRemoveTerrain;
		event SceneTraverseHandler OnPreTraverse;
		event SceneTraverseHandler OnPostTraverse;
		event Action OnMapChanged;
		event Action<string> OnMapLoadError;
		event SceneUpdateCameraHandler OnUpdateCamera;
	}

	internal enum SceneNodeKind
	{
		Group,
		Geometry,
		Crossboard,
		Lod,
		Loader
	}

	internal sealed class SceneNodeData : IDisposable
	{
		internal SceneInstanceKey Key;
		internal SceneInstanceKey Parent;
		internal string Name;
		internal SceneNodeKind Kind;
		internal Matrix4x4 LocalTransform = Matrix4x4.identity;
		internal IntersectMaskValue IntersectMask;
		internal bool IsStatic;
		internal Vector3 BoundaryCenter;
		internal GeometryBuilderBase Builder;
		internal ScenePreparedResources Prepared;

		public void Dispose()
		{
			Prepared?.Dispose();
			Prepared = null;
		}
	}
}
