using System.Collections;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEngine;

internal class NativeArrayTest : MonoBehaviour
{
    private void Update()
    {
        j1 j1 = new j1(new calc(new NativeArray<int>(0, Allocator.TempJob)));
        j2 j2 = new j2(new calc(new NativeArray<int>(0, Allocator.TempJob)));

        Stopwatch sw1= new Stopwatch();
        Stopwatch sw2= new Stopwatch();

        sw1.Start();
        JobHandle jh1 = j1.Schedule();
        jh1.Complete();
        sw1.Stop();

        sw2.Start();
        JobHandle jh2 = j2.Schedule();
        jh2.Complete();
        sw2.Stop();

        UnityEngine.Debug.Log("j1:" + sw1.Elapsed.TotalMilliseconds);
        UnityEngine.Debug.Log("j2:" + sw2.Elapsed.TotalMilliseconds);
    }
}
[BurstCompile]
struct j1 : IJob
{
    calc c;
    public j1(calc c)
    {
        this.c = c;
    }
    public void Execute()
    {
        c.meth();
    }
}
[BurstCompile]
struct j2 : IJob
{
    calc c;
    public j2(calc c)
    {
        this.c = c;
    }
    public void Execute()
    {
        meth();
    }
    public void meth()
    {
        for (int i = 0; i < 10000; i++)
        {
            for (int j = 0; j < 10000; j++)
            {
                int r = c.res[0];
                r = r + (i * j);
                c.res[0] = r;
            }
        }
    }
}
[BurstCompile]
struct calc
{
    public NativeArray<int> res;

    public calc(NativeArray<int> res)
    {
        this.res = res;
    }
    public void meth()
    {
        for(int i = 0; i < 10000; i++)
        {
            for(int j = 0; j < 10000; j++)
            {
                int r = res[0];
                r = r + (i * j);
                res[0] = r;
            }
        }
    }
}