using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

namespace FlowFieldNavigation
{
    internal class ActiveWaveFrontListFactory
    {
        List<UnsafeList<ActiveWaveFront>> _activeWaveFrontListContainer;
        List<NativeList<UnsafeList<ActiveWaveFront>>> _activeWaveFrontListListContainer;

        internal ActiveWaveFrontListFactory(int initialListCount, int initialListListCount)
        {
            _activeWaveFrontListContainer = new List<UnsafeList<ActiveWaveFront>>(initialListCount);
            _activeWaveFrontListListContainer = new List<NativeList<UnsafeList<ActiveWaveFront>>>(initialListListCount);
            for (int i = 0; i < _activeWaveFrontListContainer.Count; i++)
            {
                _activeWaveFrontListContainer[i] = new UnsafeList<ActiveWaveFront>(0, Allocator.Persistent);
            }
            for (int i = 0; i < _activeWaveFrontListListContainer.Count; i++)
            {
                _activeWaveFrontListListContainer[i] = new NativeList<UnsafeList<ActiveWaveFront>>(Allocator.Persistent);
            }
        }

        internal NativeList<UnsafeList<ActiveWaveFront>> GetActiveWaveFrontListPersistent(int count)
        {
            NativeList<UnsafeList<ActiveWaveFront>> list;
            if (_activeWaveFrontListListContainer.Count == 0)
            {
                list = new NativeList<UnsafeList<ActiveWaveFront>>(Allocator.Persistent);
                list.Length = count;
            }
            else
            {
                list = _activeWaveFrontListListContainer[_activeWaveFrontListListContainer.Count - 1];
                list.Length = count;
                _activeWaveFrontListListContainer.RemoveAtSwapBack(_activeWaveFrontListListContainer.Count - 1);
            }
            for (int i = 0; i < count; i++)
            {
                if (_activeWaveFrontListContainer.Count == 0)
                {
                    list[i] = new UnsafeList<ActiveWaveFront>(0, Allocator.Persistent);
                }
                else
                {
                    list[i] = _activeWaveFrontListContainer[_activeWaveFrontListContainer.Count - 1];
                    _activeWaveFrontListContainer.RemoveAtSwapBack(_activeWaveFrontListContainer.Count - 1);
                }
            }
            return list;
        }
        internal void AddActiveWaveFrontList(int count, NativeList<UnsafeList<ActiveWaveFront>> list)
        {
            for (int i = 0; i < count; i++)
            {
                if (_activeWaveFrontListContainer.Count == 0)
                {
                    list.Add(new UnsafeList<ActiveWaveFront>(0, Allocator.Persistent));
                }
                else
                {
                    list.Add(_activeWaveFrontListContainer[0]);
                    _activeWaveFrontListContainer.RemoveAtSwapBack(0);
                }
            }
        }
        internal void SendActiveWaveFrontList(NativeList<UnsafeList<ActiveWaveFront>> list)
        {
            for (int i = 0; i < list.Length; i++)
            {
                UnsafeList<ActiveWaveFront> fronts = list[i];
                fronts.Clear();
                _activeWaveFrontListContainer.Add(fronts);
            }
            list.Clear();
            _activeWaveFrontListListContainer.Add(list);
        }

        internal void DisposeAll()
        {
            for (int i = 0; i < _activeWaveFrontListContainer.Count; i++)
            {
                _activeWaveFrontListContainer[i].Dispose();
            }
            for (int i = 0; i < _activeWaveFrontListListContainer.Count; i++)
            {
                _activeWaveFrontListListContainer[i].Dispose();
            }
            _activeWaveFrontListContainer.Clear();
            _activeWaveFrontListListContainer.Clear();
        }
    }


}
