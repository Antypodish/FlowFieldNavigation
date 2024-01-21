using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
internal struct PathRoutineDataResetJob : IJob
{
    internal NativeArray<PathRoutineData> PathOrganizationDataArray;

    public void Execute()
    {
        for(int i = 0; i < PathOrganizationDataArray.Length; i++)
        {
            PathOrganizationDataArray[i] = new PathRoutineData()
            {
                DestinationState = 0,
                FlowRequestSourceCount = 0,
                FlowRequestSourceStart = 0,
                PathAdditionSourceCount = 0,
                PathAdditionSourceStart = 0,
                ReconstructionRequestIndex = -1,
                Task = 0,
            };
        }
    }
}
