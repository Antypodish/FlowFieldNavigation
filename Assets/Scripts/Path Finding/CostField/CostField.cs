using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

public class CostField
{
    public int Offset;
    public NativeArray<byte> Costs;
    public FieldGraph FieldGraph;
    public NativeArray<DirectionData> Directions;

    float _fieldTileSize;
    JobHandle _fieldGraphConfigJobHandle;
    public CostField(WalkabilityData walkabilityData, int offset, int sectorSize)
    {
        _fieldTileSize = walkabilityData.TileSize;
        int rowAmount = walkabilityData.RowAmount;
        int colAmount = walkabilityData.ColAmount;
        WalkabilityCell[][] walkabilityMatrix = walkabilityData.WalkabilityMatrix;
        Offset = offset;

        //configure costs
        Costs = new NativeArray<byte>(rowAmount * colAmount, Allocator.Persistent);
        CalculateCosts();

        //configure directions
        Directions = new NativeArray<DirectionData>(Costs.Length, Allocator.Persistent);
        CalculateDirections();



        //allocate field graph
        FieldGraph = new FieldGraph(Costs, Directions, sectorSize, rowAmount, colAmount, offset, _fieldTileSize);


        //HELPERS
        void CalculateCosts()
        {
            //calculate costs without offset
            for (int r = 0; r < rowAmount; r++)
            {
                for (int c = 0; c < colAmount; c++)
                {
                    int index = r * colAmount + c;
                    byte cost = walkabilityMatrix[r][c].Walkability == Walkability.Walkable ? (byte)1 : byte.MaxValue;
                    Costs[index] = cost;
                }
            }
            //apply offset
            for (int r = 0; r < rowAmount; r++)
            {
                for (int c = 0; c < colAmount; c++)
                {
                    if (walkabilityMatrix[r][c].Walkability == Walkability.Unwalkable)
                    {
                        Index2 index = new Index2(r, c);
                        int minX = index.C - Offset < 0 ? 0 : index.C - Offset;
                        int maxX = index.C + Offset > colAmount - 1 ? colAmount - 1 : index.C + Offset;
                        int minY = index.R - Offset < 0 ? 0 : index.R - Offset;
                        int maxY = index.R + Offset > rowAmount - 1 ? rowAmount - 1 : index.R + Offset;

                        for (int row = minY; row <= maxY; row++)
                        {
                            for (int col = minX; col <= maxX; col++)
                            {
                                int i = row * colAmount + col;
                                Costs[i] = byte.MaxValue;
                            }
                        }
                    }
                }
            }
        }
        void CalculateDirections()
        {
            for(int i = 0; i < Directions.Length; i++)
            {
                Directions[i] = new DirectionData(i, rowAmount, colAmount);
            }
        }
    }
    public void ScheduleConfigurationJob()
    {
        FieldGraphConfigurationJob _fieldGraphConfigJob = FieldGraph.GetConfigJob();
        _fieldGraphConfigJobHandle = _fieldGraphConfigJob.Schedule();
    }
    public void EndConfigurationJobIfCompleted()
    {
        if (_fieldGraphConfigJobHandle.IsCompleted)
        {
            _fieldGraphConfigJobHandle.Complete();
        }
    }
    public void ForceCompleteConigurationJob()
    {
        _fieldGraphConfigJobHandle.Complete();
    }
    public CostFieldEditJob GetEditJob(Index2 bound1, Index2 bound2, byte newCost)
    {
        return FieldGraph.GetEditJob(bound1, bound2, newCost);
    }
}