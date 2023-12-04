using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor.PackageManager.Requests;
using System.Diagnostics;
using UnityEngine.EventSystems;

internal class AdditionPortalTraversalScheduler
{
    PathfindingManager _pathfindingManager;
    PathProducer _pathProducer;
    AdditionActivePortalSubmissionScheduler _additionActivePortalSubmissionScheduler;
    RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;
    NativeList<HandleWithPathIndex> ScheduledAdditionPortalTraversals;

    public AdditionPortalTraversalScheduler(PathfindingManager pathfindingManager, RequestedSectorCalculationScheduler requestedSectorCalculationScheduler)
    {
        ScheduledAdditionPortalTraversals = new NativeList<HandleWithPathIndex>(Allocator.Persistent);
        _pathfindingManager = pathfindingManager;
        _pathProducer = _pathfindingManager.PathProducer;
        _additionActivePortalSubmissionScheduler = new AdditionActivePortalSubmissionScheduler(pathfindingManager);
        _requestedSectorCalculationScheduler = requestedSectorCalculationScheduler;
    }
    public void SchedulePortalTraversalFor(int pathIndex, NativeSlice<float2> sources)
    {
        Path path = _pathProducer.ProducedPaths[pathIndex];
        path.PathAdditionSequenceBorderStartIndex[0] = path.PortalSequenceBorders.Length - 1;
        path.NewPickedSectorStartIndex[0] = path.PickedToSector.Length;

        FieldGraph pickedFieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(path.Offset);

        PortalNodeAdditionReductionJob reductionJob = new PortalNodeAdditionReductionJob()
        {
            TargetIndex = path.TargetIndex,
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
            PortalTraversalDataArray = path.PortalTraversalDataArray,
            SourcePortalIndexList = path.SourcePortalIndexList,
            AStarTraverseIndexList = path.AStartTraverseIndexList,
            IslandFields = pickedFieldGraph.IslandFields,
            SectorStateTable = path.SectorStateTable,
            DijkstraStartIndicies = new NativeList<int>(Allocator.Persistent),
        };

        PortalNodeAdditionTraversalJob travJob = new PortalNodeAdditionTraversalJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            LOSRange = FlowFieldUtilities.LOSRange,
            Target = path.TargetIndex,
            PickedToSector = path.PickedToSector,
            PortalSequenceBorders = path.PortalSequenceBorders,
            PortalNodes = pickedFieldGraph.PortalNodes,
            WindowNodes = pickedFieldGraph.WindowNodes,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            PorPtrs = pickedFieldGraph.PorToPorPtrs,
            SectorNodes = pickedFieldGraph.SectorNodes,
            PortalSequence = path.PortalSequence,
            FlowFieldLength = path.FlowFieldLength,
            PortalTraversalDataArray = path.PortalTraversalDataArray,
            SourcePortalIndexList = path.SourcePortalIndexList,
            IslandFields = pickedFieldGraph.IslandFields,
            SectorStateTable = path.SectorStateTable,
            DijkstraStartIndicies = reductionJob.DijkstraStartIndicies,
            AddedPortalSequenceBorderStartIndex = path.PortalSequenceBorders.Length,
            SectorWithinLOSState = path.SectorWithinLOSState,
            NewPickedSectorStartIndex = path.NewPickedSectorStartIndex,
        };

        JobHandle reductHandle = reductionJob.Schedule();
        JobHandle travHandle = travJob.Schedule(reductHandle);

        if (FlowFieldUtilities.DebugMode) { travHandle.Complete(); }
        HandleWithPathIndex pathHandle = new HandleWithPathIndex()
        {
            Handle = travHandle,
            PathIndex = pathIndex,
        };
        ScheduledAdditionPortalTraversals.Add(pathHandle);
    }

    public void TryComplete(NativeArray<PathData> existingPaths, NativeArray<float2> sources)
    {
        for(int i = ScheduledAdditionPortalTraversals.Length - 1; i >= 0; i--)
        {
            HandleWithPathIndex addPorTravHandle = ScheduledAdditionPortalTraversals[i];
            if (addPorTravHandle.Handle.IsCompleted)
            {
                addPorTravHandle.Handle.Complete();
                ScheduledAdditionPortalTraversals.RemoveAtSwapBack(i);

                //SCHEDULE ADDITION ACTIVE PORTAL SUBMIT JOB
                HandleWithPathIndex _addActivePorSubmitHandle = _additionActivePortalSubmissionScheduler.ScheduleActivePortalSubmission(addPorTravHandle.PathIndex);
                PathData existingPath = existingPaths[addPorTravHandle.PathIndex];
                NativeSlice<float2> sourcePositions = new NativeSlice<float2>(sources, existingPath.FlowRequestSourceStart, existingPath.FlowRequestSourceCount + existingPath.PathAdditionSourceCount);
                _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(addPorTravHandle.PathIndex, _addActivePorSubmitHandle.Handle, sourcePositions);
            }
        }
    }
    public void ForceComplete(NativeArray<PathData> existingPaths, NativeArray<float2> sources)
    {
        for (int i = ScheduledAdditionPortalTraversals.Length - 1; i >= 0; i--)
        {
            HandleWithPathIndex addPorTravHandle = ScheduledAdditionPortalTraversals[i];
            addPorTravHandle.Handle.Complete();
            ScheduledAdditionPortalTraversals.RemoveAtSwapBack(i);

            //SCHEDULE ADDITION ACTIVE PORTAL SUBMIT JOB
            HandleWithPathIndex _addActivePorSubmitHandle = _additionActivePortalSubmissionScheduler.ScheduleActivePortalSubmission(addPorTravHandle.PathIndex);
            PathData existingPath = existingPaths[addPorTravHandle.PathIndex];
            NativeSlice<float2> sourcePositions = new NativeSlice<float2>(sources, existingPath.FlowRequestSourceStart, existingPath.FlowRequestSourceCount + existingPath.PathAdditionSourceCount);
            _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(addPorTravHandle.PathIndex, _addActivePorSubmitHandle.Handle, sourcePositions);

        }
        ScheduledAdditionPortalTraversals.Clear();
    }
}