﻿using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

public class TargetSectorCostArrayFactory
{
    List<UnsafeList<DijkstraTile>> _targetSectorCostAllocations;
    int _sectorTileAmount;
    public TargetSectorCostArrayFactory(int sectorTileAmount)
    {
        _targetSectorCostAllocations = new List<UnsafeList<DijkstraTile>>();
        _sectorTileAmount = sectorTileAmount;
    }
    public UnsafeList<DijkstraTile> GetTargetSecorCosts()
    {
        if (_targetSectorCostAllocations.Count == 0)
        {
            UnsafeList<DijkstraTile> targetSectorCostArray = new UnsafeList<DijkstraTile>(_sectorTileAmount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            targetSectorCostArray.Length = _sectorTileAmount;
            return targetSectorCostArray;
        }
        int index = _targetSectorCostAllocations.Count - 1;
        UnsafeList<DijkstraTile> targetSectorCosts = _targetSectorCostAllocations[index];
        _targetSectorCostAllocations.RemoveAtSwapBack(index);
        return targetSectorCosts;
    }
    public void SendTargetSectorCosts(UnsafeList<DijkstraTile> targetSectorCosts)
    {
        UnsafeListCleaningJob<DijkstraTile> cleaning = new UnsafeListCleaningJob<DijkstraTile>()
        {
            List = targetSectorCosts,
        };
        cleaning.Schedule().Complete();
        _targetSectorCostAllocations.Add(targetSectorCosts);
    }
}
