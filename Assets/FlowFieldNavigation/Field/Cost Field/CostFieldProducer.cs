using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

public class CostFieldProducer
{
    WalkabilityCell[][] _walkabilityMatrix;
    CostField[] _producedCostFields;

    //utility
    public NativeArray<SectorDirectionData> SectorDirections;
    public CostFieldProducer(WalkabilityCell[][] walkabilityMatrix)
    {
        _walkabilityMatrix = walkabilityMatrix;

        //CALCULATE SECTOR DIRECTIONS
        SectorDirections = new NativeArray<SectorDirectionData>(FlowFieldUtilities.SectorTileAmount, Allocator.Persistent);
        for (byte i = 0; i < SectorDirections.Length; i++)
        {
            SectorDirections[i] = new SectorDirectionData(i, FlowFieldUtilities.SectorColAmount);
        }
    }
    public void ProduceCostFields(int maxOffset)
    {
        int count = maxOffset + 1;
        _producedCostFields = new CostField[count];
        for(int i = 0; i < count; i++)
        {
            _producedCostFields[i] = new CostField(_walkabilityMatrix, i);
        }
    }
    public CostField[] GetAllCostFields()
    {
        return _producedCostFields;
    }
    public UnsafeListReadOnly<byte>[] GetAllCostsAsUnsafeListReadonly()
    {
        UnsafeListReadOnly<byte>[] arrayToReturn = new UnsafeListReadOnly<byte>[_producedCostFields.Length];
        for(int i = 0; i < _producedCostFields.Length; i++)
        {
            arrayToReturn[i] = _producedCostFields[i].CostsLReadonlyUnsafe;
        }
        return arrayToReturn;
    }
    public CostField GetCostFieldWithOffset(int offset)
    {
        return _producedCostFields[offset];
    }
}