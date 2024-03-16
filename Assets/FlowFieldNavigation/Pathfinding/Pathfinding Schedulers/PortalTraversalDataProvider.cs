using System;
using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Jobs;

namespace FlowFieldNavigation
{
    internal class PortalTraversalDataProvider
    {
        readonly int _coreCount;
        int _dataPointer;
        NativeArray<PortalTraversalData>[] _producedPortalTraversalDataArrays;
        NativeArray<JobHandle> _jobhandleForEachPortalTraversalDataArray;

        internal PortalTraversalDataProvider(FlowFieldNavigationManager navManager)
        {
            _dataPointer = 0;
            _coreCount = SystemInfo.processorCount;
            int maxDataSize = navManager.FieldDataContainer.GetFieldGraphWithOffset(0).PortalNodes.Length;
            _producedPortalTraversalDataArrays = new NativeArray<PortalTraversalData>[_coreCount];
            _jobhandleForEachPortalTraversalDataArray = new NativeArray<JobHandle>(_coreCount, Allocator.Persistent);
            for(int i = 0;i  < _producedPortalTraversalDataArrays.Length; i++)
            {
                NativeArray<PortalTraversalData> portalTraversalDataArray = new NativeArray<PortalTraversalData>(maxDataSize, Allocator.Persistent);
                for(int j = 0; j < portalTraversalDataArray.Length; j++)
                {
                    PortalTraversalData data = portalTraversalDataArray[j];
                    data.Reset();
                    portalTraversalDataArray[j] = data;
                }
                _producedPortalTraversalDataArrays[i] = portalTraversalDataArray;
                _jobhandleForEachPortalTraversalDataArray[i] = new JobHandle();
            }
        }
        internal NativeArray<PortalTraversalData> GetAvailableData(out JobHandle dependency)
        {
            dependency = _jobhandleForEachPortalTraversalDataArray[_dataPointer];
            return _producedPortalTraversalDataArrays[_dataPointer];
        }
        internal void IncerimentPointer(JobHandle handle)
        {
            _jobhandleForEachPortalTraversalDataArray[_dataPointer] = handle;
            _dataPointer = (_dataPointer + 1) % _coreCount;
        }
    }
}