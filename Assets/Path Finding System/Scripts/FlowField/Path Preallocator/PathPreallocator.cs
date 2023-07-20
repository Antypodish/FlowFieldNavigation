using Unity.Collections;
using UnityEditor.Profiling.Memory.Experimental;
using UnityEngine.Rendering.Universal;

internal class PathPreallocator
{
    PortalTraversalDataArrayAllocator _porTravDataArrayAllocator;
    public PathPreallocator(CostFieldProducer costFieldProducer)
    {
        _porTravDataArrayAllocator = new PortalTraversalDataArrayAllocator(costFieldProducer);
    }

    public NativeArray<PortalTraversalData> GetPortalTraversalDataArray(int offset)
    {
        return _porTravDataArrayAllocator.GetPortalTraversalDataArray(offset);
    }
    public void Send(NativeArray<PortalTraversalData> array, int offset)
    {
        _porTravDataArrayAllocator.SendPortalTraversalDataArray(array, offset);
    }
}