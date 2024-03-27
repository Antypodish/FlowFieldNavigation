using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace FlowFieldNavigation
{
    internal class SectorTransformationFactory
    {
        List<NativeList<int>> _pickedToSectorLists;

        internal SectorTransformationFactory(int sectorMatrixSectorAmount)
        {
            _pickedToSectorLists = new List<NativeList<int>>();
        }
        internal NativeList<int> GetPickedToSectorList()
        {
            if (_pickedToSectorLists.Count == 0) { return new NativeList<int>(Allocator.Persistent); }
            int index = _pickedToSectorLists.Count - 1;
            NativeList<int> list = _pickedToSectorLists[index];
            _pickedToSectorLists.RemoveAtSwapBack(index);
            return list;
        }
        internal void SendSectorTransformationsBack(NativeList<int> pickedToSector)
        {
            pickedToSector.Clear();
            _pickedToSectorLists.Add(pickedToSector);
        }

        struct CleaningHandle
        {
            internal UnsafeList<int> List;
            internal JobHandle Handle;
        }
    }


}
