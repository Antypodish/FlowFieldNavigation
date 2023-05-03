public class CostFieldProducer
{
    WalkabilityData _walkabilityData;
    CostField[] _producedCostFields;

    public CostFieldProducer(WalkabilityData walkabilityData)
    {
        _walkabilityData = walkabilityData;
    }
    public void ProduceCostFields(int minOffset, int maxOffset, int sectorSize)
    {
        int count = maxOffset - minOffset + 1;
        _producedCostFields = new CostField[count];
        for(int i = 0; i < count; i++)
        {
            _producedCostFields[i] = new CostField(_walkabilityData, i + minOffset, sectorSize);
        }
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
