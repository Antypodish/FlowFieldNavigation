using Unity.Collections;
using Unity.Jobs;

public class CostFieldProducer
{
    WalkabilityData _walkabilityData;
    CostField[] _producedCostFields;

    public CostFieldProducer(WalkabilityData walkabilityData)
    {
        _walkabilityData = walkabilityData;
    }
    public void StartCostFieldProduction(int minOffset, int maxOffset, int sectorSize)
    {
        int count = maxOffset - minOffset + 1;
        _producedCostFields = new CostField[count];
        for(int i = 0; i < count; i++)
        {
            _producedCostFields[i] = new CostField(_walkabilityData, i + minOffset, sectorSize);
            _producedCostFields[i].StartJobs();
        }
    }
    public void CompleteCostFieldProduction()
    {
        for (int i = _producedCostFields.Length - 1; i >=0; i--)
        {
            _producedCostFields[i].EndJobsIfCompleted();
        }
    }
    public void ForceCompleteCostFieldProduction()
    {
        for (int i = _producedCostFields.Length - 1; i >= 0; i--)
        {
            _producedCostFields[i].ForceEndJob();
        }
    }
    public CostFieldEditJob[] GetEditJobs(Index2 bound1, Index2 bound2)
    {
        CostFieldEditJob[] editJobs = new CostFieldEditJob[_producedCostFields.Length];
        for(int i = 0; i < editJobs.Length; i++)
        {
            editJobs[i] = _producedCostFields[i].GetEditJob(bound1, bound2);
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