using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using xshazwar.MergeSort;

namespace xshazwar.Renderer {
    public class GPUCulling{
        BitonicMergeSort _sort;
        ComputeBuffer cullingPlanesBuffer;
        ComputeBuffer cullingCornerScoresBuffer;
        ComputeBuffer cullingFOVScoresBuffer;
        ComputeBuffer scanReduceBuffer;
        ComputeBuffer offsetBuffer;
        ComputeBuffer fovBuffer;

        private Vector4[] planes;
        int terrainCount;

        BitonicMergeSort sorter;

        public static string cullingShaderName = "Culling";
        ComputeShader cullShader;
        int cullShader_stageScore;
        int cullShader_stageSetFOV;
        int scanShader_stageScan;
        int scanShader_stageReduce;
        int threadCount = 0;
        int sortSize = 0;
        int scanGroupSize = 32;
        int scanThreadSize;
        int scanReductions;
        int instanceCount;

        float[] fovScores;

        public GPUCulling(ComputeShader _cpt, ComputeBuffer offsetBuffer, ComputeBuffer fovBuffer, int sortSize){
            this.cullShader = _cpt;
            this.offsetBuffer = offsetBuffer;
            this.fovBuffer = fovBuffer;
            this.sortSize = sortSize;
            planes = new Vector4[6];
        }

        public void init(int terrainCount, float tileSize, float height){
            this.terrainCount = terrainCount;
            Debug.Log($"sort size {sortSize}; terrain count {terrainCount}");
            fovScores = new float[terrainCount];
            cullShader_stageScore = cullShader.FindKernel("ScorePlane");
            cullShader_stageSetFOV = cullShader.FindKernel("SetFOV");
            scanShader_stageScan = cullShader.FindKernel("Scan");
            scanShader_stageReduce = cullShader.FindKernel("ScanReduce");

            threadCount = Mathf.CeilToInt(terrainCount / 32.0f);
            
            cullingCornerScoresBuffer = new ComputeBuffer(terrainCount * 6, 4);
            cullingFOVScoresBuffer = new ComputeBuffer(sortSize, 4);
            cullingPlanesBuffer = new ComputeBuffer(6, 4 * 4);

            cullShader.SetBuffer(cullShader_stageScore, "planes", cullingPlanesBuffer);
            cullShader.SetBuffer(cullShader_stageScore, "scores", cullingCornerScoresBuffer);
            cullShader.SetBuffer(cullShader_stageScore, "_Offset", offsetBuffer);
            
            cullShader.SetBuffer(cullShader_stageSetFOV, "scores", cullingCornerScoresBuffer);
            cullShader.SetBuffer(cullShader_stageSetFOV, "_Offset", offsetBuffer);
            cullShader.SetBuffer(cullShader_stageSetFOV, "_FOV", fovBuffer);
            cullShader.SetBuffer(cullShader_stageSetFOV, "fovscores", cullingFOVScoresBuffer);
            
            scanThreadSize = (int) Math.Ceiling(sortSize / (float) scanGroupSize);
            scanReductions = scanThreadSize / scanGroupSize;
            if (scanReductions > 0){ // > size of 1024
                Debug.Log($"reductions {scanReductions}, threads {scanThreadSize}");
                scanReduceBuffer = new ComputeBuffer(scanReductions, 4);
            }else{
                scanReduceBuffer = new ComputeBuffer(1, 4);
            }
            cullShader.SetBuffer(scanShader_stageScan, "SCAN_VALUES", cullingFOVScoresBuffer);
            cullShader.SetBuffer(scanShader_stageScan, "REDUCE_BLOCK", scanReduceBuffer);
            cullShader.SetBuffer(scanShader_stageReduce, "SCAN_VALUES", cullingFOVScoresBuffer);
            cullShader.SetBuffer(scanShader_stageReduce, "REDUCE_BLOCK", scanReduceBuffer);
    

            cullShader.SetFloat("MAX_OFFSET", terrainCount * 1f);
            cullShader.SetFloat("TS", tileSize * 1f);
            cullShader.SetFloat("HEIGHT", height * 1f);
            sorter = new BitonicMergeSort(this.cullShader);
        }

        public int setCullingGetInstanceCount(Camera camera){
            UnityEngine.Profiling.Profiler.BeginSample("CullGPUTiles");
            // reuse corners and planes for all camera calcs
            OffsetData.frustrumFromMatrix(camera.cullingMatrix, ref planes);
            
            cullingPlanesBuffer.SetData(planes, 0, 0, 6);
            UnityEngine.Profiling.Profiler.BeginSample("ComputeScore");
            cullShader.Dispatch(cullShader_stageScore, threadCount, 1, 1);
            UnityEngine.Profiling.Profiler.EndSample();
            UnityEngine.Profiling.Profiler.BeginSample("ComputeCullFOV");
            cullShader.Dispatch(cullShader_stageSetFOV, threadCount, 1, 1);
            UnityEngine.Profiling.Profiler.EndSample();
            UnityEngine.Profiling.Profiler.BeginSample("ComputeSort");
            sorter.Init(fovBuffer);
            sorter.Sort(fovBuffer, cullingFOVScoresBuffer);
            UnityEngine.Profiling.Profiler.EndSample();
            // UnityEngine.Profiling.Profiler.BeginSample("ComputeScan");
            // cullShader.Dispatch(scanShader_stageScan, scanThreadSize, 1, 1);
            // if (scanReductions > 0){
            //     cullShader.Dispatch(scanShader_stageReduce, scanThreadSize, 1, 1);
            // }
            // UnityEngine.Profiling.Profiler.EndSample();
    
            // UnityEngine.Profiling.Profiler.EndSample();
            // UnityEngine.Profiling.Profiler.BeginSample("sort / count");
            // Array.Sort<FOV>(
            //     fov_array, (a, b) => FOV.Compare(a, b));
            
            UnityEngine.Profiling.Profiler.BeginSample("Get FoV buffer from GPU");
            cullingFOVScoresBuffer.GetData(fovScores, 0, 0, terrainCount);
            UnityEngine.Profiling.Profiler.EndSample();
            UnityEngine.Profiling.Profiler.BeginSample("count");
            instanceCount = 0;
            for(int i = 0; i < terrainCount; i ++){
                if (fovScores[i] < 0f){
                    instanceCount ++;
                }
            }
            // Debug.Log(instanceCount);
            UnityEngine.Profiling.Profiler.EndSample();
            // UnityEngine.Profiling.Profiler.BeginSample("set buffer");
            // fovBuffer.SetData(fov_array, 0, 0, instanceCount);
            // UnityEngine.Profiling.Profiler.EndSample();

            // UnityEngine.Profiling.Profiler.BeginSample("ComputeScan");
            // cullShader.Dispatch(scanShader_stageScan, scanThreadSize, 1, 1);
            // if (scanReductions > 0){
            //     cullShader.Dispatch(scanShader_stageReduce, scanThreadSize, 1, 1);
            // }
            // UnityEngine.Profiling.Profiler.EndSample();
            
            return instanceCount;
        }

        public void Destroy(){
            foreach(ComputeBuffer b in new List<ComputeBuffer>{
                cullingPlanesBuffer,
                cullingCornerScoresBuffer,
                cullingFOVScoresBuffer,
                scanReduceBuffer
            }){
                try{
                    b.Release();
                }catch{}
            }
        }
    }
}