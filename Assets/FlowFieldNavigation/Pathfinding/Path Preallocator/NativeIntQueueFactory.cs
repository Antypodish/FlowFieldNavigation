using System.Collections.Generic;
using Unity.Collections;

internal class NativeIntQueueFactory
{
    List<NativeQueue<int>> _nativeQueueContainer;

    internal NativeIntQueueFactory(int startingQueueCount)
    {
        _nativeQueueContainer = new List<NativeQueue<int>>(startingQueueCount);
        for (int i = 0; i < _nativeQueueContainer.Count; i++)
        {
            _nativeQueueContainer[i] = new NativeQueue<int>(Allocator.Persistent);
        }
    }

    internal NativeQueue<int> GetNativeIntQueue()
    {
        if (_nativeQueueContainer.Count == 0)
        {
            return new NativeQueue<int>(Allocator.Persistent);
        }
        NativeQueue<int> list = _nativeQueueContainer[_nativeQueueContainer.Count - 1];
        _nativeQueueContainer.RemoveAtSwapBack(_nativeQueueContainer.Count - 1);
        return list;
    }
    internal void SendNativeIntQueue(NativeQueue<int> list)
    {
        list.Clear();
        _nativeQueueContainer.Add(list);
    }
    internal void DestroyAll()
    {
        for(int i = 0; i < _nativeQueueContainer.Count; i++)
        {
            _nativeQueueContainer[i].Dispose();
        }
        _nativeQueueContainer.Clear();
    }
}
