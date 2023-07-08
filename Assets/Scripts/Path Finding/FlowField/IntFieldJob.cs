using System.Runtime.ConstrainedExecution;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

[BurstCompile]
public struct IntFieldJob : IJob
{
    public int2 Target;
    public int FieldColAmount;
    public int FieldRowAmount;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public NativeArray<IntegrationFieldSector> IntegrationField;
    public NativeQueue<LocalIndex1d> IntegrationQueue;
    [ReadOnly] public NativeArray<int> SectorMarks;
    [ReadOnly] public NativeArray<UnsafeList<byte>> Costs;
    public void Execute()
    {
        Integrate();
    }
    void Integrate()
    {
        //DATA
        int sectorColAmount = SectorColAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int fieldColAmount = FieldColAmount;
        int sectorTileAmount = SectorColAmount * sectorColAmount;
        NativeArray<IntegrationFieldSector> integrationField = IntegrationField;
        NativeArray<int> sectorMarks = SectorMarks;
        NativeArray<UnsafeList<byte>> costs = Costs;
        NativeQueue<LocalIndex1d> integrationQueue = IntegrationQueue;

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
        int nwSector1d;
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
        while (!integrationQueue.IsEmpty())
        {
            LocalIndex1d cur = integrationQueue.Dequeue();
            SetLookupTable(cur.index, cur.sector);
            float newCost = GetCost();
            IntegrationTile tile = curSector[cur.index];
            tile.Cost = newCost;
            tile.Mark = IntegrationMark.Integrated;
            curIntCost = newCost;
            curSector[cur.index] = tile;
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
            int curSectorMark = sectorMarks[curSector1d];
            int nSectorMark = sectorMarks[nSector1d];
            int eSectorMark = sectorMarks[eSector1d];
            int sSectorMark = sectorMarks[sSector1d];
            int wSectorMark = sectorMarks[wSector1d];
            int neSectorMark = sectorMarks[neSector1d];
            int seSectorMark = sectorMarks[seSector1d];
            int swSectorMark = sectorMarks[swSector1d];
            int nwSectorMark = sectorMarks[nwSector1d];


            IntegrationMark nMark = IntegrationMark.None;
            IntegrationMark eMark = IntegrationMark.None;
            IntegrationMark sMark = IntegrationMark.None;
            IntegrationMark wMark = IntegrationMark.None;

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

            if (nSectorMark != 0) { nIntCost = nSector[nLocal1d].Cost; nMark = nSector[nLocal1d].Mark; }
            if (eSectorMark != 0) { eIntCost = eSector[eLocal1d].Cost; eMark = eSector[eLocal1d].Mark; }
            if (sSectorMark != 0) { sIntCost = sSector[sLocal1d].Cost; sMark = sSector[sLocal1d].Mark; }
            if (wSectorMark != 0) { wIntCost = wSector[wLocal1d].Cost; wMark = wSector[wLocal1d].Mark; }
            if (neSectorMark != 0) { neIntCost = neSector[neLocal1d].Cost; }
            if (seSectorMark != 0) { seIntCost = seSector[seLocal1d].Cost; }
            if (swSectorMark != 0) { swIntCost = swSector[swLocal1d].Cost; }
            if (nwSectorMark != 0) { nwIntCost = nwSector[nwLocal1d].Cost; }

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
                IntegrationTile tile = nSector[nLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                nSector[nLocal1d] = tile;
                integrationQueue.Enqueue(new LocalIndex1d(nLocal1d, nSector1d));
            }
            if (eAvailable && eDif > 2f)
            {
                IntegrationTile tile = eSector[eLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                eSector[eLocal1d] = tile;
                integrationQueue.Enqueue(new LocalIndex1d(eLocal1d, eSector1d));
            }
            if (sAvailable && sDif > 2f)
            {
                IntegrationTile tile = sSector[sLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                sSector[sLocal1d] = tile;
                integrationQueue.Enqueue(new LocalIndex1d(sLocal1d, sSector1d));
            }
            if (wAvailable && wDif > 2f)
            {
                IntegrationTile tile = wSector[wLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                wSector[wLocal1d] = tile;
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
    }
}