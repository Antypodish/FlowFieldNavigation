using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using System.Diagnostics;

public class test : MonoBehaviour
{
    public int buffercnt;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {/*
        NativeArray<int> d1 = new NativeArray<int>(buffercnt * 4, Allocator.Persistent);
        NativeArray<float> d2 = new NativeArray<float>(buffercnt * 4, Allocator.Persistent);
        NativeArray<int> d3 = new NativeArray<int>(buffercnt * 4, Allocator.Persistent);
        NativeArray<float> d4 = new NativeArray<float>(buffercnt * 4, Allocator.Persistent);
        NativeArray<int> d5 = new NativeArray<int>(buffercnt * 4, Allocator.Persistent);

        NativeArray<normalStr> normalbuff = new NativeArray<normalStr>(buffercnt * 4, Allocator.Persistent);
        for(int i = 0; i < buffercnt * 4; i++)
        {
            d1[i] = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            d2[i] = UnityEngine.Random.Range(float.MinValue, float.MaxValue);
            d3[i] = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            d4[i] = UnityEngine.Random.Range(float.MinValue, float.MaxValue);
            d5[i] = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

            normalbuff[i] = new normalStr()
            {
                d1 = d1[i],
                d2 = d2[i],
                d3 = d3[i],
                d4 = d4[i],
                d5 = d5[i],
            };
        }

        normaljob nj = new normaljob()
        {
            buff = normalbuff,
            result = new NativeArray<float>(buffercnt * 4, Allocator.Persistent),
        };
        Stopwatch sw = new Stopwatch();
        JobHandle nh = nj.Schedule();
        sw.Start();
        nh.Complete();
        sw.Stop();
        UnityEngine.Debug.Log("normal: " + sw.Elapsed.TotalMilliseconds);
        simdjob sj = new simdjob()
        {
            d1 = d1,
            d2 = d2,
            d3 = d3,
            d4 = d4,
            d5 = d5,
            result = new NativeArray<float>(buffercnt * 4, Allocator.Persistent),
        };
        sw = new Stopwatch();
        JobHandle sh = sj.Schedule();
        sw.Start();
        sh.Complete();
        sw.Stop();
        UnityEngine.Debug.Log("simd: " + sw.Elapsed.TotalMilliseconds);
        d1.Dispose();
        d2.Dispose();
        d3.Dispose();
        d4.Dispose();
        d5.Dispose();
        normalbuff.Dispose();
        nj.result.Dispose();
        sj.result.Dispose();*/
    }


}
[BurstCompile]
struct normaljob : IJob
{
    [ReadOnly] public NativeArray<normalStr> buff;

    [WriteOnly] public NativeArray<float> result;
    public void Execute()
    {
        for(int i = 0; i < buff.Length; i++)
        {
            normalStr normal = buff[i];
            float res = (normal.d1 * normal.d2 + normal.d3 * normal.d4) / normal.d5;
            result[i] = res;
        }
    }
}
[BurstCompile]
struct simdjob : IJob
{
    [ReadOnly] public NativeArray<int> d1;
    [ReadOnly] public NativeArray<float> d2;
    [ReadOnly] public NativeArray<int> d3;
    [ReadOnly] public NativeArray<float> d4;
    [ReadOnly] public NativeArray<int> d5;

    [WriteOnly] public NativeArray<float> result;

    public void Execute()
    {
        for (int i = 0; i < d1.Length; i+=4)
        {
            int4 data1 = new int4()
            {
                x = d1[i],
                y = d1[i + 1],
                z = d1[i + 2],
                w = d1[i + 3],
            };
            float4 data2 = new float4()
            {
                x = d2[i],
                y = d2[i + 1],
                z = d2[i + 2],
                w = d2[i + 3],
            };
            float4 res1 = (data1 * data2);
            int4 data3 = new int4()
            {
                x = d3[i],
                y = d3[i + 1],
                z = d3[i + 2],
                w = d3[i + 3],
            };
            float4 data4 = new float4()
            {
                x = d4[i],
                y = d4[i + 1],
                z = d4[i + 2],
                w = d4[i + 3],
            };
            float4 res2 = (data3 * data4);
            int4 data5 = new int4()
            {
                x = d5[i],
                y = d5[i + 1],
                z = d5[i + 2],
                w = d5[i + 3],
            };

            float4 res = (res1 + res2) / data5;
            result[i] = res.x;
            result[i + 1] = res.y;
            result[i + 1] = res.z;
            result[i + 1] = res.w;
        }

    }
}

public struct normalStr
{
    public int d1;
    public float d2;
    public int d3;
    public float d4;
    public int d5;
}