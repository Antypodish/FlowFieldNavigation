using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct PortalNodeToPosJob : IJobParallelFor
    {
        internal float2 FieldGridStartPos;
        internal float TileSize;
        [ReadOnly] internal NativeArray<PortalNode> Nodes;
        [WriteOnly] internal NativeArray<float2> Positions;
        public void Execute(int index)
        {
            Positions[index] = Nodes[index].GetPosition2(TileSize, FieldGridStartPos);
        }
    }


}
