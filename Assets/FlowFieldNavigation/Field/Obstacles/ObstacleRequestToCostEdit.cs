using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;
using System;

[BurstCompile]
internal struct ObstacleRequestToCostEdit : IJob
{
    internal float TileSize;
    internal int FieldColAmount;
    internal int FieldRowAmount;
    internal float FieldMaxXExcluding;
    internal float FieldMaxYExcluding;
    internal float FieldMinXIncluding;
    internal float FieldMinYIncluding;
    internal float2 FieldGridStartPos;

    internal NativeArray<ObstacleRequest> ObstacleRequests;
    internal NativeList<CostEdit> CostEditOutput;
    internal NativeList<int> NewObstacleKeyListToAdd;

    internal NativeList<Obstacle> ObstacleList;
    internal NativeList<int> RemovedObstacleIndexList;

    public void Execute()
    {
        for(int i = 0; i < ObstacleRequests.Length; i++)
        {
            ObstacleRequest obstacleRequest = ObstacleRequests[i];
            float2 pos2d = obstacleRequest.Position;
            float2 halfSize = obstacleRequest.HalfSize;
            if (pos2d.x < FieldMinXIncluding || pos2d.y < FieldMinYIncluding || pos2d.x >= FieldMaxXExcluding || pos2d.y >= FieldMaxYExcluding) { continue; }

            float2 botLeft = pos2d - halfSize;
            float2 topRight = pos2d + halfSize;
            int2 botLeftBound = FlowFieldUtilities.PosTo2D(botLeft, TileSize, FieldGridStartPos);
            int2 toprightBound = FlowFieldUtilities.PosTo2D(topRight, TileSize, FieldGridStartPos);
            botLeftBound.x = math.select(botLeftBound.x, 0, botLeftBound.x < 0);
            botLeftBound.y = math.select(botLeftBound.y, 0, botLeftBound.y < 0);
            toprightBound.x = math.select(toprightBound.x, FieldColAmount - 1, toprightBound.x >= FieldColAmount);
            toprightBound.y = math.select(toprightBound.y, FieldRowAmount - 1, toprightBound.y >= FieldRowAmount);

            Obstacle newObstacle = new Obstacle()
            {
                BotLeftBound = botLeftBound,
                TopRightBound = toprightBound,
                State = ObstacleState.Live,
            };

            CostEdit newCostEdit = new CostEdit()
            {
                BotLeftBound = botLeftBound,
                TopRightBound = toprightBound,
                EditType = CostEditType.Set,
            };

            int newObstacleIndex;
            if (RemovedObstacleIndexList.IsEmpty)
            {
                newObstacleIndex = ObstacleList.Length;
                ObstacleList.Add(newObstacle);
            }
            else
            {
                newObstacleIndex = RemovedObstacleIndexList[0];
                RemovedObstacleIndexList.RemoveAtSwapBack(0);
                ObstacleList[newObstacleIndex] = newObstacle;
            }

            CostEditOutput.Add(newCostEdit);
            NewObstacleKeyListToAdd.Add(newObstacleIndex);
        }
    }
}
