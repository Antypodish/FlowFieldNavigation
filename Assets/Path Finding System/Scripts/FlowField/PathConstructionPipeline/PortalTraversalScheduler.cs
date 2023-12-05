using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor.PackageManager.Requests;

internal class PortalTraversalScheduler
{
    PathfindingManager _pathfindingManager;
    PathContainer _pathProducer;
    ActivePortalSubmissionScheduler _activePortalSubmissionScheduler;
    RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;

    NativeList<HandleWithReqAndPathIndex> ScheduledPortalTraversals;
    public PortalTraversalScheduler(PathfindingManager pathfindingManager, RequestedSectorCalculationScheduler requestedSectorCalculationScheduler)
    {
        ScheduledPortalTraversals = new NativeList<HandleWithReqAndPathIndex>(Allocator.Persistent);
        _pathfindingManager = pathfindingManager;
        _pathProducer = _pathfindingManager.PathProducer;
        _activePortalSubmissionScheduler = new ActivePortalSubmissionScheduler(pathfindingManager);
        _requestedSectorCalculationScheduler = requestedSectorCalculationScheduler;
    }
    public void SchedulePortalTraversalFor(int pathIndex, int requestIndex, NativeSlice<float2> sources)
    {
        Path path = _pathfindingManager.PathProducer.ProducedPaths[pathIndex];
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
            SectorToPicked = path.SectorToPicked,
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
            TargetSectorCosts = path.TargetSectorCosts,
            PortalNodes = pickedFieldGraph.PortalNodes,
            SecToWinPtrs = pickedFieldGraph.SecToWinPtrs,
            WindowNodes = pickedFieldGraph.WindowNodes,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            SourcePositions = sources,
            PorPtrs = pickedFieldGraph.PorToPorPtrs,
            SectorNodes = pickedFieldGraph.SectorNodes,
            PortalSequence = path.PortalSequence,
            SectorToPicked = path.SectorToPicked,
            FlowFieldLength = path.FlowFieldLength,
            PortalTraversalDataArray = path.PortalTraversalDataArray,
            TargetNeighbourPortalIndicies = path.TargetSectorPortalIndexList,
            SectorStateTable = path.SectorStateTable,
            SourcePortals = path.SourcePortalIndexList,
        };

        JobHandle reductHandle = reductionJob.Schedule();
        JobHandle travHandle = traversalJob.Schedule(reductHandle);
        if (FlowFieldUtilities.DebugMode) { travHandle.Complete(); }

        HandleWithReqAndPathIndex pathHandle = new HandleWithReqAndPathIndex()
        {
            Handle = travHandle,
            PathIndex = pathIndex,
            RequestIndex = requestIndex,
        };
        ScheduledPortalTraversals.Add(pathHandle);
    }

    public void TryComplete(NativeList<PathRequest> requestedPaths, NativeArray<float2> sources)
    {
        for(int i = ScheduledPortalTraversals.Length - 1; i >= 0; i--)
        {
            HandleWithReqAndPathIndex porTravHandle = ScheduledPortalTraversals[i];
            if (porTravHandle.Handle.IsCompleted)
            {
                porTravHandle.Handle.Complete();
                _pathProducer.FinalizePathBuffers(porTravHandle.PathIndex);
                ScheduledPortalTraversals.RemoveAtSwapBack(i);

                //SCHEDULE ACTIVE PORTAL SUBMISSION
                HandleWithReqAndPathIndex activePorHandle = _activePortalSubmissionScheduler.ScheduleActivePortalSubmission(porTravHandle.PathIndex, porTravHandle.RequestIndex);

                //SCHEDULE REQUESTED SECTOR CALCULATION
                PathRequest pathReq = requestedPaths[porTravHandle.RequestIndex];
                NativeSlice<float2> sourcePositions = new NativeSlice<float2>(sources, pathReq.SourcePositionStartIndex, pathReq.AgentCount);
                _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(activePorHandle.PathIndex, activePorHandle.Handle, sourcePositions);
            }
        }

    }
    public void ForceComplete(NativeList<PathRequest> requestedPaths, NativeArray<float2> sources)
    {
        for (int i = ScheduledPortalTraversals.Length - 1; i >= 0; i--)
        {
            HandleWithReqAndPathIndex porTravHandle = ScheduledPortalTraversals[i];
            porTravHandle.Handle.Complete();
            _pathProducer.FinalizePathBuffers(porTravHandle.PathIndex);

            //SCHEDULE ACTIVE PORTAL SUBMISSION
            HandleWithReqAndPathIndex activePorHandle = _activePortalSubmissionScheduler.ScheduleActivePortalSubmission(porTravHandle.PathIndex, porTravHandle.RequestIndex);

            //SCHEDULE REQUESTED SECTOR CALCULATION
            PathRequest pathReq = requestedPaths[porTravHandle.RequestIndex];
            NativeSlice<float2> sourcePositions = new NativeSlice<float2>(sources, pathReq.SourcePositionStartIndex, pathReq.AgentCount);
            _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(activePorHandle.PathIndex, activePorHandle.Handle, sourcePositions);
        }
        ScheduledPortalTraversals.Clear();
    }
}