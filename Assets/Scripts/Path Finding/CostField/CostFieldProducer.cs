using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class CostFieldProducer
{
    WalkabilityData _walkabilityData;
    CostField[] _producedCostFields;

    //utility
    public NativeArray<LocalDirectionData1d> LocalDirections;
    public NativeArray<SectorDirectionData> SectorDirections;
    public CostFieldProducer(WalkabilityData walkabilityData, byte sectorTileAmount, int fieldColAmount, int fieldRowAmount, int sectorMatrixColAmount, int sectorMatrixRowAmount)
    {
        _walkabilityData = walkabilityData;

        //calculate local directions
        LocalDirections = new NativeArray<LocalDirectionData1d>(fieldColAmount * fieldRowAmount, Allocator.Persistent);
        LocalDirectionCalculationJob localDirCalcJob = new LocalDirectionCalculationJob()
        {
            FieldColAmount = fieldColAmount,
            SectorColAmount = sectorTileAmount,
            LocalDirections = LocalDirections,
            SectorTileAmount = sectorTileAmount * sectorTileAmount,
            SectorMatrixColAmount = sectorMatrixColAmount,
            SectorMatrixRowAmount = sectorMatrixRowAmount,
        };
        localDirCalcJob.Schedule(LocalDirections.Length, 512).Complete();

        //calculate sector directions
        SectorDirections = new NativeArray<SectorDirectionData>(sectorTileAmount * sectorTileAmount, Allocator.Persistent);
        for (byte i = 0; i < SectorDirections.Length; i++)
        {
            SectorDirections[i] = new SectorDirectionData(i, sectorTileAmount);
        }
    }
    public void StartCostFieldProduction(int minOffset, int maxOffset, int sectorSize, int sectorMatrixColAmount, int sectorMatrixRowAmount)
    {
        int count = maxOffset - minOffset + 1;
        _producedCostFields = new CostField[count];
        for(int i = 0; i < count; i++)
        {
            _producedCostFields[i] = new CostField(_walkabilityData, i + minOffset, sectorSize, sectorMatrixColAmount, sectorMatrixRowAmount);
            _producedCostFields[i].ScheduleConfigurationJob();
        }
    }
    public void CompleteCostFieldProduction()
    {
        for (int i = _producedCostFields.Length - 1; i >=0; i--)
        {
            _producedCostFields[i].EndConfigurationJobIfCompleted();
        }
    }
    public void ForceCompleteCostFieldProduction()
    {
        for (int i = _producedCostFields.Length - 1; i >= 0; i--)
        {
            _producedCostFields[i].ForceCompleteConigurationJob();
        }
    }
    public CostFieldEditJob[] GetEditJobs(BoundaryData bounds, byte newCost)
    {
        CostFieldEditJob[] editJobs = new CostFieldEditJob[_producedCostFields.Length];
        for(int i = 0; i < editJobs.Length; i++)
        {
            editJobs[i] = _producedCostFields[i].GetEditJob(bounds, newCost);
        }
        return editJobs;
    }
    public CostField GetCostFieldWithOffset(int offset)
    {
        for(int i = 0; i < _producedCostFields.Length; i++)
        {
            if (_producedCostFields[i].Offset == offset)
            {
                return _producedCostFields[i];
            }
        }
        return null;
    }
}