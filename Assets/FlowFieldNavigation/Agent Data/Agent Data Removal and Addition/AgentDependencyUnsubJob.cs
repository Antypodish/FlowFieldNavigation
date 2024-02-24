using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using System.Collections.Generic;
using UnityEngine.Jobs;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct AgentDependencyUnsubJob : IJob
    {
        [ReadOnly] internal NativeArray<int> RemovedAgentIndicies;
        [WriteOnly] internal NativeList<int> AgentRequestedPathIndicies;
        internal NativeArray<int> PathSubscriberCounts;
        internal NativeArray<Flock> FlockList;
        internal NativeList<int> AgentFlockIndicies;
        internal NativeList<int> AgentCurPathIndicies;
        public void Execute()
        {
            //Unsub agents to be removed
            for (int i = 0; i < RemovedAgentIndicies.Length; i++)
            {
                int agentIndex = RemovedAgentIndicies[i];

                //Unsub cur path
                int curPathIndex = AgentCurPathIndicies[agentIndex];
                if (curPathIndex != -1)
                {
                    AgentCurPathIndicies[agentIndex] = -1;
                    PathSubscriberCounts[curPathIndex]--;
                }

                //Unsub new path
                AgentRequestedPathIndicies[agentIndex] = -1;

                //Unsub flock
                int flockIndex = AgentFlockIndicies[agentIndex];
                if (flockIndex != 0)
                {
                    AgentFlockIndicies[agentIndex] = 0;
                    Flock flock = FlockList[flockIndex];
                    flock.AgentCount--;
                    FlockList[flockIndex] = flock;
                }

            }
        }
    }


}
