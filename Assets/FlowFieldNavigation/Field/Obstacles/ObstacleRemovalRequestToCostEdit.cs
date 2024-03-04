using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct ObstacleRemovalRequestToCostEdit : IJob
    {
        internal NativeArray<int>.ReadOnly ObstacleRemovalIndicies;
        internal NativeList<CostEdit> CostEditOutput;
        internal NativeList<Obstacle> ObstacleList;
        internal NativeList<ObstacleState> ObstacleStates;
        internal NativeList<int> RemovedObstacleIndexList;
         
        public void Execute()
        {
            for (int i = 0; i < ObstacleRemovalIndicies.Length; i++)
            {
                int indexToRemove = ObstacleRemovalIndicies[i];
                if (indexToRemove < 0 || indexToRemove >= ObstacleList.Length) { continue; }
                ObstacleState obstacleState = ObstacleStates[indexToRemove];
                if (obstacleState == ObstacleState.Removed) { continue; }
                ObstacleStates[indexToRemove] = ObstacleState.Removed;
                Obstacle obstacleToRemove = ObstacleList[indexToRemove];
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


}
