using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

public class SectorStateTableFactory
{
    List<UnsafeList<PathSectorState>> _pathSectorStateTableContainer;

    public SectorStateTableFactory(int preallocationAmount)
    {
        _pathSectorStateTableContainer = new List<UnsafeList<PathSectorState>>(preallocationAmount);
        for(int i = 0; i < _pathSectorStateTableContainer.Count; i++)
        {
            UnsafeList<PathSectorState> table = new UnsafeList<PathSectorState>(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            table.Length = FlowFieldUtilities.SectorMatrixTileAmount;
        }
    }
    public UnsafeList<PathSectorState> GetSectorStateTable()
    {
        if(_pathSectorStateTableContainer.Count == 0)
        {
            UnsafeList<PathSectorState> newtable = new UnsafeList<PathSectorState>(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            newtable.Length = FlowFieldUtilities.SectorMatrixTileAmount;
            return newtable;
        }
        UnsafeList<PathSectorState> table = _pathSectorStateTableContainer[0];
        _pathSectorStateTableContainer.RemoveAtSwapBack(0);
        return table;
    }
    public void SendSectorStateTable(UnsafeList<PathSectorState> table)
    {
        UnsafeListCleaningJob<PathSectorState> cleaner = new UnsafeListCleaningJob<PathSectorState>()
        {
            List = table,
        };
        cleaner.Schedule().Complete();
        _pathSectorStateTableContainer.Add(table);
    }
}
