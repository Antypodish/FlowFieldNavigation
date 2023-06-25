using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

public class Test : MonoBehaviour
{
    private void Start()
    {
        UnsafeList<int> mlist = new UnsafeList<int>(4, Allocator.Persistent);
        mlist.Add(0);
        mlist.Add(1);
        mlist.Add(2);
        mlist.Add(3);
        mlist.Add(4);
        for(int i = 0; i < mlist.Length; i++)
        {
            Debug.Log(mlist[i]);
        }
    }
}
