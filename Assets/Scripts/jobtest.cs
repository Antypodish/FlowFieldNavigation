using Mono.Cecil;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEditor;
using UnityEngine;
using UnityEngine.Jobs;

internal class jobtest : MonoBehaviour
{
    public GameObject position;
    public GameObject desired;


    private void Update()
    {/*
        Stopwatch normal = new Stopwatch();
        Stopwatch statik = new Stopwatch();
        JobNormal norm = new JobNormal()
        {
            fun = new FuncNormal(),
        };
        JobStatic stat = new JobStatic();
        normal.Start();
        norm.Schedule().Complete();
        normal.Stop();
        statik.Start();
        stat.Schedule().Complete();
        statik.Stop();
        UnityEngine.Debug.Log("normal:" + normal.Elapsed.TotalMilliseconds);
        UnityEngine.Debug.Log("static:" + statik.Elapsed.TotalMilliseconds);*/
    }
}
[BurstCompile]
struct JobNormal : IJob
{
    public FuncNormal fun;

    public void Execute()
    {
        fun.Run();
    }
}
struct FuncNormal
{
    public void Run()
    {
        int j = 0;
        for(int i = 0; i < 1000000; i++)
        {
            j += i;
        }
    }
}
[BurstCompile]
struct JobStatic : IJob
{
    public void Execute()
    {
        FuncStatic.Run();
    }
}
struct FuncStatic
{
    public static void Run()
    {
        int j = 0;
        for (int i = 0; i < 1000000; i++)
        {
            j += i;
        }
    }
}