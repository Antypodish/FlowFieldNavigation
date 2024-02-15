using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct RequestedToNewPathIndexTransferJob : IJob
    {
        internal NativeArray<int> AgentRequestedPathIndicies;
        internal NativeArray<int> AgentNewPathIndicies;
        public void Execute()
        {
            int length = AgentRequestedPathIndicies.Length;
            for (int i = 0; i < length; i++)
            {
                AgentNewPathIndicies[i] = AgentRequestedPathIndicies[i];
                AgentRequestedPathIndicies[i] = -1;
            }
        }
    }


}