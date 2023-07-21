using System.Collections.Generic;
using Unity.Collections;
using UnityEditor.Profiling.Memory.Experimental;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

internal class PathPreallocator
{
    PortalTraversalDataArrayFactory _porTravDataArrayFactory;
    PortalSequenceFactory _portalSequenceFactory;
    TargetSectorCostArrayFactory _targetSectorCostsArrayFactory;
    BlockedWaveFrontQueueFactory _blockedWaveFrontQueueFactory;
    SectorTransformationFactory _sectorTransformationFactory;
    FlowFieldLengthArrayFactory _flowFieldLengthArrayFactory;
    public PathPreallocator(CostFieldProducer costFieldProducer, int sectorTileAmount, int sectorMatrixSectorAmount)
    {
        _porTravDataArrayFactory = new PortalTraversalDataArrayFactory(costFieldProducer);
        _portalSequenceFactory = new PortalSequenceFactory();
        _targetSectorCostsArrayFactory = new TargetSectorCostArrayFactory(sectorTileAmount);
        _blockedWaveFrontQueueFactory = new BlockedWaveFrontQueueFactory();
        _sectorTransformationFactory = new SectorTransformationFactory(sectorMatrixSectorAmount);
        _flowFieldLengthArrayFactory = new FlowFieldLengthArrayFactory();
    }

    public NativeArray<PortalTraversalData> GetPortalTraversalDataArray(int offset)
    {
        return _porTravDataArrayFactory.GetPortalTraversalDataArray(offset);
    }
    public void Send(ref NativeArray<PortalTraversalData> array, int offset)
    {
        _porTravDataArrayFactory.SendPortalTraversalDataArray(ref array, offset);
    }
    public PreallocationPack GetPreallocations(int offset)
    {
        return new PreallocationPack()
        {
            PortalTraversalDataArray = _porTravDataArrayFactory.GetPortalTraversalDataArray(offset),
            PortalSequence = _portalSequenceFactory.GetPortalSequenceList(),
            PortalSequenceBorders = _portalSequenceFactory.GetPathRequestBorders(),
            TargetSectorCosts = _targetSectorCostsArrayFactory.GetTargetSecorCosts(),
            BlockedWaveFronts = _blockedWaveFrontQueueFactory.GetBlockedWaveFrontQueue(),
            SectorToPicked = _sectorTransformationFactory.GetSectorToPickedArray(),
            PickedToSector = _sectorTransformationFactory.GetPickedToSectorList(),
            FlowFieldLength = _flowFieldLengthArrayFactory.GetFlowFieldLengthArray(),
        };
    }
    public void SendPreallocationsBack(ref PreallocationPack preallocations, int offset)
    {
        _porTravDataArrayFactory.SendPortalTraversalDataArray(ref preallocations.PortalTraversalDataArray, offset);
        _portalSequenceFactory.SendPortalSequences(ref preallocations.PortalSequence, ref preallocations.PortalSequenceBorders);
        _targetSectorCostsArrayFactory.SendTargetSectorCosts(ref preallocations.TargetSectorCosts);
        _blockedWaveFrontQueueFactory.SendBlockedWaveFrontQueueBack(ref preallocations.BlockedWaveFronts);
        _sectorTransformationFactory.SendSectorTransformationsBack(ref preallocations.SectorToPicked, ref preallocations.PickedToSector);
        _flowFieldLengthArrayFactory.SendFlowFieldLengthArray(ref preallocations.FlowFieldLength);
    }
    public void DisposeAllPreallocations()
    {

    }
}