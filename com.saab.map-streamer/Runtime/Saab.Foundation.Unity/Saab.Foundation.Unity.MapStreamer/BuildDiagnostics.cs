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
//
//*****************************************************************************

using System;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Saab.Foundation.Unity.MapStreamer
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

	/// <summary>Records mesh and texture builds for the current Unity frame and since the last reset.</summary>
	/// <remarks>Record and read on the Unity main thread.</remarks>
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

		internal static long ResetVersion { get; private set; }

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
			ResetVersion++;
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
