using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using static UnityEngine.GraphicsBuffer;
using Unity.VisualScripting;

[BurstCompile]
public partial struct DynamicAreaIntegrationJob : IJob
{
    public int2 TargetIndex;
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public int SectorTileAmount;
    public int FieldColAmount;

    [ReadOnly] public NativeArray<UnsafeList<byte>> Costs;
    [ReadOnly] public UnsafeList<SectorFlowStart> PickedSectorFlowStarts;
    public NativeArray<IntegrationTile> IntegrationField;
    public void Execute()
    {
        //RESET
        for(int i = 0; i < IntegrationField.Length; i++)
        {
            IntegrationField[i] = new IntegrationTile()
            {
                Cost = float.MaxValue,
                Mark = 0,
            };
        }


        NativeArray<UnsafeList<byte>> costs = Costs;
        NativeArray<IntegrationTile> integrationField = IntegrationField;
        NativeQueue<LocalIndex1d> integrationQueue = new NativeQueue<LocalIndex1d>(Allocator.Temp);
        int sectorColAmount = SectorColAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int sectorTileAmount = SectorTileAmount;
        UnsafeList<SectorFlowStart> pickedSectorFlowStarts = PickedSectorFlowStarts;

        //LOOKUP TABLE
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

        int curSectorFlowStart;
        int nSectorFlowStart;
        int eSectorFlowStart;
        int sSectorFlowStart;
        int wSectorFlowStart;
        int neSectorFlowStart;
        int seSectorFlowStart;
        int swSectorFlowStart;
        int nwSectorFlowStart;

        bool nUnwalkable;
        bool eUnwalkable;
        bool sUnwalkable;
        bool wUnwalkable;

        bool nEnqueueable;
        bool eEnqueueable;
        bool sEnqueueable;
        bool wEnqueueable;

        bool nCostUnlookable;
        bool eCostUnlookable;
        bool sCostUnlookable;
        bool wCostUnlookable;
        bool neCostUnlookable;
        bool seCostUnlookable;
        bool swCostUnlookable;
        bool nwCostUnlookable;

        IntegrationTile curTile;
        IntegrationTile nTile;
        IntegrationTile eTile;
        IntegrationTile sTile;
        IntegrationTile wTile;
        IntegrationTile neTile;
        IntegrationTile seTile;
        IntegrationTile swTile;
        IntegrationTile nwTile;

        LocalIndex1d targetLocal = FlowFieldUtilities.GetLocal1D(TargetIndex, SectorColAmount, SectorMatrixColAmount);
        int targetLocalIndex = targetLocal.index;
        int targetSectorIndex = targetLocal.sector;
        int targetSectorFlowStart = GetSectorFlowStartInThePassedOrder(targetSectorIndex).a;

        IntegrationField[targetSectorFlowStart + targetLocalIndex] = new IntegrationTile()
        {
            Cost = 0,
            Mark = IntegrationMark.Integrated,
        };
        SetLookupTable(targetLocal);
        EnqueueNeighbours();

        while (!integrationQueue.IsEmpty())
        {
            LocalIndex1d curlocal = integrationQueue.Dequeue();
            SetLookupTable(curlocal);
            curTile.Cost = GetCost();
            integrationField[curSectorFlowStart + curlocal.index] = curTile;
            EnqueueNeighbours();
        }

        void SetLookupTable(LocalIndex1d curIndex)
        {
            int curLocal1d = curIndex.index;
            int curSector1d = curIndex.sector;

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

            int9 sectorFlowStarts = GetSectorFlowStartInThePassedOrder(nSector1d, eSector1d, sSector1d, wSector1d, neSector1d, seSector1d, swSector1d, nwSector1d, curSector1d);
            nSectorFlowStart = sectorFlowStarts.a;
            eSectorFlowStart = sectorFlowStarts.b;
            sSectorFlowStart = sectorFlowStarts.c;
            wSectorFlowStart = sectorFlowStarts.d;
            neSectorFlowStart = sectorFlowStarts.e;
            seSectorFlowStart = sectorFlowStarts.f;
            swSectorFlowStart = sectorFlowStarts.g;
            nwSectorFlowStart = sectorFlowStarts.h;
            curSectorFlowStart = sectorFlowStarts.i;

            //TILES
            nTile = integrationField[nSectorFlowStart + nLocal1d];
            eTile = integrationField[eSectorFlowStart + eLocal1d];
            sTile = integrationField[sSectorFlowStart + sLocal1d];
            wTile = integrationField[wSectorFlowStart + wLocal1d];
            neTile = integrationField[neSectorFlowStart + neLocal1d];
            seTile = integrationField[seSectorFlowStart + seLocal1d];
            swTile = integrationField[swSectorFlowStart + swLocal1d];
            nwTile = integrationField[nwSectorFlowStart + nwLocal1d];
            curTile = integrationField[curSectorFlowStart + curLocal1d];

            //COSTS
            UnsafeList<byte> nCosts = costs[nSector1d];
            UnsafeList<byte> eCosts = costs[eSector1d];
            UnsafeList<byte> sCosts = costs[sSector1d];
            UnsafeList<byte> wCosts = costs[wSector1d];
            UnsafeList<byte> neCosts = costs[neSector1d];
            UnsafeList<byte> seCosts = costs[seSector1d];
            UnsafeList<byte> swCosts = costs[swSector1d];
            UnsafeList<byte> nwCosts = costs[nwSector1d];
            nUnwalkable = nCosts[nLocal1d] == byte.MaxValue;
            eUnwalkable = eCosts[eLocal1d] == byte.MaxValue;
            sUnwalkable = sCosts[sLocal1d] == byte.MaxValue;
            wUnwalkable = wCosts[wLocal1d] == byte.MaxValue;

            nEnqueueable = nTile.Mark == IntegrationMark.None && !nUnwalkable && nSectorFlowStart != 0;
            eEnqueueable = eTile.Mark == IntegrationMark.None && !eUnwalkable && eSectorFlowStart != 0;
            sEnqueueable = sTile.Mark == IntegrationMark.None && !sUnwalkable && sSectorFlowStart != 0;
            wEnqueueable = wTile.Mark == IntegrationMark.None && !wUnwalkable && wSectorFlowStart != 0;

            nCostUnlookable = nSectorFlowStart == 0;
            eCostUnlookable = eSectorFlowStart == 0;
            sCostUnlookable = sSectorFlowStart == 0;
            wCostUnlookable = wSectorFlowStart == 0;
            neCostUnlookable = neSectorFlowStart == 0 || (nUnwalkable && eUnwalkable);
            seCostUnlookable = seSectorFlowStart == 0 || (sUnwalkable && eUnwalkable);
            swCostUnlookable = swSectorFlowStart == 0 || (sUnwalkable && wUnwalkable);
            nwCostUnlookable = nwSectorFlowStart == 0 || (nUnwalkable && wUnwalkable);
        }

        void EnqueueNeighbours()
        {

            if (nEnqueueable)
            {
                integrationField[nSectorFlowStart + nLocal1d] = new IntegrationTile()
                {
                    Cost = nTile.Cost,
                    Mark = IntegrationMark.Integrated,
                };
                integrationQueue.Enqueue(new LocalIndex1d(nLocal1d, nSector1d));
            }
            if (eEnqueueable)
            {
                integrationField[eSectorFlowStart + eLocal1d] = new IntegrationTile()
                {
                    Cost = eTile.Cost,
                    Mark = IntegrationMark.Integrated,
                };
                integrationQueue.Enqueue(new LocalIndex1d(eLocal1d, eSector1d));
            }
            if (sEnqueueable)
            {
                integrationField[sSectorFlowStart + sLocal1d] = new IntegrationTile()
                {
                    Cost = sTile.Cost,
                    Mark = IntegrationMark.Integrated,
                };
                integrationQueue.Enqueue(new LocalIndex1d(sLocal1d, sSector1d));
            }
            if (wEnqueueable)
            {
                integrationField[wSectorFlowStart + wLocal1d] = new IntegrationTile()
                {
                    Cost = wTile.Cost,
                    Mark = IntegrationMark.Integrated,
                };
                integrationQueue.Enqueue(new LocalIndex1d(wLocal1d, wSector1d));
            }
        }

        float GetCost()
        {
            float costToReturn = float.MaxValue;
            float nCost = math.select(nTile.Cost + 1f, float.MaxValue, nCostUnlookable);
            float eCost = math.select(eTile.Cost + 1f, float.MaxValue, eCostUnlookable);
            float sCost = math.select(sTile.Cost + 1f, float.MaxValue, sCostUnlookable);
            float wCost = math.select(wTile.Cost + 1f, float.MaxValue, wCostUnlookable);
            float neCost = math.select(neTile.Cost + 1.4f, float.MaxValue, neCostUnlookable);
            float seCost = math.select(seTile.Cost + 1.4f, float.MaxValue, seCostUnlookable);
            float swCost = math.select(swTile.Cost + 1.4f, float.MaxValue, swCostUnlookable);
            float nwCost = math.select(nwTile.Cost + 1.4f, float.MaxValue, nwCostUnlookable);

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

        int9 GetSectorFlowStartInThePassedOrder(int sec1, int sec2 = 0, int sec3 = 0, int sec4 = 0, int sec5 = 0, int sec6 = 0, int sec7 = 0, int sec8 = 0, int sec9 = 0)
        {
            int sector1FlowStart = 0;
            int sector2FlowStart = 0;
            int sector3FlowStart = 0;
            int sector4FlowStart = 0;
            int sector5FlowStart = 0;
            int sector6FlowStart = 0;
            int sector7FlowStart = 0;
            int sector8FlowStart = 0;
            int sector9FlowStart = 0;
            for (int i = 0; i < pickedSectorFlowStarts.Length; i++)
            {
                SectorFlowStart flowStart = pickedSectorFlowStarts[i];
                sector1FlowStart = math.select(sector1FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec1);
                sector2FlowStart = math.select(sector2FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec2);
                sector3FlowStart = math.select(sector3FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec3);
                sector4FlowStart = math.select(sector4FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec4);
                sector5FlowStart = math.select(sector5FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec5);
                sector6FlowStart = math.select(sector6FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec6);
                sector7FlowStart = math.select(sector7FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec7);
                sector8FlowStart = math.select(sector8FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec8);
                sector9FlowStart = math.select(sector9FlowStart, flowStart.FlowStartIndex, flowStart.SectorIndex == sec9);
            }
            return new int9()
            {
                a = sector1FlowStart,
                b = sector2FlowStart,
                c = sector3FlowStart,
                d = sector4FlowStart,
                e = sector5FlowStart,
                f = sector6FlowStart,
                g = sector7FlowStart,
                h = sector8FlowStart,
                i = sector9FlowStart,
            };
        }
    }
}

public struct SectorFlowStart
{
    public int SectorIndex;
    public int FlowStartIndex;

    public SectorFlowStart(int sectorIndex, int flowStartIndex) { SectorIndex = sectorIndex; FlowStartIndex = flowStartIndex; }
}