using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct NewPathToCurPathTransferJob : IJob
{
    public NativeArray<int> AgentCurPathIndicies;
    public NativeArray<int> AgentNewPathIndicies;
    public NativeArray<int> PathSubscribers;
    public void Execute()
    {
        for (int i = 0; i < AgentCurPathIndicies.Length; i++)
        {
            int curPathIndex = AgentCurPathIndicies[i];
            int newPathIndex = AgentNewPathIndicies[i];
            if (newPathIndex == -1) { continue; }
            if (curPathIndex == -1)
            {
                AgentCurPathIndicies[i] = newPathIndex;
                AgentNewPathIndicies[i] = -1;
            }
            else
            {
                int subscriber = PathSubscribers[curPathIndex];
                PathSubscribers[curPathIndex] = subscriber - 1;
                AgentCurPathIndicies[i] = newPathIndex;
                AgentNewPathIndicies[i] = -1;
            }
        }
    }
}
