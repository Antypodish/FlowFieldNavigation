using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

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
    [ReadOnly] internal NativeHashMap<int, UnsafeBitArray> SurfaceVolumeBits;
    [WriteOnly] internal NativeList<int3> CollidedIndicies;
    public void Execute()
    {
        NativeHashSet<int3> markedIndicies = new NativeHashSet<int3>(0, Allocator.Temp);
        NativeQueue<int3> bfsQueue = new NativeQueue<int3>(Allocator.Temp);
        for(int i = 0; i < StaticObstacles.Length; i++)
        {
            bfsQueue.Clear();
            StaticObstacle obstacle = StaticObstacles[i];
            float3 leftFacePoint = obstacle.LBL;
            float3 leftFaceNormal = math.cross(obstacle.LBL - obstacle.LTL, obstacle.LBL - obstacle.UTL);
            float3 rightFacePoint = obstacle.LTR;
            float3 rightFaceNormal = math.cross(obstacle.LTR - obstacle.LBR, obstacle.LTR - obstacle.UBR);
            float3 frontFacePoint = obstacle.LBL;
            float3 frontFaceNormal = math.cross(obstacle.LBL - obstacle.UBL, obstacle.LBL - obstacle.UBR);
            float3 backFacePoint = obstacle.LTR;
            float3 backFaceNormal = math.cross(obstacle.LTR - obstacle.UTR, obstacle.LTR - obstacle.UTL);
            float3 topFacePoint = obstacle.LBL;
            float3 topFaceNormal = math.cross(obstacle.LBL - obstacle.UTL, obstacle.LBL - obstacle.UTR);
            float3 botFacePoint = obstacle.LTL;
            float3 botFaceNormal = math.cross(obstacle.LTL - obstacle.LBL, obstacle.LTL - obstacle.LBR);

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
                LocalIndex1d curLocal = FlowFieldVolumeUtilities.GetLocal1D(curIndex, SecCompVoxCount, XSecCount, ZSecCount);
                if(SurfaceVolumeBits.TryGetValue(curLocal.sector, out UnsafeBitArray bits))
                {
                    if (bits.IsSet(curLocal.index)) { CollidedIndicies.Add(curIndex); }
                }

                //Enqueue neighbours
                int3 left = new int3(curIndex.x - 1, curIndex.y, curIndex.z);
                int3 right = new int3(curIndex.x + 1, curIndex.y, curIndex.z);
                int3 top = new int3(curIndex.x, curIndex.y + 1, curIndex.z);
                int3 bot = new int3(curIndex.x, curIndex.y - 1, curIndex.z);
                int3 front = new int3(curIndex.x, curIndex.y, curIndex.z - 1);
                int3 back = new int3(curIndex.x, curIndex.y, curIndex.z + 1);
                if(FlowFieldVolumeUtilities.WithinBounds(left, XVoxCount, YVoxCount, ZVoxCount) && !markedIndicies.Contains(left))
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
