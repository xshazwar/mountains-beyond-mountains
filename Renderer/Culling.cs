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
        ComputeBuffer drawArgsBuffer;

        private Vector4[] planes;
        int terrainCount;

        BitonicMergeSort sorter;

        public static string cullingShaderName = "Culling";
        ComputeShader cullShader;
        int cullShader_zeroScores;
        int cullShader_stageScore;
        int cullShader_stageSetFOV;
        int scanShader_zeroReduce;
        int scanShader_stageScan;
        int scanShader_stageReduce;
        int scanShader_stageSetDraws;
        int threadCount = 0;
        int sortSize = 0;
        int scanGroupSize = 32;
        int scanThreadSize;
        int scanReductions;
        int instanceCount;

        float[] fovScores;

        private uint[] drawArgs;

        public GPUCulling(
                ComputeShader _cpt,
                ComputeBuffer offsetBuffer,
                ComputeBuffer fovBuffer,
                ComputeBuffer drawArgsBuffer,
                int sortSize
            ){
            this.cullShader = _cpt;
            this.offsetBuffer = offsetBuffer;
            this.fovBuffer = fovBuffer;
            this.drawArgsBuffer = drawArgsBuffer;
            this.sortSize = sortSize;
            planes = new Vector4[6];
        }

        public void init(int terrainCount, float tileSize, float height, uint meshIndexSize){
            this.terrainCount = terrainCount;
            Debug.Log($"sort size {sortSize}; terrain count {terrainCount}");
            fovScores = new float[terrainCount];
            cullShader_zeroScores = cullShader.FindKernel("ZeroScores");
            cullShader_stageScore = cullShader.FindKernel("ScorePlane");
            cullShader_stageSetFOV = cullShader.FindKernel("SetFOV");
            scanShader_zeroReduce = cullShader.FindKernel("ZeroReduce");
            scanShader_stageScan = cullShader.FindKernel("Scan");
            scanShader_stageReduce = cullShader.FindKernel("ScanReduce");
            scanShader_stageSetDraws = cullShader.FindKernel("SetDrawBuffer");

            threadCount = Mathf.CeilToInt(terrainCount / 32.0f);
            
            cullingCornerScoresBuffer = new ComputeBuffer(terrainCount * 6, 4);
            cullingFOVScoresBuffer = new ComputeBuffer(sortSize, 4);
            cullingPlanesBuffer = new ComputeBuffer(6, 4 * 4);

            cullShader.SetInt("SCAN_SIZE", sortSize);
            cullShader.SetBuffer(cullShader_zeroScores, "fovscores", cullingFOVScoresBuffer);

            cullShader.SetBuffer(cullShader_stageScore, "planes", cullingPlanesBuffer);
            cullShader.SetBuffer(cullShader_stageScore, "scores", cullingCornerScoresBuffer);
            cullShader.SetBuffer(cullShader_stageScore, "_Offset", offsetBuffer);
            
            cullShader.SetBuffer(cullShader_stageSetFOV, "scores", cullingCornerScoresBuffer);
            cullShader.SetBuffer(cullShader_stageSetFOV, "_Offset", offsetBuffer);
            cullShader.SetBuffer(cullShader_stageSetFOV, "_FOV", fovBuffer);
            cullShader.SetBuffer(cullShader_stageSetFOV, "fovscores", cullingFOVScoresBuffer);
            
            scanThreadSize = (int) Math.Ceiling(sortSize /(1.0 * scanGroupSize));
            scanReductions = scanThreadSize / scanGroupSize;
            if (scanReductions > 0){ // > size of 1024
                Debug.Log($"reductions {scanReductions}, threads {scanThreadSize}");
                scanReduceBuffer = new ComputeBuffer(scanReductions, 4);
            }else{
                Debug.Log($"no reductions");
                scanReduceBuffer = new ComputeBuffer(1, 4);
            }
            cullShader.SetBuffer(scanShader_zeroReduce, "REDUCE_BLOCK", scanReduceBuffer);

            cullShader.SetBuffer(scanShader_stageScan, "SCAN_VALUES", cullingFOVScoresBuffer);
            cullShader.SetBuffer(scanShader_stageScan, "REDUCE_BLOCK", scanReduceBuffer);

            cullShader.SetBuffer(scanShader_stageReduce, "SCAN_VALUES", cullingFOVScoresBuffer);
            cullShader.SetBuffer(scanShader_stageReduce, "REDUCE_BLOCK", scanReduceBuffer);

            cullShader.SetBuffer(scanShader_stageSetDraws, "SCAN_VALUES", cullingFOVScoresBuffer);
            cullShader.SetBuffer(scanShader_stageSetDraws, "DRAW_BUFFER", drawArgsBuffer);

            cullShader.SetFloat("MAX_OFFSET", terrainCount * 1f);
            cullShader.SetFloat("TS", tileSize * 1f);
            cullShader.SetFloat("HEIGHT", height * 1f);
            sorter = new BitonicMergeSort(this.cullShader);

            drawArgs = new uint[5] { 0, 0, 0, 0, 0 };
            drawArgs[0] = meshIndexSize;
            drawArgsBuffer.SetData(drawArgs);
        }

        public void setCullingGetInstanceCount(Camera camera){
            UnityEngine.Profiling.Profiler.BeginSample("CullGPUTiles");

            cullShader.Dispatch(cullShader_zeroScores, sortSize, 1, 1);
            if (scanReductions > 0){
                cullShader.Dispatch(scanShader_zeroReduce, scanReductions, 1, 1);
            }
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

            UnityEngine.Profiling.Profiler.BeginSample("ComputeScan");
            cullShader.Dispatch(scanShader_stageScan, scanThreadSize, 1, 1);
            if (scanReductions > 0){
                cullShader.Dispatch(scanShader_stageReduce, scanThreadSize, 1, 1);
            }
            cullShader.Dispatch(scanShader_stageSetDraws, 1, 1, 1);
            UnityEngine.Profiling.Profiler.EndSample();
        }

        public void Destroy(){
            foreach(ComputeBuffer b in new List<ComputeBuffer>{
                cullingPlanesBuffer,
                cullingCornerScoresBuffer,
                cullingFOVScoresBuffer,
                scanReduceBuffer,
                drawArgsBuffer
            }){
                try{
                    b.Release();
                }catch{
                    Debug.LogError("Could not release buffer!");
                }
            }
        }
    }
}