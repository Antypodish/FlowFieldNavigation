internal class PathPreallocator
{
    PortalTraversalDataArrayFactory _porTravDataArrayFactory;
    PortalSequenceFactory _portalSequenceFactory;
    TargetSectorCostArrayFactory _targetSectorCostsArrayFactory;
    SectorTransformationFactory _sectorTransformationFactory;
    FlowFieldLengthArrayFactory _flowFieldLengthArrayFactory;
    public PathPreallocator(CostFieldProducer costFieldProducer, int sectorTileAmount, int sectorMatrixSectorAmount)
    {
        _porTravDataArrayFactory = new PortalTraversalDataArrayFactory(costFieldProducer);
        _portalSequenceFactory = new PortalSequenceFactory();
        _targetSectorCostsArrayFactory = new TargetSectorCostArrayFactory(sectorTileAmount);
        _sectorTransformationFactory = new SectorTransformationFactory(sectorMatrixSectorAmount);
        _flowFieldLengthArrayFactory = new FlowFieldLengthArrayFactory();
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
        };
    }
    public void SendPreallocationsBack(ref PreallocationPack preallocations, int offset)
    {
        _porTravDataArrayFactory.SendPortalTraversalDataArray(preallocations.PortalTraversalDataArray, offset);
        _portalSequenceFactory.SendPortalSequences(preallocations.PortalSequence, preallocations.PortalSequenceBorders); 
        _targetSectorCostsArrayFactory.SendTargetSectorCosts(preallocations.TargetSectorCosts); 
        _sectorTransformationFactory.SendSectorTransformationsBack(preallocations.SectorToPicked, preallocations.PickedToSector);
        _flowFieldLengthArrayFactory.SendFlowFieldLengthArray(preallocations.FlowFieldLength);
    }
    public void DisposeAllPreallocations()
    {

    }
}