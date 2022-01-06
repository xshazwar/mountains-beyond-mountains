using UnityEngine;

namespace xshazwar.Meshes {

	public interface IMeshGenerator {

		Bounds Bounds { get; }

		int VertexCount { get; }

		int IndexCount { get; }

		int JobLength { get; }

		int Resolution { get; set; }

		void Execute<S> (int i, S streams) where S : struct, IMeshStreams;
	}
}