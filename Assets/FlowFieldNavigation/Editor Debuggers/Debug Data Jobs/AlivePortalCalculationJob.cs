using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

[BurstCompile]
internal struct AlivePortalCalculationJob : IJob
{
    [ReadOnly] internal NativeArray<WindowNode> WindowNodes;
    [ReadOnly] internal NativeArray<PortalNode> PortalNodes;
    [WriteOnly] internal NativeList<int> AlivePortalIndicies;
    public void Execute()
    {
        for(int i = 0; i < WindowNodes.Length; i++)
        {
            WindowNode window = WindowNodes[i];
            for(int j = window.PorPtr; j < window.PorPtr + window.PorCnt; j++)
            {
                AlivePortalIndicies.Add(j);
            }
        }
    }
}