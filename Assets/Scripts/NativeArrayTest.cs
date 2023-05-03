using System.Diagnostics;
using Unity.Collections;
using UnityEngine;

internal class NativeArrayTest : MonoBehaviour
{
    NativeArray<Vector3> m_Array;
    NativeList<Vector3> m_List;

    private void Start()
    {
        m_Array = new NativeArray<Vector3>(10000000, Allocator.Persistent);
        m_List = new NativeList<Vector3>(Allocator.Persistent);

        for(int i =0; i< m_Array.Length; i++)
        {
            m_Array[i] = Vector3.one;
            m_List.Add(Vector3.one);
        }
    }

    private void Update()
    {
        Stopwatch sw1 = new Stopwatch();
        sw1.Start();
        for(int i = 0; i < m_Array.Length; i++)
        {
            Vector3 v3 = m_Array[i];
        }
        sw1.Stop();
        Stopwatch sw2 = new Stopwatch();
        sw2.Start();
        for (int i = 0; i < m_List.Length; i++)
        {
            Vector3 v3 = m_List[i];
        }
        sw2.Stop();

        UnityEngine.Debug.Log("array: " + sw1.ElapsedMilliseconds);
        UnityEngine.Debug.Log("list: " + sw2.ElapsedMilliseconds);
    }
}
