using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct FlowFieldJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<IntegrationTile> IntegrationField;
    [ReadOnly] public NativeArray<DirectionData> DirectionData;
    public NativeArray<FlowData> FlowField;

    public void Execute(int index)
    {
        NativeArray<IntegrationTile> integrationField = IntegrationField;
        DirectionData directionData = DirectionData[index];
        FlowField[index] = GetFlowDirection();

        FlowData GetFlowDirection()
        {
            FlowData flowData = FlowData.None;
            float cost = float.MaxValue;
            if (integrationField[directionData.N].Cost < cost)
            {
                flowData = FlowData.N;
                cost = integrationField[directionData.N].Cost;
            }
            if (integrationField[directionData.NE].Cost < cost)
            {
                flowData = FlowData.NE;
                cost = integrationField[directionData.NE].Cost;
            }
            if (integrationField[directionData.E].Cost < cost)
            {
                flowData = FlowData.E;
                cost = integrationField[directionData.E].Cost;
            }
            if (integrationField[directionData.SE].Cost < cost)
            {
                flowData = FlowData.SE;
                cost = integrationField[directionData.SE].Cost;
            }
            if (integrationField[directionData.S].Cost < cost)
            {
                flowData = FlowData.S;
                cost = integrationField[directionData.S].Cost;
            }
            if (integrationField[directionData.SW].Cost < cost)
            {
                flowData = FlowData.SW;
                cost = integrationField[directionData.SW].Cost;
            }
            if (integrationField[directionData.W].Cost < cost)
            {
                flowData = FlowData.W;
                cost = integrationField[directionData.W].Cost;
            }
            if (integrationField[directionData.NW].Cost < cost)
            {
                flowData = FlowData.NW;
                cost = integrationField[directionData.NW].Cost;
            }
            return flowData;
        }
    }
}
public enum FlowData : byte
{
    None = 0,
    N = 1,
    NE = 2,
    E = 3,
    SE = 4,
    S = 5,
    SW = 6,
    W = 7,
    NW = 8,
}