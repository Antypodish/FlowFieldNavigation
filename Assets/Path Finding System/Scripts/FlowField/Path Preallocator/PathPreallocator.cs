using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

internal class PathPreallocator
{
    PortalTraversalDataArrayFactory _porTravDataArrayFactory;
    PortalSequenceFactory _portalSequenceFactory;
    TargetSectorCostArrayFactory _targetSectorCostsArrayFactory;
    SectorTransformationFactory _sectorTransformationFactory;
    FlowFieldLengthArrayFactory _flowFieldLengthArrayFactory;
    NativeIntListFactory _nativeIntListFactory;
    NativeIntQueueFactory _nativeIntQueueFactory;
    ActiveWaveFrontListFactory _activeWaveFrontListFactory;
    public PathPreallocator(FieldProducer fieldProducer, int sectorTileAmount, int sectorMatrixSectorAmount)
    {
        _porTravDataArrayFactory = new PortalTraversalDataArrayFactory(fieldProducer.GetAllFieldGraphs());
        _portalSequenceFactory = new PortalSequenceFactory();
        _targetSectorCostsArrayFactory = new TargetSectorCostArrayFactory(sectorTileAmount);
        _sectorTransformationFactory = new SectorTransformationFactory(sectorMatrixSectorAmount);
        _flowFieldLengthArrayFactory = new FlowFieldLengthArrayFactory();
        _nativeIntListFactory = new NativeIntListFactory(0);
        _nativeIntQueueFactory = new NativeIntQueueFactory(0);
        _activeWaveFrontListFactory = new ActiveWaveFrontListFactory(0, 0);
    }
    public void CheckForDeallocations()
    {
        _porTravDataArrayFactory.CheckForCleaningHandles();
        _sectorTransformationFactory.CheckForCleaningHandles();
    }
    public PreallocationPack GetPreallocations(int offset)
    {
        return new PreallocationPack()
        {
            PortalTraversalDataArray = _porTravDataArrayFactory.GetPortalTraversalDataArray(offset),
            PortalSequence = _portalSequenceFactory.GetPortalSequenceList(),
            PortalSequenceBorders = _portalSequenceFactory.GetPathRequestBorders(),
            TargetSectorCosts = _targetSectorCostsArrayFactory.GetTargetSecorCosts(),
            SectorToPicked = _sectorTransformationFactory.GetSectorToPickedArray(),
            PickedToSector = _sectorTransformationFactory.GetPickedToSectorList(),
            FlowFieldLength = _flowFieldLengthArrayFactory.GetFlowFieldLengthArray(),
            AStartTraverseIndexList = _nativeIntListFactory.GetNativeIntList(),
            SourcePortalIndexList = _nativeIntListFactory.GetNativeIntList(),
            TargetSectorPortalIndexList = _nativeIntListFactory.GetNativeIntList(),
            PortalTraversalFastMarchingQueue = _nativeIntQueueFactory.GetNativeIntQueue(),
        };
    }
    public void SendPreallocationsBack(ref PreallocationPack preallocations, NativeList<UnsafeList<ActiveWaveFront>> activeWaveFrontList, int offset)
    {
        _porTravDataArrayFactory.SendPortalTraversalDataArray(preallocations.PortalTraversalDataArray, offset);
        _portalSequenceFactory.SendPortalSequences(preallocations.PortalSequence, preallocations.PortalSequenceBorders); 
        _targetSectorCostsArrayFactory.SendTargetSectorCosts(preallocations.TargetSectorCosts); 
        _sectorTransformationFactory.SendSectorTransformationsBack(preallocations.SectorToPicked, preallocations.PickedToSector);
        _flowFieldLengthArrayFactory.SendFlowFieldLengthArray(preallocations.FlowFieldLength);
        _nativeIntListFactory.SendNativeIntList(preallocations.TargetSectorPortalIndexList);
        _nativeIntListFactory.SendNativeIntList(preallocations.SourcePortalIndexList);
        _nativeIntListFactory.SendNativeIntList(preallocations.AStartTraverseIndexList);
        _nativeIntQueueFactory.SendNativeIntQueue(preallocations.PortalTraversalFastMarchingQueue);
        _activeWaveFrontListFactory.SendActiveWaveFrontList(activeWaveFrontList);
    }
    public NativeList<UnsafeList<ActiveWaveFront>> GetActiveWaveFrontListPersistent(int count)
    {
        return _activeWaveFrontListFactory.GetActiveWaveFrontListPersistent(count);
    }
    public void AddToActiveWaveFrontList(int count, NativeList<UnsafeList<ActiveWaveFront>> destinationList)
    {
        _activeWaveFrontListFactory.AddActiveWaveFrontList(count, destinationList);
    }
    public void DisposeAllPreallocations()
    {

    }
}