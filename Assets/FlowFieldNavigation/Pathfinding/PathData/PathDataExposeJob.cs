using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
internal struct PathDataExposeJob : IJob
{
    [ReadOnly] internal NativeArray<int> NewPathIndicies;
    [ReadOnly] internal NativeArray<int> ExpandedPathIndicies;
    [ReadOnly] internal NativeArray<int> DestinationUpdatedPathIndicies;

    internal NativeList<float2> ExposedPathDestinationList;
    internal NativeList<PathLocationData> ExposedPathLocationList;
    internal NativeList<PathFlowData> ExposedPathFlowDataList;
    internal NativeList<int> ExposedPathFlockIndicies;
    internal NativeList<float> ExposedPathReachDistanceCheckRange;
    internal NativeList<PathState> PathStateList;
    internal NativeList<bool> PathStopFlagList;

    [ReadOnly] internal NativeArray<PathDestinationData> PathDestinationDataArray;
    [ReadOnly] internal NativeArray<PathLocationData> PathLocationDataArray;
    [ReadOnly] internal NativeArray<PathFlowData> PathFlowDataArray;
    [ReadOnly] internal NativeArray<int> PathFlockIndicies;
    public void Execute()
    {
        ExposedPathDestinationList.Length = PathDestinationDataArray.Length;
        ExposedPathFlowDataList.Length = PathFlowDataArray.Length;
        ExposedPathLocationList.Length = PathLocationDataArray.Length;
        ExposedPathFlockIndicies.Length = PathFlockIndicies.Length;
        ExposedPathReachDistanceCheckRange.Length = PathFlowDataArray.Length;
        PathStateList.Length = PathFlowDataArray.Length;
        PathStopFlagList.Length = PathFlowDataArray.Length;

        for (int i = 0; i < NewPathIndicies.Length; i++)
        {
            int pathIndex = NewPathIndicies[i];
            PathDestinationData destinationData = PathDestinationDataArray[pathIndex];
            ExposedPathDestinationList[pathIndex] = destinationData.Destination;
            ExposedPathFlowDataList[pathIndex] = PathFlowDataArray[pathIndex];
            ExposedPathLocationList[pathIndex] = PathLocationDataArray[pathIndex];
            ExposedPathFlockIndicies[pathIndex] = PathFlockIndicies[pathIndex];
            ExposedPathReachDistanceCheckRange[pathIndex] = 0;
            PathStateList[pathIndex] = PathState.Clean;
            PathStopFlagList[pathIndex] = destinationData.DestinationType == DestinationType.StaticDestination;
        }

        for (int i = 0; i < ExpandedPathIndicies.Length; i++)
        {
            int pathIndex = ExpandedPathIndicies[i];

            ExposedPathFlowDataList[pathIndex] = PathFlowDataArray[pathIndex];
            ExposedPathLocationList[pathIndex] = PathLocationDataArray[pathIndex];
        }

        for (int i = 0; i < DestinationUpdatedPathIndicies.Length; i++)
        {
            int pathIndex = DestinationUpdatedPathIndicies[i];

            ExposedPathDestinationList[pathIndex] = PathDestinationDataArray[pathIndex].Destination;
            ExposedPathLocationList[pathIndex] = PathLocationDataArray[pathIndex];
            ExposedPathFlowDataList[pathIndex] = PathFlowDataArray[pathIndex];
        }
    }
}
