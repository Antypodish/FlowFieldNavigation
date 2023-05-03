using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

public class CostField
{
    public int Offset;
    public NativeArray<byte> Costs;
    public SectorGraph SectorGraph;
    public NativeArray<DirectionData> Directions;
    public CostField(WalkabilityData walkabilityData, int offset, int sectorSize)
    {
        int tileAmount = walkabilityData.TileAmount;
        WalkabilityCell[][] walkabilityMatrix = walkabilityData.WalkabilityMatrix;
        Offset = offset;

        //configure costs
        Costs = new NativeArray<byte>(tileAmount * tileAmount, Allocator.Persistent);
        CalculateCosts();

        //configure directions
        Directions = new NativeArray<DirectionData>(Costs.Length, Allocator.Persistent);
        CalculateDirections();



        //create sector graph
        SectorGraph = new SectorGraph(sectorSize, tileAmount, Costs, Directions);


        //HELPERS
        void CalculateCosts()
        {
            //calculate costs without offset
            for (int r = 0; r < tileAmount; r++)
            {
                for (int c = 0; c < tileAmount; c++)
                {
                    int index = r * tileAmount + c;
                    byte cost = walkabilityMatrix[r][c].Walkability == Walkability.Walkable ? (byte)1 : byte.MaxValue;
                    Costs[index] = cost;
                }
            }
            //apply offset
            for (int r = 0; r < tileAmount; r++)
            {
                for (int c = 0; c < tileAmount; c++)
                {
                    if (walkabilityMatrix[r][c].Walkability == Walkability.Unwalkable)
                    {
                        Index2 index = new Index2(r, c);
                        int minX = index.C - Offset < 0 ? 0 : index.C - Offset;
                        int maxX = index.C + Offset > tileAmount - 1 ? tileAmount - 1 : index.C + Offset;
                        int minY = index.R - Offset < 0 ? 0 : index.R - Offset;
                        int maxY = index.R + Offset > tileAmount - 1 ? tileAmount - 1 : index.R + Offset;

                        for (int row = minY; row <= maxY; row++)
                        {
                            for (int col = minX; col <= maxX; col++)
                            {
                                int i = row * tileAmount + col;
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
                Directions[i] = new DirectionData(i, tileAmount);
            }
        }
    }
}
