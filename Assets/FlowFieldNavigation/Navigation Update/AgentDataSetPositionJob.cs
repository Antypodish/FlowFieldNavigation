﻿using Unity.Burst;
using Unity.Collections;
using UnityEngine.Jobs;
using Unity.Mathematics;

[BurstCompile]
internal struct AgentDataSetPositionJob : IJobParallelForTransform
{
    internal float MinXIncluding;
    internal float MinYIncluding;
    internal float MaxXExcluding;
    internal float MaxYExcluding;
    internal NativeArray<AgentData> AgentDataArray;
    public void Execute(int index, TransformAccess transform)
    {
        float3 pos = transform.position;
        pos.x = math.select(pos.x, MinXIncluding, pos.x < MinXIncluding);
        pos.x = math.select(pos.x, MaxXExcluding - 1, pos.x >= MaxXExcluding);
        pos.z = math.select(pos.z, MinYIncluding, pos.z < MinYIncluding);
        pos.z = math.select(pos.z, MaxYExcluding - 1, pos.z >= MaxYExcluding);
        transform.position = pos;

        AgentData data = AgentDataArray[index];
        data.Position = pos;
        AgentDataArray[index] = data;
    }
}
