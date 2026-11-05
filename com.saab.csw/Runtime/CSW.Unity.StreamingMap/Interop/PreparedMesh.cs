//*****************************************************************************
//
// Copyright (C) SAAB AB
//
// Information Class:	COMPANY UNCLASSIFIED
// Defence Secrecy:		NOT CLASSIFIED
// Export Control:		NOT EXPORT CONTROLLED
//
// File			: PreparedMesh.cs
// Description	: Triangle mesh data copied off the main thread in Unity vertex layout
// Product		: Gizmo3D 2.12.326
//
// Revision History...
//
// Who	Date	Description
//
// AMO	261105	Copied geometry into Unity interleaved vertex and index data on loader threads.	(2.12.327)
//
//*****************************************************************************

using System;
using System.Runtime.InteropServices;
using System.Threading;
using GizmoSDK.Gizmo3D;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace CSW.Unity.StreamingMap
{
	/// <summary>Triangle mesh data copied from a Geometry into Unity's interleaved float vertex layout.</summary>
	/// <remarks>
	/// Create on any thread while the Geometry is private to the caller or locked. Copy into Unity mesh data
	/// on the main thread. The instance owns unmanaged memory; dispose it after use.
	/// The vertex stream holds position (0), normal (12), optional color (24) and optional texture unit 0.
	/// </remarks>
	public sealed class PreparedMesh : IDisposable
	{
		private const int PositionOffset = 0;
		private const int NormalOffset = 12;
		private const int ColorOffset = 24;

		private IntPtr _vertices;
		private IntPtr _indices;

		public int VertexCount { get; }
		public int IndexCount { get; }
		public bool HasColors { get; }
		public bool HasTexCoords { get; }
		public IndexFormat IndexFormat { get; }
		public int Stride { get; }
		public Bounds Bounds { get; private set; }
		public Color UniformColor { get; }
		public long VertexBytes { get; }
		public long IndexBytes { get; }
		public long DataBytes => VertexBytes + IndexBytes;
		private int TexCoordOffset => HasColors ? 40 : 24;

		private PreparedMesh(in GeometryMeshExport.Info info, bool hasTexCoords)
		{
			VertexCount = (int)info.VertexCount;
			IndexCount = (int)info.IndexCount;
			HasColors = (info.AvailableAttributes & GeometryMeshExport.Attributes.Color) != 0;
			HasTexCoords = hasTexCoords;
			IndexFormat = info.VertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
			Stride = 24 + (HasColors ? 16 : 0) + (HasTexCoords ? 8 : 0);
			UniformColor = info.UsesOverallColor
				? new Color(info.OverallColor.x, info.OverallColor.y, info.OverallColor.z, info.OverallColor.w)
				: Color.white;
			VertexBytes = (long)VertexCount * Stride;
			IndexBytes = (long)IndexCount * (IndexFormat == IndexFormat.UInt16 ? sizeof(ushort) : sizeof(uint));
		}

		~PreparedMesh()
		{
			Free();
		}

		/// <summary>Exports a triangle copy of the geometry, or returns null when it has no supported triangle data.</summary>
		/// <remarks>The geometry must stay unchanged during the call. No Unity object is touched.</remarks>
		public static unsafe PreparedMesh Create(Geometry geometry)
		{
			if (geometry == null)
				return null;
			if (geometry.PrepareMeshExport(out var preparation, Geometry.PrimType.TRIS) !=
				GeometryMeshExport.Status.Success)
				return null;

			using (preparation)
			{
				var info = preparation.Info;
				if (info.Topology != Geometry.PrimType.TRIS ||
					info.VertexCount < 3 || info.IndexCount < 3 ||
					info.VertexCount > int.MaxValue || info.IndexCount > int.MaxValue)
					return null;

				var mesh = new PreparedMesh(in info, preparation.HasTextureCoordinateUnit(0));
				if (mesh.VertexBytes > uint.MaxValue || mesh.IndexBytes > uint.MaxValue)
					return null;
				try
				{
					mesh._vertices = Marshal.AllocHGlobal((IntPtr)mesh.VertexBytes);
					mesh._indices = Marshal.AllocHGlobal((IntPtr)mesh.IndexBytes);

					var vertexBytes = (uint)mesh.VertexBytes;
					var stride = (uint)mesh.Stride;
					var destination = GeometryMeshExport.Destination.Create(in info);
					destination.Positions = new GeometryMeshExport.AttributeDestination(
						mesh._vertices, vertexBytes, stride, PositionOffset);
					destination.Normals = new GeometryMeshExport.AttributeDestination(
						mesh._vertices, vertexBytes, stride, NormalOffset);
					if (mesh.HasColors)
						destination.Colors = new GeometryMeshExport.AttributeDestination(
							mesh._vertices, vertexBytes, stride, ColorOffset);
					if (mesh.HasTexCoords)
						destination.SetTexCoord(0, new GeometryMeshExport.AttributeDestination(
							mesh._vertices, vertexBytes, stride, (uint)mesh.TexCoordOffset));
					destination.Indices = new GeometryMeshExport.IndexDestination(
						mesh._indices, (uint)mesh.IndexBytes,
						mesh.IndexFormat == IndexFormat.UInt16
							? GeometryMeshExport.IndexFormat.UInt16
							: GeometryMeshExport.IndexFormat.UInt32);

					if (preparation.Copy(destination) != GeometryMeshExport.Status.Success)
					{
						mesh.Dispose();
						return null;
					}
					mesh.Bounds = ComputeBounds((byte*)mesh._vertices, mesh.VertexCount, mesh.Stride);
					return mesh;
				}
				catch
				{
					mesh.Dispose();
					throw;
				}
			}
		}

		private static unsafe Bounds ComputeBounds(byte* vertices, int count, int stride)
		{
			var position = (float*)(vertices + PositionOffset);
			var min = new Vector3(position[0], position[1], position[2]);
			var max = min;
			for (var index = 1; index < count; index++)
			{
				position = (float*)(vertices + (long)index * stride + PositionOffset);
				var value = new Vector3(position[0], position[1], position[2]);
				min = Vector3.Min(min, value);
				max = Vector3.Max(max, value);
			}
			var bounds = new Bounds();
			bounds.SetMinMax(min, max);
			return bounds;
		}

		/// <summary>Copies the data into mesh data allocated with matching vertex and index parameters.</summary>
		/// <returns>False without writing when the Unity layout differs from the prepared layout.</returns>
		public unsafe bool CopyTo(Mesh.MeshData meshData)
		{
			if (_vertices == IntPtr.Zero)
				throw new ObjectDisposedException(nameof(PreparedMesh));
			if (meshData.vertexBufferCount < 1 ||
				meshData.GetVertexBufferStride(0) != Stride ||
				meshData.indexFormat != IndexFormat ||
				meshData.GetVertexAttributeOffset(VertexAttribute.Position) != PositionOffset ||
				meshData.GetVertexAttributeOffset(VertexAttribute.Normal) != NormalOffset ||
				(HasColors && meshData.GetVertexAttributeOffset(VertexAttribute.Color) != ColorOffset) ||
				(HasTexCoords && meshData.GetVertexAttributeOffset(VertexAttribute.TexCoord0) != TexCoordOffset))
				return false;

			var vertices = meshData.GetVertexData<byte>(0);
			if (vertices.Length != VertexBytes)
				return false;
			void* indexPointer;
			long indexBytes;
			if (IndexFormat == IndexFormat.UInt16)
			{
				var indices = meshData.GetIndexData<ushort>();
				indexPointer = NativeArrayUnsafeUtility.GetUnsafePtr(indices);
				indexBytes = (long)indices.Length * sizeof(ushort);
			}
			else
			{
				var indices = meshData.GetIndexData<uint>();
				indexPointer = NativeArrayUnsafeUtility.GetUnsafePtr(indices);
				indexBytes = (long)indices.Length * sizeof(uint);
			}
			if (indexBytes != IndexBytes)
				return false;

			UnsafeUtility.MemCpy(NativeArrayUnsafeUtility.GetUnsafePtr(vertices), (void*)_vertices, VertexBytes);
			UnsafeUtility.MemCpy(indexPointer, (void*)_indices, IndexBytes);
			return true;
		}

		/// <summary>Frees the unmanaged copy. Safe to call more than once and from any thread.</summary>
		public void Dispose()
		{
			Free();
			GC.SuppressFinalize(this);
		}

		private void Free()
		{
			var vertices = Interlocked.Exchange(ref _vertices, IntPtr.Zero);
			if (vertices != IntPtr.Zero)
				Marshal.FreeHGlobal(vertices);
			var indices = Interlocked.Exchange(ref _indices, IntPtr.Zero);
			if (indices != IntPtr.Zero)
				Marshal.FreeHGlobal(indices);
		}
	}
}
