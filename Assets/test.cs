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
    public int count1;
    public int count2;
    public int iterationCOunt;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
    }
}
[BurstCompile]
public struct hashmaptest : IJob
{
    public int count1;
    public int count2;
    public NativeMultiHashMap<int, int> map;

    public void Execute()
    {
        for(int i = 0; i< count1; i++)
        {
            for(int j = 0; j < count2; j++)
            {
                map.Add(i, j);
            }
        }
    }
}
[BurstCompile]
public struct arraytest : IJob
{
    public int valueToBeware;
    public int valueToWrite;
    public NativeArray<int> array;
    public int iterationCount;
    public void Execute()
    {
        for(int i = 0; i < iterationCount; i++)
        {
            for (int j = 0; j < array.Length; j++)
            {
                if (j == valueToBeware) { continue; }
                array[j] = i;
            }
        }
    }
}