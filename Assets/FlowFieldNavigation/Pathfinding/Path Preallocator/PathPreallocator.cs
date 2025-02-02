﻿using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace FlowFieldNavigation
{

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
        SectorFlowStartListFactory _sectorFlowStartListFactory;
        NativeReferanceIntFactory _nativeReferanceIntFactory;
        SectorWithinLOSStateReferanceFactory _sectorWithinLOSStateReferanceFactory;
        SectorBitArrayFactory _sectorBitArrayFactory;
        internal PathPreallocator(FieldDataContainer fieldProducer, int sectorTileAmount, int sectorMatrixSectorAmount)
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
            _nativeReferanceIntFactory = new NativeReferanceIntFactory(0);
            _sectorFlowStartListFactory = new SectorFlowStartListFactory(0);
            _sectorWithinLOSStateReferanceFactory = new SectorWithinLOSStateReferanceFactory(0);
            _sectorBitArrayFactory = new SectorBitArrayFactory(FlowFieldUtilities.SectorMatrixTileAmount, 0);
        }
        internal void CheckForDeallocations()
        {
            _porTravDataArrayFactory.CheckForCleaningHandles();
        }
        internal PreallocationPack GetPreallocations()
        {
            UnsafeList<float> targetSectorCosts = new UnsafeList<float>(FlowFieldUtilities.SectorTileAmount, Allocator.Persistent);
            targetSectorCosts.Length = FlowFieldUtilities.SectorTileAmount;
            return new PreallocationPack()
            {
                PortalSequence = _portalSequenceFactory.GetPortalSequenceList(),
                TargetSectorCosts = targetSectorCosts,
                PickedToSector = _sectorTransformationFactory.GetPickedToSectorList(),
                SourcePortalIndexList = _nativeIntListFactory.GetNativeIntList(),
                TargetSectorPortalIndexList = _nativeIntListFactory.GetNativeIntList(),
                PortalTraversalFastMarchingQueue = _nativeIntQueueFactory.GetNativeIntQueue(),
                SectorStateTable = _sectorStateTableFactory.GetSectorStateTable(),
                SectorStartIndexListToCalculateFlow = _nativeIntListFactory.GetNativeIntList(),
                SectorStartIndexListToCalculateIntegration = _nativeIntListFactory.GetNativeIntList(),
                NotActivePortalList = new NativeList<NotActivePortalRecord>(Allocator.Persistent),
                NewPickedSectorStartIndex = _nativeReferanceIntFactory.GetNativeReferanceInt(),
                PathAdditionSequenceBorderStartIndex = _nativeReferanceIntFactory.GetNativeReferanceInt(),
                DynamicAreaIntegrationField = _integrationFieldFactory.GetIntegrationField(0),
                SectorsWithinLOSState = _sectorWithinLOSStateReferanceFactory.GetNativeReferance(),
                SectorBitArray = _sectorBitArrayFactory.GetSectorBitArray(),
                DijkstraStartIndicies = _nativeIntListFactory.GetNativeIntList(),
            };
        }
        internal void SendPreallocationsBack(ref PreallocationPack preallocations)
        {
            preallocations.TargetSectorCosts.Dispose();
            _sectorTransformationFactory.SendSectorTransformationsBack(preallocations.PickedToSector);
            _nativeIntListFactory.SendNativeIntList(preallocations.TargetSectorPortalIndexList);
            _nativeIntListFactory.SendNativeIntList(preallocations.SourcePortalIndexList);
            _nativeIntQueueFactory.SendNativeIntQueue(preallocations.PortalTraversalFastMarchingQueue);
            _sectorStateTableFactory.SendSectorStateTable(preallocations.SectorStateTable);
            _nativeIntListFactory.SendNativeIntList(preallocations.SectorStartIndexListToCalculateIntegration);
            _nativeIntListFactory.SendNativeIntList(preallocations.SectorStartIndexListToCalculateFlow);
            preallocations.NotActivePortalList.Dispose();
            _nativeReferanceIntFactory.SendNativeReferanceInt(preallocations.NewPickedSectorStartIndex);
            _nativeReferanceIntFactory.SendNativeReferanceInt(preallocations.PathAdditionSequenceBorderStartIndex);
            _integrationFieldFactory.SendIntegrationField(preallocations.DynamicAreaIntegrationField);
            _sectorWithinLOSStateReferanceFactory.SendNativeReferance(preallocations.SectorsWithinLOSState);
            _sectorBitArrayFactory.SendSectorBitArray(preallocations.SectorBitArray);
            _nativeIntListFactory.SendNativeIntList(preallocations.DijkstraStartIndicies);
        }
    }

}