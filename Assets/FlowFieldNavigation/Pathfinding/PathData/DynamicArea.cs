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
}