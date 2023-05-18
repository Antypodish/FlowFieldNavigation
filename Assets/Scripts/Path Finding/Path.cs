using Unity.Collections;
using UnityEditor;
using UnityEditor.Profiling.Memory.Experimental;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public struct Path
{
    public int Offset;
    public NativeArray<float> PortalDistances;
    public NativeArray<int> ConnectionIndicies;
}
