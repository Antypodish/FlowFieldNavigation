using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;

[BurstCompile]
public struct LocalDirectionCalculationJob : IJobParallelFor
{
    public int SectorColAmount;
    public int SectorMatrixColAmount;
    public int SectorMatrixRowAmount;
    public int SectorTileAmount;
    public int FieldColAmount;
    [WriteOnly] public NativeArray<LocalDirectionData1d> LocalDirections;
    public void Execute(int index)
    {
        int sectorColAmount = SectorColAmount;
        int sectorMatrixColAmount = SectorMatrixColAmount;
        int sectorTileAmount = SectorTileAmount;
        int sectorMatrixTileAmount = sectorMatrixColAmount * SectorMatrixRowAmount;

        LocalDirectionData1d directions;
        ///////////LOOKUP TABLE////////////////
        ///////////////////////////////////////
        int n;
        int e;
        int s;
        int w;
        int ne;
        int se;
        int sw;
        int nw;
        int nSector;
        int eSector;
        int sSector;
        int wSector;
        int neSector;
        int seSector;
        int swSector;
        int nwSector;
        bool nLocalOverflow;
        bool eLocalOverflow;
        bool sLocalOverflow;
        bool wLocalOverflow;
        bool nSectorOverflow;
        bool eSectorOverflow;
        bool sSectorOverflow;
        bool wSectorOverflow;
        ///////////////////////////////////////////////
        int2 index2d = new int2(index % FieldColAmount, index / FieldColAmount);
        int2 sector2d = index2d / SectorColAmount;
        int2 sectorStart2d = sector2d * sectorColAmount;
        int2 local2d = index2d - sectorStart2d;
        int local1d = local2d.y * sectorColAmount + local2d.x;
        int sector1d = sector2d.y * sectorMatrixColAmount + sector2d.x;
        SetLookupTable(local1d, sector1d);
        LocalDirections[index] = directions;

        void SetLookupTable(int curLocal1d, int curSector1d)
        {
            //LOCAL INDICIES
            n = curLocal1d + sectorColAmount;
            e = curLocal1d + 1;
            s = curLocal1d - sectorColAmount;
            w = curLocal1d - 1;
            ne = n + 1;
            se = s + 1;
            sw = s - 1;
            nw = n - 1;

            //OVERFLOWS
            nLocalOverflow = n >= sectorTileAmount;
            eLocalOverflow = (e % sectorColAmount) == 0;
            sLocalOverflow = s < 0;
            wLocalOverflow = (curLocal1d % sectorColAmount) == 0;


            //SECTOR INDICIES
            nSector = math.select(curSector1d, curSector1d + sectorMatrixColAmount, nLocalOverflow);
            eSector = math.select(curSector1d, curSector1d + 1, eLocalOverflow);
            sSector = math.select(curSector1d, curSector1d - sectorMatrixColAmount, sLocalOverflow);
            wSector = math.select(curSector1d, curSector1d - 1, wLocalOverflow);
            neSector = math.select(curSector1d, curSector1d + sectorMatrixColAmount, nLocalOverflow);
            neSector = math.select(neSector, neSector + 1, eLocalOverflow);
            seSector = math.select(curSector1d, curSector1d - sectorMatrixColAmount, sLocalOverflow);
            seSector = math.select(seSector, seSector + 1, eLocalOverflow);
            swSector = math.select(curSector1d, curSector1d - sectorMatrixColAmount, sLocalOverflow);
            swSector = math.select(swSector, swSector - 1, wLocalOverflow);
            nwSector = math.select(curSector1d, curSector1d + sectorMatrixColAmount, nLocalOverflow);
            nwSector = math.select(nwSector, nwSector - 1, wLocalOverflow);

            n = math.select(n, curLocal1d - (sectorColAmount * sectorColAmount - sectorColAmount), nLocalOverflow);
            e = math.select(e, curLocal1d - sectorColAmount + 1, eLocalOverflow);
            s = math.select(s, curLocal1d + (sectorColAmount * sectorColAmount - sectorColAmount), sLocalOverflow);
            w = math.select(w, curLocal1d + sectorColAmount - 1, wLocalOverflow);
            ne = math.select(ne, ne - (sectorColAmount * sectorColAmount), nLocalOverflow);
            ne = math.select(ne, ne - sectorColAmount, eLocalOverflow);
            se = math.select(se, se + (sectorColAmount * sectorColAmount), sLocalOverflow);
            se = math.select(se, se - sectorColAmount, eLocalOverflow);
            sw = math.select(sw, sw + (sectorColAmount * sectorColAmount), sLocalOverflow);
            sw = math.select(sw, sw + sectorColAmount, wLocalOverflow);
            nw = math.select(nw, nw - (sectorColAmount * sectorColAmount), nLocalOverflow);
            nw = math.select(nw, nw + sectorColAmount, wLocalOverflow);

            //SECTOR OVERFLOWS
            nSectorOverflow = nSector >= sectorMatrixTileAmount;
            eSectorOverflow = (eSector % sectorMatrixColAmount) == 0 && eSector != curSector1d;
            sSectorOverflow = sSector < 0;
            wSectorOverflow = (curSector1d % sectorMatrixColAmount) == 0 && wSector != curSector1d;

            if (nSectorOverflow)
            {
                n = curLocal1d;
                ne = curLocal1d;
                nw = curLocal1d;
                nSector = curSector1d;
                neSector = curSector1d;
                nwSector = curSector1d;
            }
            if (eSectorOverflow)
            {
                e = curLocal1d;
                ne = curLocal1d;
                se = curLocal1d;
                eSector = curSector1d;
                neSector = curSector1d;
                seSector = curSector1d;
            }
            if (sSectorOverflow)
            {
                s = curLocal1d;
                se = curLocal1d;
                sw = curLocal1d;
                sSector = curSector1d;
                seSector = curSector1d;
                swSector = curSector1d;
            }
            if (wSectorOverflow)
            {
                nw = curLocal1d;
                sw = curLocal1d;
                w = curLocal1d;
                nwSector = curSector1d;
                swSector = curSector1d;
                wSector = curSector1d;
            }

            directions.n = (ushort)n;
            directions.e = (ushort)e;
            directions.s = (ushort)s;
            directions.w = (ushort)w;
            directions.ne = (ushort)ne;
            directions.se = (ushort)se;
            directions.sw = (ushort)sw;
            directions.nw = (ushort)nw;

            directions.nSector = (ushort)nSector;
            directions.eSector = (ushort)eSector;
            directions.sSector = (ushort)sSector;
            directions.wSector = (ushort)wSector;
            directions.neSector = (ushort)neSector;
            directions.seSector = (ushort)seSector;
            directions.swSector = (ushort)swSector;
            directions.nwSector = (ushort)nwSector;
        }
    }
}
