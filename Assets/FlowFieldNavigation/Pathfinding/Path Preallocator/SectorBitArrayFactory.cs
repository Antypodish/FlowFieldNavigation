using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

public class SectorBitArrayFactory
{
    List<SectorBitArray> _sectorBitArrayContainer;
    int _sectorBitArrayLength;
    internal SectorBitArrayFactory(int sectorBitArrayLength, int initialSize)
    {
        _sectorBitArrayLength = sectorBitArrayLength;
        _sectorBitArrayContainer = new List<SectorBitArray>(initialSize);
        for (int i = 0; i < initialSize; i++)
        {
            _sectorBitArrayContainer.Add(new SectorBitArray(sectorBitArrayLength, Allocator.Persistent));
        }
    }
    internal SectorBitArray GetSectorBitArray()
    {
        if (_sectorBitArrayContainer.Count == 0)
        {
            return new SectorBitArray(_sectorBitArrayLength, Allocator.Persistent);
        }
        else
        {
            SectorBitArray sectorBitArray = _sectorBitArrayContainer[0];
            _sectorBitArrayContainer.RemoveAtSwapBack(0);
            return sectorBitArray;
        }
    }
    internal void SendSectorBitArray(SectorBitArray sectorBitArray)
    {
        sectorBitArray.Clear();
        _sectorBitArrayContainer.Add(sectorBitArray);
    }
}
