using System;
using System.Numerics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
internal struct DynamicArea
{
    internal NativeList<IntegrationTile> IntegrationField;
    internal UnsafeList<FlowData> FlowFieldCalculationBuffer;
    internal UnsafeList<SectorFlowStart> SectorFlowStartCalculationBuffer;

    internal void Dispose()
    {
        if (IntegrationField.IsCreated) { IntegrationField.Dispose(); }
        if (FlowFieldCalculationBuffer.IsCreated) { FlowFieldCalculationBuffer.Dispose(); }
        if (SectorFlowStartCalculationBuffer.IsCreated) { SectorFlowStartCalculationBuffer.Dispose(); }
    }
}