using Mono.Cecil;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEditor;
using UnityEngine;

internal class jobtest : MonoBehaviour
{
    private void Start()
    {
    }
    private void Update()
    {
        Stopwatch sw1 = new Stopwatch();
        Stopwatch sw2 = new Stopwatch();
        sw1.Start();
        UnsafeList<int> ul = new UnsafeList<int>(10000, Allocator.Persistent);
        ul.Length = 10000;
        sw1.Stop();
        sw2.Start();
        NativeArray<int> na = new NativeArray<int>(10000, Allocator.Persistent);
        sw2.Stop();
        UnityEngine.Debug.Log("unsafe: " + sw1.Elapsed.TotalMilliseconds);
        UnityEngine.Debug.Log("native: " + sw2.Elapsed.TotalMilliseconds);
        ul.Dispose();
        na.Dispose();
    }
}
