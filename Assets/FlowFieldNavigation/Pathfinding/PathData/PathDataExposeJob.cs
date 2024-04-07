using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct PathDataExposeJob : IJob
    {
        [ReadOnly] internal NativeArray<int> NewPathIndicies;
        [ReadOnly] internal NativeArray<int> ExpandedPathIndicies;
        [ReadOnly] internal NativeArray<int> DestinationUpdatedPathIndicies;

        internal NativeList<float2> ExposedPathDestinationList;
        internal NativeList<int> ExposedPathFlockIndicies;
        internal NativeList<float> ExposedPathReachDistanceCheckRange;
        internal NativeList<PathState> PathStateList;
        internal NativeList<bool> PathStopFlagList;

        [ReadOnly] internal NativeArray<PathDestinationData> PathDestinationDataArray;
        [ReadOnly] internal NativeArray<int> PathFlockIndicies;
        public void Execute()
        {
            ExposedPathDestinationList.Length = PathDestinationDataArray.Length;
            ExposedPathFlockIndicies.Length = PathFlockIndicies.Length;
            ExposedPathReachDistanceCheckRange.Length = PathDestinationDataArray.Length;
            PathStateList.Length = PathDestinationDataArray.Length;
            PathStopFlagList.Length = PathDestinationDataArray.Length;

            for (int i = 0; i < NewPathIndicies.Length; i++)
            {
                int pathIndex = NewPathIndicies[i];
                PathDestinationData destinationData = PathDestinationDataArray[pathIndex];
                ExposedPathDestinationList[pathIndex] = destinationData.Destination;
                ExposedPathFlockIndicies[pathIndex] = PathFlockIndicies[pathIndex];
                ExposedPathReachDistanceCheckRange[pathIndex] = 0;
                PathStateList[pathIndex] = PathState.Clean;
                PathStopFlagList[pathIndex] = destinationData.DestinationType == DestinationType.StaticDestination;
            }

            for (int i = 0; i < DestinationUpdatedPathIndicies.Length; i++)
            {
                int pathIndex = DestinationUpdatedPathIndicies[i];

                ExposedPathDestinationList[pathIndex] = PathDestinationDataArray[pathIndex].Destination;
            }
        }
    }


}
