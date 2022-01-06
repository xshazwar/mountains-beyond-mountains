using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace xshazwar.Meshes {

	// [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
	// TODO
	// write an ifdef for high performance
	// https://github.com/UnityCommunity/UnityLibrary/blob/master/Assets/Scripts/Editor/AddDefineSymbols.cs
	public struct MeshJob<G, S> : IJobFor
		where G : struct, IMeshGenerator
		where S : struct, IMeshStreams {

		G generator;

		[WriteOnly]
		S streams;

		public void Execute (int i) => generator.Execute(i, streams);

		public static JobHandle ScheduleParallel (
			Mesh mesh, Mesh.MeshData meshData, int resolution, JobHandle dependency
		) {
			var job = new MeshJob<G, S>();
			job.generator.Resolution = resolution;
			job.streams.Setup(
				meshData,
				mesh.bounds = job.generator.Bounds,
				job.generator.VertexCount,
				job.generator.IndexCount
			);
			return job.ScheduleParallel(
				job.generator.JobLength, 1, dependency
			);
		}
	}

	public delegate JobHandle MeshJobScheduleDelegate (
		Mesh mesh, Mesh.MeshData meshData, int resolution, JobHandle dependency
	);
}