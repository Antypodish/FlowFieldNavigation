using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct PortalTraversalDataArrayCleaningJob : IJob
{
    public NativeArray<PortalTraversalData> Array;
    public void Execute()
    {
        for(int i = 0; i < Array.Length; i++)
        {
            Array[i] = new PortalTraversalData()
            {
                NextIndex = -1,
                DistanceFromTarget = float.MaxValue,
            };
        }
    }
}
