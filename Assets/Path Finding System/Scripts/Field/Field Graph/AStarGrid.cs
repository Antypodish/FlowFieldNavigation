using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public struct AStarGrid
{
    int _rowAmount;
    int _colAmount;
    public NativeArray<AStarTile> _integratedCosts;
    public NativeQueue<int> _searchQueue;

    public AStarGrid(int rowAmount, int colAmount)
    {
        _rowAmount = rowAmount;
        _colAmount = colAmount;
        _integratedCosts = new NativeArray<AStarTile>(rowAmount * colAmount, Allocator.Persistent);
        _searchQueue = new NativeQueue<int>(Allocator.Persistent);
    }
    public void DisposeNatives()
    {
        _integratedCosts.Dispose();
        _searchQueue.Dispose();
    }
    public NativeArray<AStarTile> GetIntegratedCostsFor(Sector sector, Index2 target, UnsafeList<byte> costs)
    {
        int targetIndex = Index2.ToIndex(target, _colAmount);
        NativeArray<AStarTile> integratedCosts = _integratedCosts;
        NativeQueue<int> searchQueue = _searchQueue;
        int colAmount = _colAmount;
        int rowAmount = _rowAmount;

        /////////////LOOKUP TABLE/////////////////
        //////////////////////////////////////////
        int n;
        int e;
        int s;
        int w;
        int ne;
        int se;
        int sw;
        int nw;
        //////////////////////////////////////////


        Reset(sector, costs);
        AStarTile targetTile = _integratedCosts[targetIndex];
        targetTile.IntegratedCost = 0f;
        targetTile.Enqueued = true;
        _integratedCosts[targetIndex] = targetTile;
        SetLookupTable(targetIndex);
        Enqueue();

        while (!_searchQueue.IsEmpty())
        {
            int index = _searchQueue.Dequeue();
            AStarTile tile = _integratedCosts[index];
            SetLookupTable(index);
            tile.IntegratedCost = GetCost();
            _integratedCosts[index] = tile;
            Enqueue();
        }
        return _integratedCosts;

        void SetLookupTable(int index)
        {
            n = index + colAmount;
            e = index + 1;
            s = index - colAmount;
            w = index - 1;
            ne = n + 1;
            se = s + 1;
            sw = s - 1;
            nw = n - 1;
        }
        void Reset(Sector sector, UnsafeList<byte> costs)
        {
            Index2 lowerBound = sector.StartIndex;
            Index2 upperBound = new Index2(sector.StartIndex.R + sector.Size - 1, sector.StartIndex.C + sector.Size - 1);
            int lowerBoundIndex = Index2.ToIndex(lowerBound, colAmount);
            int upperBoundIndex = Index2.ToIndex(upperBound, colAmount);

            for (int r = lowerBoundIndex; r < lowerBoundIndex + sector.Size * colAmount; r += colAmount)
            {
                for (int i = r; i < r + sector.Size; i++)
                {
                    if (costs[i] == byte.MaxValue)
                    {
                        integratedCosts[i] = new AStarTile(float.MaxValue, true);
                        continue;
                    }
                    integratedCosts[i] = new AStarTile(float.MaxValue, false);
                }
            }
            SetEdgesUnwalkable(sector, lowerBoundIndex, upperBoundIndex);
        }
        void SetEdgesUnwalkable(Sector sector, int lowerBoundIndex, int upperBoundIndex)
        {
            bool notOnBottom = !sector.IsOnBottom();
            bool notOnTop = !sector.IsOnTop(rowAmount);
            bool notOnRight = !sector.IsOnRight(colAmount);
            bool notOnLeft = !sector.IsOnLeft();
            if (notOnBottom)
            {
                for (int i = lowerBoundIndex - colAmount; i < (lowerBoundIndex - colAmount) + sector.Size; i++)
                {
                    integratedCosts[i] = new AStarTile(float.MaxValue, true);
                }
            }
            if (notOnTop)
            {
                for (int i = upperBoundIndex + colAmount; i > upperBoundIndex + colAmount - sector.Size; i--)
                {
                    integratedCosts[i] = new AStarTile(float.MaxValue, true);
                }
            }
            if (notOnRight)
            {
                for (int i = upperBoundIndex + 1; i >= lowerBoundIndex + sector.Size; i -= colAmount)
                {
                    integratedCosts[i] = new AStarTile(float.MaxValue, true);
                }
            }
            if (notOnLeft)
            {
                for (int i = lowerBoundIndex - 1; i <= upperBoundIndex - sector.Size; i += colAmount)
                {
                    integratedCosts[i] = new AStarTile(float.MaxValue, true);
                }
            }
            if (notOnRight && notOnBottom)
            {
                integratedCosts[lowerBoundIndex + sector.Size - colAmount] = new AStarTile(float.MaxValue, true);
            }
            if (notOnRight && notOnTop)
            {
                integratedCosts[upperBoundIndex + colAmount + 1] = new AStarTile(float.MaxValue, true);
            }
            if (notOnLeft && notOnBottom)
            {
                integratedCosts[lowerBoundIndex - colAmount - 1] = new AStarTile(float.MaxValue, true);
            }
            if (notOnLeft && notOnTop)
            {
                integratedCosts[upperBoundIndex + colAmount - sector.Size] = new AStarTile(float.MaxValue, true);
            }
        }
        void Enqueue()
        {
            if (!integratedCosts[n].Enqueued)
            {
                searchQueue.Enqueue(n);
                AStarTile tile = integratedCosts[n];
                tile.Enqueued = true;
                integratedCosts[n] = tile;
            }
            if (!integratedCosts[e].Enqueued)
            {
                searchQueue.Enqueue(e);
                AStarTile tile = integratedCosts[e];
                tile.Enqueued = true;
                integratedCosts[e] = tile;
            }
            if (!integratedCosts[s].Enqueued)
            {
                searchQueue.Enqueue(s);
                AStarTile tile = integratedCosts[s];
                tile.Enqueued = true;
                integratedCosts[s] = tile;
            }
            if (!integratedCosts[w].Enqueued)
            {
                searchQueue.Enqueue(w);
                AStarTile tile = integratedCosts[w];
                tile.Enqueued = true;
                integratedCosts[w] = tile;
            }
        }
        float GetCost()
        {
            float costToReturn = float.MaxValue;
            float nCost = integratedCosts[n].IntegratedCost + 1f;
            float neCost = integratedCosts[ne].IntegratedCost + 1.4f;
            float eCost = integratedCosts[e].IntegratedCost + 1f;
            float seCost = integratedCosts[se].IntegratedCost + 1.4f;
            float sCost = integratedCosts[s].IntegratedCost + 1f;
            float swCost = integratedCosts[sw].IntegratedCost + 1.4f;
            float wCost = integratedCosts[w].IntegratedCost + 1f;
            float nwCost = integratedCosts[nw].IntegratedCost + 1.4f;
            if (nCost < costToReturn) { costToReturn = nCost; }
            if (neCost < costToReturn) { costToReturn = neCost; }
            if (eCost < costToReturn) { costToReturn = eCost; }
            if (seCost < costToReturn) { costToReturn = seCost; }
            if (sCost < costToReturn) { costToReturn = sCost; }
            if (swCost < costToReturn) { costToReturn = swCost; }
            if (wCost < costToReturn) { costToReturn = wCost; }
            if (nwCost < costToReturn) { costToReturn = nwCost; }
            return costToReturn;
        }
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