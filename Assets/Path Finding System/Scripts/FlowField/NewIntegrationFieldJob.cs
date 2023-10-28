using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct NewIntegrationFieldJob : IJob
{
    public int SectorIndex;
    [ReadOnly] public UnsafeList<ActiveWaveFront> StartIndicies;
    public int FieldColAmount;
    public int FieldRowAmount;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    [NativeDisableContainerSafetyRestriction] public NativeSlice<IntegrationTile> IntegrationField;
    [ReadOnly] public UnsafeList<int> SectorToPicked;
    [ReadOnly] public UnsafeList<byte> Costs;
    public void Execute()
    {
        Integrate();
    }
    void Integrate()
    {
        //DATA
        NativeSlice<IntegrationTile> integrationField = IntegrationField;
        UnsafeList<int> sectorMarks = SectorToPicked;
        UnsafeList<byte> costs = Costs;
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
        int nwSector1d;
        bool nBlocked;
        bool eBlocked;
        bool sBlocked;
        bool wBlocked;
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
            ActiveWaveFront front = StartIndicies[i];
            integrationField[front.LocalIndex] = new IntegrationTile()
            {
                Cost = front.Distance,
                Mark = IntegrationMark.Integrated,
            };
        }
        for (int i = 0; i < StartIndicies.Length; i++)
        {
            int index = StartIndicies[i].LocalIndex;
            SetLookupTable(index);
            Enqueue();
        }
        StartIndicies.Clear();
        while (!integrationQueue.IsEmpty())
        {
            LocalIndex1d cur = integrationQueue.Dequeue();
            SetLookupTable(cur.index);
            float newCost = GetCost();
            IntegrationTile tile = integrationField[cur.index];
            tile.Cost = newCost;
            tile.Mark = IntegrationMark.Integrated;
            curIntCost = newCost;
            integrationField[cur.index] = tile;
            Enqueue();
        }
        //HELPERS
        void SetLookupTable(int curLocal1d)
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

            nLocal1d = math.select(nLocal1d, curLocal1d, nLocalOverflow);
            eLocal1d = math.select(eLocal1d, curLocal1d, eLocalOverflow);
            sLocal1d = math.select(sLocal1d, curLocal1d, sLocalOverflow);
            wLocal1d = math.select(wLocal1d, curLocal1d, wLocalOverflow);
            neLocal1d = math.select(neLocal1d, curLocal1d, nLocalOverflow || eLocalOverflow);
            seLocal1d = math.select(seLocal1d, curLocal1d, sLocalOverflow || eLocalOverflow);
            swLocal1d = math.select(swLocal1d, curLocal1d, sLocalOverflow || wLocalOverflow);
            nwLocal1d = math.select(nwLocal1d, curLocal1d, nLocalOverflow || wLocalOverflow);

            //COSTS
            nBlocked = costs[nLocal1d] == byte.MaxValue;
            eBlocked = costs[eLocal1d] == byte.MaxValue;
            sBlocked = costs[sLocal1d] == byte.MaxValue;
            wBlocked = costs[wLocal1d] == byte.MaxValue;

            IntegrationMark nMark = IntegrationMark.None;
            IntegrationMark eMark = IntegrationMark.None;
            IntegrationMark sMark = IntegrationMark.None;
            IntegrationMark wMark = IntegrationMark.None;

            nMark = integrationField[nLocal1d].Mark;
            eMark = integrationField[eLocal1d].Mark;
            sMark = integrationField[sLocal1d].Mark;
            wMark = integrationField[wLocal1d].Mark;

            //INTEGRATED COSTS
            curIntCost = integrationField[curLocal1d].Cost;
            nIntCost = integrationField[nLocal1d].Cost;
            eIntCost = integrationField[eLocal1d].Cost;
            sIntCost = integrationField[sLocal1d].Cost;
            wIntCost = integrationField[wLocal1d].Cost;
            neIntCost = integrationField[neLocal1d].Cost;
            seIntCost = integrationField[seLocal1d].Cost;
            swIntCost = integrationField[swLocal1d].Cost;
            nwIntCost = integrationField[nwLocal1d].Cost;

            //AVAILABILITY
            nAvailable = !nBlocked && !nLocalOverflow && (nMark == IntegrationMark.None || nMark == IntegrationMark.Integrated);
            eAvailable = !eBlocked && !eLocalOverflow && (eMark == IntegrationMark.None || eMark == IntegrationMark.Integrated);
            sAvailable = !sBlocked && !sLocalOverflow && (sMark == IntegrationMark.None || sMark == IntegrationMark.Integrated);
            wAvailable = !wBlocked && !wLocalOverflow && (wMark == IntegrationMark.None || wMark == IntegrationMark.Integrated);
        }
        void Enqueue()
        {
            float nDif = nIntCost - curIntCost;
            float eDif = eIntCost - curIntCost;
            float sDif = sIntCost - curIntCost;
            float wDif = wIntCost - curIntCost;
            if (nAvailable && nDif > 2f)
            {
                IntegrationTile tile = integrationField[nLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[nLocal1d] = tile;
                integrationQueue.Enqueue(new LocalIndex1d(nLocal1d, 0));
            }
            if (eAvailable && eDif > 2f)
            {
                IntegrationTile tile = integrationField[eLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[eLocal1d] = tile;
                integrationQueue.Enqueue(new LocalIndex1d(eLocal1d, 0));
            }
            if (sAvailable && sDif > 2f)
            {
                IntegrationTile tile = integrationField[sLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[sLocal1d] = tile;
                integrationQueue.Enqueue(new LocalIndex1d(sLocal1d, 0));
            }
            if (wAvailable && wDif > 2f)
            {
                IntegrationTile tile = integrationField[wLocal1d];
                tile.Mark = IntegrationMark.Awaiting;
                integrationField[wLocal1d] = tile;
                integrationQueue.Enqueue(new LocalIndex1d(wLocal1d, 0));
            }
        }
        float GetCost()
        {
            float costToReturn = float.MaxValue;
            float nCost = nIntCost + 1f;
            float eCost = eIntCost + 1f;
            float sCost = sIntCost + 1f;
            float wCost = wIntCost + 1f;
            float neCost = math.select(neIntCost + 1.4f, float.MaxValue, nBlocked && eBlocked);
            float seCost = math.select(seIntCost + 1.4f, float.MaxValue, sBlocked && eBlocked);
            float swCost = math.select(swIntCost + 1.4f, float.MaxValue, sBlocked && wBlocked);
            float nwCost = math.select(nwIntCost + 1.4f, float.MaxValue, nBlocked&& wBlocked);

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