using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using System.Collections.Generic;
internal class SectorFlowStartListFactory
{
    List<UnsafeList<SectorFlowStart>> sectorFlowStartListContainer;

    internal SectorFlowStartListFactory(int initialSize)
    {
        sectorFlowStartListContainer = new List<UnsafeList<SectorFlowStart>>(initialSize);
        for (int i = 0; i < initialSize; i++)
        {
            sectorFlowStartListContainer.Add(new UnsafeList<SectorFlowStart>(0, Allocator.Persistent));
        }
    }
    internal UnsafeList<SectorFlowStart> GetSectorFlowStartList()
    {
        if (sectorFlowStartListContainer.Count == 0)
        {
            return new UnsafeList<SectorFlowStart>(0, Allocator.Persistent);
        }
        else
        {
            UnsafeList<SectorFlowStart> nativeReferance = sectorFlowStartListContainer[0];
            sectorFlowStartListContainer.RemoveAtSwapBack(0);
            return nativeReferance;
        }
    }
    internal void SendSectorFlowStartList(UnsafeList<SectorFlowStart> sectorFlowStartList)
    {
        sectorFlowStartList.Clear();
        sectorFlowStartListContainer.Add(sectorFlowStartList);
    }
}
