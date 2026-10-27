//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: SceneResources.cs
// Description	: Shared render resources independent of occurrence identity
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261022	Added revisioned render snapshots and shared leases that retain replaced resources until final release.	(2.12.327)
// AMO	261023	Removed the full-scene state-affinity lookup in favor of indexed source users.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Collections.Generic;
using GizmoSDK.Gizmo3D;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Csw
{
	/// <summary>A mesh generation shared by active scene-resource leases.</summary>
	internal sealed class SceneMeshResource
	{
		internal IntPtr Source;
		internal Mesh Mesh;
		internal Color Color;
		internal int References;
	}

	/// <summary>A copied State snapshot holding texture-cache references shared by active leases.</summary>
	internal sealed class SceneStateResource
	{
		internal IntPtr Source;
		internal ulong Revision;
		internal StateBuildOutput Data;
		internal IntPtr[] TextureSources;
		internal int References;
	}

	/// <summary>Tracks current State snapshots sharing one native texture identity and revision.</summary>
	internal sealed class SceneTextureSource
	{
		internal ulong Revision;
		internal readonly HashSet<SceneStateResource> States = new HashSet<SceneStateResource>();
	}

	/// <summary>A cached Unity material shared by balanced scene-resource leases.</summary>
	internal sealed class SceneMaterialResource
	{
		internal (IntPtr State, ulong Revision, int Builder, Color Color) Key;
		internal int Id;
		internal Material Material;
		internal int References;
	}

	/// <summary>Owns the references keeping one mesh, State and material generation alive.</summary>
	/// <remarks>
	/// Use and dispose on the Unity main thread, before disposing the resource owner.
	/// Exposed Unity objects are borrowed views; do not destroy them directly.
	/// </remarks>
	internal sealed class SceneResourceLease : IDisposable
	{
		private SceneResources _owner;
		internal SceneMeshResource Mesh;
		internal SceneStateResource State;
		internal SceneMaterialResource Material;

		internal SceneResourceLease(SceneResources owner)
		{
			_owner = owner;
		}

		/// <summary>Releases the mesh, material and texture references held by this lease.</summary>
		public void Dispose()
		{
			if (_owner == null)
				return;
			_owner.Release(this);
			_owner = null;
		}
	}

	/// <summary>Owns unconsumed render payloads and publication cleanup obligations.</summary>
	/// <remarks>
	/// Capture and Publish return caller-owned instances. Dispose on the Unity main thread
	/// after publication or rebinding to release untransferred and retired resources.
	/// </remarks>
	internal sealed class ScenePreparedResources : IDisposable
	{
		private readonly SceneResources _owner;
		internal IntPtr MeshSource;
		internal IntPtr StateSource;
		internal Mesh Mesh;
		internal Color Color;
		internal StateBuildOutput State;
		internal ulong StateRevision;
		internal bool HasMesh;
		internal bool HasState;
		internal readonly IntPtr[] TextureSources = new IntPtr[3];
		internal readonly Dictionary<IntPtr, ulong> TextureRevisions = new Dictionary<IntPtr, ulong>();
		internal readonly Dictionary<IntPtr, StateBuildOutput> DependentStates =
			new Dictionary<IntPtr, StateBuildOutput>();
		internal readonly List<SceneStateResource> PublishedStates = new List<SceneStateResource>();

		internal ScenePreparedResources(SceneResources owner)
		{
			_owner = owner;
		}

		/// <summary>Releases remaining owned payloads and completes publication cleanup.</summary>
		public void Dispose()
		{
			var cleanup = new SceneCleanup();
			cleanup.Attempt(() => _owner.ReleaseTextures(State));
			foreach (var state in DependentStates.Values)
				cleanup.Attempt(() => _owner.ReleaseTextures(state));
			DependentStates.Clear();
			foreach (var state in PublishedStates)
				cleanup.Attempt(() => _owner.ReleaseUnreferencedState(state));
			PublishedStates.Clear();
			SceneObjects.Destroy(Mesh);
			Mesh = null;
			State = default;
			cleanup.Complete("Prepared scene resources could not be released completely.");
		}
	}

	/// <summary>Owns render-resource generations independently of scene occurrence identity.</summary>
	/// <remarks>
	/// Use on the Unity main thread. Native handle keys are identities, not retained references.
	/// Dispose all prepared snapshots and leases before disposing this resource owner.
	/// </remarks>
	internal sealed class SceneResources : IDisposable
	{
		private readonly Dictionary<IntPtr, SceneMeshResource> _meshes =
			new Dictionary<IntPtr, SceneMeshResource>();
		private readonly HashSet<SceneMeshResource> _liveMeshes = new HashSet<SceneMeshResource>();
		private readonly Dictionary<IntPtr, SceneStateResource> _states =
			new Dictionary<IntPtr, SceneStateResource>();
		private readonly Dictionary<IntPtr, SceneTextureSource> _textureSources =
			new Dictionary<IntPtr, SceneTextureSource>();
		private readonly Dictionary<(IntPtr, ulong, int, Color), SceneMaterialResource> _materials =
			new Dictionary<(IntPtr, ulong, int, Color), SceneMaterialResource>();
		private readonly TextureManager _textures = new TextureManager();
		private readonly MaterialManager _materialManager = new MaterialManager();
		private ulong _nextRevision;
		private ulong _nextTextureRevision;
		private int _nextMaterialId;

		internal int MeshCount => _liveMeshes.Count;
		internal int MaterialCount => _materials.Count;
		internal TextureManagerDiagnostics TextureDiagnostics => _textures.Diagnostics;

		/// <summary>Copies borrowed graph inputs into caller-owned prepared render resources.</summary>
		/// <remarks>
		/// The caller holds graph edit access and retains the Geometry and State wrappers.
		/// Dispose the returned snapshot after use, whether or not it is published.
		/// </remarks>
		internal ScenePreparedResources Capture(Geometry geometry, State state, bool update)
		{
			var prepared = new ScenePreparedResources(this)
			{
				MeshSource = geometry.GetNativeReference(),
				StateSource = state?.GetNativeReference() ?? IntPtr.Zero
			};
			try
			{
				if (update || !_meshes.ContainsKey(prepared.MeshSource))
				{
					if (!GeometryHelper.Build(geometry, out prepared.Mesh, out prepared.Color))
						throw new InvalidOperationException($"Geometry export failed for '{geometry.Name}'.");
					prepared.HasMesh = true;
				}

				if (state != null && (update || !_states.ContainsKey(prepared.StateSource)))
				{
					prepared.StateRevision = checked(++_nextRevision);
					if (state.HasTexture(0) && state.GetMode(StateMode.TEXTURE) == StateModeActivation.ON &&
						!StateHelper.Build(state, out prepared.State, _textures,
							(texture, unit, mipMaps) => CaptureTexture(prepared, texture, unit, update)))
						throw new InvalidOperationException($"Effective State export failed for '{geometry.Name}'.");
					prepared.HasState = true;
				}
				return prepared;
			}
			catch
			{
				prepared.Dispose();
				throw;
			}
		}

		private ulong CaptureTexture(
			ScenePreparedResources prepared, GizmoSDK.Gizmo3D.Texture texture, uint unit, bool update)
		{
			var source = texture.GetNativeReference();
			prepared.TextureSources[unit] = source;
			if (prepared.TextureRevisions.TryGetValue(source, out var revision))
				return revision;
			_textureSources.TryGetValue(source, out var published);
			revision = !update && published != null ? published.Revision : checked(++_nextTextureRevision);
			prepared.TextureRevisions.Add(source, revision);
			if (!update || published == null)
				return revision;

			foreach (var state in published.States)
			{
				if (state.Source == prepared.StateSource)
					continue;
				if (!prepared.DependentStates.TryGetValue(state.Source, out var data))
				{
					data = RetainTextures(state.Data);
					prepared.DependentStates.Add(state.Source, data);
				}
				for (uint dependentUnit = 0; dependentUnit < 3; dependentUnit++)
				{
					if (state.TextureSources[dependentUnit] != source)
						continue;
					if (!StateHelper.ReadTexture(texture, out var replacement, _textures, out var info,
						dependentUnit, dependentUnit == 0, revision, dependentUnit != 0) && dependentUnit == 0)
						throw new InvalidOperationException("A shared source texture could not be refreshed.");
					if (dependentUnit != 0 && (info == null || !info.HasHomography))
					{
						if (replacement)
							FreeTexture(replacement);
						replacement = null;
					}
					var previous = TextureAt(data, dependentUnit);
					if (previous)
						FreeTexture(previous);
					SetTexture(ref data, dependentUnit, replacement, info);
					prepared.DependentStates[state.Source] = data;
				}
			}
			return revision;
		}

		private StateBuildOutput RetainTextures(StateBuildOutput source)
		{
			var result = default(StateBuildOutput);
			try
			{
				for (uint unit = 0; unit < 3; unit++)
				{
					var texture = TextureAt(source, unit);
					if (texture && !_textures.Retain(texture))
						throw new InvalidOperationException("A shared State lost its texture lease.");
					SetTexture(ref result, unit, texture, null);
				}
				result.Feature_homography = source.Feature_homography;
				result.SurfaceHeight_homography = source.SurfaceHeight_homography;
				return result;
			}
			catch
			{
				ReleaseTextures(result);
				throw;
			}
		}

		private static Texture2D TextureAt(StateBuildOutput data, uint unit)
		{
			return unit == 0 ? data.Texture : unit == 1 ? data.Feature : data.SurfaceHeight;
		}

		private static void SetTexture(
			ref StateBuildOutput data, uint unit, Texture2D texture, TextureImageInfo info)
		{
			switch (unit)
			{
				case 0:
					data.Texture = texture;
					break;
				case 1:
					data.Feature = texture;
					data.Feature_homography = info?.Homography ?? default;
					break;
				case 2:
					data.SurfaceHeight = texture;
					data.SurfaceHeight_homography = info?.Homography ?? default;
					break;
			}
		}

		/// <summary>Transfers captured payloads into the cache and returns owned retirement cleanup.</summary>
		/// <remarks>Keep the result alive through replacement-lease acquisition, then dispose it.</remarks>
		internal ScenePreparedResources Publish(ScenePreparedResources prepared)
		{
			var retired = new ScenePreparedResources(this);
			if (prepared.HasMesh)
			{
				if (_meshes.TryGetValue(prepared.MeshSource, out var previous) && previous.References == 0)
				{
					retired.Mesh = previous.Mesh;
					_liveMeshes.Remove(previous);
				}
				var mesh = new SceneMeshResource
				{
					Source = prepared.MeshSource,
					Mesh = prepared.Mesh,
					Color = prepared.Color
				};
				_meshes[mesh.Source] = mesh;
				_liveMeshes.Add(mesh);
				prepared.Mesh = null;
			}
			if (prepared.HasState)
			{
				PublishState(prepared.StateSource, prepared.State, prepared.StateRevision,
					prepared.TextureSources, retired);
				foreach (var dependent in prepared.DependentStates)
				{
					var sources = _states[dependent.Key].TextureSources;
					PublishState(dependent.Key, dependent.Value, checked(++_nextRevision), sources, retired);
				}
				foreach (var revision in prepared.TextureRevisions)
					_textureSources[revision.Key].Revision = revision.Value;
				prepared.DependentStates.Clear();
				prepared.State = default;
			}
			return retired;
		}

		private void PublishState(IntPtr source, StateBuildOutput data, ulong revision,
			IntPtr[] textureSources, ScenePreparedResources retired)
		{
			if (_states.TryGetValue(source, out var previous))
			{
				if (previous.References == 0)
					retired.DependentStates.Add(source, previous.Data);
			}
			var state = new SceneStateResource
			{
				Source = source,
				Data = data,
				Revision = revision,
				TextureSources = textureSources
			};
			_states[source] = state;
			retired.PublishedStates.Add(state);
			foreach (var textureSource in textureSources)
			{
				if (textureSource == IntPtr.Zero)
					continue;
				if (!_textureSources.TryGetValue(textureSource, out var texture))
				{
					texture = new SceneTextureSource();
					_textureSources.Add(textureSource, texture);
				}
				texture.States.Add(state);
			}
			if (previous != null)
				UnregisterTextureSources(previous);
		}

		internal void ReleaseUnreferencedState(SceneStateResource state)
		{
			if (state.References != 0 ||
				!_states.TryGetValue(state.Source, out var current) || current != state)
				return;
			_states.Remove(state.Source);
			UnregisterTextureSources(state);
			ReleaseTextures(state.Data);
		}

		/// <summary>Returns an owned lease on the current mesh, State and material snapshots.</summary>
		/// <remarks>The builder is borrowed; dispose the returned lease before its resource owner.</remarks>
		internal SceneResourceLease Acquire(IntPtr geometry, IntPtr state, GeometryBuilderBase builder)
		{
			var meshResource = _meshes[geometry];
			var stateResource = state == IntPtr.Zero ? null : _states[state];
			var key = (state, stateResource?.Revision ?? 0, builder.GetInstanceID(), meshResource.Color);
			if (!_materials.TryGetValue(key, out var materialResource))
			{
				var material = builder.CreateSceneMaterial(stateResource?.Data ?? default, meshResource.Color);
				materialResource = new SceneMaterialResource
				{
					Key = key,
					Id = checked(++_nextMaterialId),
					Material = material
				};
				if (!_materialManager.TryAdd(materialResource.Id, material))
				{
					SceneObjects.Destroy(material);
					throw new InvalidOperationException("Failed to register a scene material lease.");
				}
				_materials.Add(key, materialResource);
			}
			else if (!_materialManager.TryGet(materialResource.Id, out _))
			{
				throw new InvalidOperationException("A scene material lease lost its cached material.");
			}

			materialResource.References++;
			meshResource.References++;
			if (stateResource != null)
				stateResource.References++;
			return new SceneResourceLease(this)
			{
				Mesh = meshResource,
				State = stateResource,
				Material = materialResource
			};
		}

		/// <summary>Balances one lease's resource references; consumers normally use the lease's Dispose.</summary>
		internal void Release(SceneResourceLease lease)
		{
			var material = lease.Material;
			if (!_materialManager.Free(material.Material))
				throw new InvalidOperationException("A scene material lease was released twice.");
			if (--material.References == 0)
				_materials.Remove(material.Key);

			var mesh = lease.Mesh;
			if (--mesh.References == 0)
			{
				if (_meshes.TryGetValue(mesh.Source, out var current) && current == mesh)
					_meshes.Remove(mesh.Source);
				_liveMeshes.Remove(mesh);
				SceneObjects.Destroy(mesh.Mesh);
			}
			var state = lease.State;
			if (state != null && --state.References == 0)
			{
				if (_states.TryGetValue(state.Source, out var current) && current == state)
				{
					_states.Remove(state.Source);
					UnregisterTextureSources(state);
				}
				ReleaseTextures(state.Data);
			}
		}

		private void UnregisterTextureSources(SceneStateResource state)
		{
			if (state.TextureSources == null)
				return;
			foreach (var source in state.TextureSources)
			{
				if (!_textureSources.TryGetValue(source, out var texture))
					continue;
				texture.States.Remove(state);
				if (texture.States.Count == 0)
					_textureSources.Remove(source);
			}
		}

		/// <summary>Balances texture-cache references owned by one copied State payload.</summary>
		internal void ReleaseTextures(StateBuildOutput state)
		{
			var cleanup = new SceneCleanup();
			if (state.Texture)
				cleanup.Attempt(() => FreeTexture(state.Texture));
			if (state.Feature)
				cleanup.Attempt(() => FreeTexture(state.Feature));
			if (state.SurfaceHeight)
				cleanup.Attempt(() => FreeTexture(state.SurfaceHeight));
			cleanup.Complete("Scene texture leases could not be released completely.");
		}

		private void FreeTexture(Texture2D texture)
		{
			if (!_textures.Free(texture))
				throw new InvalidOperationException("A scene texture lease was released twice.");
		}

		internal void ResetDiagnostics()
		{
			_textures.ResetDiagnostics();
		}

		/// <summary>Releases cached resources after all leases and prepared snapshots have been disposed.</summary>
		public void Dispose()
		{
			foreach (var mesh in _liveMeshes)
				SceneObjects.Destroy(mesh.Mesh);
			_meshes.Clear();
			_liveMeshes.Clear();
			_states.Clear();
			_textureSources.Clear();
			_materials.Clear();
			_materialManager.Clear();
			_textures.Clear();
		}
	}

	internal static class SceneObjects
	{
		/// <summary>Destroys an owned Unity object using the current play-mode destruction policy.</summary>
		internal static void Destroy(UnityEngine.Object value)
		{
			if (!value)
				return;
			if (Application.isPlaying)
				UnityEngine.Object.Destroy(value);
			else
				UnityEngine.Object.DestroyImmediate(value);
		}
	}
}
