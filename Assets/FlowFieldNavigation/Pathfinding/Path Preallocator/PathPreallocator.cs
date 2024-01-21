﻿using Unity.Collections;
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
    SectorStateTableFactory _sectorStateTableFactory;
    FlowFieldFactory _flowFieldFactory;
    IntegrationFieldFactory _integrationFieldFactory;
    public PathPreallocator(FieldManager fieldProducer, int sectorTileAmount, int sectorMatrixSectorAmount)
    {
        _porTravDataArrayFactory = new PortalTraversalDataArrayFactory(fieldProducer.GetAllFieldGraphs());
        _portalSequenceFactory = new PortalSequenceFactory();
        _targetSectorCostsArrayFactory = new TargetSectorCostArrayFactory(sectorTileAmount);
        _sectorTransformationFactory = new SectorTransformationFactory(sectorMatrixSectorAmount);
        _flowFieldLengthArrayFactory = new FlowFieldLengthArrayFactory();
        _nativeIntListFactory = new NativeIntListFactory(0);
        _nativeIntQueueFactory = new NativeIntQueueFactory(0);
        _activeWaveFrontListFactory = new ActiveWaveFrontListFactory(0, 0);
        _sectorStateTableFactory = new SectorStateTableFactory(0);
        _flowFieldFactory = new FlowFieldFactory(0);
        _integrationFieldFactory = new IntegrationFieldFactory(0);
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
            SectorStateTable = _sectorStateTableFactory.GetSectorStateTable(),
        };
    }
    public void SendPreallocationsBack(ref PreallocationPack preallocations, NativeList<UnsafeList<ActiveWaveFront>> activeWaveFrontList, UnsafeList<FlowData> flowField, NativeList<IntegrationTile> integrationField, int offset)
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
        _sectorStateTableFactory.SendSectorStateTable(preallocations.SectorStateTable);
        _flowFieldFactory.SendFlowField(flowField);
        _integrationFieldFactory.SendIntegrationField(integrationField);
    }
    public NativeList<UnsafeList<ActiveWaveFront>> GetActiveWaveFrontListPersistent(int count)
    {
        return _activeWaveFrontListFactory.GetActiveWaveFrontListPersistent(count);
    }
    public UnsafeList<FlowData> GetFlowField(int length)
    {
        return _flowFieldFactory.GetFlowfield(length);
    }
    public NativeList<IntegrationTile> GetIntegrationField(int length)
    {
        return _integrationFieldFactory.GetIntegrationField(length);
    }
    public void AddToActiveWaveFrontList(int count, NativeList<UnsafeList<ActiveWaveFront>> destinationList)
    {
        _activeWaveFrontListFactory.AddActiveWaveFrontList(count, destinationList);
    }
    public void DisposeAllPreallocations()
    {

    }
}