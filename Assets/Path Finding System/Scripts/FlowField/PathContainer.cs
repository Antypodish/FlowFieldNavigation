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
    }
    public void Update()
    {
        for (int i = 0; i < ProducedPaths.Count; i++)
        {
            Path path = ProducedPaths[i];
            if(path.State == PathState.Removed) { continue; }
            int subsciriber = ProducedPathSubscribers[i];
            if(subsciriber == 0 && path.IsCalculated) { path.State = PathState.ToBeDisposed; }
            if (path.State == PathState.ToBeDisposed && path.IsCalculated)
            {
                path.Dispose();
                path.State = PathState.Removed;
                _removedPathIndicies.Push(i);
                PreallocationPack preallocations = new PreallocationPack()
                {
                    SectorToPicked = path.SectorToPicked,
                    PickedToSector = path.PickedToSector,
                    PortalSequence = path.PortalSequence,
                    PortalSequenceBorders = path.PortalSequenceBorders,
                    PortalTraversalDataArray = path.PortalTraversalDataArray,
                    TargetSectorCosts = path.TargetSectorCosts,
                    FlowFieldLength = path.FlowFieldLength,
                    SourcePortalIndexList = path.SourcePortalIndexList,
                    AStartTraverseIndexList = path.AStartTraverseIndexList,
                    TargetSectorPortalIndexList = path.TargetSectorPortalIndexList,
                    PortalTraversalFastMarchingQueue = path.PortalTraversalFastMarchingQueue,
                    SectorStateTable = path.SectorStateTable,
                };
                _preallocator.SendPreallocationsBack(ref preallocations, path.ActiveWaveFrontList, path.FlowField, path.IntegrationField, path.Offset);
            }
        }
        _preallocator.CheckForDeallocations();
    }
    public int CreatePath(PathRequest request)
    {
        int2 destinationIndex = new int2(Mathf.FloorToInt(request.Destination.x / FlowFieldUtilities.TileSize), Mathf.FloorToInt(request.Destination.y / FlowFieldUtilities.TileSize));
        PreallocationPack preallocations = _preallocator.GetPreallocations(request.Offset);

        int pathIndex;
        if (_removedPathIndicies.Count != 0) { pathIndex = _removedPathIndicies.Pop(); }
        else { pathIndex = ProducedPaths.Count; }

        Path producedPath = new Path()
        {
            IsCalculated = true,
            Type = request.PathType,
            TargetAgentIndex = request.TargetAgentIndex,
            Id = pathIndex,
            PickedToSector = preallocations.PickedToSector,
            PortalSequenceBorders = preallocations.PortalSequenceBorders,
            TargetIndex = destinationIndex,
            TargetSectorCosts = preallocations.TargetSectorCosts,
            Destination = request.Destination,
            State = PathState.Clean,
            Offset = request.Offset,
            PortalSequence = preallocations.PortalSequence,
            PortalTraversalDataArray = preallocations.PortalTraversalDataArray,
            SectorToPicked = preallocations.SectorToPicked,
            FlowFieldLength = preallocations.FlowFieldLength,
            SourcePortalIndexList = preallocations.SourcePortalIndexList,
            AStartTraverseIndexList = preallocations.AStartTraverseIndexList,
            TargetSectorPortalIndexList = preallocations.TargetSectorPortalIndexList,
            PortalTraversalFastMarchingQueue = preallocations.PortalTraversalFastMarchingQueue,
            SectorStateTable = preallocations.SectorStateTable,
            PathAdditionSequenceBorderStartIndex = new NativeArray<int>(1, Allocator.Persistent),
            NotActivePortalList = new NativeList<int>(Allocator.Persistent),
            NewPickedSectorStartIndex = new NativeArray<int>(1, Allocator.Persistent),
            SectorFlowStartIndiciesToCalculateIntegration = new NativeList<int>(Allocator.Persistent),
            SectorFlowStartIndiciesToCalculateFlow = new NativeList<int>(Allocator.Persistent),
            SectorWithinLOSState = new NativeArray<SectorsWihinLOSArgument>(1, Allocator.Persistent),
        };

        if (ProducedPaths.Count == pathIndex)
        {
            ProducedPaths.Add(producedPath);
            ProducedPathSubscribers.Add(request.AgentCount);
        }
        else
        {
            ProducedPaths[pathIndex] = producedPath;
            ProducedPathSubscribers[pathIndex] = request.AgentCount;
        }

        return pathIndex;
    }
    public void FinalizePathBuffers(int pathIndex)
    {
        Path path = ProducedPaths[pathIndex];
        NativeArray<int> flowFieldLength = path.FlowFieldLength;
        path.FlowField = _preallocator.GetFlowField(flowFieldLength[0]);
        path.IntegrationField = _preallocator.GetIntegrationField(flowFieldLength[0]);
        path.LOSMap = new UnsafeLOSBitmap(flowFieldLength[0], Allocator.Persistent, NativeArrayOptions.ClearMemory);
        path.ActiveWaveFrontList = _preallocator.GetActiveWaveFrontListPersistent(path.PickedToSector.Length);
    }
    public void ResizeActiveWaveFrontList(int newLength, NativeList<UnsafeList<ActiveWaveFront>> activeWaveFrontList)
    {
        _preallocator.AddToActiveWaveFrontList(newLength, activeWaveFrontList);
    }
    public void GetCurrentPathData(NativeList<PathData> currentPathData, NativeArray<AgentData>.ReadOnly agentData)
    {
        float tileSize = FlowFieldUtilities.TileSize;
        int sectorColAmount = FlowFieldUtilities.SectorColAmount;
        int sectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount;
        currentPathData.Length = ProducedPaths.Count;

        for(int i = 0; i < ProducedPaths.Count; i++)
        {
            Path path = ProducedPaths[i];
            if(path.State == PathState.Removed)
            {
                currentPathData[i] = new PathData()
                {
                    State = PathState.Removed,
                };
            }
            else if(path.Type == PathType.StaticDestination)
            {
                currentPathData[i] = new PathData()
                {
                    State = path.State,
                    Target = path.Destination,
                    Task = 0,
                    SectorStateTable = path.SectorStateTable,
                    SectorToPicked = path.SectorToPicked,
                    FlowField = path.FlowField,
                    ReconstructionRequestIndex = -1,
                    Type = path.Type,
                    TargetAgentIndex = path.TargetAgentIndex,
                };
            }
            else if(path.Type == PathType.DynamicDestination)
            {
                float3 targetAgentPos = agentData[path.TargetAgentIndex].Position;
                float2 targetAgentPos2 = new float2(targetAgentPos.x, targetAgentPos.z);
                int2 oldTargetIndex = path.TargetIndex;
                int2 newTargetIndex = (int2)math.floor(targetAgentPos2 / tileSize);
                int oldSector = FlowFieldUtilities.GetSector1D(oldTargetIndex, sectorColAmount, sectorMatrixColAmount);
                LocalIndex1d newLocal = FlowFieldUtilities.GetLocal1D(newTargetIndex, sectorColAmount, sectorMatrixColAmount);
                bool outOfReach = oldSector != newLocal.sector;
                DijkstraTile targetTile = path.TargetSectorCosts[newLocal.index];
                outOfReach = outOfReach || targetTile.IntegratedCost == float.MaxValue;
                DynamicDestinationState destinationState = oldTargetIndex.Equals(newTargetIndex) ? DynamicDestinationState.None : DynamicDestinationState.Moved;
                destinationState = outOfReach ? DynamicDestinationState.OutOfReach : destinationState;
                path.Destination = targetAgentPos2;
                path.TargetIndex = newTargetIndex;
                currentPathData[i] = new PathData()
                {
                    State = path.State,
                    Target = path.Destination,
                    Task = 0,
                    SectorStateTable = path.SectorStateTable,
                    SectorToPicked = path.SectorToPicked,
                    FlowField = path.FlowField,
                    ReconstructionRequestIndex = -1,
                    Type = path.Type,
                    TargetAgentIndex = path.TargetAgentIndex,
                    DestinationState = destinationState,
                };
            }
        }
    }
}