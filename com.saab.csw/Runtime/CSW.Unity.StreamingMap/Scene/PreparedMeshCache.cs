//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: PreparedMeshCache.cs
// Description	: Loader-thread mesh preparation handed to edit-locked capture
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261105	Prepared meshes on CSW loader threads before attach and handed them to capture.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using GizmoSDK.Gizmo3D;
using Stopwatch = System.Diagnostics.Stopwatch;
using ProfilerCategory = global::Unity.Profiling.ProfilerCategory;
using ProfilerMarker = global::Unity.Profiling.ProfilerMarker;

namespace CSW.Unity.StreamingMap
{
	/// <summary>Holds meshes copied on CSW loader threads until edit-locked capture realizes their geometry.</summary>
	/// <remarks>
	/// <see cref="Prepare"/> is the SceneManager PrepareReference delegate. It runs on parallel loader threads
	/// without the edit lock while the loaded subtree is private to its loader. Each entry keeps its Geometry
	/// wrapper so the native address cannot be reused while the entry is keyed by it.
	/// All other members run on the Unity main thread with edit access held.
	/// </remarks>
	internal sealed class PreparedMeshCache
	{
		private static readonly ProfilerMarker _prepareMarker =
			new ProfilerMarker(ProfilerCategory.Loading, "CSW-PrepareMesh");
		private static readonly long _expiryTicks = 20 * Stopwatch.Frequency;
		private static readonly long _sweepTicks = Stopwatch.Frequency;

		private readonly struct Entry
		{
			internal readonly Geometry Geometry;
			internal readonly PreparedMesh Mesh;
			internal readonly long Timestamp;

			internal Entry(Geometry geometry, PreparedMesh mesh, long timestamp)
			{
				Geometry = geometry;
				Mesh = mesh;
				Timestamp = timestamp;
			}
		}

		private readonly ConcurrentDictionary<IntPtr, Entry> _entries = new ConcurrentDictionary<IntPtr, Entry>();
		private readonly List<IntPtr> _expired = new List<IntPtr>();
		private volatile bool _accepting = true;
		private long _nextSweep;

		internal int Count => _entries.Count;

		/// <summary>Copies a private Geometry into Unity layout; other nodes are released.</summary>
		internal void Prepare(CSW.SceneManager manager, Node node)
		{
			if (node == null)
				return;
			if (!_accepting || !(node is Geometry geometry))
			{
				ReleaseUnattached(node);
				return;
			}

			PreparedMesh mesh = null;
			var retained = false;
			using (_prepareMarker.Auto())
			{
				try
				{
					var start = Stopwatch.GetTimestamp();
					mesh = PreparedMesh.Create(geometry);
					if (mesh == null)
					{
						BuildDiagnostics.RecordUnprepared();
						return;
					}
					var now = Stopwatch.GetTimestamp();
					var key = geometry.GetNativeReference();
					if (!_entries.TryAdd(key, new Entry(geometry, mesh, now)))
						return;
					// Clear may have drained the entries between the first check and the insertion.
					if (!_accepting && _entries.TryRemove(key, out _))
						return;
					retained = true;
					BuildDiagnostics.RecordPrepared(mesh.DataBytes, now - start);
				}
				finally
				{
					if (!retained)
					{
						mesh?.Dispose();
						ReleaseUnattached(geometry);
					}
				}
			}
		}

		// The subtree is not attached and owned by its loader, so its wrappers are released without edit access.
		private static void ReleaseUnattached(Node node)
		{
			node.ReleaseAlreadyLocked();
			GC.SuppressFinalize(node);
		}

		/// <summary>Removes and returns the caller-owned preparation of a geometry, or null.</summary>
		/// <remarks>Requires edit access. Dispose the result.</remarks>
		internal PreparedMesh TakeAlreadyLocked(IntPtr geometry)
		{
			if (geometry == IntPtr.Zero || !_entries.TryRemove(geometry, out var entry))
				return null;
			SceneCommandPump.ReleaseLocked(entry.Geometry);
			return entry.Mesh;
		}

		/// <summary>Drops preparations of loads that did not reach the scene within the expiry time.</summary>
		/// <remarks>Requires edit access. Scans at most once per second.</remarks>
		internal void ExpireAlreadyLocked()
		{
			var now = Stopwatch.GetTimestamp();
			if (now < _nextSweep || _entries.IsEmpty)
				return;
			_nextSweep = now + _sweepTicks;
			foreach (var pair in _entries)
				if (now - pair.Value.Timestamp > _expiryTicks)
					_expired.Add(pair.Key);
			var count = 0;
			try
			{
				foreach (var key in _expired)
				{
					if (!_entries.TryRemove(key, out var entry))
						continue;
					entry.Mesh.Dispose();
					SceneCommandPump.ReleaseLocked(entry.Geometry);
					count++;
				}
			}
			finally
			{
				_expired.Clear();
				BuildDiagnostics.RecordPreparedExpired(count);
			}
		}

		/// <summary>Stops accepting preparations and releases every entry.</summary>
		/// <remarks>Requires edit access. Later loader callbacks release their own preparation.</remarks>
		internal void ClearAlreadyLocked()
		{
			_accepting = false;
			var cleanup = new SceneCleanup();
			foreach (var key in _entries.Keys)
			{
				if (!_entries.TryRemove(key, out var entry))
					continue;
				cleanup.Attempt(entry.Mesh.Dispose);
				cleanup.Attempt(() => SceneCommandPump.ReleaseLocked(entry.Geometry));
			}
			cleanup.Complete("Could not release every loader-prepared mesh.");
		}
	}
}
