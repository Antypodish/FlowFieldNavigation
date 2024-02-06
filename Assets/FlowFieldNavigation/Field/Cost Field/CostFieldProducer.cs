using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

internal class CostFieldProducer
{
    CostField[] _producedCostFields;

    //utility
    internal NativeArray<SectorDirectionData> SectorDirections;
    internal CostFieldProducer()
    {
        //CALCULATE SECTOR DIRECTIONS
        SectorDirections = new NativeArray<SectorDirectionData>(FlowFieldUtilities.SectorTileAmount, Allocator.Persistent);
        for (byte i = 0; i < SectorDirections.Length; i++)
        {
            SectorDirections[i] = new SectorDirectionData(i, FlowFieldUtilities.SectorColAmount);
        }
    }
    internal void ProduceCostFields(int maxOffset, NativeArray<byte> inputCosts)
    {
        int count = maxOffset + 1;
        _producedCostFields = new CostField[count];

        //set edges unwalkable
        for (int r = 0; r < FlowFieldUtilities.FieldRowAmount; r++)
        {
            for (int c = 0; c < FlowFieldUtilities.FieldColAmount; c++)
            {
                int index = r * FlowFieldUtilities.FieldColAmount + c;
                if(!(c == 0 || c == FlowFieldUtilities.FieldColAmount - 1 || r == 0 || r == FlowFieldUtilities.FieldRowAmount - 1)) { continue; }
                inputCosts[index] = byte.MaxValue;
            }
        }

        for (int i = 0; i < count; i++)
        {
            _producedCostFields[i] = new CostField(inputCosts, i);
        }
    }
    internal CostField[] GetAllCostFields()
    {
        return _producedCostFields;
    }
    internal UnsafeListReadOnly<byte>[] GetAllCostsAsUnsafeListReadonly()
    {
        UnsafeListReadOnly<byte>[] arrayToReturn = new UnsafeListReadOnly<byte>[_producedCostFields.Length];
        for(int i = 0; i < _producedCostFields.Length; i++)
        {
            arrayToReturn[i] = _producedCostFields[i].CostsLReadonlyUnsafe;
        }
        return arrayToReturn;
    }
    internal CostField GetCostFieldWithOffset(int offset)
    {
        return _producedCostFields[offset];
    }
    internal void DisposeAll()
    {
        SectorDirections.Dispose();
        for(int i = 0; i < _producedCostFields.Length; i++)
        {
            _producedCostFields[i].DisposeAll();
        }
        _producedCostFields = null;
    }
}