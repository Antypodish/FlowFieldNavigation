using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using System.Globalization;
using Unity.Burst;
using System.Diagnostics;

internal class test : MonoBehaviour
{
    UnsafeList<UnsafeList<int>> list;
    NativeArray<int> res;

    private void Start()
    {
        
    }
    private void Update()
    {
        speedtest();
    }
    void unsafetest()
    {
        res = new NativeArray<int>(25, Allocator.Persistent);
        list = new UnsafeList<UnsafeList<int>>(5, Allocator.Persistent);
        list.Length = 5;
        for (int i = 0; i < list.Length; i++)
        {
            list[i] = new UnsafeList<int>(5, Allocator.Persistent);
        }

        myjob job = new myjob()
        {
            list = list,
            result = res
        };
        JobHandle jh = job.Schedule();
        jh.Complete();

        for (int i = 0; i < res.Length; i++)
        {
            UnityEngine.Debug.Log(res[i]);
        }
    }
    void speedtest()
    {
        int size = 1000;
        NativeArray<int> ntv = new NativeArray<int>(size, Allocator.Persistent);
        UnsafeList<int> uns = new UnsafeList<int>(size, Allocator.Persistent);
        uns.Length = size;
        for(int i = 0; i < size; i++)
        {
            ntv[i] = i;
            uns[i] = i;
        }
        Stopwatch sw1 = new Stopwatch();
        Stopwatch sw2 = new Stopwatch();

        ntvjob ntvjob = new ntvjob()
        {
            ntv = ntv
        };
        unsjob unsjob = new unsjob()
        {
            uns = uns
        };
        sw1.Start();
        JobHandle jh1 = ntvjob.Schedule();
        jh1.Complete();
        sw1.Stop();

        sw2.Start();
        JobHandle jh2 = unsjob.Schedule();
        jh2.Complete();
        sw2.Stop();

        for(int i = 0; i < size; i++)
        {
            UnityEngine.Debug.Log("ntv: " + ntv[i]);
            UnityEngine.Debug.Log("uns: " + uns[i]);
        }
        UnityEngine.Debug.Log("ntv ms: " + sw1.Elapsed.TotalMilliseconds);
        UnityEngine.Debug.Log("uns ms: " + sw2.Elapsed.TotalMilliseconds);
    }
}
struct myjob : IJob
{
    public UnsafeList<UnsafeList<int>> list;
    public NativeArray<int> result;

    public void Execute()
    {
        list.Length = 5;
        for (int i = 0; i < list.Length; i++)
        {
            UnsafeList<int> nestedList = list[i];
            nestedList.Length = 5;
            list[i] = nestedList;
            for (int j = 0; j < list[i].Length; j++)
            {
                UnsafeList<int> intlist = list[i];
                int integer = intlist[j];
                integer = i * j;
                intlist[j] = integer;
                list[i] = intlist;
            }
        }
        for (int i = 0; i < list.Length; i++)
        {
            for (int j = 0; j < list[i].Length; j++)
            {
                int index = i * list[i].Length + j;
                result[index] = list[i][j];
            }
        }
    }
}
[BurstCompile]
struct ntvjob : IJob
{
    public NativeArray<int> ntv;
    public void Execute()
    {
        for(int i = 0; i < ntv.Length; i++)
        {
            int n = ntv[i];
            n *= i;
            ntv[i] = n;
        }
    }

}
[BurstCompile]
struct unsjob : IJob
{
    public UnsafeList<int> uns;
    public void Execute()
    {
        for (int i = 0; i < uns.Length; i++)
        {
            int n = uns[i];
            n *= i;
            uns[i] = n;
        }
    }
}