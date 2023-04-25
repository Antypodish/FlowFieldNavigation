using System;
using Unity.Collections;
using UnityEngine;
public class CostField
{
    public int Offset;
    public NativeArray<byte> Costs;
    public CostField(WalkabilityData walkabilityData, int offset)
    {
        WalkabilityCell[][] walkabilityMatrix = walkabilityData.WalkabilityMatrix;
        Offset = offset;
        int tileAmount = walkabilityData.TileAmount;
        Costs = new NativeArray<byte>(tileAmount * tileAmount, Allocator.Persistent);

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
        ApplyOffset();

        void ApplyOffset()
        {
            for (int r = 0; r < tileAmount; r++)
            {
                for (int c = 0; c < tileAmount; c++)
                {
                    if(walkabilityMatrix[r][c].Walkability == Walkability.Unwalkable)
                    {
                        ApplyOffsetFor(new Index2(r, c));
                    }
                }
            }

            void ApplyOffsetFor(Index2 index)
            {
                int minX = index.C - Offset < 0 ? 0 : index.C - Offset;
                int maxX = index.C + Offset > tileAmount - 1 ? tileAmount - 1 : index.C + Offset;
                int minY = index.R - Offset < 0 ? 0 : index.R - Offset;
                int maxY = index.R + Offset > tileAmount - 1 ? tileAmount - 1 : index.R + Offset;

                for (int r = minY; r <= maxY; r++)
                {
                    for (int c = minX; c <= maxX; c++)
                    {
                        int i = r * tileAmount + c;
                        Costs[i] = byte.MaxValue;
                    }
                }
            }
        }
    }
}
public struct Index2
{
    public int R;
    public int C;

    public Index2(int row, int column)
    {
        R = row;
        C = column;
    }
}