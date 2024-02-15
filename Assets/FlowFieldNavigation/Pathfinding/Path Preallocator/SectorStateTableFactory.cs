using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

namespace FlowFieldNavigation
{

    internal class SectorStateTableFactory
    {
        List<UnsafeList<PathSectorState>> _pathSectorStateTableContainer;

        internal SectorStateTableFactory(int preallocationAmount)
        {
            _pathSectorStateTableContainer = new List<UnsafeList<PathSectorState>>();
            for (int i = 0; i < preallocationAmount; i++)
            {
                UnsafeList<PathSectorState> table = new UnsafeList<PathSectorState>(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                table.Length = FlowFieldUtilities.SectorMatrixTileAmount;
                _pathSectorStateTableContainer.Add(table);
            }
        }
        internal UnsafeList<PathSectorState> GetSectorStateTable()
        {
            if (_pathSectorStateTableContainer.Count == 0)
            {
                UnsafeList<PathSectorState> newtable = new UnsafeList<PathSectorState>(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                newtable.Length = FlowFieldUtilities.SectorMatrixTileAmount;
                return newtable;
            }
            UnsafeList<PathSectorState> table = _pathSectorStateTableContainer[0];
            _pathSectorStateTableContainer.RemoveAtSwapBack(0);
            return table;
        }
        internal void SendSectorStateTable(UnsafeList<PathSectorState> table)
        {
            UnsafeListCleaningJob<PathSectorState> cleaner = new UnsafeListCleaningJob<PathSectorState>()
            {
                List = table,
            };
            cleaner.Schedule().Complete();
            _pathSectorStateTableContainer.Add(table);
        }
    }


}