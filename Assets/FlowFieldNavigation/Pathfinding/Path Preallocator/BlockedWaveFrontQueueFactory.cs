using System.Collections.Generic;
using Unity.Collections;

internal class BlockedWaveFrontQueueFactory
{
    List<NativeQueue<LocalIndex1d>> _blockedWaveFrontQueues;

    internal BlockedWaveFrontQueueFactory()
    {
        _blockedWaveFrontQueues = new List<NativeQueue<LocalIndex1d>>();
    }
    internal NativeQueue<LocalIndex1d> GetBlockedWaveFrontQueue()
    {
        if(_blockedWaveFrontQueues.Count == 0) { return new NativeQueue<LocalIndex1d>(Allocator.Persistent); }
        int index = _blockedWaveFrontQueues.Count - 1;
        NativeQueue<LocalIndex1d> queue = _blockedWaveFrontQueues[index];
        _blockedWaveFrontQueues.RemoveAtSwapBack(index);
        return queue;
    }
    internal void SendBlockedWaveFrontQueueBack(NativeQueue<LocalIndex1d> blockedWaveFrontQueue)
    {
        blockedWaveFrontQueue.Clear();
        _blockedWaveFrontQueues.Add(blockedWaveFrontQueue);
    }
}
