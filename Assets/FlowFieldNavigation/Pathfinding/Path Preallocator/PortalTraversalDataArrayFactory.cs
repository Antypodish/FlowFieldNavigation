using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;

internal class PortalTraversalDataArrayFactory
{
    List<NativeArray<PortalTraversalData>>[] _preallocationMatrix;
    List<CleaningHandle> _cleaningHandles;
    int[] _portalNodeAmounts;
    internal PortalTraversalDataArrayFactory(FieldGraph[] producedFieldGraphs)
    {
        _preallocationMatrix = new List<NativeArray<PortalTraversalData>>[producedFieldGraphs.Length];
        _portalNodeAmounts = new int[producedFieldGraphs.Length];
        for(int i = 0; i < _portalNodeAmounts.Length; i++)
        {
            _portalNodeAmounts[i] = producedFieldGraphs[i].PortalNodes.Length;
        }
        for(int i = 0; i < _preallocationMatrix.Length; i++)
        {
            _preallocationMatrix[i] = new List<NativeArray<PortalTraversalData>>();
        }
        _cleaningHandles = new List<CleaningHandle>();
    }
    internal void CheckForCleaningHandles()
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
    internal NativeArray<PortalTraversalData> GetPortalTraversalDataArray(int offset)
    {
        List<NativeArray<PortalTraversalData>> _preallocations = _preallocationMatrix[offset];
        if( _preallocations.Count == 0)
        {
            NativeArray<PortalTraversalData> newTravData = new NativeArray<PortalTraversalData>(_portalNodeAmounts[offset], Allocator.Persistent);
            PortalTraversalDataArrayCleaningJob cleaninJob = new PortalTraversalDataArrayCleaningJob()
            {
                Array = newTravData,
            };
            cleaninJob.Schedule().Complete();
            return newTravData;
        }
        NativeArray<PortalTraversalData> array = _preallocations[_preallocations.Count - 1];
        _preallocations.RemoveAtSwapBack(_preallocations.Count - 1);
        return array;
    }
    internal void SendPortalTraversalDataArray(NativeArray<PortalTraversalData> array, int offset)
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
        internal NativeArray<PortalTraversalData> Array;
        internal int Offset;
        internal JobHandle handle;
    }
}
