using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;


namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct AgentsToStartMovingSetJob : IJob
    {
        [ReadOnly] internal NativeArray<int> AgentNewPathIndicies;
        internal NativeList<int> AgentsToStartMoving;
        public void Execute()
        {
            for (int i = 0; i < AgentNewPathIndicies.Length; i++)
            {
                if (AgentNewPathIndicies[i] != -1)
                {
                    AgentsToStartMoving.Add(i);
                }
            }
        }
    }


}