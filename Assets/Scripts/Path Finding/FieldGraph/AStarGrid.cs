using Unity.Collections;
using UnityEngine;

public struct AStarGrid
{
    int _tileAmount;
    NativeArray<AStarTile> _integratedCosts;
    NativeQueue<int> _searchQueue;
    
    public AStarGrid(int tileAmount)
    {
        _tileAmount = tileAmount;
        _integratedCosts = new NativeArray<AStarTile>(tileAmount * tileAmount, Allocator.Persistent);
        _searchQueue = new NativeQueue<int>(Allocator.Persistent);
    }

    public NativeArray<AStarTile> GetIntegratedCostsFor(Sector sector, Index2 target, NativeArray<byte> costs, NativeArray<DirectionData> directions)
    {
        Reset(sector, costs);
        int targetIndex = Index2.ToIndex(target, _tileAmount);

        AStarTile targetTile = _integratedCosts[targetIndex];
        targetTile.IntegratedCost = 0f;
        targetTile.Enqueued = true;
        _integratedCosts[targetIndex] = targetTile;
        Enqueue(directions[targetIndex]);
        
        while (!_searchQueue.IsEmpty())
        {
            int index = _searchQueue.Dequeue();
            AStarTile tile = _integratedCosts[index];
            tile.IntegratedCost = GetCost(directions[index]);
            _integratedCosts[index] = tile;
            Enqueue(directions[index]);
        }
        return _integratedCosts;
    }
    void Reset(Sector sector, NativeArray<byte> costs)
    {
        Index2 lowerBound = sector.StartIndex;
        Index2 upperBound = new Index2(sector.StartIndex.R + sector.Size -1 , sector.StartIndex.C + sector.Size - 1);
        int lowerBoundIndex = Index2.ToIndex(lowerBound, _tileAmount);
        int upperBoundIndex = Index2.ToIndex(upperBound, _tileAmount);

        for(int r = lowerBoundIndex; r < lowerBoundIndex + sector.Size * _tileAmount; r += _tileAmount)
        {
            for(int i = r; i < r + sector.Size; i++)
            {
                if (costs[i] == byte.MaxValue)
                {
                    _integratedCosts[i] = new AStarTile(float.MaxValue, true);
                    continue;
                }
                _integratedCosts[i] = new AStarTile(float.MaxValue, false);
            }
        }

        SetEdgesUnwalkable(sector, lowerBoundIndex, upperBoundIndex);
    }
    void SetEdgesUnwalkable(Sector sector, int lowerBoundIndex, int upperBoundIndex)
    {
        bool notOnBottom = !sector.IsOnBottom();
        bool notOnTop = !sector.IsOnTop(_tileAmount);
        bool notOnRight = !sector.IsOnRight(_tileAmount);
        bool notOnLeft = !sector.IsOnLeft();
        if (notOnBottom)
        {
            for (int i = lowerBoundIndex - _tileAmount; i < (lowerBoundIndex - _tileAmount) + sector.Size; i++)
            {
                _integratedCosts[i] = new AStarTile(float.MaxValue, true);
            }
        }
        if (notOnTop)
        {
            for (int i = upperBoundIndex + _tileAmount; i > upperBoundIndex + _tileAmount - sector.Size; i--)
            {
                _integratedCosts[i] = new AStarTile(float.MaxValue, true);
            }
        }
        if (notOnRight)
        {
            for (int i = upperBoundIndex + 1; i >= lowerBoundIndex + sector.Size; i -= _tileAmount)
            {
                _integratedCosts[i] = new AStarTile(float.MaxValue, true);
            }
        }
        if (notOnLeft)
        {
            for (int i = lowerBoundIndex - 1; i <= upperBoundIndex - sector.Size; i += _tileAmount)
            {
                _integratedCosts[i] = new AStarTile(float.MaxValue, true);
            }
        }
        if (notOnRight && notOnBottom)
        {
            _integratedCosts[lowerBoundIndex + sector.Size - _tileAmount] = new AStarTile(float.MaxValue, true);
        }
        if (notOnRight && notOnTop)
        {
            _integratedCosts[upperBoundIndex + _tileAmount + 1] = new AStarTile(float.MaxValue, true);
        }
        if (notOnLeft && notOnBottom)
        {
            _integratedCosts[lowerBoundIndex - _tileAmount - 1] = new AStarTile(float.MaxValue, true);
        }
        if (notOnLeft && notOnTop)
        {
            _integratedCosts[upperBoundIndex + _tileAmount - sector.Size] = new AStarTile(float.MaxValue, true);
        }
    }
    void Enqueue(DirectionData directions)
    {
        if (!_integratedCosts[directions.N].Enqueued)
        {
            _searchQueue.Enqueue(directions.N);
            AStarTile tile = _integratedCosts[directions.N];
            tile.Enqueued = true;
            _integratedCosts[directions.N] = tile;
        }
        if (!_integratedCosts[directions.E].Enqueued)
        {
            _searchQueue.Enqueue(directions.E);
            AStarTile tile = _integratedCosts[directions.E];
            tile.Enqueued = true;
            _integratedCosts[directions.E] = tile;
        }
        if (!_integratedCosts[directions.S].Enqueued)
        {
            _searchQueue.Enqueue(directions.S);
            AStarTile tile = _integratedCosts[directions.S];
            tile.Enqueued = true;
            _integratedCosts[directions.S] = tile;
        }
        if (!_integratedCosts[directions.W].Enqueued)
        {
            _searchQueue.Enqueue(directions.W);
            AStarTile tile = _integratedCosts[directions.W];
            tile.Enqueued = true;
            _integratedCosts[directions.W] = tile;
        }
    }
    float GetCost(DirectionData directions)
    {
        float costToReturn = float.MaxValue;
        if (_integratedCosts[directions.N].IntegratedCost +1f < costToReturn)
        {
            costToReturn = _integratedCosts[directions.N].IntegratedCost + 1f;
        }
        if (_integratedCosts[directions.NE].IntegratedCost + 1.4f < costToReturn)
        {
            costToReturn = _integratedCosts[directions.NE].IntegratedCost + 1.4f;
        }
        if (_integratedCosts[directions.E].IntegratedCost + 1f < costToReturn)
        {
            costToReturn = _integratedCosts[directions.E].IntegratedCost + 1f;
        }
        if (_integratedCosts[directions.SE].IntegratedCost + 1.4f < costToReturn)
        {
            costToReturn = _integratedCosts[directions.SE].IntegratedCost + 1.4f;
        }
        if (_integratedCosts[directions.S].IntegratedCost + 1f < costToReturn)
        {
            costToReturn = _integratedCosts[directions.S].IntegratedCost + 1f;
        }
        if (_integratedCosts[directions.SW].IntegratedCost + 1.4f < costToReturn)
        {
            costToReturn = _integratedCosts[directions.SW].IntegratedCost + 1.4f;
        }
        if (_integratedCosts[directions.W].IntegratedCost + 1f < costToReturn)
        {
            costToReturn = _integratedCosts[directions.W].IntegratedCost + 1f;
        }
        if (_integratedCosts[directions.NW].IntegratedCost + 1.4f < costToReturn)
        {
            costToReturn = _integratedCosts[directions.NW].IntegratedCost + 1.4f;
        }
        return costToReturn;
    }
}
public struct AStarTile
{
    public bool Enqueued; 
    public float IntegratedCost;

    public AStarTile(float integratedCost, bool enqueued)
    {
        Enqueued = enqueued;
        IntegratedCost = integratedCost;
    }
}