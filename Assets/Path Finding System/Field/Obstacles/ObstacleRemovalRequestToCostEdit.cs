using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
public struct ObstacleRemovalRequestToCostEdit : IJob
{
    public NativeArray<int>.ReadOnly ObstacleRemovalIndicies;
    public NativeList<CostEdit> CostEditOutput;
    public NativeList<Obstacle> ObstacleList;
    public NativeList<int> RemovedObstacleIndexList;

    public void Execute()
    {
        for (int i = 0; i < ObstacleRemovalIndicies.Length; i++)
        {
            int indexToRemove = ObstacleRemovalIndicies[i];
            if (indexToRemove < 0 || indexToRemove >= ObstacleList.Length) { continue; }
            Obstacle obstacleToRemove = ObstacleList[indexToRemove];
            if (obstacleToRemove.State == ObstacleState.Removed) { continue; }
            obstacleToRemove.State = ObstacleState.Removed;
            ObstacleList[indexToRemove] = obstacleToRemove;
            RemovedObstacleIndexList.Add(indexToRemove);
            CostEdit removeEdit = new CostEdit()
            {
                BotLeftBound = obstacleToRemove.BotLeftBound,
                TopRightBound = obstacleToRemove.TopRightBound,
                EditType = CostEditType.Clear,
            };
            CostEditOutput.Add(removeEdit);
        }
    }
}

