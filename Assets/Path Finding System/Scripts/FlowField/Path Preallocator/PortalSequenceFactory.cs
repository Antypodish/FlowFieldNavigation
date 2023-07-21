using System.Collections.Generic;
using Unity.Collections;

internal class PortalSequenceFactory
{
    List<NativeList<int>> _portalSequences;
    List<NativeList<int>> _portalSequenceBorders;
    public PortalSequenceFactory()
    {
        _portalSequences = new List<NativeList<int>>();
        _portalSequenceBorders = new List<NativeList<int>>();
    }
    public NativeList<int> GetPortalSequenceList()
    {
        if(_portalSequences.Count == 0) { return new NativeList<int>(Allocator.Persistent); }
        int index = _portalSequences.Count - 1;
        NativeList<int> portalSequence = _portalSequences[index];
        _portalSequences.RemoveAtSwapBack(index);
        return portalSequence;
    }
    public NativeList<int> GetPathRequestBorders()
    {
        if (_portalSequenceBorders.Count == 0) { return new NativeList<int>(Allocator.Persistent); }
        int index = _portalSequenceBorders.Count - 1;
        NativeList<int> portalSequenceBorders = _portalSequenceBorders[index];
        _portalSequenceBorders.RemoveAtSwapBack(index);
        return portalSequenceBorders;
    }
    public void SendPortalSequences(ref NativeList<int> portalSequence, ref NativeList<int> portalSequenceBorders)
    {
        portalSequence.Clear();
        portalSequenceBorders.Clear();
        _portalSequences.Add(portalSequence);
        _portalSequenceBorders.Add(portalSequenceBorders);
    }
}