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
    public NativeList<SectorBitArray> PathSectorBitArrays;
    public List<PathPortalTraversalData> PathPortalTraversalDataList;
    public NativeList<int> ProducedPathSubscribers;
    Stack<int> _removedPathIndicies;

    FieldProducer _fieldProducer;
    PathPreallocator _preallocator;
    public PathContainer(PathfindingManager pathfindingManager)
    {
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
        PathSectorBitArrays = new NativeList<SectorBitArray>(Allocator.Persistent);
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
                PathDestinationData destinationData = PathDestinationDataList[i];
                SectorBitArray sectorBitArray = PathSectorBitArrays[i];
                sectorBitArray.Dispose();
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
                _preallocator.SendPreallocationsBack(ref preallocations, path.ActivePortalList, flowData.FlowField, path.IntegrationField, destinationData.Offset);
            }
        }
        _preallocator.CheckForDeallocations();
    }
    public int CreatePath(FinalPathRequest request)
    {
        PreallocationPack preallocations = _preallocator.GetPreallocations(request.Offset);

        int pathIndex;
        if (_removedPathIndicies.Count != 0) { pathIndex = _removedPathIndicies.Pop(); }
        else { pathIndex = ProducedPaths.Count; }

        Path producedPath = new Path()
        {
            IsCalculated = true,
            PickedToSector = preallocations.PickedToSector,
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
            Destination = request.Destination,
            DesiredDestination = request.DesiredDestination,
            Offset = request.Offset,
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
            PathSectorBitArrays.Add(new SectorBitArray(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent));
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
            PathSectorBitArrays[pathIndex] = new SectorBitArray(FlowFieldUtilities.SectorMatrixTileAmount, Allocator.Persistent);
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
        LocalIndex1d local = FlowFieldUtilities.GetLocal1D(FlowFieldUtilities.PosTo2D(destinationData.Destination, FlowFieldUtilities.TileSize), sectorColAmount, sectorMatrixColAmount);
        return (path.IntegrationField[locationData.SectorToPicked[local.sector] + local.index].Mark & IntegrationMark.LOSPass) == IntegrationMark.LOSPass;
    }
}