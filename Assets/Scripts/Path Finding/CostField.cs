using System;
using Unity.Collections;
using UnityEngine;
public class CostField
{
    public int Offset;
    public NativeArray<byte> Costs;
    public SectorGraph SectorGraph;
    public CostField(WalkabilityData walkabilityData, int offset, int sectorSize)
    {
        int tileAmount = walkabilityData.TileAmount;
        WalkabilityCell[][] walkabilityMatrix = walkabilityData.WalkabilityMatrix;
        Offset = offset;

        //configure costs
        Costs = new NativeArray<byte>(tileAmount * tileAmount, Allocator.Persistent);
        CalculateCosts();

        //create sector graph
        SectorGraph = new SectorGraph(sectorSize, tileAmount);

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
    }
}
