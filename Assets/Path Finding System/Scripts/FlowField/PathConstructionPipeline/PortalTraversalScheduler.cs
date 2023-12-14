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
    PathContainer _pathProducer;
    ActivePortalSubmissionScheduler _activePortalSubmissionScheduler;
    RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;

    NativeList<RequestPipelineInfoWithHandle> ScheduledPortalTraversals;
    public PortalTraversalScheduler(PathfindingManager pathfindingManager, RequestedSectorCalculationScheduler requestedSectorCalculationScheduler)
    {
        ScheduledPortalTraversals = new NativeList<RequestPipelineInfoWithHandle>(Allocator.Persistent);
        _pathfindingManager = pathfindingManager;
        _pathProducer = _pathfindingManager.PathContainer;
        _activePortalSubmissionScheduler = new ActivePortalSubmissionScheduler(pathfindingManager);
        _requestedSectorCalculationScheduler = requestedSectorCalculationScheduler;
    }
    public void SchedulePortalTraversalFor(RequestPipelineInfoWithHandle reqInfo, NativeSlice<float2> sources)
    {
        Path path = _pathfindingManager.PathContainer.ProducedPaths[reqInfo.PathIndex];
        PathLocationData locationData = _pathProducer.PathLocationDataList[reqInfo.PathIndex];
        int2 destinationIndex = path.TargetIndex;
        CostField pickedCostField = _pathfindingManager.FieldProducer.GetCostFieldWithOffset(path.Offset);
        FieldGraph pickedFieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(path.Offset);

        PortalTraversalReductionJob reductionJob = new PortalTraversalReductionJob()
        {
            TargetIndex = destinationIndex,
            FieldColAmount = FlowFieldUtilities.FieldColAmount,
            FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
            FieldTileSize = FlowFieldUtilities.TileSize,
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            PickedToSector = path.PickedToSector,
            TargetSectorCosts = path.TargetSectorCosts,
            PortalNodes = pickedFieldGraph.PortalNodes,
            SecToWinPtrs = pickedFieldGraph.SecToWinPtrs,
            WindowNodes = pickedFieldGraph.WindowNodes,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            SourcePositions = sources,
            PorPtrs = pickedFieldGraph.PorToPorPtrs,
            SectorNodes = pickedFieldGraph.SectorNodes,
            Costs = pickedCostField.CostsG,
            LocalDirections = _pathfindingManager.FieldProducer.GetSectorDirections(),
            SectorToPicked = locationData.SectorToPicked,
            FlowFieldLength = path.FlowFieldLength,
            PortalTraversalDataArray = path.PortalTraversalDataArray,
            SourcePortalIndexList = path.SourcePortalIndexList,
            TargetNeighbourPortalIndicies = path.TargetSectorPortalIndexList,
            AStarTraverseIndexList = path.AStartTraverseIndexList,
            IslandFields = pickedFieldGraph.IslandFields,
            SectorStateTable = path.SectorStateTable,
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
            PortalSequenceBorders = path.PortalSequenceBorders,
            PortalNodes = pickedFieldGraph.PortalNodes,
            SecToWinPtrs = pickedFieldGraph.SecToWinPtrs,
            WindowNodes = pickedFieldGraph.WindowNodes,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            SourcePositions = sources,
            PorPtrs = pickedFieldGraph.PorToPorPtrs,
            SectorNodes = pickedFieldGraph.SectorNodes,
            PortalSequence = path.PortalSequence,
            SectorToPicked = locationData.SectorToPicked,
            FlowFieldLength = path.FlowFieldLength,
            PortalTraversalDataArray = path.PortalTraversalDataArray,
            TargetNeighbourPortalIndicies = path.TargetSectorPortalIndexList,
            SectorStateTable = path.SectorStateTable,
            SourcePortals = path.SourcePortalIndexList,
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
                _pathProducer.FinalizePathBuffers(reqInfo.PathIndex);

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
            _pathProducer.FinalizePathBuffers(reqInfo.PathIndex);

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