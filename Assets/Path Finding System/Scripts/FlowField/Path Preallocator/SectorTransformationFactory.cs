using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

public class SectorTransformationFactory
{
    List<UnsafeList<int>> _sectorToPickedArrays;
    List<NativeList<int>> _pickedToSectorLists;
    int _sectorMatrixSectorAmount;

    public SectorTransformationFactory(int sectorMatrixSectorAmount)
    {
        _sectorMatrixSectorAmount = sectorMatrixSectorAmount;
        _sectorToPickedArrays = new List<UnsafeList<int>>();
        _pickedToSectorLists = new List<NativeList<int>>();
    }
    public UnsafeList<int> GetSectorToPickedArray()
    {
        UnsafeList<int> array;
        if (_sectorToPickedArrays.Count == 0)
        {
            array = new UnsafeList<int>(_sectorMatrixSectorAmount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            array.Length = _sectorMatrixSectorAmount;
            return array;
        }
        int index = _sectorToPickedArrays.Count - 1;
        array = _sectorToPickedArrays[index];
        _sectorToPickedArrays.RemoveAtSwapBack(index);
        return array;
    }
    public NativeList<int> GetPickedToSectorList()
    {
        if (_pickedToSectorLists.Count == 0) { return new NativeList<int>(Allocator.Persistent); }
        int index = _pickedToSectorLists.Count - 1;
        NativeList<int> list = _pickedToSectorLists[index];
        _pickedToSectorLists.RemoveAtSwapBack(index);
        return list;
    }
    public void SendSectorTransformationsBack(ref UnsafeList<int> sectorToPicked, ref NativeList<int> pickedToSector)
    {
        UnsafeListCleaningJob<int> cleaning = new UnsafeListCleaningJob<int>()
        {
            List = sectorToPicked,
        };
        cleaning.Schedule().Complete();
        _sectorToPickedArrays.Add(sectorToPicked);
        pickedToSector.Clear();
        _pickedToSectorLists.Add(pickedToSector);
    }
}
