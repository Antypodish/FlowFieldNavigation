using Unity.Collections;
using Unity.Mathematics;

namespace FlowFieldNavigation
{
    internal class ObstacleContainer
    {
        internal NativeList<Obstacle> ObstacleList;
        internal NativeList<int> RemovedIndexList;

        internal ObstacleContainer()
        {
            ObstacleList = new NativeList<Obstacle>(Allocator.Persistent);
            RemovedIndexList = new NativeList<int>(Allocator.Persistent);
        }

        internal (int obstacleIndex, CostEdit costEdit) AddObstacleAndGetIndexAndCostEdit(ObstacleRequest obstacleRequest)
        {
            float fieldMinXIncluding = FlowFieldUtilities.FieldMinXIncluding;
            float fieldMinYIncluding = FlowFieldUtilities.FieldMinYIncluding;
            float fieldMaxXExcluding = FlowFieldUtilities.FieldMaxXExcluding;
            float fieldMaxYExcluding = FlowFieldUtilities.FieldMaxYExcluding;
            float tileSize = FlowFieldUtilities.TileSize;
            float2 fieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition;
            int fieldColAmount = FlowFieldUtilities.FieldColAmount;
            int fieldRowAmount = FlowFieldUtilities.FieldRowAmount;
            float2 pos2d = obstacleRequest.Position;
            float2 halfSize = obstacleRequest.HalfSize;
            
            //if (pos2d.x < fieldMinXIncluding || pos2d.y < fieldMinYIncluding || pos2d.x >= fieldMaxXExcluding || pos2d.y >= fieldMaxYExcluding) { continue; }

            float2 botLeft = pos2d - halfSize;
            float2 topRight = pos2d + halfSize;
            int2 botLeftBound = FlowFieldUtilities.PosTo2D(botLeft, tileSize, fieldGridStartPos);
            int2 toprightBound = FlowFieldUtilities.PosTo2D(topRight, tileSize, fieldGridStartPos);
            botLeftBound.x = math.select(botLeftBound.x, 0, botLeftBound.x < 0);
            botLeftBound.y = math.select(botLeftBound.y, 0, botLeftBound.y < 0);
            toprightBound.x = math.select(toprightBound.x, fieldColAmount - 1, toprightBound.x >= fieldColAmount);
            toprightBound.y = math.select(toprightBound.y, fieldRowAmount - 1, toprightBound.y >= fieldRowAmount);

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
            if (RemovedIndexList.IsEmpty)
            {
                newObstacleIndex = ObstacleList.Length;
                ObstacleList.Add(newObstacle);
            }
            else
            {
                newObstacleIndex = RemovedIndexList[0];
                RemovedIndexList.RemoveAtSwapBack(0);
                ObstacleList[newObstacleIndex] = newObstacle;
            }
            return (newObstacleIndex, newCostEdit);
        }
        internal CostEdit RemoveObstacleAndGetCostEdit(int obstacleIndex)
        {
            Obstacle obstacleToRemove = ObstacleList[obstacleIndex];
            obstacleToRemove.State = ObstacleState.Removed;
            ObstacleList[obstacleIndex] = obstacleToRemove;
            RemovedIndexList.Add(obstacleIndex);
            return new CostEdit()
            {
                BotLeftBound = obstacleToRemove.BotLeftBound,
                TopRightBound = obstacleToRemove.TopRightBound,
                EditType = CostEditType.Clear,
            };
        }
        internal void DisposeAll()
        {
            ObstacleList.Dispose();
            RemovedIndexList.Dispose();
        }
    }

}