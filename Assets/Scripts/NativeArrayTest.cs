using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEngine;

internal class NativeArrayTest : MonoBehaviour
{
    int size = 100 * 198 * 5;
    NativeArray<Boolfloat> array;
    private void Start()
    {
        array = new NativeArray<Boolfloat>(size, Allocator.Persistent);
    }

    private void Update()
    {
        Stopwatch sw1 = new Stopwatch();
        Stopwatch sw2 = new Stopwatch();

        Allocjob aj = new Allocjob();
        Resetjob rj = new Resetjob() { arr = array };
        sw1.Start();
        JobHandle ajh = aj.Schedule();
        ajh.Complete();
        sw1.Stop();

        sw2.Start();
        JobHandle rjh = rj.Schedule();
        rjh.Complete();
        sw2.Stop();

        UnityEngine.Debug.Log("allocate: " + sw1.Elapsed.TotalMilliseconds);
        UnityEngine.Debug.Log("reset: " + sw2.Elapsed.TotalMilliseconds);
    }
}
[BurstCompile]
public struct Allocjob : IJob
{
    public void Execute()
    {
        NativeArray<Boolfloat> arr = new NativeArray<Boolfloat>(100 * 198 * 5, Allocator.Temp);
    }
}
[BurstCompile]
public struct Resetjob : IJob
{
    public NativeArray<Boolfloat> arr;
    public void Execute()
    {
        for(int i = 0; i < arr.Length; i++)
        {
            Boolfloat bf = arr[i];
            bf.f = 0f;
            bf.b = false;
            arr[i] = bf;
        }
    }
}
public struct Boolfloat
{
    public bool b;
    public float f;
}