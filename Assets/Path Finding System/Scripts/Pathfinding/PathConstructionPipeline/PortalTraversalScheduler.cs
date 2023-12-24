using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor.PackageManager.Requests;
using UnityEngine.Networking.Match;

internal class PortalTraversalScheduler
{
    PathfindingManager _pathfindingManager;
    PathContainer _pathContainer;
    ActivePortalSubmissionScheduler _activePortalSubmissionScheduler;
    RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;

    NativeList<RequestPipelineInfoWithHandle> ScheduledPortalTraversals;
    public PortalTraversalScheduler(PathfindingManager pathfindingManager, RequestedSectorCalculationScheduler requestedSectorCalculationScheduler)
    {
        ScheduledPortalTraversals = new NativeList<RequestPipelineInfoWithHandle>(Allocator.Persistent);
        _pathfindingManager = pathfindingManager;
        _pathContainer = _pathfindingManager.PathContainer;
        _activePortalSubmissionScheduler = new ActivePortalSubmissionScheduler(pathfindingManager);
        _requestedSectorCalculationScheduler = requestedSectorCalculationScheduler;
    }
    public void SchedulePortalTraversalFor(RequestPipelineInfoWithHandle reqInfo, NativeSlice<float2> sources)
    {
        Path path = _pathfindingManager.PathContainer.ProducedPaths[reqInfo.PathIndex];
        PathDestinationData destinationData = _pathContainer.PathDestinationDataList[reqInfo.PathIndex];
        PathLocationData locationData = _pathContainer.PathLocationDataList[reqInfo.PathIndex];
        PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[reqInfo.PathIndex];
        UnsafeList<PathSectorState> sectorStateTable = _pathContainer.PathSectorStateTableList[reqInfo.PathIndex];
        int2 destinationIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize);
        CostField pickedCostField = _pathfindingManager.FieldProducer.GetCostFieldWithOffset(destinationData.Offset);
        FieldGraph pickedFieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(destinationData.Offset);

        PortalTraversalReductionJob reductionJob = new PortalTraversalReductionJob()
        {
            TargetIndex = destinationIndex,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            FieldTileSize = FlowFieldUtilities.TileSize,
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            PickedToSector = path.PickedToSector,
            TargetSectorCosts = _pathContainer.TargetSectorIntegrationList[reqInfo.PathIndex],
            PortalNodes = pickedFieldGraph.PortalNodes,
            SecToWinPtrs = pickedFieldGraph.SecToWinPtrs,
            WindowNodes = pickedFieldGraph.WindowNodes,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            SourcePositions = sources,
            PorPtrs = pickedFieldGraph.PorToPorPtrs,
            SectorNodes = pickedFieldGraph.SectorNodes,
            Costs = pickedCostField.Costs,
            LocalDirections = _pathfindingManager.FieldProducer.GetSectorDirections(),
            SectorToPicked = locationData.SectorToPicked,
            FlowFieldLength = path.FlowFieldLength,
            PortalTraversalDataArray = portalTraversalData.PortalTraversalDataArray,
            SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,
            TargetNeighbourPortalIndicies = portalTraversalData.TargetSectorPortalIndexList,
            AStarTraverseIndexList = portalTraversalData.AStartTraverseIndexList,
            IslandFields = pickedFieldGraph.IslandFields,
            SectorStateTable = sectorStateTable,
        };

        //TRAVERSAL
        PortalNodeTraversalJob traversalJob = new PortalNodeTraversalJob()
        {
            TargetIndex = destinationIndex,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            FieldTileSize = FlowFieldUtilities.TileSize,
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            PickedToSector = path.PickedToSector,
            PortalSequenceBorders = portalTraversalData.PortalSequenceBorders,
            PortalNodes = pickedFieldGraph.PortalNodes,
            SecToWinPtrs = pickedFieldGraph.SecToWinPtrs,
            WindowNodes = pickedFieldGraph.WindowNodes,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            SourcePositions = sources,
            PorPtrs = pickedFieldGraph.PorToPorPtrs,
            SectorNodes = pickedFieldGraph.SectorNodes,
            PortalSequence = portalTraversalData.PortalSequence,
            SectorToPicked = locationData.SectorToPicked,
            FlowFieldLength = path.FlowFieldLength,
            PortalTraversalDataArray = portalTraversalData.PortalTraversalDataArray,
            TargetNeighbourPortalIndicies = portalTraversalData.TargetSectorPortalIndexList,
            SectorStateTable = sectorStateTable,
            SourcePortals = portalTraversalData.SourcePortalIndexList,
        };

        JobHandle reductHandle = reductionJob.Schedule();
        JobHandle travHandle = traversalJob.Schedule(reductHandle);
        if (FlowFieldUtilities.DebugMode) { travHandle.Complete(); }

        reqInfo.Handle = travHandle;
        ScheduledPortalTraversals.Add(reqInfo);
    }

    public void TryComplete(NativeList<FinalPathRequest> requestedPaths, NativeArray<float2> sources)
    {
        for(int i = ScheduledPortalTraversals.Length - 1; i >= 0; i--)
        {
            RequestPipelineInfoWithHandle reqInfo = ScheduledPortalTraversals[i];
            if (reqInfo.Handle.IsCompleted)
            {
                reqInfo.Handle.Complete();
                _pathContainer.FinalizePathBuffers(reqInfo.PathIndex);

                //SCHEDULE ACTIVE PORTAL SUBMISSION
                RequestPipelineInfoWithHandle portalSubmissionReqInfo = _activePortalSubmissionScheduler.ScheduleActivePortalSubmission(reqInfo);
                PathPipelineInfoWithHandle portalSubmissionPathInfo = portalSubmissionReqInfo.ToPathPipelineInfoWithHandle();
                //SCHEDULE REQUESTED SECTOR CALCULATION
                FinalPathRequest pathReq = requestedPaths[reqInfo.RequestIndex];
                NativeSlice<float2> sourcePositions = new NativeSlice<float2>(sources, pathReq.SourcePositionStartIndex, pathReq.SourceCount);
                _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(portalSubmissionPathInfo, portalSubmissionPathInfo.Handle, sourcePositions);

                ScheduledPortalTraversals.RemoveAtSwapBack(i);
            }
        }
    }
    public void ForceComplete(NativeList<FinalPathRequest> requestedPaths, NativeArray<float2> sources)
    {
        for (int i = ScheduledPortalTraversals.Length - 1; i >= 0; i--)
        {
            RequestPipelineInfoWithHandle reqInfo = ScheduledPortalTraversals[i];
            reqInfo.Handle.Complete();
            _pathContainer.FinalizePathBuffers(reqInfo.PathIndex);

            //SCHEDULE ACTIVE PORTAL SUBMISSION
            RequestPipelineInfoWithHandle portalSubmissionReqInfo = _activePortalSubmissionScheduler.ScheduleActivePortalSubmission(reqInfo);
            PathPipelineInfoWithHandle portalSubmissionPathInfo = portalSubmissionReqInfo.ToPathPipelineInfoWithHandle();
            //SCHEDULE REQUESTED SECTOR CALCULATION
            FinalPathRequest pathReq = requestedPaths[reqInfo.RequestIndex];
            NativeSlice<float2> sourcePositions = new NativeSlice<float2>(sources, pathReq.SourcePositionStartIndex, pathReq.SourceCount);
            _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(portalSubmissionPathInfo, portalSubmissionPathInfo.Handle, sourcePositions);
        }
        ScheduledPortalTraversals.Clear();
    }
}