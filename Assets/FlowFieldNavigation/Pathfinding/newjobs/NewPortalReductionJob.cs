using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct NewPortalReductionJob : IJob
    {
        internal int2 TargetIndex;
        internal int FieldColAmount;
        internal int FieldRowAmount;
        internal float FieldTileSize;
        internal int SectorColAmount;
        internal int SectorMatrixColAmount;
        internal int SectorTileAmount;
        internal float2 FieldGridStartPos;

        internal NativeArray<PortalTraversalData> PortalTraversalDataArray;

        internal UnsafeList<PathSectorState> SectorStateTable;
        internal NativeList<int> PickedToSector;
        internal UnsafeList<DijkstraTile> TargetSectorCosts;
        internal NativeReference<int> FlowFieldLength;

        [ReadOnly] internal NativeSlice<float2> SourcePositions;
        [ReadOnly] internal NativeArray<SectorNode> SectorNodes;
        [ReadOnly] internal NativeArray<int> SecToWinPtrs;
        [ReadOnly] internal NativeArray<WindowNode> WindowNodes;
        [ReadOnly] internal NativeArray<int> WinToSecPtrs;
        [ReadOnly] internal NativeArray<PortalNode> PortalNodes;
        [ReadOnly] internal NativeArray<PortalToPortal> PorPtrs;
        [ReadOnly] internal NativeArray<byte> Costs;
        [ReadOnly] internal NativeArray<SectorDirectionData> LocalDirections;
        [ReadOnly] internal NativeArray<UnsafeList<int>> IslandFields;

        internal NativeList<int> TargetNeighbourPortalIndicies;
        internal NativeList<int> AStarTraverseIndexList;
        internal NativeList<int> SourcePortalIndexList;

        int _targetSectorStartIndex1d;
        int _targetSectorIndex1d;
        public void Execute()
        {

        }
    }
}
