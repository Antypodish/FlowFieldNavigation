using Unity.Collections;
using UnityEngine;

public class CostFieldProducer
{
    WalkabilityData _walkabilityData;
    CostField[] _producedCostFields;

    //utility
    public NativeArray<LocalDirectionData> LocalDirections;
    public CostFieldProducer(WalkabilityData walkabilityData, byte sectorTileAmount)
    {
        _walkabilityData = walkabilityData;

        //calculate directions

        //calculate local directions
        LocalDirections = new NativeArray<LocalDirectionData>(sectorTileAmount * sectorTileAmount, Allocator.Persistent);
        CalculateLocalDirections();

        //HELPERS
        void CalculateLocalDirections()
        {
            for (byte i = 0; i < LocalDirections.Length; i++)
            {
                LocalDirections[i] = new LocalDirectionData(i, sectorTileAmount);
            }
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