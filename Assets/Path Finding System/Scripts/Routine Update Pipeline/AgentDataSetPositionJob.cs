using Unity.Burst;
using Unity.Collections;
using UnityEngine.Jobs;

[BurstCompile]
public struct AgentDataSetPositionJob : IJobParallelForTransform
{
    public NativeArray<AgentData> AgentDataArray;
    public void Execute(int index, TransformAccess transform)
    {
        AgentData data = AgentDataArray[index];
        data.Position = transform.position;
        AgentDataArray[index] = data;
    }
}
