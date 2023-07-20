using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

public class CostField
{
    public int Offset;
    public NativeArray<byte> CostsG;
    public NativeArray<UnsafeList<byte>> CostsL;
    public NativeArray<UnsafeList<LocalDirectionData1d>> LocalDirections;
    public FieldGraph FieldGraph;

    float _fieldTileSize;
    JobHandle _fieldGraphConfigJobHandle;
    public CostField(WalkabilityData walkabilityData, NativeArray<UnsafeList<LocalDirectionData1d>> localDirections, int offset, int sectorColAmount, int sectorMatrixColAmount, int sectorMatrixRowAmount)
    {
        _fieldTileSize = walkabilityData.TileSize;
        int fieldRowAmount = walkabilityData.RowAmount;
        int fieldColAmount = walkabilityData.ColAmount;
        WalkabilityCell[][] walkabilityMatrix = walkabilityData.WalkabilityMatrix;
        Offset = offset;

        //configure costs
        CostsG = new NativeArray<byte>(fieldRowAmount * fieldColAmount, Allocator.Persistent);
        CostsL = new NativeArray<UnsafeList<byte>>(sectorMatrixColAmount * sectorMatrixRowAmount, Allocator.Persistent);
        LocalDirections = localDirections;
        CalculateCosts();
        ConvertToNewCosts();

        //allocate field graph
        FieldGraph = new FieldGraph(CostsG, CostsL, sectorColAmount, fieldRowAmount, fieldColAmount, offset, _fieldTileSize);

        //HELPERS
        void CalculateCosts()
        {
            //calculate costs without offset
            for (int r = 0; r < fieldRowAmount; r++)
            {
                for (int c = 0; c < fieldColAmount; c++)
                {
                    int index = r * fieldColAmount + c;
                    byte cost = walkabilityMatrix[r][c].Walkability == Walkability.Walkable ? (byte)1 : byte.MaxValue;
                    CostsG[index] = cost;
                }
            }
            //apply offset
            for (int r = 0; r < fieldRowAmount; r++)
            {
                for (int c = 0; c < fieldColAmount; c++)
                {
                    if (walkabilityMatrix[r][c].Walkability == Walkability.Unwalkable)
                    {
                        Index2 index = new Index2(r, c);
                        int minX = index.C - Offset < 0 ? 0 : index.C - Offset;
                        int maxX = index.C + Offset > fieldColAmount - 1 ? fieldColAmount - 1 : index.C + Offset;
                        int minY = index.R - Offset < 0 ? 0 : index.R - Offset;
                        int maxY = index.R + Offset > fieldRowAmount - 1 ? fieldRowAmount - 1 : index.R + Offset;

                        for (int row = minY; row <= maxY; row++)
                        {
                            for (int col = minX; col <= maxX; col++)
                            {
                                int i = row * fieldColAmount + col;
                                CostsG[i] = byte.MaxValue;
                            }
                        }
                    }
                }
            }
        }
        void ConvertToNewCosts()
        {
            //INITIALIZE SECTORS
            for(int i = 0; i < CostsL.Length; i++)
            {
                UnsafeList<byte> sector = new UnsafeList<byte>(sectorColAmount * sectorColAmount, Allocator.Persistent);
                sector.Length = sectorColAmount * sectorColAmount;
                CostsL[i] = sector;
            }

            //SET COSTS
            for (int y = 0; y < fieldRowAmount; y += sectorColAmount)
            {
                for(int x = 0; x < fieldColAmount; x += sectorColAmount)
                {
                    int2 sectorStart2d = new int2(x, y);
                    int2 sectorEnd2d = new int2(x + sectorColAmount, y + sectorColAmount);
                    int2 sector2d = new int2(x / sectorColAmount, y / sectorColAmount);
                    int sectorStart1d = sectorStart2d.y * fieldColAmount + sectorStart2d.x;
                    int sectorEnd1d = sectorEnd2d.y * fieldColAmount + sectorEnd2d.x;
                    int sector1d = sector2d.y * sectorMatrixColAmount + sector2d.x;
                    int sectorDownLeft = sectorStart1d;
                    int sectorUpLeft = sectorEnd1d - sectorColAmount;
                    UnsafeList<byte> curSector = CostsL[sector1d];
                    for (int i = sectorDownLeft; i < sectorUpLeft; i += fieldColAmount)
                    {
                        for(int j = i; j < i + sectorColAmount; j++)
                        {
                            int curGeneral1d = j;
                            int2 curGeneral2d = new int2(j % fieldColAmount, j / fieldColAmount);
                            int2 curLocal2d = curGeneral2d - sectorStart2d;
                            int curLocal1d = curLocal2d.y * sectorColAmount + curLocal2d.x;
                            byte curCost = CostsG[curGeneral1d];
                            curSector[curLocal1d] = curCost;
                        }
                    }
                }
            }
        }
    }
    public void ScheduleConfigurationJob()
    {
        FieldGraphConfigurationJob _fieldGraphConfigJob = FieldGraph.GetConfigJob();
        _fieldGraphConfigJobHandle = _fieldGraphConfigJob.Schedule();
    }
    public void EndConfigurationJobIfCompleted()
    {
        if (_fieldGraphConfigJobHandle.IsCompleted)
        {
            _fieldGraphConfigJobHandle.Complete();
        }
    }
    public void ForceCompleteConigurationJob()
    {
        _fieldGraphConfigJobHandle.Complete();
    }
    public CostFieldEditJob GetEditJob(BoundaryData bounds, byte newCost)
    {
        return FieldGraph.GetEditJob(bounds, newCost);
    }
}