using System.Collections.Generic;
using Unity.Collections;
internal class NativeIntListFactory
{
    List<NativeList<int>> _nativeListContainer;

    internal NativeIntListFactory(int startingListCount)
    {
        _nativeListContainer = new List<NativeList<int>>(startingListCount);
        for(int i = 0; i < _nativeListContainer.Count; i++)
        {
            _nativeListContainer[i] = new NativeList<int>(Allocator.Persistent);
        }
    }

    internal NativeList<int> GetNativeIntList()
    {
        if(_nativeListContainer.Count == 0)
        {
            return new NativeList<int>(Allocator.Persistent);
        }
        NativeList<int> list = _nativeListContainer[_nativeListContainer.Count - 1];
        _nativeListContainer.RemoveAtSwapBack(_nativeListContainer.Count - 1);
        return list;
    }
    internal void SendNativeIntList(NativeList<int> list)
    {
        list.Clear();
        _nativeListContainer.Add(list);
    }
    internal void DestroyAll()
    {
        for (int i = 0; i < _nativeListContainer.Count; i++)
        {
            _nativeListContainer[i].Dispose();
        }
        _nativeListContainer.Clear();
    }
}
