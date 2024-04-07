using System;
using System.Numerics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    internal struct DynamicArea
    {
        internal NativeList<IntegrationTile> IntegrationField;
        internal NativeList<FlowData> FlowFieldCalculationBuffer;
        internal NativeList<SectorFlowStart> SectorFlowStartCalculationBuffer;
    }

}