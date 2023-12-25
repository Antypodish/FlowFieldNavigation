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
    PathContainer _pathContainer;
    AdditionActivePortalSubmissionScheduler _additionActivePortalSubmissionScheduler;
    RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;
    NativeList<PathPipelineInfoWithHandle> ScheduledAdditionPortalTraversals;

    public AdditionPortalTraversalScheduler(PathfindingManager pathfindingManager, RequestedSectorCalculationScheduler requestedSectorCalculationScheduler)
    {
        ScheduledAdditionPortalTraversals = new NativeList<PathPipelineInfoWithHandle>(Allocator.Persistent);
        _pathfindingManager = pathfindingManager;
        _pathContainer = _pathfindingManager.PathContainer;
        _additionActivePortalSubmissionScheduler = new AdditionActivePortalSubmissionScheduler(pathfindingManager);
        _requestedSectorCalculationScheduler = requestedSectorCalculationScheduler;
    }
    public void SchedulePortalTraversalFor(PathPipelineInfoWithHandle pathInfo, NativeSlice<float2> sources)
    {
        Path path = _pathContainer.ProducedPaths[pathInfo.PathIndex];
        PathDestinationData destinationData = _pathContainer.PathDestinationDataList[pathInfo.PathIndex];
        UnsafeList<PathSectorState> sectorStateTable = _pathContainer.PathSectorStateTableList[pathInfo.PathIndex];
        PathPortalTraversalData portalTraversalData = _pathContainer.PathPortalTraversalDataList[pathInfo.PathIndex];
        PathLocationData locationData = _pathContainer.PathLocationDataList[pathInfo.PathIndex];
        portalTraversalData.PathAdditionSequenceBorderStartIndex[0] = portalTraversalData.PortalSequenceBorders.Length - 1;
        portalTraversalData.NewPickedSectorStartIndex[0] = path.PickedToSector.Length;

        FieldGraph pickedFieldGraph = _pathfindingManager.FieldProducer.GetFieldGraphWithOffset(destinationData.Offset);

        PortalNodeAdditionReductionJob reductionJob = new PortalNodeAdditionReductionJob()
        {
            TargetIndex = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize),
            FieldTileSize = FlowFieldUtilities.TileSize,
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            PickedToSector = path.PickedToSector,
            SectorToPickedTable = locationData.SectorToPicked,
            TargetSectorCosts = _pathContainer.TargetSectorIntegrationList[pathInfo.PathIndex],
            PortalNodes = pickedFieldGraph.PortalNodes,
            SecToWinPtrs = pickedFieldGraph.SecToWinPtrs,
            WindowNodes = pickedFieldGraph.WindowNodes,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            SourcePositions = sources,
            PorPtrs = pickedFieldGraph.PorToPorPtrs,
            SectorNodes = pickedFieldGraph.SectorNodes,
            PortalTraversalDataArray = portalTraversalData.PortalTraversalDataArray,
            SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,
            AStarTraverseIndexList = portalTraversalData.AStartTraverseIndexList,
            IslandFields = pickedFieldGraph.IslandFields,
            SectorStateTable = sectorStateTable,
            DijkstraStartIndicies = new NativeList<int>(Allocator.Persistent),
        };

        PortalNodeAdditionTraversalJob travJob = new PortalNodeAdditionTraversalJob()
        {
            SectorColAmount = FlowFieldUtilities.SectorColAmount,
            SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
            SectorMatrixRowAmount = FlowFieldUtilities.SectorMatrixRowAmount,
            SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
            LOSRange = FlowFieldUtilities.LOSRange,
            Target = FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize),
            PickedToSector = path.PickedToSector,
            PortalSequenceBorders = portalTraversalData.PortalSequenceBorders,
            PortalNodes = pickedFieldGraph.PortalNodes,
            WindowNodes = pickedFieldGraph.WindowNodes,
            WinToSecPtrs = pickedFieldGraph.WinToSecPtrs,
            PorPtrs = pickedFieldGraph.PorToPorPtrs,
            SectorNodes = pickedFieldGraph.SectorNodes,
            PortalSequence = portalTraversalData.PortalSequence,
            FlowFieldLength = path.FlowFieldLength,
            PortalTraversalDataArray = portalTraversalData.PortalTraversalDataArray,
            SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,
            IslandFields = pickedFieldGraph.IslandFields,
            SectorStateTable = sectorStateTable,
            DijkstraStartIndicies = reductionJob.DijkstraStartIndicies,
            AddedPortalSequenceBorderStartIndex = portalTraversalData.PortalSequenceBorders.Length,
            SectorWithinLOSState = path.SectorWithinLOSState,
            NewPickedSectorStartIndex = portalTraversalData.NewPickedSectorStartIndex,
        };

        JobHandle reductHandle = reductionJob.Schedule();
        JobHandle travHandle = travJob.Schedule(reductHandle);

        if (FlowFieldUtilities.DebugMode) { travHandle.Complete(); }
        pathInfo.Handle = travHandle;
        ScheduledAdditionPortalTraversals.Add(pathInfo);
    }

    public void TryComplete(NativeArray<float2> sources)
    {
        NativeList<PathRoutineData> pathRoutineDataList = _pathContainer.PathRoutineDataList;
        for(int i = ScheduledAdditionPortalTraversals.Length - 1; i >= 0; i--)
        {
            PathPipelineInfoWithHandle pathInfo = ScheduledAdditionPortalTraversals[i];
            if (pathInfo.Handle.IsCompleted)
            {
                pathInfo.Handle.Complete();
                ScheduledAdditionPortalTraversals.RemoveAtSwapBack(i);

                //SCHEDULE ADDITION ACTIVE PORTAL SUBMIT JOB
                PathPipelineInfoWithHandle _addActivePorSubmitHandle = _additionActivePortalSubmissionScheduler.ScheduleActivePortalSubmission(pathInfo);
                PathRoutineData existingPath = pathRoutineDataList[pathInfo.PathIndex];
                NativeSlice<float2> sourcePositions = new NativeSlice<float2>(sources, existingPath.FlowRequestSourceStart, existingPath.FlowRequestSourceCount + existingPath.PathAdditionSourceCount);
                _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(pathInfo, _addActivePorSubmitHandle.Handle, sourcePositions);

            }
        }
    }
    public void ForceComplete(NativeArray<float2> sources)
    {
        NativeList<PathRoutineData> pathRoutineDataList = _pathContainer.PathRoutineDataList;
        for (int i = ScheduledAdditionPortalTraversals.Length - 1; i >= 0; i--)
        {
            PathPipelineInfoWithHandle pathInfo = ScheduledAdditionPortalTraversals[i];
            pathInfo.Handle.Complete();
            ScheduledAdditionPortalTraversals.RemoveAtSwapBack(i);

            //SCHEDULE ADDITION ACTIVE PORTAL SUBMIT JOB
            PathPipelineInfoWithHandle _addActivePorSubmitHandle = _additionActivePortalSubmissionScheduler.ScheduleActivePortalSubmission(pathInfo);
            PathRoutineData existingPath = pathRoutineDataList[pathInfo.PathIndex];
            NativeSlice<float2> sourcePositions = new NativeSlice<float2>(sources, existingPath.FlowRequestSourceStart, existingPath.FlowRequestSourceCount + existingPath.PathAdditionSourceCount);
            _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(pathInfo, _addActivePorSubmitHandle.Handle, sourcePositions);

        }
        ScheduledAdditionPortalTraversals.Clear();
    }
}