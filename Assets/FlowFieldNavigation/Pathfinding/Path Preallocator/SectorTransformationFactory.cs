using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

public class SectorTransformationFactory
{
    List<UnsafeList<int>> _sectorToPickedArrays;
    List<NativeList<int>> _pickedToSectorLists;
    List<CleaningHandle> _cleaningHandles;
    int _sectorMatrixSectorAmount;

    public SectorTransformationFactory(int sectorMatrixSectorAmount)
    {
        _sectorMatrixSectorAmount = sectorMatrixSectorAmount;
        _sectorToPickedArrays = new List<UnsafeList<int>>();
        _pickedToSectorLists = new List<NativeList<int>>();
        _cleaningHandles = new List<CleaningHandle>();
    }
    public void CheckForCleaningHandles()
    {
        for (int i = _cleaningHandles.Count - 1; i >= 0; i--)
        {
            CleaningHandle cleaningHandle = _cleaningHandles[i];
            if (cleaningHandle.Handle.IsCompleted)
            {
                cleaningHandle.Handle.Complete();
                _sectorToPickedArrays.Add(cleaningHandle.List);
                _cleaningHandles.RemoveAtSwapBack(i);
            }
        }
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
    public void SendSectorTransformationsBack(UnsafeList<int> sectorToPicked, NativeList<int> pickedToSector)
    {
        UnsafeListCleaningJob<int> cleaning = new UnsafeListCleaningJob<int>()
        {
            List = sectorToPicked,
        };
        CleaningHandle cleaningHandle = new CleaningHandle()
        {
            Handle = cleaning.Schedule(),
            List = sectorToPicked,
        };
        _cleaningHandles.Add(cleaningHandle);
        pickedToSector.Clear();
        _pickedToSectorLists.Add(pickedToSector);
    }

    struct CleaningHandle
    {
        public UnsafeList<int> List;
        public JobHandle Handle;
    }
}
