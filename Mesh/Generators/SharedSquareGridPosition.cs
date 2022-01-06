using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;

namespace xshazwar.Meshes.Generators {

	public struct SharedSquareGridPosition : IMeshGenerator {

		public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(1f, 0f, 1f));

		//Need our heightmap (Native Slice)?
        // For Now we can use a sin generator
        public int VertexCount => (Resolution + 1) * (Resolution + 1);

		public int IndexCount => 6 * Resolution * Resolution;

		public int JobLength => Resolution + 1;

		public int Resolution { get; set; }

		public void Execute<S> (int z, S streams) where S : struct, IMeshStreams {
			int vi = (Resolution + 1) * z, ti = 2 * Resolution * (z - 1);

			var vertex = new Vertex();
			vertex.position.x = -0.5f;
			vertex.position.z = (float)z / Resolution - 0.5f;
			//fake heights for test
			vertex.position.y = .1f * math.sin(-0.5f);
			streams.SetVertex(vi, vertex);
			vi += 1;

			for (int x = 1; x <= Resolution; x++, vi++, ti += 2) {
				vertex.position.x = (float)x / Resolution - 0.5f;
				//fake heights for test
				vertex.position.y = .1f * math.sin((float)x);
				streams.SetVertex(vi, vertex);

				if (z > 0) {
					streams.SetTriangle(
						ti + 0, vi + int3(-Resolution - 2, -1, -Resolution - 1)
					);
					streams.SetTriangle(
						ti + 1, vi + int3(-Resolution - 1, -1, 0)
					);
				}
			}
		}
	}
}