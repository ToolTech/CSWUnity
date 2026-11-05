//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: BuildDiagnostics.cs
// Description	: Per-frame and accumulated mesh and texture build diagnostics
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261028	Moved build diagnostics out of the removed legacy SceneManager component.	(2.12.327)
// AMO	261104	Moved to namespace CSW.Unity.StreamingMap.	(2.12.327)
// AMO	261105	Counted loader-thread mesh preparation and its main-thread use.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Threading;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace CSW.Unity.StreamingMap
{
	public readonly struct MeshBuildDiagnostics
	{
		public readonly long TotalMeshes;
		public readonly long TotalBytes;
		public readonly TimeSpan TotalTime;
		public readonly TimeSpan WorstTime;

		internal MeshBuildDiagnostics(long totalMeshes, long totalBytes, TimeSpan totalTime, TimeSpan worstTime)
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

		internal TextureBuildDiagnostics(long totalTextures, long totalBytes, TimeSpan totalTime, TimeSpan worstTime)
		{
			TotalTextures = totalTextures;
			TotalBytes = totalBytes;
			TotalTime = totalTime;
			WorstTime = worstTime;
		}
	}

	/// <summary>Loader-thread mesh preparations and their consumption since the last reset.</summary>
	public readonly struct PreparedMeshDiagnostics
	{
		/// <summary>Meshes copied on loader threads.</summary>
		public readonly long Prepared;
		public readonly long PreparedBytes;
		/// <summary>Summed loader-thread time; loaders run in parallel.</summary>
		public readonly TimeSpan PrepareTime;
		/// <summary>Geometries the loader threads could not prepare.</summary>
		public readonly long Unprepared;
		/// <summary>Preparations copied into Unity meshes.</summary>
		public readonly long Used;
		/// <summary>Preparations dropped because the mesh existed, was updated or had another layout.</summary>
		public readonly long Discarded;
		/// <summary>Preparations of loads that never reached the scene.</summary>
		public readonly long Expired;

		internal PreparedMeshDiagnostics(long prepared, long preparedBytes, TimeSpan prepareTime, long unprepared,
			long used, long discarded, long expired)
		{
			Prepared = prepared;
			PreparedBytes = preparedBytes;
			PrepareTime = prepareTime;
			Unprepared = unprepared;
			Used = used;
			Discarded = discarded;
			Expired = expired;
		}
	}

	/// <summary>Records mesh and texture builds for the current Unity frame and since the last reset.</summary>
	/// <remarks>Record and read on the Unity main thread, except the thread-safe loader preparation counters.</remarks>
	internal static class BuildDiagnostics
	{
		private struct Counter
		{
			internal long Count;
			internal long Bytes;
			internal long Ticks;
			internal long WorstTicks;

			internal void Add(long bytes, long ticks)
			{
				Count++;
				Bytes += bytes;
				Ticks += ticks;
				WorstTicks = Math.Max(WorstTicks, ticks);
			}
		}

		private static Counter _frameMeshes;
		private static Counter _frameTextures;
		private static Counter _totalMeshes;
		private static Counter _totalTextures;
		private static int _frame = -1;
		private static long _prepared;
		private static long _preparedBytes;
		private static long _prepareTicks;
		private static long _unprepared;
		private static long _preparedUsed;
		private static long _preparedDiscarded;
		private static long _preparedExpired;

		internal static long ResetVersion { get; private set; }

		internal static PreparedMeshDiagnostics PreparedMeshTotal => new PreparedMeshDiagnostics(
			Interlocked.Read(ref _prepared), Interlocked.Read(ref _preparedBytes),
			ToTime(Interlocked.Read(ref _prepareTicks)), Interlocked.Read(ref _unprepared),
			_preparedUsed, _preparedDiscarded, _preparedExpired);

		internal static MeshBuildDiagnostics MeshFrame
		{
			get
			{
				RollFrame();
				return CreateMesh(_frameMeshes);
			}
		}

		internal static TextureBuildDiagnostics TextureFrame
		{
			get
			{
				RollFrame();
				return CreateTexture(_frameTextures);
			}
		}

		internal static MeshBuildDiagnostics MeshTotal => CreateMesh(_totalMeshes);
		internal static TextureBuildDiagnostics TextureTotal => CreateTexture(_totalTextures);

		internal static void ResetTotals()
		{
			_totalMeshes = default;
			_totalTextures = default;
			Interlocked.Exchange(ref _prepared, 0);
			Interlocked.Exchange(ref _preparedBytes, 0);
			Interlocked.Exchange(ref _prepareTicks, 0);
			Interlocked.Exchange(ref _unprepared, 0);
			_preparedUsed = 0;
			_preparedDiscarded = 0;
			_preparedExpired = 0;
			ResetVersion++;
		}

		/// <summary>Records one loader-thread preparation; thread-safe.</summary>
		internal static void RecordPrepared(long bytes, long elapsedTicks)
		{
			Interlocked.Increment(ref _prepared);
			Interlocked.Add(ref _preparedBytes, bytes);
			Interlocked.Add(ref _prepareTicks, elapsedTicks);
		}

		/// <summary>Records one geometry a loader thread could not prepare; thread-safe.</summary>
		internal static void RecordUnprepared()
		{
			Interlocked.Increment(ref _unprepared);
		}

		internal static void RecordPreparedUse(bool used)
		{
			if (used)
				_preparedUsed++;
			else
				_preparedDiscarded++;
		}

		internal static void RecordPreparedExpired(int count)
		{
			_preparedExpired += count;
		}

		internal static void RecordMesh(long bytes, long elapsedTicks)
		{
			RollFrame();
			_frameMeshes.Add(bytes, elapsedTicks);
			_totalMeshes.Add(bytes, elapsedTicks);
		}

		internal static void RecordTexture(long bytes, long elapsedTicks)
		{
			RollFrame();
			_frameTextures.Add(bytes, elapsedTicks);
			_totalTextures.Add(bytes, elapsedTicks);
		}

		private static MeshBuildDiagnostics CreateMesh(Counter counter)
		{
			return new MeshBuildDiagnostics(counter.Count, counter.Bytes, ToTime(counter.Ticks), ToTime(counter.WorstTicks));
		}

		private static TextureBuildDiagnostics CreateTexture(Counter counter)
		{
			return new TextureBuildDiagnostics(counter.Count, counter.Bytes, ToTime(counter.Ticks), ToTime(counter.WorstTicks));
		}

		private static TimeSpan ToTime(long ticks)
		{
			return TimeSpan.FromSeconds((double)ticks / Stopwatch.Frequency);
		}

		private static void RollFrame()
		{
			var frame = UnityEngine.Time.frameCount;
			if (_frame == frame)
				return;
			_frame = frame;
			_frameMeshes = default;
			_frameTextures = default;
		}
	}
}
