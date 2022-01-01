using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

public class TestScan : MonoBehaviour
{
    public ComputeShader shader;
    ComputeBuffer nbuff;
    ComputeBuffer bbuff;
    ComputeBuffer drawBuffer;

    int count = 2 << 15;  //2power of 2, minimum of 2 << 4;
    // int count = 256 * 77;
    int groupSize = 32;
    int threads;
    int reductions;
    float[] nums;
    float[] bsums;
    uint[] drawArgs;
    System.Random rand;
    int idReduce;
    int idScan;
    int idCopy;
    float sumcheck;
    void Start()
    { 
        threads = (int) Math.Ceiling(count / (float) groupSize);
        reductions = threads / groupSize;
        Debug.Log(threads);
        Debug.Log(count);
        nums = new float[count];
        bsums = new float[reductions];
        rand = new System.Random();
        idScan = shader.FindKernel("Scan");
        idReduce = shader.FindKernel("ReduceScan");
        idCopy = shader.FindKernel("SetDrawBuffer");
        nbuff = new ComputeBuffer(count, 4);
        if(reductions > 0){
            bbuff = new ComputeBuffer(reductions, 4);
        }
        else{
            bbuff = new ComputeBuffer(1, 4);
        }
        drawBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        drawArgs = new uint[5] { 0, 0, 0, 0, 0 };
        drawBuffer.SetData(drawArgs);
        work();
    }

    public void work(){
        for (int i = 0; i < count ; i ++){
            nums[i] = (float) rand.Next(0, 255);
        }
        sumcheck = nums.Sum();
        Debug.Log(sumcheck);
        nbuff.SetData(nums);
        
        shader.SetBuffer(idScan, "SCAN_VALUES", nbuff);
        shader.SetBuffer(idScan, "REDUCE_BLOCK", bbuff);
        shader.SetBuffer(idReduce, "SCAN_VALUES", nbuff);
        shader.SetBuffer(idReduce, "REDUCE_BLOCK", bbuff);
        shader.SetBuffer(idCopy, "SCAN_VALUES", nbuff);
        shader.SetBuffer(idCopy, "REDUCE_BLOCK", bbuff);
        shader.SetBuffer(idCopy, "DRAW_BUFFER", drawBuffer);
        UnityEngine.Profiling.Profiler.BeginSample("Scan");
        shader.Dispatch(idScan, threads, 1, 1);
        if(reductions > 0){
            shader.Dispatch(idReduce, threads, 1, 1);
        }

        shader.Dispatch(idCopy, threads, 1, 1);
        UnityEngine.Profiling.Profiler.EndSample();
        nbuff.GetData(nums);
        bbuff.GetData(bsums);
        drawBuffer.GetData(drawArgs);
        // foreach(float f in bsums){
        //     Debug.Log(f);
        // }
        Debug.Log(nums[count-1] == sumcheck);
        Debug.Log(drawArgs[1]);
        Debug.Log(drawArgs[1] == sumcheck);
        // Debug.Log(nums[count-1]);
    }

    // Update is called once per frame
    void Update()
    {
        // for (int x = 0 ; x < 1; x ++){
        //     work();
        // }
    }

    void OnDestroy(){
        nbuff.Release();
        bbuff.Release();
        drawBuffer.Release();
    }
}
