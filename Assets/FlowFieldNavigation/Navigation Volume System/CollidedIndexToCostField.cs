using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct CollidedIndexToCostField : IJob
    {
        internal float3 VolStartPos;
        internal float VoxHorSize;
        internal float VoxVerSize;
        internal float FieldTileSize;
        internal float2 FieldGridStartPos;
        internal int FieldColAmount;
        internal int FieldRowAmount;
        [ReadOnly] internal NativeList<int3> CollidedIndicies;
        [WriteOnly] internal NativeArray<byte> Costs;
        public void Execute()
        {
            for (int i = 0; i < CollidedIndicies.Length; i++)
            {
                int3 collidedVolumeIndex = CollidedIndicies[i];
                float3 indexStart = FlowFieldVolumeUtilities.GetVoxelStartPos(collidedVolumeIndex, VolStartPos, VoxHorSize, VoxVerSize);
                float3 indexEnd = indexStart + new float3(VoxHorSize, VoxVerSize, VoxHorSize);
                float2 indexStart2 = new float2(indexStart.x, indexStart.z) + FieldTileSize / 1000f;
                float2 indexEnd2 = new float2(indexEnd.x, indexEnd.z) - FieldTileSize / 1000f;
                int2 startIndex = FlowFieldUtilities.PosTo2D(indexStart2, FieldTileSize, FieldGridStartPos);
                int2 endIndex = FlowFieldUtilities.PosTo2D(indexEnd2, FieldTileSize, FieldGridStartPos);
                startIndex = FlowFieldUtilities.Clamp(startIndex, FieldColAmount, FieldRowAmount);
                endIndex = FlowFieldUtilities.Clamp(endIndex, FieldColAmount, FieldRowAmount);

                for (int r = startIndex.y; r <= endIndex.y; r++)
                {
                    for (int c = startIndex.x; c <= endIndex.x; c++)
                    {
                        int2 fieldIndex = new int2(c, r);
                        int fieldIndex1d = FlowFieldUtilities.To1D(fieldIndex, FieldColAmount);
                        Costs[fieldIndex1d] = byte.MaxValue;
                    }
                }
            }

        }
    }


}