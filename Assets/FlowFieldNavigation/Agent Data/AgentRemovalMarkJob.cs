using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
[BurstCompile]
internal struct AgentRemovalMarkJob : IJob
{
    internal int CurAgentCount;
    [ReadOnly] internal NativeArray<int> RemovedAgentIndicies;
    internal NativeList<int> AgentRemovalMarks;
    public void Execute()
    {
        AgentRemovalMarks.Length = CurAgentCount;
        NativeArray<int> agentRemovalMarksAsArray = AgentRemovalMarks.AsArray();
        //Reset
        for(int i = 0; i < agentRemovalMarksAsArray.Length; i++)
        {
            agentRemovalMarksAsArray[i] = -1;
        }

        //Set removed marks
        for(int i = 0; i < RemovedAgentIndicies.Length; i++)
        {
            int agentIndex = RemovedAgentIndicies[i];
            agentRemovalMarksAsArray[agentIndex] = -2;
        }
    }
}
