using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
public struct PathDataExposeJob : IJob
{
    [ReadOnly] public NativeArray<int> NewPathIndicies;
    [ReadOnly] public NativeArray<int> ExpandedPathIndicies;
    [ReadOnly] public NativeArray<int> DestinationUpdatedPathIndicies;

    public NativeList<float2> ExposedPathDestinationList;
    public NativeList<PathLocationData> ExposedPathLocationList;
    public NativeList<PathFlowData> ExposedPathFlowDataList;
    public NativeList<int> ExposedPathFlockIndicies;
    public NativeList<float> ExposedPathReachDistanceCheckRange;

    [ReadOnly] public NativeArray<PathDestinationData> PathDestinationDataArray;
    [ReadOnly] public NativeArray<PathLocationData> PathLocationDataArray;
    [ReadOnly] public NativeArray<PathFlowData> PathFlowDataArray;
    [ReadOnly] public NativeArray<int> PathFlockIndicies;
    public void Execute()
    {
        ExposedPathDestinationList.Length = PathDestinationDataArray.Length;
        ExposedPathFlowDataList.Length = PathFlowDataArray.Length;
        ExposedPathLocationList.Length = PathLocationDataArray.Length;
        ExposedPathFlockIndicies.Length = PathFlockIndicies.Length;
        ExposedPathReachDistanceCheckRange.Length = PathFlowDataArray.Length;

        for(int i = 0; i < NewPathIndicies.Length; i++)
        {
            int pathIndex = NewPathIndicies[i];

            ExposedPathDestinationList[pathIndex] = PathDestinationDataArray[pathIndex].Destination;
            ExposedPathFlowDataList[pathIndex] = PathFlowDataArray[pathIndex];
            ExposedPathLocationList[pathIndex] = PathLocationDataArray[pathIndex];
            ExposedPathFlockIndicies[pathIndex] = PathFlockIndicies[pathIndex];
            ExposedPathReachDistanceCheckRange[pathIndex] = 0;
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
        }
    }
}
