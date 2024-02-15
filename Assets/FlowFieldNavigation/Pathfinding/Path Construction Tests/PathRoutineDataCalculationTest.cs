using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Mathematics;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct PathRoutineDataCalculationTest : IJob
    {
        internal float TileSize;
        internal int SectorColAmount;
        internal int SectorMatrixColAmount;
        internal int SectorTileAmount;
        internal float FieldMinXIncluding;
        internal float FieldMinYIncluding;
        internal float FieldMaxXExcluding;
        internal float FieldMaxYExcluding;
        internal float2 FieldGridStartPos;
        [ReadOnly] public NativeArray<UnsafeListReadOnly<byte>> CostFields;
        [ReadOnly] public NativeArray<IslandFieldProcessor> IslandFieldProcessors;
        [ReadOnly] public NativeArray<PathState> PathStates;
        [ReadOnly] public NativeArray<PathDestinationData> PathDestinationData;
        [ReadOnly] public NativeArray<PathRoutineData> PathRoutineData;

        [WriteOnly] public NativeReference<bool> PathBotReconstructedAndUpdated;
        [WriteOnly] public NativeReference<bool> RemovedPathUpdatedOrReconstructed;
        [WriteOnly] public NativeReference<bool> DestinationOnInvalidIslandButNotReconstructed;
        [WriteOnly] public NativeReference<bool> DestinationOnUnwalkableButNotReconstructed;
        [WriteOnly] public NativeReference<bool> DestinationOutsideFieldBounds;

        public void Execute()
        {
            for (int i = 0; i < PathStates.Length; i++)
            {
                PathState pathState = PathStates[i];
                PathDestinationData destinationData = PathDestinationData[i];
                PathRoutineData routineData = PathRoutineData[i];
                bool isRemoved = pathState == PathState.Removed;
                bool isReconstructed = (routineData.Task & PathTask.Reconstruct) == PathTask.Reconstruct;
                bool isDestinationStateChanged = routineData.DestinationState != DynamicDestinationState.None;

                if (isRemoved && (isReconstructed || isDestinationStateChanged)) { RemovedPathUpdatedOrReconstructed.Value = true; }
                if (isRemoved) { continue; }

                if (isReconstructed && isDestinationStateChanged) { PathBotReconstructedAndUpdated.Value = true; }

                float2 destination = destinationData.Destination;
                bool destinationWithinBunds = destination.x >= FieldMinXIncluding && destination.x < FieldMaxXExcluding && destination.y >= FieldMinYIncluding && destination.y < FieldMaxYExcluding;
                if (!destinationWithinBunds) { DestinationOutsideFieldBounds.Value = true; continue; }

                IslandFieldProcessor islandFieldProcessor = IslandFieldProcessors[destinationData.Offset];
                int destinationIsland = islandFieldProcessor.GetIsland(destinationData.Destination);
                if (destinationIsland == int.MaxValue && !isReconstructed) { DestinationOnInvalidIslandButNotReconstructed.Value = true; }

                UnsafeListReadOnly<byte> costs = CostFields[destinationData.Offset];
                int2 destinationIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, TileSize, FieldGridStartPos);
                LocalIndex1d destinationLocal = FlowFieldUtilities.GetLocal1D(destinationIndex, SectorColAmount, SectorMatrixColAmount);
                byte cost = costs[destinationLocal.sector * SectorTileAmount + destinationLocal.index];
                if (cost == byte.MaxValue && !isReconstructed)
                {
                    DestinationOnUnwalkableButNotReconstructed.Value = true;
                }
            }
        }
    }



}