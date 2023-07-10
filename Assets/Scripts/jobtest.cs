using Mono.Cecil;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEditor;
using UnityEngine;

internal class jobtest : MonoBehaviour
{
    private void Start()
    {
        NativeArray<int> ints = new NativeArray<int>(1000, Allocator.Persistent);
        myjob mj = new myjob()
        {
            ints = ints
        };
        JobHandle mjh = mj.Schedule();

        myparalleljob mpj1 = new myparalleljob()
        {
            ints = ints
        };
        JobHandle mpj1h = mpj1.Schedule(ints.Length, 1, mjh);

        myparalleljob mpj2 = new myparalleljob()
        {
            ints = ints
        };
        mpj2.Schedule(ints.Length, 512, mpj1h);
    }
    private void Update()
    {
        
    }
}

struct myjob : IJob
{
    public NativeArray<int> ints;

    public void Execute()
    {
        for(int i = 0; i < ints.Length; i++)
        {
            ints[i] = i;
        }
    }
}
struct myparalleljob : IJobParallelFor
{
    public NativeArray<int> ints;
    public void Execute(int index)
    {
        int i = ints[index];
        i *= i;
        ints[index] = i;
    }
}
