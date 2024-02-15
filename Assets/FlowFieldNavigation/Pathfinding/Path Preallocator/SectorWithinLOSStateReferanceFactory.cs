using System.Collections.Generic;
using Unity.Collections;


namespace FlowFieldNavigation
{
    internal class SectorWithinLOSStateReferanceFactory
    {
        List<NativeReference<SectorsWihinLOSArgument>> _nativeReferanceContainer;

        internal SectorWithinLOSStateReferanceFactory(int initialSize)
        {
            _nativeReferanceContainer = new List<NativeReference<SectorsWihinLOSArgument>>(initialSize);
            for (int i = 0; i < initialSize; i++)
            {
                _nativeReferanceContainer.Add(new NativeReference<SectorsWihinLOSArgument>(Allocator.Persistent));
            }
        }
        internal NativeReference<SectorsWihinLOSArgument> GetNativeReferance()
        {
            if (_nativeReferanceContainer.Count == 0)
            {
                return new NativeReference<SectorsWihinLOSArgument>(Allocator.Persistent);
            }
            else
            {
                NativeReference<SectorsWihinLOSArgument> nativeReferance = _nativeReferanceContainer[0];
                _nativeReferanceContainer.RemoveAtSwapBack(0);
                return nativeReferance;
            }
        }
        internal void SendNativeReferance(NativeReference<SectorsWihinLOSArgument> nativeReferanceInt)
        {
            nativeReferanceInt.Value = SectorsWihinLOSArgument.None;
            _nativeReferanceContainer.Add(nativeReferanceInt);
        }
    }


}