using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEngine.UIElements;
using System;
using UnityEngine.Jobs;
public class testjob : MonoBehaviour
{
    NativeArray<float2> points = new NativeArray<float2>(10000, Allocator.Persistent);
    NativeArray<float> dists = new NativeArray<float>(10000, Allocator.Persistent);
    NativeArray<float2x4> rect = new NativeArray<float2x4>(1, Allocator.Persistent);
    private void Start()
    {
        rect[0] = new float2x4()
        {
            c0 = UnityEngine.Random.Range(-1000, 1000),
            c1 = UnityEngine.Random.Range(-1000, 1000),
            c2 = UnityEngine.Random.Range(-1000, 1000),
            c3 = UnityEngine.Random.Range(-1000, 1000),
        };

        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new float2()
            {
                x = UnityEngine.Random.Range(-10000, 10000),
                y = UnityEngine.Random.Range(-10000, 10000),
            };
        }

    }
    private void Update()
    {

        distanceBetweenRectAndPoint distjob = new distanceBetweenRectAndPoint()
        {
            Distances = dists,
            Rects = rect,
            Points = points,
        };
        Stopwatch sw = new Stopwatch();
        sw.Start();
        distjob.Schedule().Complete();
        sw.Stop();
        UnityEngine.Debug.Log(sw.Elapsed.TotalMilliseconds);
    }
}

[BurstCompile]
struct distanceBetweenRectAndPoint : IJob
{
    [ReadOnly] internal NativeArray<float2x4> Rects;
    [ReadOnly] internal NativeArray<float2> Points;
    [WriteOnly] internal NativeArray<float> Distances;

    public void Execute()
    {
        for(int pointIndex = 0; pointIndex < Points.Length; pointIndex++)
        {
            float2 point = Points[pointIndex];
            for(int rectIndex = 0; rectIndex < Rects.Length; rectIndex++)
            {
                float2x4 rect = Rects[rectIndex];
                float2 v01 = rect.c1 - rect.c0;
                float2 v10 = rect.c0 - rect.c1;
                float2 v12 = rect.c2 - rect.c1;
                float2 v21 = rect.c1 - rect.c2;
                float2 v0p = point - rect.c0;
                float2 v1p = point - rect.c1;
                float2 v2p = point - rect.c2;

                float dot01_0p = math.dot(v01, v0p);
                float dot10_1p = math.dot(v10, v1p);
                float dot12_1p = math.dot(v12, v1p);
                float dot21_2p = math.dot(v21, v2p);

                bool4 lessThan0 = new float4(dot01_0p, dot10_1p, dot12_1p, dot21_2p) < 0;
                int col = 2;
                int row = 2;
                col = math.select(col, 0, lessThan0.y && !lessThan0.x);
                col = math.select(col, 1, !lessThan0.y && !lessThan0.x);

                row = math.select(row, 0, lessThan0.z && !lessThan0.w);
                row = math.select(row, 1, !lessThan0.z && !lessThan0.w);
                float2 v23 = rect.c3 - rect.c2;
                float2 v03 = rect.c3 - rect.c0;
                switch(col, row)
                {
                    case (0, 0):
                        Distances[pointIndex] = math.distance(point, rect.c1);
                        continue;
                    case (1, 0):
                        float mod = math.sqrt(math.dot(v12, v1p));
                        Distances[pointIndex] = math.abs(v12.x * v1p.y - v12.y * v1p.x) / mod;
                        continue;
                    case (2, 0):
                        Distances[pointIndex] = math.distance(point, rect.c2);
                        continue;
                    case (0, 1):
                        mod = math.sqrt(math.dot(v01, v0p));
                        Distances[pointIndex] = math.abs(v01.x * v0p.y - v01.y * v0p.x) / mod;
                        continue;
                    case (1, 1):
                        Distances[pointIndex] = 0;
                        continue;
                    case (2, 1):
                        mod = math.sqrt(math.dot(v23, v2p));
                        Distances[pointIndex] = math.abs(v23.x * v2p.y - v23.y * v2p.x) / mod;
                        continue;
                    case (0, 2):
                        Distances[pointIndex] = math.distance(point, rect.c0);
                        continue;
                    case (1, 2):
                        mod = math.sqrt(math.dot(v03, v0p));
                        Distances[pointIndex] = math.abs(v03.x * v0p.y - v03.y * v0p.x) / mod;
                        continue;
                    case (2, 2):
                        Distances[pointIndex] = math.distance(point, rect.c3);
                        continue;
                }
            }
        }
    }
}