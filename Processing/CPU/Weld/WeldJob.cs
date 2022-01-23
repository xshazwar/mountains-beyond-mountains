using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace xshazwar.processing.cpu.weld {

    public struct WeldJob<G> : IJobFor
		where G : struct, IGenerateWelds {
	
		G generator;
        [ReadOnly]
        NativeSlice<float> source;
        [WriteOnly]
        NativeSlice<float> target;

		public void Execute (int i) => generator.Execute(i, source, target);

		public static JobHandle ScheduleParallel (
			NativeSlice<float> target, NativeSlice<float> source, WeldSide side, int resolution, int margin, JobHandle dependency
		) {
			var job = new WeldJob<G>();
			job.generator.Resolution = resolution;
            job.generator.MarginWidth = margin;
            job.generator.side = side;
            job.target = target;
            job.source = source;

			return job.ScheduleParallel(job.generator.JobLength, 1, dependency
			);
		}
	}

	public delegate JobHandle MeshJobScheduleDelegate (
		Mesh mesh, Mesh.MeshData meshData, int resolution, JobHandle dependency
	);

}