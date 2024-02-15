
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;

namespace FlowFieldNavigation
{

    internal class NativeReferanceIntFactory
    {
        List<NativeReference<int>> _nativeReferanceContainer;

        internal NativeReferanceIntFactory(int initialSize)
        {
            _nativeReferanceContainer = new List<NativeReference<int>>(initialSize);
            for (int i = 0; i < initialSize; i++)
            {
                _nativeReferanceContainer.Add(new NativeReference<int>(Allocator.Persistent));
            }
        }
        internal NativeReference<int> GetNativeReferanceInt()
        {
            if (_nativeReferanceContainer.Count == 0)
            {
                return new NativeReference<int>(Allocator.Persistent);
            }
            else
            {
                NativeReference<int> nativeReferance = _nativeReferanceContainer[0];
                _nativeReferanceContainer.RemoveAtSwapBack(0);
                return nativeReferance;
            }
        }
        internal void SendNativeReferanceInt(NativeReference<int> nativeReferanceInt)
        {
            nativeReferanceInt.Value = 0;
            _nativeReferanceContainer.Add(nativeReferanceInt);
        }

    }


}