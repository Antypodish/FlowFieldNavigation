using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;

[BurstCompile]
internal struct NavObstacleDetectionJob : IJob
{
    internal float3 VolStartPos;
    internal float VoxHorSize;
    internal float VoxVerSize;
    internal int XVoxCount;
    internal int YVoxCount;
    internal int ZVoxCount;
    internal int SecCompVoxCount;
    internal int XSecCount;
    internal int ZSecCount;
    [ReadOnly] internal NativeArray<StaticObstacle> StaticObstacles;
    [ReadOnly] internal NativeArray<HeightTile> HighestVoxelsEachTile;
    [WriteOnly] internal NativeList<int3> CollidedIndicies;
    public void Execute()
    {
        NativeHashSet<int3> markedIndicies = new NativeHashSet<int3>(0, Allocator.Temp);
        NativeQueue<int3> bfsQueue = new NativeQueue<int3>(Allocator.Temp);
        for (int i = 0; i < StaticObstacles.Length; i++)
        {
            bfsQueue.Clear();
            StaticObstacle obstacle = StaticObstacles[i];
            float3 leftFacePoint = obstacle.LBL;
            float3 leftFaceNormal = -math.cross(obstacle.LTL - obstacle.LBL, obstacle.UTL - obstacle.LBL);
            float3 rightFacePoint = obstacle.LTR;
            float3 rightFaceNormal = -math.cross(obstacle.LBR - obstacle.LTR, obstacle.UBR - obstacle.LTR);
            float3 frontFacePoint = obstacle.LBL;
            float3 frontFaceNormal = -math.cross(obstacle.UBL - obstacle.LBL, obstacle.UBR - obstacle.LBL);
            float3 backFacePoint = obstacle.LTR;
            float3 backFaceNormal = -math.cross(obstacle.UTR - obstacle.LTR, obstacle.UTL - obstacle.LTR);
            float3 topFacePoint = obstacle.UBL;
            float3 topFaceNormal = -math.cross(obstacle.UTL - obstacle.UBL, obstacle.UTR - obstacle.UBL);
            float3 botFacePoint = obstacle.LTL;
            float3 botFaceNormal = -math.cross(obstacle.LBL - obstacle.LTL, obstacle.LBR - obstacle.LTL);

            float3 obsatcelCenter = (obstacle.LBL + obstacle.LBR + obstacle.LTL + obstacle.LTR + obstacle.UBL + obstacle.UBR + obstacle.UTL + obstacle.UTR) / 8;
            int3 bfsStartIndex = FlowFieldVolumeUtilities.PosToIndex(obsatcelCenter, VolStartPos, VoxHorSize, VoxVerSize);
            bfsStartIndex = FlowFieldVolumeUtilities.Clamp(bfsStartIndex, XVoxCount, YVoxCount, ZVoxCount);
            bfsQueue.Enqueue(bfsStartIndex);
            markedIndicies.Add(bfsStartIndex);

            //6 connected 3d bfs
            while (!bfsQueue.IsEmpty())
            {
                int3 curIndex = bfsQueue.Dequeue();

                //Check if inside the obstacle
                float3 indexPos = FlowFieldVolumeUtilities.GetVoxelCenterPos(curIndex, VolStartPos, VoxHorSize, VoxVerSize);
                float4 dots4 = new float4()
                {
                    x = math.dot(leftFaceNormal, indexPos - leftFacePoint),
                    y = math.dot(rightFaceNormal, indexPos - rightFacePoint),
                    z = math.dot(frontFaceNormal, indexPos - frontFacePoint),
                    w = math.dot(backFaceNormal, indexPos - backFacePoint),
                };
                float2 dots2 = new float2()
                {
                    x = math.dot(topFaceNormal, indexPos - topFacePoint),
                    y = math.dot(botFaceNormal, indexPos - botFacePoint),
                };
                bool4 indexInside4 = dots4 >= 0;
                bool2 indexInside2 = dots2 >= 0;
                if (!(indexInside2.x && indexInside2.y && indexInside4.x && indexInside4.y && indexInside4.z && indexInside4.w)) { continue; }

                //Check if collides
                int2 curFieldIndex2 = new int2(curIndex.x, curIndex.z);
                int curFieldIndex1 = FlowFieldUtilities.To1D(curFieldIndex2, ZVoxCount);
                HeightTile highestVoxel = HighestVoxelsEachTile[curFieldIndex1];
                int maxY = highestVoxel.VoxIndex.y;
                int depth = highestVoxel.StackCount;
                if (curIndex .y <= maxY && curIndex.y > maxY - depth)
                {
                    CollidedIndicies.Add(curIndex);
                }
                //Enqueue neighbours
                int3 left = new int3(curIndex.x - 1, curIndex.y, curIndex.z);
                int3 right = new int3(curIndex.x + 1, curIndex.y, curIndex.z);
                int3 top = new int3(curIndex.x, curIndex.y + 1, curIndex.z);
                int3 bot = new int3(curIndex.x, curIndex.y - 1, curIndex.z);
                int3 front = new int3(curIndex.x, curIndex.y, curIndex.z - 1);
                int3 back = new int3(curIndex.x, curIndex.y, curIndex.z + 1);
                if (FlowFieldVolumeUtilities.WithinBounds(left, XVoxCount, YVoxCount, ZVoxCount) && !markedIndicies.Contains(left))
                {
                    markedIndicies.Add(left);
                    bfsQueue.Enqueue(left);
                }
                if (FlowFieldVolumeUtilities.WithinBounds(right, XVoxCount, YVoxCount, ZVoxCount) && !markedIndicies.Contains(right))
                {
                    markedIndicies.Add(right);
                    bfsQueue.Enqueue(right);
                }
                if (FlowFieldVolumeUtilities.WithinBounds(front, XVoxCount, YVoxCount, ZVoxCount) && !markedIndicies.Contains(front))
                {
                    markedIndicies.Add(front);
                    bfsQueue.Enqueue(front);
                }
                if (FlowFieldVolumeUtilities.WithinBounds(back, XVoxCount, YVoxCount, ZVoxCount) && !markedIndicies.Contains(back))
                {
                    markedIndicies.Add(back);
                    bfsQueue.Enqueue(back);
                }
                if (FlowFieldVolumeUtilities.WithinBounds(top, XVoxCount, YVoxCount, ZVoxCount) && !markedIndicies.Contains(top))
                {
                    markedIndicies.Add(top);
                    bfsQueue.Enqueue(top);
                }
                if (FlowFieldVolumeUtilities.WithinBounds(bot, XVoxCount, YVoxCount, ZVoxCount) && !markedIndicies.Contains(bot))
                {
                    markedIndicies.Add(bot);
                    bfsQueue.Enqueue(bot);
                }
            }
        }
    }
}
