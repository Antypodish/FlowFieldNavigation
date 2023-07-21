using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;

public class PortalTraversalDataArrayFactory
{
    List<NativeArray<PortalTraversalData>>[] _preallocationMatrix;
    List<CleaningHandle> _cleaningHandles;
    int[] _portalNodeAmounts;
    public PortalTraversalDataArrayFactory(CostFieldProducer costFieldProducer)
    {
        CostField[] costFields = costFieldProducer.GetAllCostFields();
        _preallocationMatrix = new List<NativeArray<PortalTraversalData>>[costFields.Length];
        _portalNodeAmounts = new int[costFields.Length];
        for(int i = 0; i < _portalNodeAmounts.Length; i++)
        {
            _portalNodeAmounts[i] = costFields[i].FieldGraph.PortalNodes.Length;
        }
        for(int i = 0; i < _preallocationMatrix.Length; i++)
        {
            _preallocationMatrix[i] = new List<NativeArray<PortalTraversalData>>();
        }
        _cleaningHandles = new List<CleaningHandle>();
    }
    public void CheckForCleaningHandles()
    {
        for(int i = _cleaningHandles.Count - 1; i >= 0; i--)
        {
            CleaningHandle cleaningHandle = _cleaningHandles[i];
            if (cleaningHandle.handle.IsCompleted)
            {
                cleaningHandle.handle.Complete();
                _preallocationMatrix[cleaningHandle.Offset].Add(cleaningHandle.Array);
                _cleaningHandles.RemoveAtSwapBack(i);
            }
        }
    }
    public NativeArray<PortalTraversalData> GetPortalTraversalDataArray(int offset)
    {
        List<NativeArray<PortalTraversalData>> _preallocations = _preallocationMatrix[offset];
        if( _preallocations.Count == 0)
        {
            return new NativeArray<PortalTraversalData>(_portalNodeAmounts[offset], Allocator.Persistent);
        }
        NativeArray<PortalTraversalData> array = _preallocations[_preallocations.Count - 1];
        _preallocations.RemoveAtSwapBack(_preallocations.Count - 1);
        return array;
    }
    public void SendPortalTraversalDataArray(NativeArray<PortalTraversalData> array, int offset)
    {
        PortalTraversalDataArrayCleaningJob cleaninJob = new PortalTraversalDataArrayCleaningJob()
        {
            Array = array,
        };
        CleaningHandle cleaningHandle = new CleaningHandle()
        {
            handle = cleaninJob.Schedule(),
            Offset = offset,
            Array = array,
        };
        _cleaningHandles.Add(cleaningHandle);
    }

    struct CleaningHandle
    {
        public NativeArray<PortalTraversalData> Array;
        public int Offset;
        public JobHandle handle;
    }
}
