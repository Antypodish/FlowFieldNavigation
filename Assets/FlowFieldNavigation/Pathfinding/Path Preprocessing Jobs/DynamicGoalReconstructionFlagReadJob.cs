using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct DynamicGoalReconstructionFlagReadJob : IJob
    {
        [ReadOnly] internal NativeArray<PathUpdateSeed> Seeds;
        internal NativeArray<PathRoutineData> PathRoutineDataArray;
        public void Execute()
        {
            for(int i = 0; i <Seeds.Length; i++)
            {
                PathUpdateSeed seed = Seeds[i];
                if (seed.UpdateFlag)
                {
                    PathRoutineData pathRoutineData = PathRoutineDataArray[seed.PathIndex];
                    pathRoutineData.DestinationState = DynamicDestinationState.OutOfReach;
                    PathRoutineDataArray[seed.PathIndex] = pathRoutineData;
                }
            }
        }
    }
}