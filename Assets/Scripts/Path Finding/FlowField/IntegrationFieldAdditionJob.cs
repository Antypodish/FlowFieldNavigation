using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

[BurstCompile]
public struct IntegrationFieldAdditionJob : IJob
{
    public NativeList<LocalIndex1d> StartIndicies;
    public int FieldColAmount;
    public int FieldRowAmount;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public NativeList<IntegrationFieldSector> IntegrationField;
    public NativeQueue<LocalIndex1d> IntegrationQueue;
    [ReadOnly] public NativeArray<int> SectorMarks;
    [ReadOnly] public NativeArray<UnsafeList<LocalDirectionData1d>> LocalDirections;
    [ReadOnly] public NativeArray<UnsafeList<byte>> Costs;
    public void Execute()
    {
        Integrate();
    }
    void Integrate()
    {
        //DATA
        NativeList<IntegrationFieldSector> integrationField = IntegrationField;
        NativeArray<int> sectorMarks = SectorMarks;
        NativeArray<UnsafeList<byte>> costs = Costs;
        NativeArray<UnsafeList<LocalDirectionData1d>> localDirections = LocalDirections;
        NativeQueue<LocalIndex1d> integrationQueue = IntegrationQueue;
        int fieldColAmount = FieldColAmount;
        int sectorColAmount = SectorColAmount;
        int sectorTileAmount = sectorColAmount * sectorColAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;

        ///////////LOOKUP TABLE////////////////
        ///////////////////////////////////////
        LocalDirectionData1d directions;
        byte nCost;
        byte eCost;
        byte sCost;
        byte wCost;
        float curIntCost;
        float nIntCost;
        float eIntCost;
        float sIntCost;
        float wIntCost;
        float neIntCost;
        float seIntCost;
        float swIntCost;
        float nwIntCost;
        bool nAvailable;
        bool eAvailable;
        bool sAvailable;
        bool wAvailable;
        UnsafeList<IntegrationTile> curSector;
        UnsafeList<IntegrationTile> nSector;
        UnsafeList<IntegrationTile> eSector;
        UnsafeList<IntegrationTile> sSector;
        UnsafeList<IntegrationTile> wSector;
        UnsafeList<IntegrationTile> neSector;
        UnsafeList<IntegrationTile> seSector;
        UnsafeList<IntegrationTile> swSector;
        UnsafeList<IntegrationTile> nwSector;
        ///////////////////////////////////////////////
        //CODE

        for(int i = 0; i < StartIndicies.Length; i++)
        {
            integrationQueue.Enqueue(StartIndicies[i]);
        }
        while (!integrationQueue.IsEmpty())
        {
            LocalIndex1d cur = integrationQueue.Dequeue();
            SetLookupTable(cur.index, cur.sector);
            float newCost = GetCost();
            if ((curIntCost - newCost) < 1f) { continue; }
            IntegrationTile tile = curSector[cur.index];
            tile.Cost = newCost;
            curIntCost = newCost;
            curSector[cur.index] = tile;
            Enqueue();
        }
        //HELPERS
        void SetLookupTable(int curLocal1d, int curSector1d)
        {
            directions = localDirections[curSector1d][curLocal1d];
            //COSTS
            nCost = costs[directions.nSector][directions.n];
            eCost = costs[directions.eSector][directions.e];
            sCost = costs[directions.sSector][directions.s];
            wCost = costs[directions.wSector][directions.w];

            //SECTOR MARKS
            int curSectorMark = sectorMarks[curSector1d];
            int nSectorMark = sectorMarks[directions.nSector];
            int eSectorMark = sectorMarks[directions.eSector];
            int sSectorMark = sectorMarks[directions.sSector];
            int wSectorMark = sectorMarks[directions.wSector];
            int neSectorMark = sectorMarks[directions.neSector];
            int seSectorMark = sectorMarks[directions.seSector];
            int swSectorMark = sectorMarks[directions.swSector];
            int nwSectorMark = sectorMarks[directions.nwSector];

            //INTEGRATED COSTS
            curIntCost = integrationField[curSectorMark].integrationSector[curLocal1d].Cost;
            nIntCost = curIntCost;
            eIntCost = curIntCost;
            sIntCost = curIntCost;
            wIntCost = curIntCost;
            neIntCost = curIntCost;
            seIntCost = curIntCost;
            swIntCost = curIntCost;
            nwIntCost = curIntCost;

            curSector = integrationField[curSectorMark].integrationSector;
            nSector = integrationField[nSectorMark].integrationSector;
            eSector = integrationField[eSectorMark].integrationSector;
            sSector = integrationField[sSectorMark].integrationSector;
            wSector = integrationField[wSectorMark].integrationSector;
            neSector = integrationField[neSectorMark].integrationSector;
            seSector = integrationField[seSectorMark].integrationSector;
            swSector = integrationField[swSectorMark].integrationSector;
            nwSector = integrationField[nwSectorMark].integrationSector;

            IntegrationMark nMark = IntegrationMark.None;
            IntegrationMark eMark = IntegrationMark.None;
            IntegrationMark sMark = IntegrationMark.None;
            IntegrationMark wMark = IntegrationMark.None;

            if (nSectorMark != 0) { nIntCost = nSector[directions.n].Cost; nMark = nSector[directions.n].Mark; }
            if (eSectorMark != 0) { eIntCost = eSector[directions.e].Cost; eMark = eSector[directions.e].Mark; }
            if (sSectorMark != 0) { sIntCost = sSector[directions.s].Cost; sMark = sSector[directions.s].Mark; }
            if (wSectorMark != 0) { wIntCost = wSector[directions.w].Cost; wMark = wSector[directions.w].Mark; }
            if (neSectorMark != 0) { neIntCost = neSector[directions.ne].Cost; }
            if (seSectorMark != 0) { seIntCost = seSector[directions.se].Cost; }
            if (swSectorMark != 0) { swIntCost = swSector[directions.sw].Cost; }
            if (nwSectorMark != 0) { nwIntCost = nwSector[directions.nw].Cost; }

            //AVAILABILITY
            nAvailable = nCost != byte.MaxValue && nMark != IntegrationMark.LOSPass && nSectorMark != 0;
            eAvailable = eCost != byte.MaxValue && eMark != IntegrationMark.LOSPass && eSectorMark != 0;
            sAvailable = sCost != byte.MaxValue && sMark != IntegrationMark.LOSPass && sSectorMark != 0;
            wAvailable = wCost != byte.MaxValue && wMark != IntegrationMark.LOSPass && wSectorMark != 0;
        }
        void Enqueue()
        {
            float nDif = nIntCost - curIntCost;
            float eDif = eIntCost - curIntCost;
            float sDif = sIntCost - curIntCost;
            float wDif = wIntCost - curIntCost;
            if (nAvailable && nDif > 1f)
            {
                integrationQueue.Enqueue(new LocalIndex1d(directions.n, directions.nSector));
            }
            if (eAvailable && eDif > 1f)
            {
                integrationQueue.Enqueue(new LocalIndex1d(directions.e, directions.eSector));
            }
            if (sAvailable && sDif > 1f)
            {
                integrationQueue.Enqueue(new LocalIndex1d(directions.s, directions.sSector));
            }
            if (wAvailable && wDif > 1f)
            {
                integrationQueue.Enqueue(new LocalIndex1d(directions.w, directions.wSector));
            }
        }
        float GetCost()
        {
            float costToReturn = float.MaxValue;
            float nCost = nIntCost + 1f;
            float eCost = eIntCost + 1f;
            float sCost = sIntCost + 1f;
            float wCost = wIntCost + 1f;
            float neCost = neIntCost + 1.4f;
            float seCost = seIntCost + 1.4f;
            float swCost = swIntCost + 1.4f;
            float nwCost = nwIntCost + 1.4f;

            costToReturn = math.select(costToReturn, nCost, nCost < costToReturn);
            costToReturn = math.select(costToReturn, eCost, eCost < costToReturn);
            costToReturn = math.select(costToReturn, sCost, sCost < costToReturn);
            costToReturn = math.select(costToReturn, wCost, wCost < costToReturn);
            costToReturn = math.select(costToReturn, neCost, neCost < costToReturn);
            costToReturn = math.select(costToReturn, seCost, seCost < costToReturn);
            costToReturn = math.select(costToReturn, swCost, swCost < costToReturn);
            costToReturn = math.select(costToReturn, nwCost, nwCost < costToReturn);
            return costToReturn;
        }
        int To1D(int2 index2, int colAmount)
        {
            return index2.y * colAmount + index2.x;
        }
        int2 To2D(int index, int colAmount)
        {
            return new int2(index % colAmount, index / colAmount);
        }
        int2 GetSectorIndex(int2 index)
        {
            return new int2(index.x / sectorColAmount, index.y / sectorColAmount);
        }
        int2 GetLocalIndex(int2 index, int2 sectorStartIndex)
        {
            return index - sectorStartIndex;
        }
        int2 GetSectorStartIndex(int2 sectorIndex)
        {
            return new int2(sectorIndex.x * sectorColAmount, sectorIndex.y * sectorColAmount);
        }
        int GetGeneralIndex1d(int local1d, int sector1d)
        {
            int2 sector2d = To2D(sector1d, sectorMatrixColAmount);
            int2 sectorStartIndex = GetSectorStartIndex(sector2d);
            int2 local2d = To2D(local1d, sectorColAmount);
            return To1D(sectorStartIndex + local2d, fieldColAmount);
        }
    }
}
