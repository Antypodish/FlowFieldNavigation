using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using UnityEditor.PackageManager.Requests;
using UnityEngine.UIElements;

public class PathContainer
{
    public List<Path> ProducedPaths;
    public NativeList<PathLocationData> PathLocationDataList;
    public NativeList<PathFlowData> PathFlowDataList;
    public NativeList<UnsafeList<PathSectorState>> PathSectorStateTableList;
    public NativeList<PathDestinationData> PathDestinationDataList;
    public NativeList<PathState> PathStateList;
    public NativeList<UnsafeList<DijkstraTile>> TargetSectorIntegrationList;
    public NativeList<PathRoutineData> PathRoutineDataList;
    public List<PathPortalTraversalData> PathPortalTraversalDataList;
    public NativeList<int> ProducedPathSubscribers;
    Stack<int> _removedPathIndicies;

    FieldProducer _fieldProducer;
    PathPreallocator _preallocator;
    PathfindingManager _pathfindingManager;
    public PathContainer(PathfindingManager pathfindingManager)
    {
        _pathfindingManager = pathfindingManager;
        _fieldProducer = pathfindingManager.FieldProducer;
        ProducedPaths = new List<Path>(1);
        _preallocator = new PathPreallocator(_fieldProducer, FlowFieldUtilities.SectorTileAmount, FlowFieldUtilities.SectorMatrixTileAmount);
        _removedPathIndicies = new Stack<int>();
        ProducedPathSubscribers = new NativeList<int>(Allocator.Persistent);
        PathLocationDataList = new NativeList<PathLocationData>(1, Allocator.Persistent);
        PathFlowDataList = new NativeList<PathFlowData>(Allocator.Persistent);
        PathSectorStateTableList = new NativeList<UnsafeList<PathSectorState>>(Allocator.Persistent);
        PathPortalTraversalDataList = new List<PathPortalTraversalData>();
        PathDestinationDataList = new NativeList<PathDestinationData>(Allocator.Persistent);
        PathStateList = new NativeList<PathState>(Allocator.Persistent);
        TargetSectorIntegrationList = new NativeList<UnsafeList<DijkstraTile>>(Allocator.Persistent);
        PathRoutineDataList = new NativeList<PathRoutineData>(Allocator.Persistent);
    }
    public void Update()
    {
        for (int i = 0; i < ProducedPaths.Count; i++)
        {
            Path path = ProducedPaths[i];
            PathState pathState = PathStateList[i];
            if(pathState == PathState.Removed) { continue; }
            int subsciriber = ProducedPathSubscribers[i];
            if(subsciriber == 0 && path.IsCalculated) { pathState = PathState.ToBeDisposed; }
            if (pathState == PathState.ToBeDisposed && path.IsCalculated)
            {
                PathLocationData locationData = PathLocationDataList[i];
                PathFlowData flowData = PathFlowDataList[i];
                UnsafeList<PathSectorState> sectorStateTable = PathSectorStateTableList[i];
                PathPortalTraversalData portalTraversalData = PathPortalTraversalDataList[i];
                UnsafeList<DijkstraTile> targetSectorIntegration = TargetSectorIntegrationList[i];
                path.Dispose();
                locationData.Dispose();
                flowData.Dispose();
                portalTraversalData.Dispose();
                PathStateList[i] = PathState.Removed;
                _removedPathIndicies.Push(i);
                PreallocationPack preallocations = new PreallocationPack()
                {
                    SectorToPicked = locationData.SectorToPicked,
                    PickedToSector = path.PickedToSector,
                    PortalSequence = portalTraversalData.PortalSequence,
                    PortalSequenceBorders = portalTraversalData.PortalSequenceBorders,
                    PortalTraversalDataArray = portalTraversalData.PortalTraversalDataArray,
                    TargetSectorCosts = targetSectorIntegration,
                    FlowFieldLength = path.FlowFieldLength,
                    SourcePortalIndexList = portalTraversalData.SourcePortalIndexList,
                    AStartTraverseIndexList = portalTraversalData.AStartTraverseIndexList,
                    TargetSectorPortalIndexList = portalTraversalData.TargetSectorPortalIndexList,
                    PortalTraversalFastMarchingQueue = path.PortalTraversalFastMarchingQueue,
                    SectorStateTable = sectorStateTable,
                };
                _preallocator.SendPreallocationsBack(ref preallocations, path.ActivePortalList, flowData.FlowField, path.IntegrationField, path.Offset);
            }
        }
        _preallocator.CheckForDeallocations();
    }
    public int CreatePath(FinalPathRequest request)
    {
        int2 destinationIndex = new int2(Mathf.FloorToInt(request.Destination.x / FlowFieldUtilities.TileSize), Mathf.FloorToInt(request.Destination.y / FlowFieldUtilities.TileSize));
        PreallocationPack preallocations = _preallocator.GetPreallocations(request.Offset);

        int pathIndex;
        if (_removedPathIndicies.Count != 0) { pathIndex = _removedPathIndicies.Pop(); }
        else { pathIndex = ProducedPaths.Count; }

        Path producedPath = new Path()
        {
            IsCalculated = true,
            PickedToSector = preallocations.PickedToSector,
            Offset = request.Offset,
            FlowFieldLength = preallocations.FlowFieldLength,
            PortalTraversalFastMarchingQueue = preallocations.PortalTraversalFastMarchingQueue,
            NotActivePortalList = new NativeList<int>(Allocator.Persistent),
            SectorFlowStartIndiciesToCalculateIntegration = new NativeList<int>(Allocator.Persistent),
            SectorFlowStartIndiciesToCalculateFlow = new NativeList<int>(Allocator.Persistent),
            SectorWithinLOSState = new NativeArray<SectorsWihinLOSArgument>(1, Allocator.Persistent),
            DynamicArea = new DynamicArea()
            {
                FlowFieldCalculationBuffer = new UnsafeList<FlowData>(0, Allocator.Persistent),
                IntegrationField = new NativeList<IntegrationTile>(0, Allocator.Persistent),
            }
        };
        PathDestinationData destinationData = new PathDestinationData()
        {
            DestinationType = request.Type,
            TargetAgentIndex = request.TargetAgentIndex,
            TargetIndex = destinationIndex,
            Destination = request.Destination,
        };

        PathLocationData locationData = new PathLocationData()
        {
            SectorToPicked = preallocations.SectorToPicked,
            DynamicAreaPickedSectorFlowStarts = new UnsafeList<SectorFlowStart>(0, Allocator.Persistent),
        };

        PathFlowData flowData = new PathFlowData()
        {
            DynamicAreaFlowField = new UnsafeList<FlowData>(0, Allocator.Persistent),
        };

        PathPortalTraversalData portalTraversalData = new PathPortalTraversalData()
        {
            PortalSequenceBorders = preallocations.PortalSequenceBorders,
            PortalSequence = preallocations.PortalSequence,
            PortalTraversalDataArray = preallocations.PortalTraversalDataArray,
            SourcePortalIndexList = preallocations.SourcePortalIndexList,
            AStartTraverseIndexList = preallocations.AStartTraverseIndexList,
            TargetSectorPortalIndexList = preallocations.TargetSectorPortalIndexList,
            NewPickedSectorStartIndex = new NativeArray<int>(1, Allocator.Persistent),
            PathAdditionSequenceBorderStartIndex = new NativeArray<int>(1, Allocator.Persistent),
        };
        if (ProducedPaths.Count == pathIndex)
        {
            ProducedPaths.Add(producedPath);
            PathLocationDataList.Add(locationData);
            PathFlowDataList.Add(flowData);
            PathSectorStateTableList.Add(preallocations.SectorStateTable);
            PathPortalTraversalDataList.Add(portalTraversalData);
            PathDestinationDataList.Add(destinationData);
            PathStateList.Add(PathState.Clean);
            TargetSectorIntegrationList.Add(preallocations.TargetSectorCosts);
            PathRoutineDataList.Add(new PathRoutineData());
            ProducedPathSubscribers.Add(request.SourceCount);
        }
        else
        {
            ProducedPaths[pathIndex] = producedPath;
            PathLocationDataList[pathIndex] = locationData;
            PathFlowDataList[pathIndex] = flowData;
            PathSectorStateTableList[pathIndex] = preallocations.SectorStateTable;
            PathPortalTraversalDataList[pathIndex] = portalTraversalData;
            PathDestinationDataList[pathIndex] = destinationData;
            PathStateList[pathIndex] = PathState.Clean;
            TargetSectorIntegrationList[pathIndex] = preallocations.TargetSectorCosts;
            PathRoutineDataList[pathIndex] = new PathRoutineData();
            ProducedPathSubscribers[pathIndex] = request.SourceCount;
        }

        return pathIndex;
    }
    public void FinalizePathBuffers(int pathIndex)
    {
        Path path = ProducedPaths[pathIndex];
        PathFlowData pathFlowData = PathFlowDataList[pathIndex];
        NativeArray<int> flowFieldLength = path.FlowFieldLength;
        pathFlowData.FlowField = _preallocator.GetFlowField(flowFieldLength[0]);
        pathFlowData.LOSMap = new UnsafeLOSBitmap(flowFieldLength[0], Allocator.Persistent, NativeArrayOptions.ClearMemory);
        path.IntegrationField = _preallocator.GetIntegrationField(flowFieldLength[0]);
        path.ActivePortalList = _preallocator.GetActiveWaveFrontListPersistent(path.PickedToSector.Length);

        PathFlowDataList[pathIndex] = pathFlowData;
    }
    public void ResizeActiveWaveFrontList(int newLength, NativeList<UnsafeList<ActiveWaveFront>> activeWaveFrontList)
    {
        _preallocator.AddToActiveWaveFrontList(newLength, activeWaveFrontList);
    }
    public bool IsLOSCalculated(int pathIndex)
    {
        Path path = ProducedPaths[pathIndex];
        PathLocationData locationData = PathLocationDataList[pathIndex];
        PathDestinationData destinationData = PathDestinationDataList[pathIndex];
        int sectorColAmount = FlowFieldUtilities.SectorColAmount;
        int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;
        LocalIndex1d local = FlowFieldUtilities.GetLocal1D(destinationData.TargetIndex, sectorColAmount, sectorMatrixColAmount);
        return (path.IntegrationField[locationData.SectorToPicked[local.sector] + local.index].Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
    }
    public void GetCurrentPathData(NativeList<PathData> currentPathData, NativeArray<AgentData>.ReadOnly agentData)
    {
        float tileSize = FlowFieldUtilities.TileSize;
        int sectorColAmount = FlowFieldUtilities.SectorColAmount;
        int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;
        currentPathData.Length = ProducedPaths.Count;

        for(int i = 0; i < ProducedPaths.Count; i++)
        {
            PathLocationData locationData = PathLocationDataList[i];
            PathFlowData flowData = PathFlowDataList[i];
            UnsafeList<PathSectorState> sectorStateTable = PathSectorStateTableList[i];
            PathDestinationData destinationData = PathDestinationDataList[i];
            PathState pathState = PathStateList[i];
            UnsafeList<DijkstraTile> targetSectorIntegration = TargetSectorIntegrationList[i];
            if (pathState == PathState.Removed)
            {
                currentPathData[i] = new PathData()
                {
                    State = PathState.Removed,
                };
            }
            else if(destinationData.DestinationType == DestinationType.StaticDestination)
            {
                currentPathData[i] = new PathData()
                {
                    State = pathState,
                    Target = destinationData.Destination,
                    Task = 0,
                    SectorStateTable = sectorStateTable,
                    SectorToPicked = locationData.SectorToPicked,
                    FlowField = flowData.FlowField,
                    ReconstructionRequestIndex = -1,
                    Type = destinationData.DestinationType,
                    TargetAgentIndex = destinationData.TargetAgentIndex,
                };
            }
            else if(destinationData.DestinationType == DestinationType.DynamicDestination)
            {
                float3 targetAgentPos = agentData[destinationData.TargetAgentIndex].Position;
                float2 targetAgentPos2 = new float2(targetAgentPos.x, targetAgentPos.z);
                int2 oldTargetIndex = destinationData.TargetIndex;
                int2 newTargetIndex = (int2)math.floor(targetAgentPos2 / tileSize);
                int oldSector = FlowFieldUtilities.GetSector1D(oldTargetIndex, sectorColAmount, sectorMatrixColAmount);
                LocalIndex1d newLocal = FlowFieldUtilities.GetLocal1D(newTargetIndex, sectorColAmount, sectorMatrixColAmount);
                bool outOfReach = oldSector != newLocal.sector;
                DijkstraTile targetTile = targetSectorIntegration[newLocal.index];
                outOfReach = outOfReach || targetTile.IntegratedCost == float.MaxValue;
                DynamicDestinationState destinationState = oldTargetIndex.Equals(newTargetIndex) ? DynamicDestinationState.None : DynamicDestinationState.Moved;
                destinationState = outOfReach ? DynamicDestinationState.OutOfReach : destinationState;
                destinationData.Destination = targetAgentPos2;
                destinationData.TargetIndex = newTargetIndex;
                PathDestinationDataList[i] = destinationData;
                currentPathData[i] = new PathData()
                {
                    State = pathState,
                    Target = destinationData.Destination,
                    Task = 0,
                    SectorStateTable = sectorStateTable,
                    SectorToPicked = locationData.SectorToPicked,
                    FlowField = flowData.FlowField,
                    ReconstructionRequestIndex = -1,
                    Type = destinationData.DestinationType,
                    TargetAgentIndex = destinationData.TargetAgentIndex,
                    DestinationState = destinationState,
                };
            }
        }
    }
}