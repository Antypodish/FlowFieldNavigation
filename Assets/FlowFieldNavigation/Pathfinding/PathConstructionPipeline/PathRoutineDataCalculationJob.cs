using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
internal struct PathRoutineDataCalculationJob : IJobParallelFor
{
    internal float TileSize;
    internal int SectorColAmount;
    internal int SectorMatrixColAmount;
    internal int SectorRowAmount;
    internal int SectorTileAmount;
    internal int FieldRowAmount;
    internal int FieldColAmount;
    [ReadOnly] internal NativeArray<UnsafeList<DijkstraTile>> TargetSectorIntegrations;
    [ReadOnly] internal NativeArray<PathLocationData> PathLocationDataArray;
    [ReadOnly] internal NativeArray<PathFlowData> PathFlowDataArray;
    [ReadOnly] internal NativeArray<PathState> PathStateArray;
    [ReadOnly] internal NativeArray<AgentData> AgentDataArray;
    [ReadOnly] internal NativeArray<IslandFieldProcessor> IslandFieldProcessors;
    [ReadOnly] internal NativeArray<UnsafeListReadOnly<byte>> CostFields;
    internal NativeArray<PathDestinationData> PathDestinationDataArray;
    internal NativeArray<PathRoutineData> PathOrganizationDataArray;
    internal NativeArray<UnsafeList<PathSectorState>> PathSectorStateTables;

    public void Execute(int index)
    {
        PathState pathState = PathStateArray[index];
        if (pathState == PathState.Removed)
        {
            return;
        }
        UnsafeList<DijkstraTile> targetSectorIntegration = TargetSectorIntegrations[index];
        PathDestinationData destinationData = PathDestinationDataArray[index];
        if (destinationData.DestinationType == DestinationType.DynamicDestination)
        {
            int2 oldTargetIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, TileSize);
            float3 targetAgentPos = AgentDataArray[destinationData.TargetAgentIndex].Position;
            float2 targetAgentPos2 = new float2(targetAgentPos.x, targetAgentPos.z);
            targetAgentPos2 = CheckIfDestinationExtensionNeeded(targetAgentPos2, oldTargetIndex, destinationData.Offset);
            int2 newTargetIndex = (int2)math.floor(targetAgentPos2 / TileSize);
            int oldSector = FlowFieldUtilities.GetSector1D(oldTargetIndex, SectorColAmount, SectorMatrixColAmount);
            LocalIndex1d newLocal = FlowFieldUtilities.GetLocal1D(newTargetIndex, SectorColAmount, SectorMatrixColAmount);
            bool outOfReach = oldSector != newLocal.sector;
            DijkstraTile targetTile = targetSectorIntegration[newLocal.index];
            outOfReach = outOfReach || targetTile.IntegratedCost == float.MaxValue;
            DynamicDestinationState destinationState = oldTargetIndex.Equals(newTargetIndex) ? DynamicDestinationState.None : DynamicDestinationState.Moved;
            destinationState = outOfReach ? DynamicDestinationState.OutOfReach : destinationState;
            destinationData.DesiredDestination = targetAgentPos2;
            destinationData.Destination = targetAgentPos2;
            PathDestinationDataArray[index] = destinationData;

            PathRoutineData organizationData = PathOrganizationDataArray[index];
            organizationData.DestinationState = destinationState;
            PathOrganizationDataArray[index] = organizationData;
        }
    }

    float2 CheckIfDestinationExtensionNeeded(float2 newPosition, int2 oldTargetIndex, int offset)
    {
        UnsafeListReadOnly<byte> costs = CostFields[offset];
        int2 newTargetIndex = FlowFieldUtilities.PosTo2D(newPosition, TileSize);
        LocalIndex1d curLocal = FlowFieldUtilities.GetLocal1D(newTargetIndex, SectorColAmount, SectorMatrixColAmount);
        byte cost = costs[curLocal.sector * SectorTileAmount + curLocal.index];
        IslandFieldProcessor islandFieldProcessor = IslandFieldProcessors[offset];
        int desiredIsland = islandFieldProcessor.GetIsland(oldTargetIndex);
        int currentIsland = islandFieldProcessor.GetIsland(newTargetIndex);
        if (cost != byte.MaxValue && desiredIsland == currentIsland) { return newPosition; }

        float2 closestDestination = GetClosestDestination(newTargetIndex, desiredIsland, islandFieldProcessor, costs);
        return math.select(closestDestination, 0, closestDestination.Equals(-1));
    }

    float2 GetClosestDestination(int2 destinationIndex, int desiredIsland, IslandFieldProcessor islandFieldProcessors, UnsafeListReadOnly<byte> costField)
    {
        int sectorTileAmount = SectorTileAmount;
        int sectorColAmount = SectorColAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;

        LocalIndex1d destinationLocal = FlowFieldUtilities.GetLocal1D(destinationIndex, SectorColAmount, SectorMatrixColAmount);
        int destinationLocalIndex = destinationLocal.index;
        int destinationSector = destinationLocal.sector;

        int offset = 1;

        float pickedExtensionIndexCost = float.MaxValue;
        int pickedExtensionIndexLocalIndex = 0;
        int pickedExtensionIndexSector = 0;


        while (pickedExtensionIndexCost == float.MaxValue)
        {
            int2 topLeft = destinationIndex + new int2(-offset, offset);
            int2 topRight = destinationIndex + new int2(offset, offset);
            int2 botLeft = destinationIndex + new int2(-offset, -offset);
            int2 botRight = destinationIndex + new int2(offset, -offset);

            bool topOverflow = topLeft.y >= FieldRowAmount;
            bool botOverflow = botLeft.y < 0;
            bool rightOverflow = topRight.x >= FieldColAmount;
            bool leftOverflow = topLeft.x < 0;

            if (topOverflow && botOverflow && rightOverflow && leftOverflow) { return -1; }

            if (topOverflow)
            {
                topLeft.y = FieldRowAmount - 1;
                topRight.y = FieldRowAmount - 1;
            }
            if (botOverflow)
            {
                botLeft.y = 0;
                botRight.y = 0;
            }
            if (rightOverflow)
            {
                botRight.x = FieldColAmount - 1;
                topRight.x = FieldColAmount - 1;
            }
            if (leftOverflow)
            {
                topLeft.x = 0;
                botLeft.x = 0;
            }

            int topLeftSector = FlowFieldUtilities.GetSector1D(topLeft, sectorColAmount, SectorMatrixColAmount);
            int topRightSector = FlowFieldUtilities.GetSector1D(topRight, sectorColAmount, SectorMatrixColAmount);
            int botRightSector = FlowFieldUtilities.GetSector1D(botRight, sectorColAmount, SectorMatrixColAmount);
            int botLeftSector = FlowFieldUtilities.GetSector1D(botLeft, sectorColAmount, SectorMatrixColAmount);
            if (!topOverflow)
            {
                int rowToCheck = topLeft.y % SectorRowAmount;
                for (int i = topLeftSector; i <= topRightSector; i++)
                {
                    int colStart = math.select(0, topLeft.x % SectorColAmount, i == topLeftSector);
                    int colEnd = math.select(10, topRight.x % SectorColAmount, i == topRightSector);
                    ExtensionIndex checkedExtension = CheckSectorRow(i, rowToCheck, colStart, colEnd);
                    if (checkedExtension.IsValid() && checkedExtension.Cost < pickedExtensionIndexCost)
                    {
                        pickedExtensionIndexCost = checkedExtension.Cost;
                        pickedExtensionIndexLocalIndex = checkedExtension.LocalIndex;
                        pickedExtensionIndexSector = checkedExtension.SectorIndex;
                    }
                }
            }
            if (!rightOverflow)
            {
                int colToCheck = topRight.x % SectorColAmount;
                for (int i = topRightSector; i >= botRightSector; i -= SectorMatrixColAmount)
                {
                    int rowStart = math.select(9, topRight.y % SectorRowAmount, i == topRightSector);
                    int rowEnd = math.select(-1, botRight.y % SectorRowAmount, i == botRightSector);
                    ExtensionIndex checkedExtension = CheckSectorCol(i, colToCheck, rowStart, rowEnd);
                    if (checkedExtension.IsValid() && checkedExtension.Cost < pickedExtensionIndexCost)
                    {
                        pickedExtensionIndexCost = checkedExtension.Cost;
                        pickedExtensionIndexLocalIndex = checkedExtension.LocalIndex;
                        pickedExtensionIndexSector = checkedExtension.SectorIndex;
                    }
                }
            }
            if (!botOverflow)
            {
                int rowToCheck = botRight.y % SectorRowAmount;
                for (int i = botRightSector; i >= botLeftSector; i--)
                {
                    int colStart = math.select(9, botRight.x % SectorColAmount, i == botRightSector);
                    int colEnd = math.select(-1, botLeft.x % SectorColAmount, i == botLeftSector);
                    ExtensionIndex checkedExtension = CheckSectorRow(i, rowToCheck, colStart, colEnd);
                    if (checkedExtension.IsValid() && checkedExtension.Cost < pickedExtensionIndexCost)
                    {
                        pickedExtensionIndexCost = checkedExtension.Cost;
                        pickedExtensionIndexLocalIndex = checkedExtension.LocalIndex;
                        pickedExtensionIndexSector = checkedExtension.SectorIndex;
                    }
                }
            }
            if (!leftOverflow)
            {
                int colToCheck = topLeft.x % SectorColAmount;
                for (int i = botLeftSector; i <= topLeftSector; i += SectorMatrixColAmount)
                {
                    int rowStart = math.select(0, botLeft.y % SectorRowAmount, i == botLeftSector);
                    int rowEnd = math.select(10, topLeft.y % SectorRowAmount, i == topLeftSector);
                    ExtensionIndex checkedExtension = CheckSectorCol(i, colToCheck, rowStart, rowEnd);
                    if (checkedExtension.IsValid() && checkedExtension.Cost < pickedExtensionIndexCost)
                    {
                        pickedExtensionIndexCost = checkedExtension.Cost;
                        pickedExtensionIndexLocalIndex = checkedExtension.LocalIndex;
                        pickedExtensionIndexSector = checkedExtension.SectorIndex;
                    }
                }
            }
            offset++;
        }

        int2 outputGeneral2d = FlowFieldUtilities.GetGeneral2d(pickedExtensionIndexLocalIndex, pickedExtensionIndexSector, sectorMatrixColAmount, sectorColAmount);
        return FlowFieldUtilities.IndexToPos(outputGeneral2d, TileSize);

        ExtensionIndex CheckSectorRow(int sectorToCheck, int rowToCheck, int colToStart, int colToEnd)
        {
            if (islandFieldProcessors.GetIslandIfNotField(sectorToCheck, out int islandOut))
            {
                if (islandOut != desiredIsland) { return new ExtensionIndex() { Cost = float.MaxValue }; }
            }
            float currentExtensionIndexCost = float.MaxValue;
            int currentExtensionIndexLocalIndex = 0;
            int sectorStride = sectorToCheck * sectorTileAmount;
            int startLocal = rowToCheck * sectorColAmount + colToStart;
            int checkRange = colToEnd - colToStart;
            int checkCount = math.abs(checkRange);
            int checkCountNonZero = math.select(checkCount, 1, checkCount == 0);
            int checkUnit = checkRange / checkCountNonZero;

            int startIndex = sectorStride + startLocal;
            for (int i = 0; i < checkCount; i++)
            {
                int indexToCheck = startIndex + i * checkUnit;
                int localIndex = indexToCheck - sectorStride;
                byte cost = costField[indexToCheck];
                if (cost == byte.MaxValue) { continue; }
                int island = islandFieldProcessors.GetIsland(sectorToCheck, localIndex);
                if (island == desiredIsland)
                {
                    float newExtensionCost = FlowFieldUtilities.GetCostBetween(sectorToCheck, localIndex, destinationSector, destinationLocalIndex, sectorColAmount, sectorMatrixColAmount);
                    if (newExtensionCost < currentExtensionIndexCost) { currentExtensionIndexCost = newExtensionCost; currentExtensionIndexLocalIndex = localIndex; }
                }
            }
            return new ExtensionIndex()
            {
                SectorIndex = sectorToCheck,
                LocalIndex = currentExtensionIndexLocalIndex,
                Cost = currentExtensionIndexCost
            };
        }
        ExtensionIndex CheckSectorCol(int sectorToCheck, int colToCheck, int rowToStart, int rowToEnd)
        {
            if (islandFieldProcessors.GetIslandIfNotField(sectorToCheck, out int islandOut))
            {
                if (islandOut != desiredIsland) { return new ExtensionIndex() { Cost = float.MaxValue }; }
            }
            float currentExtensionIndexCost = float.MaxValue;
            int currentExtensionIndexLocalIndex = 0;
            int sectorStride = sectorToCheck * sectorTileAmount;
            int startLocal = rowToStart * sectorColAmount + colToCheck;
            int checkRange = rowToEnd - rowToStart;
            int checkCount = math.abs(checkRange);
            int checkCountNonZero = math.select(checkCount, 1, checkCount == 0);
            int checkUnit = checkRange / checkCountNonZero;

            int startIndex = sectorStride + startLocal;
            for (int i = 0; i < checkCount; i++)
            {
                int indexToCheck = startIndex + i * sectorColAmount * checkUnit;
                int localIndex = indexToCheck - sectorStride;
                byte cost = costField[indexToCheck];
                if (cost == byte.MaxValue) { continue; }
                int island = islandFieldProcessors.GetIsland(sectorToCheck, localIndex);
                if (island == desiredIsland)
                {
                    float newExtensionCost = FlowFieldUtilities.GetCostBetween(sectorToCheck, localIndex, destinationSector, destinationLocalIndex, sectorColAmount, sectorMatrixColAmount);
                    if (newExtensionCost < currentExtensionIndexCost) { currentExtensionIndexCost = newExtensionCost; currentExtensionIndexLocalIndex = localIndex; }
                }
            }
            return new ExtensionIndex()
            {
                SectorIndex = sectorToCheck,
                LocalIndex = currentExtensionIndexLocalIndex,
                Cost = currentExtensionIndexCost
            };
        }
    }


    private struct ExtensionIndex
    {
        internal int LocalIndex;
        internal int SectorIndex;
        internal float Cost;

        internal bool IsValid()
        {
            return Cost != float.MaxValue;
        }
    }
}
