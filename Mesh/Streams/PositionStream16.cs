using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace xshazwar.Meshes.Streams {

    public struct PositionStream16 : IMeshStreams {
		// This is only valid for square mesh up to resolution 256*256

		[StructLayout(LayoutKind.Sequential)]
		struct Stream0 {
			// could just use a float3 but this is nice and explicit 
			public float3 position;
		}

		[NativeDisableContainerSafetyRestriction]
		NativeArray<Stream0> stream0;

		[NativeDisableContainerSafetyRestriction]
		NativeArray<TriangleUInt16> triangles;

		public void Setup (
			Mesh.MeshData meshData, Bounds bounds, int vertexCount, int indexCount
		) {
			var descriptor = new NativeArray<VertexAttributeDescriptor>(
				1, Allocator.Temp, NativeArrayOptions.UninitializedMemory
			);
			descriptor[0] = new VertexAttributeDescriptor(dimension: 3);
			meshData.SetVertexBufferParams(vertexCount, descriptor);
			descriptor.Dispose();

			meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt16);

			meshData.subMeshCount = 1;
			meshData.SetSubMesh(
				0, new SubMeshDescriptor(0, indexCount) {
					bounds = bounds,
					vertexCount = vertexCount
				},
				MeshUpdateFlags.DontRecalculateBounds |
				MeshUpdateFlags.DontValidateIndices
			);

			stream0 = meshData.GetVertexData<Stream0>();
			triangles = meshData.GetIndexData<ushort>().Reinterpret<TriangleUInt16>(2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetVertex (int index, Vertex vertex) => stream0[index] = new Stream0 {
			position = vertex.position
		};

		public void SetTriangle (int index, int3 triangle) => triangles[index] = triangle;
	}
}