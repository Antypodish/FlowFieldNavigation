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

    public NativeArray<PathDestinationData> PathDestinationDataArray;
    public NativeArray<PathLocationData> PathLocationDataArray;
    public NativeArray<PathFlowData> PathFlowDataArray;
    public void Execute()
    {
        ExposedPathDestinationList.Length = PathDestinationDataArray.Length;
        ExposedPathFlowDataList.Length = PathFlowDataArray.Length;
        ExposedPathLocationList.Length = PathLocationDataArray.Length;

        for(int i = 0; i < NewPathIndicies.Length; i++)
        {
            int pathIndex = NewPathIndicies[i];

            ExposedPathDestinationList[pathIndex] = PathDestinationDataArray[pathIndex].Destination;
            ExposedPathFlowDataList[pathIndex] = PathFlowDataArray[pathIndex];
            ExposedPathLocationList[pathIndex] = PathLocationDataArray[pathIndex];
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
