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
    public NativeArray<IntegrationTile> IntegrationField;
    [ReadOnly] public UnsafeList<int> SectorToPicked;
    [ReadOnly] public NativeArray<UnsafeList<byte>> Costs;
    public void Execute()
    {
        Integrate();
    }
    void Integrate()
    {
        //DATA
        NativeArray<IntegrationTile> integrationField = IntegrationField;
        UnsafeList<int> sectorMarks = SectorToPicked;
        NativeArray<UnsafeList<byte>> costs = Costs;
        NativeQueue<LocalIndex1d> integrationQueue = new NativeQueue<LocalIndex1d>(Allocator.Temp);
        int fieldColAmount = FieldColAmount;
        int sectorColAmount = SectorColAmount;
        int sectorTileAmount = sectorColAmount * sectorColAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;

        ///////////LOOKUP TABLE////////////////
        ///////////////////////////////////////
        int nLocal1d;
        int eLocal1d;
        int sLocal1d;
        int wLocal1d;
        int neLocal1d;
        int seLocal1d;
        int swLocal1d;
        int nwLocal1d;
        int nSector1d;
        int eSector1d;
        int sSector1d;
        int wSector1d;
        int neSector1d;
        int seSector1d;
        int swSector1d;
        int nwSector1d; byte nCost;
        byte eCost;
        byte sCost;
        byte wCost;
        int curSectorMark;
        int nSectorMark;
        int eSectorMark;
        int sSectorMark;
        int wSectorMark;
        int neSectorMark;
        int seSectorMark;
        int swSectorMark;
        int nwSectorMark;
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
            IntegrationTile tile = integrationField[curSectorMark + cur.index];
            tile.Cost = newCost;
            tile.Mark = IntegrationMark.Integrated;
            curIntCost = newCost;
            integrationField[curSectorMark + cur.index] = tile;
            Enqueue();
        }
        //HELPERS
        void SetLookupTable(int curLocal1d, int curSector1d)
        {
            //LOCAL INDICIES
            nLocal1d = curLocal1d + sectorColAmount;
            eLocal1d = curLocal1d + 1;
            sLocal1d = curLocal1d - sectorColAmount;
            wLocal1d = curLocal1d - 1;
            neLocal1d = nLocal1d + 1;
            seLocal1d = sLocal1d + 1;
            swLocal1d = sLocal1d - 1;
            nwLocal1d = nLocal1d - 1;

            //OVERFLOWS
            bool nLocalOverflow = nLocal1d >= sectorTileAmount;
            bool eLocalOverflow = (eLocal1d % sectorColAmount) == 0;
            bool sLocalOverflow = sLocal1d < 0;
            bool wLocalOverflow = (curLocal1d % sectorColAmount) == 0;

            //SECTOR INDICIES
            nSector1d = math.select(curSector1d, curSector1d + sectorMatrixColAmount, nLocalOverflow);
            eSector1d = math.select(curSector1d, curSector1d + 1, eLocalOverflow);
            sSector1d = math.select(curSector1d, curSector1d - sectorMatrixColAmount, sLocalOverflow);
            wSector1d = math.select(curSector1d, curSector1d - 1, wLocalOverflow);
            neSector1d = math.select(curSector1d, curSector1d + sectorMatrixColAmount, nLocalOverflow);
            neSector1d = math.select(neSector1d, neSector1d + 1, eLocalOverflow);
            seSector1d = math.select(curSector1d, curSector1d - sectorMatrixColAmount, sLocalOverflow);
            seSector1d = math.select(seSector1d, seSector1d + 1, eLocalOverflow);
            swSector1d = math.select(curSector1d, curSector1d - sectorMatrixColAmount, sLocalOverflow);
            swSector1d = math.select(swSector1d, swSector1d - 1, wLocalOverflow);
            nwSector1d = math.select(curSector1d, curSector1d + sectorMatrixColAmount, nLocalOverflow);
            nwSector1d = math.select(nwSector1d, nwSector1d - 1, wLocalOverflow);


            nLocal1d = math.select(nLocal1d, curLocal1d - (sectorColAmount * sectorColAmount - sectorColAmount), nLocalOverflow);
            eLocal1d = math.select(eLocal1d, curLocal1d - sectorColAmount + 1, eLocalOverflow);
            sLocal1d = math.select(sLocal1d, curLocal1d + (sectorColAmount * sectorColAmount - sectorColAmount), sLocalOverflow);
            wLocal1d = math.select(wLocal1d, curLocal1d + sectorColAmount - 1, wLocalOverflow);
            neLocal1d = math.select(neLocal1d, neLocal1d - (sectorColAmount * sectorColAmount), nLocalOverflow);
            neLocal1d = math.select(neLocal1d, neLocal1d - sectorColAmount, eLocalOverflow);
            seLocal1d = math.select(seLocal1d, seLocal1d + (sectorColAmount * sectorColAmount), sLocalOverflow);
            seLocal1d = math.select(seLocal1d, seLocal1d - sectorColAmount, eLocalOverflow);
            swLocal1d = math.select(swLocal1d, swLocal1d + (sectorColAmount * sectorColAmount), sLocalOverflow);
            swLocal1d = math.select(swLocal1d, swLocal1d + sectorColAmount, wLocalOverflow);
            nwLocal1d = math.select(nwLocal1d, nwLocal1d - (sectorColAmount * sectorColAmount), nLocalOverflow);
            nwLocal1d = math.select(nwLocal1d, nwLocal1d + sectorColAmount, wLocalOverflow);

            //COSTS
            nCost = costs[nSector1d][nLocal1d];
            eCost = costs[eSector1d][eLocal1d];
            sCost = costs[sSector1d][sLocal1d];
            wCost = costs[wSector1d][wLocal1d];

            //SECTOR MARKS
            curSectorMark = sectorMarks[curSector1d];
            nSectorMark = sectorMarks[nSector1d];
            eSectorMark = sectorMarks[eSector1d];
            sSectorMark = sectorMarks[sSector1d];
            wSectorMark = sectorMarks[wSector1d];
            neSectorMark = sectorMarks[neSector1d];
            seSectorMark = sectorMarks[seSector1d];
            swSectorMark = sectorMarks[swSector1d];
            nwSectorMark = sectorMarks[nwSector1d];


            IntegrationMark nMark = IntegrationMark.None;
            IntegrationMark eMark = IntegrationMark.None;
            IntegrationMark sMark = IntegrationMark.None;
            IntegrationMark wMark = IntegrationMark.None;

            //INTEGRATED COSTS
            curIntCost = integrationField[curSectorMark + curLocal1d].Cost;
            nIntCost = curIntCost;
            eIntCost = curIntCost;
            sIntCost = curIntCost;
            wIntCost = curIntCost;
            neIntCost = curIntCost;
            seIntCost = curIntCost;
            swIntCost = curIntCost;
            nwIntCost = curIntCost;

            if (nSectorMark != 0) { nIntCost = integrationField[nSectorMark + nLocal1d].Cost; nMark = integrationField[nSectorMark + nLocal1d].Mark; }
            if (eSectorMark != 0) { eIntCost = integrationField[eSectorMark + eLocal1d].Cost; eMark = integrationField[eSectorMark + eLocal1d].Mark; }
            if (sSectorMark != 0) { sIntCost = integrationField[sSectorMark + sLocal1d].Cost; sMark = integrationField[sSectorMark + sLocal1d].Mark; }
            if (wSectorMark != 0) { wIntCost = integrationField[wSectorMark + wLocal1d].Cost; wMark = integrationField[wSectorMark + wLocal1d].Mark; }
            if (neSectorMark != 0) { neIntCost = integrationField[neSectorMark + neLocal1d].Cost; }
            if (seSectorMark != 0) { seIntCost = integrationField[seSectorMark + seLocal1d].Cost; }
            if (swSectorMark != 0) { swIntCost = integrationField[swSectorMark + swLocal1d].Cost; }
            if (nwSectorMark != 0) { nwIntCost = integrationField[nwSectorMark + nwLocal1d].Cost; }

            //AVAILABILITY
            nAvailable = nCost != byte.MaxValue && (nMark == IntegrationMark.Integrated || nMark == IntegrationMark.None) && nSectorMark != 0;
            eAvailable = eCost != byte.MaxValue && (eMark == IntegrationMark.Integrated || eMark == IntegrationMark.None) && eSectorMark != 0;
            sAvailable = sCost != byte.MaxValue && (sMark == IntegrationMark.Integrated || sMark == IntegrationMark.None) && sSectorMark != 0;
            wAvailable = wCost != byte.MaxValue && (wMark == IntegrationMark.Integrated || wMark == IntegrationMark.None) && wSectorMark != 0;
        }
        void Enqueue()
        {
            float nDif = nIntCost - curIntCost;
            float eDif = eIntCost - curIntCost;
            float sDif = sIntCost - curIntCost;
            float wDif = wIntCost - curIntCost;
            if (nAvailable && nDif > 2f)
            {
                IntegrationTile tile = integrationField[nSectorMark + nLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[nSectorMark + nLocal1d] = tile;
                integrationQueue.Enqueue(new LocalIndex1d(nLocal1d, nSector1d));
            }
            if (eAvailable && eDif > 2f)
            {
                IntegrationTile tile = integrationField[eSectorMark + eLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[eSectorMark + eLocal1d] = tile;
                integrationQueue.Enqueue(new LocalIndex1d(eLocal1d, eSector1d));
            }
            if (sAvailable && sDif > 2f)
            {
                IntegrationTile tile = integrationField[sSectorMark + sLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[sSectorMark + sLocal1d] = tile;
                integrationQueue.Enqueue(new LocalIndex1d(sLocal1d, sSector1d));
            }
            if (wAvailable && wDif > 2f)
            {
                IntegrationTile tile = integrationField[wSectorMark + wLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[wSectorMark + wLocal1d] = tile;
                integrationQueue.Enqueue(new LocalIndex1d(wLocal1d, wSector1d));
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
