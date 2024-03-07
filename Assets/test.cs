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
    private void Start()
    {
        FixedList512Bytes<NativeArray<int>> arraylist = new FixedList512Bytes<NativeArray<int>>();
    }
    private void Update()
    {
    }
}
[BurstCompile]
struct job : IJob
{
    internal FixedList512Bytes<NativeArray<int>> arrays;
    internal NativeList<int2> Result;
    public void Execute()
    {
        for(int i = 0; i < arrays.Length; i++)
        {
            NativeArray<int> arr = arrays[i];
            for(int j = 0; j < arr.Length; j++)
            {
                Result.Add(new int2(i, j));
            }
        }
    }
}