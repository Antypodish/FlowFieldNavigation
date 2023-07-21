using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

public class TargetSectorCostArrayFactory
{
    List<NativeArray<DijkstraTile>> _targetSectorCostAllocations;
    int _sectorTileAmount;
    public TargetSectorCostArrayFactory(int sectorTileAmount)
    {
        _targetSectorCostAllocations = new List<NativeArray<DijkstraTile>>();
        _sectorTileAmount = sectorTileAmount;
    }
    public NativeArray<DijkstraTile> GetTargetSecorCosts()
    {
        if (_targetSectorCostAllocations.Count == 0) { return new NativeArray<DijkstraTile>(_sectorTileAmount, Allocator.Persistent); }
        int index = _targetSectorCostAllocations.Count - 1;
        NativeArray<DijkstraTile> portalSequence = _targetSectorCostAllocations[index];
        _targetSectorCostAllocations.RemoveAtSwapBack(index);
        return portalSequence;
    }
    public void SendTargetSectorCosts(ref NativeArray<DijkstraTile> targetSectorCosts)
    {
        DijkstraTileArrayCleaningJob cleaning = new DijkstraTileArrayCleaningJob()
        {
            Array = targetSectorCosts,
        };
        cleaning.Schedule().Complete();
        _targetSectorCostAllocations.Add(targetSectorCosts);
    }
}
