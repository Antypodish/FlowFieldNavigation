using System;
using System.Numerics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
public struct DynamicArea
{
    public NativeList<IntegrationTile> IntegrationField;
    public UnsafeList<SectorFlowStart> PickedSectorFlowStarts;
    public UnsafeList<FlowData> FlowField;
    public UnsafeList<FlowData> FlowFieldCalculationBuffer;

    public void Dispose()
    {
        if (IntegrationField.IsCreated) { IntegrationField.Dispose(); }
        if (FlowField.IsCreated) { FlowField.Dispose(); }
        if (PickedSectorFlowStarts.IsCreated) { PickedSectorFlowStarts.Dispose(); }
        if (FlowFieldCalculationBuffer.IsCreated) { FlowFieldCalculationBuffer.Dispose(); }
    }
}