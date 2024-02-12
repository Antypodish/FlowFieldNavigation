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
    internal float FieldMinXIncluding;
    internal float FieldMinYIncluding;
    internal float FieldMaxXExcluding;
    internal float FieldMaxYExcluding;
    internal float2 FieldGridStartPos;
    [ReadOnly] internal NativeArray<UnsafeList<DijkstraTile>> TargetSectorIntegrations;
    [ReadOnly] internal NativeArray<PathLocationData> PathLocationDataArray;
    [ReadOnly] internal NativeArray<PathFlowData> PathFlowDataArray;
    [ReadOnly] internal NativeArray<PathState> PathStateArray;
    [ReadOnly] internal NativeArray<AgentData> AgentDataArray;
    [ReadOnly] internal NativeArray<IslandFieldProcessor> IslandFieldProcessors;
    [ReadOnly] internal NativeArray<UnsafeListReadOnly<byte>> CostFields;
    internal NativeArray<PathDestinationData> PathDestinationDataArray;
    internal NativeArray<PathRoutineData> PathOrganizationDataArray;

    public void Execute(int index)
    {
        PathState pathState = PathStateArray[index];
        bool isBeingReconstructed = (PathOrganizationDataArray[index].Task & PathTask.Reconstruct) == PathTask.Reconstruct;
        if (pathState == PathState.Removed || isBeingReconstructed)
        {
            return;
        }
        UnsafeList<DijkstraTile> targetSectorIntegration = TargetSectorIntegrations[index];
        PathDestinationData destinationData = PathDestinationDataArray[index];
        if (destinationData.DestinationType == DestinationType.DynamicDestination)
        {
            //Data structures
            UnsafeListReadOnly<byte> costs = CostFields[destinationData.Offset];
            IslandFieldProcessor islandFieldProcessor = IslandFieldProcessors[destinationData.Offset];

            //Get targets
            float3 targetAgentPos = AgentDataArray[destinationData.TargetAgentIndex].Position;
            float2 targetAgentPos2 = new float2(targetAgentPos.x, targetAgentPos.z);

            //Clamp destination to bounds
            targetAgentPos2.x = math.select(targetAgentPos2.x, FieldMinXIncluding, targetAgentPos2.x < FieldMinXIncluding);
            targetAgentPos2.y = math.select(targetAgentPos2.y, FieldMinYIncluding, targetAgentPos2.y < FieldMinYIncluding);
            targetAgentPos2.x = math.select(targetAgentPos2.x, FieldMaxXExcluding - TileSize / 2, targetAgentPos2.x >= FieldMaxXExcluding);
            targetAgentPos2.y = math.select(targetAgentPos2.y, FieldMaxYExcluding - TileSize / 2, targetAgentPos2.y >= FieldMaxYExcluding);

            float2 oldDestination = destinationData.Destination;
            float2 newDestination = targetAgentPos2;
            int2 oldDestinationIndex = FlowFieldUtilities.PosTo2D(oldDestination, TileSize, FieldGridStartPos);
            int2 newDestinationIndex = FlowFieldUtilities.PosTo2D(newDestination, TileSize, FieldGridStartPos);
            LocalIndex1d newDestinationLocal = FlowFieldUtilities.GetLocal1D(newDestinationIndex, SectorColAmount, SectorMatrixColAmount);
            byte newDestinationCost = costs[newDestinationLocal.sector * SectorTileAmount + newDestinationLocal.index];
            int oldDestinationIsland = islandFieldProcessor.GetIsland(oldDestinationIndex);
            int newDestinationIsland = islandFieldProcessor.GetIsland(newDestinationIndex);

            //Test
            bool shouldExpandNewDestination = newDestinationCost == byte.MaxValue || oldDestinationIsland != newDestinationIsland;
            if (shouldExpandNewDestination)
            {
                int desiredIsland = oldDestinationIsland;
                bool succesfull = TryGetExtendedPosition(newDestination, desiredIsland, islandFieldProcessor, costs, out float2 extendedPos);
                newDestination = math.select(oldDestination, extendedPos, succesfull);
                newDestinationIndex = FlowFieldUtilities.PosTo2D(newDestination, TileSize, FieldGridStartPos);
                newDestinationLocal = FlowFieldUtilities.GetLocal1D(newDestinationIndex, SectorColAmount, SectorMatrixColAmount);
            }
            int oldSector = FlowFieldUtilities.GetSector1D(oldDestinationIndex, SectorColAmount, SectorMatrixColAmount);
            bool outOfReach = oldSector != newDestinationLocal.sector;
            DijkstraTile targetTile = targetSectorIntegration[newDestinationLocal.index];
            outOfReach = outOfReach || targetTile.IntegratedCost == float.MaxValue;

            //Output
            DynamicDestinationState destinationState = oldDestinationIndex.Equals(newDestinationIndex) ? DynamicDestinationState.None : DynamicDestinationState.Moved;
            destinationState = outOfReach ? DynamicDestinationState.OutOfReach : destinationState;
            destinationData.DesiredDestination = targetAgentPos2;
            destinationData.Destination = newDestination;
            PathDestinationDataArray[index] = destinationData;
            PathRoutineData organizationData = PathOrganizationDataArray[index];
            organizationData.DestinationState = destinationState;
            PathOrganizationDataArray[index] = organizationData;
        }
    }
    bool TryGetExtendedPosition(float2 position, int desiredIsland, IslandFieldProcessor islandFieldProcessor, UnsafeListReadOnly<byte> costs, out float2 extendedPos)
    {
        int2 newTargetIndex = FlowFieldUtilities.PosTo2D(position, TileSize, FieldGridStartPos);
        bool succesfull = TryGetClosestDestination(newTargetIndex, desiredIsland, islandFieldProcessor, costs, out float2 closestDestination);
        extendedPos = closestDestination;
        return succesfull;
    }
    bool TryGetClosestDestination(int2 destinationIndex, int desiredIsland, IslandFieldProcessor islandFieldProcessors, UnsafeListReadOnly<byte> costField, out float2 closestDestination)
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

            if (topOverflow && botOverflow && rightOverflow && leftOverflow)
            {
                closestDestination = 0;
                return false;
            }

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
        closestDestination = FlowFieldUtilities.IndexToPos(outputGeneral2d, TileSize, FieldGridStartPos);
        return true;

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
