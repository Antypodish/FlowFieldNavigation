using System.Collections.Generic;
using Unity.Collections;

public class BlockedWaveFrontQueueFactory
{
    List<NativeQueue<LocalIndex1d>> _blockedWaveFrontQueues;

    public BlockedWaveFrontQueueFactory()
    {
        _blockedWaveFrontQueues = new List<NativeQueue<LocalIndex1d>>();
    }
    public NativeQueue<LocalIndex1d> GetBlockedWaveFrontQueue()
    {
        if(_blockedWaveFrontQueues.Count == 0) { return new NativeQueue<LocalIndex1d>(Allocator.Persistent); }
        int index = _blockedWaveFrontQueues.Count - 1;
        NativeQueue<LocalIndex1d> queue = _blockedWaveFrontQueues[index];
        _blockedWaveFrontQueues.RemoveAtSwapBack(index);
        return queue;
    }
    public void SendBlockedWaveFrontQueueBack(ref NativeQueue<LocalIndex1d> blockedWaveFrontQueue)
    {
        blockedWaveFrontQueue.Clear();
        _blockedWaveFrontQueues.Add(blockedWaveFrontQueue);
    }
}
