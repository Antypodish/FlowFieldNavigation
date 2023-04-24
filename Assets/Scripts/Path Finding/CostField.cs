using Unity.Collections;
using UnityEngine;
public class CostField
{
    public readonly int Size;
    const int _precalculationAmount = 10;

    NativeArray<CostCell> _sampleCosts;
    LinkedList<NativeArray<CostCell>> _precalculatedCosts;
    /*public CostField(MapperCell[][] map)
    {
        Size = map.Length;
        _sampleCosts = new NativeArray<CostCell>(Size * Size, Allocator.Persistent);

        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                int index = r * Size + c;
                int cost = map[r][c].Walkability == Walkability.Walkable ? 1 : int.MaxValue;
                AdjacentData adjacents = new AdjacentData(index, Size);
                DiagonalData diagonals = new DiagonalData(index, Size);
                _sampleCosts[index] = new CostCell(cost, adjacents, diagonals);
            }
        }
        _precalculatedCosts = new LinkedList<NativeArray<CostCell>>();
        for (int i = 0; i < _precalculationAmount; i++)
        {
            _precalculatedCosts.AddToTail(new NativeArray<CostCell>(_sampleCosts, Allocator.Persistent));
        }
    }*/
    public NativeArray<CostCell> GetCosts()
    {
        return _precalculatedCosts.PushHeadToTail();
    }
}
public struct CostCell
{
    public int Cost;
    public AdjacentData Adjacents;
    public DiagonalData Diagonals;

    public CostCell(int cost, AdjacentData adjacentIndicies, DiagonalData diagonalIndicies)
    {
        Cost = cost;
        Adjacents = adjacentIndicies;
        Diagonals = diagonalIndicies;
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