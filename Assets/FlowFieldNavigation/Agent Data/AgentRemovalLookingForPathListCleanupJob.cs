using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct AgentRemovalLookingForPathListCleanupJob : IJob
    {
        [ReadOnly] internal NativeArray<int> AgentRemovalMarks;
        internal NativeList<int> AgentsLookingForPath;
        internal NativeList<PathRequestRecord> AgentsLookingForPathRecords;
        public void Execute()
        {
            for (int i = AgentsLookingForPath.Length - 1; i >= 0; i--)
            {
                int agentIndex = AgentsLookingForPath[i];
                int removalMark = AgentRemovalMarks[agentIndex];
                if (removalMark == -1) { continue; }

                if (removalMark == -2)
                {
                    AgentsLookingForPath.RemoveAtSwapBack(i);
                    AgentsLookingForPathRecords.RemoveAtSwapBack(i);
                    continue;
                }


                PathRequestRecord record = AgentsLookingForPathRecords[i];
                if (record.Type == DestinationType.DynamicDestination)
                {
                    int targetAgent = record.TargetAgent;
                    int targetAgentRemovalMark = AgentRemovalMarks[targetAgent];
                    if (targetAgentRemovalMark == -2)
                    {
                        AgentsLookingForPath.RemoveAtSwapBack(i);
                        AgentsLookingForPathRecords.RemoveAtSwapBack(i);
                        continue;
                    }
                    if (targetAgentRemovalMark >= 0)
                    {
                        record.TargetAgent = targetAgentRemovalMark;
                        AgentsLookingForPathRecords[i] = record;
                    }
                }

                if (removalMark >= 0)
                {
                    AgentsLookingForPath[i] = removalMark;
                    continue;
                }
            }
        }
    }
}
