using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Diagnostics;

namespace FlowFieldNavigation
{
    internal class PathConstructionPipeline
    {
        internal NativeList<int> AgentsLookingForPath;
        internal NativeList<PathRequestRecord> AgentsLookingForPathRecords;
        const int _FinalPathRequestExpansionJobCount = 12;

        FlowFieldNavigationManager _navigationManager;
        PathDataContainer _pathContainer;
        PortalTraversalScheduler _portalTravesalScheduler;
        RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;
        AdditionPortalTraversalScheduler _additionPortalTraversalScheduler;
        LOSIntegrationScheduler _losIntegrationScheduler;
        DynamicAreaScheduler _dynamicAreaScheduler;
        PathConstructionTester _pathConstructionTester;
        NativeList<float2> _sourcePositions;
        NativeList<OffsetDerivedPathRequest> _offsetDerivedPathRequests;
        NativeList<FinalPathRequest> _finalPathRequests;
        NativeReference<int> _currentPathSourceCount;
        NativeReference<int> _pathRequestSourceCount;
        NativeList<PathTask> _agentPathTaskList;
        NativeArray<IslandFieldProcessor> _islandFieldProcessors;
        NativeList<int> _newPathIndicies;
        NativeList<int> _destinationUpdatedPathIndicies;
        NativeList<int> _expandedPathIndicies;
        NativeList<int> _agentIndiciesToStartMoving;
        NativeList<int> _newPathRequestedAgentIndicies;
        NativeList<int> _agentIndiciesToUnsubCurPath;
        NativeList<int> _agentIndiciesToSubNewPath;
        NativeList<AgentAndPath> _agentIndiciesToSubExistingPath;
        NativeList<int> _readyAgentsLookingForPath;
        NativeList<PathRequestRecord> _readyAgentsLookingForPathRecords;
        NativeHashMap<int, int> _flockIndexToPathRequestIndex;
        NativeList<FlockSlice> _hashMapFlockSlices;
        NativeList<int> _hashMapPathIndicies;
        List<JobHandle> _pathfindingTaskOrganizationHandle;
        internal PathConstructionPipeline(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            _pathContainer = navigationManager.PathDataContainer;
            _losIntegrationScheduler = new LOSIntegrationScheduler(navigationManager);
            _requestedSectorCalculationScheduler = new RequestedSectorCalculationScheduler(navigationManager, _losIntegrationScheduler);
            _portalTravesalScheduler = new PortalTraversalScheduler(navigationManager, _requestedSectorCalculationScheduler);
            _additionPortalTraversalScheduler = new AdditionPortalTraversalScheduler(navigationManager, _requestedSectorCalculationScheduler);
            _dynamicAreaScheduler = new DynamicAreaScheduler(navigationManager);
            _sourcePositions = new NativeList<float2>(Allocator.Persistent);
            _pathfindingTaskOrganizationHandle = new List<JobHandle>(1);
            _offsetDerivedPathRequests = new NativeList<OffsetDerivedPathRequest>(Allocator.Persistent);
            _finalPathRequests = new NativeList<FinalPathRequest>(Allocator.Persistent);
            _currentPathSourceCount = new NativeReference<int>(Allocator.Persistent);
            _pathRequestSourceCount = new NativeReference<int>(Allocator.Persistent);
            _agentPathTaskList = new NativeList<PathTask>(Allocator.Persistent);
            _newPathIndicies = new NativeList<int>(Allocator.Persistent);
            _destinationUpdatedPathIndicies = new NativeList<int>(Allocator.Persistent);
            _expandedPathIndicies = new NativeList<int>(Allocator.Persistent);
            _pathConstructionTester = new PathConstructionTester();
            _agentIndiciesToStartMoving = new NativeList<int>(Allocator.Persistent);
            _newPathRequestedAgentIndicies = new NativeList<int>(Allocator.Persistent);
            _agentIndiciesToUnsubCurPath = new NativeList<int>(Allocator.Persistent);
            _agentIndiciesToSubNewPath = new NativeList<int>(Allocator.Persistent);
            AgentsLookingForPath = new NativeList<int>(Allocator.Persistent);
            _readyAgentsLookingForPath = new NativeList<int>(Allocator.Persistent);
            AgentsLookingForPathRecords = new NativeList<PathRequestRecord>(Allocator.Persistent);
            _readyAgentsLookingForPathRecords = new NativeList<PathRequestRecord>(Allocator.Persistent);
            _flockIndexToPathRequestIndex = new NativeHashMap<int, int>(0, Allocator.Persistent);
            _agentIndiciesToSubExistingPath = new NativeList<AgentAndPath>(Allocator.Persistent);
            _hashMapFlockSlices = new NativeList<FlockSlice>(Allocator.Persistent);
            _hashMapPathIndicies = new NativeList<int>(Allocator.Persistent);
        }
        internal void DisposeAll()
        {
            _sourcePositions.Dispose();
            _pathfindingTaskOrganizationHandle = null;
            _offsetDerivedPathRequests.Dispose();
            _finalPathRequests.Dispose();
            _currentPathSourceCount.Dispose();
            _pathRequestSourceCount.Dispose();
            _agentPathTaskList.Dispose();
            _newPathIndicies.Dispose();
            _destinationUpdatedPathIndicies.Dispose();
            _expandedPathIndicies.Dispose();
            _agentIndiciesToStartMoving.Dispose();
            _newPathRequestedAgentIndicies.Dispose();
            _agentIndiciesToSubNewPath.Dispose();
            _agentIndiciesToUnsubCurPath.Dispose();
            AgentsLookingForPath.Dispose();
            AgentsLookingForPathRecords.Dispose();
            _flockIndexToPathRequestIndex.Dispose();
            _flockIndexToPathRequestIndex.Dispose();
            _agentIndiciesToSubExistingPath.Dispose();
            _readyAgentsLookingForPath.Dispose();
            _hashMapPathIndicies.Dispose();
            _hashMapFlockSlices.Dispose();
            _readyAgentsLookingForPathRecords.Dispose();
            _portalTravesalScheduler.DisposeAll();
            _requestedSectorCalculationScheduler.DisposeAll();
            _additionPortalTraversalScheduler.DisposeAll();
            _losIntegrationScheduler.DisposeAll();
            _dynamicAreaScheduler.DisposeAll();
            _portalTravesalScheduler = null;
            _requestedSectorCalculationScheduler = null;
            _additionPortalTraversalScheduler = null;
            _losIntegrationScheduler = null;
            _dynamicAreaScheduler = null;
        }
        internal void ShcedulePathRequestEvalutaion(NativeList<PathRequest> requestedPaths,
            NativeArray<UnsafeListReadOnly<byte>> costFieldCosts,
            NativeArray<SectorBitArray>.ReadOnly editedSectorBitArray,
            NativeArray<IslandFieldProcessor> islandFieldProcessors,
            JobHandle islandFieldHandleAsDependency)
        {
            //RESET CONTAINERS
            _sourcePositions.Clear();
            _offsetDerivedPathRequests.Clear();
            _finalPathRequests.Clear();
            _newPathIndicies.Clear();
            _destinationUpdatedPathIndicies.Clear();
            _expandedPathIndicies.Clear();
            _agentIndiciesToStartMoving.Clear();
            _newPathRequestedAgentIndicies.Clear();
            _agentIndiciesToUnsubCurPath.Clear();
            _agentIndiciesToSubNewPath.Clear();
            _flockIndexToPathRequestIndex.Clear();
            _agentIndiciesToSubExistingPath.Clear();
            _readyAgentsLookingForPath.Clear();
            _readyAgentsLookingForPathRecords.Clear();
            _pathRequestSourceCount.Value = 0;
            _currentPathSourceCount.Value = 0;

            NativeArray<AgentData> agentDataArray = _navigationManager.AgentDataContainer.AgentDataList.AsArray();
            NativeArray<int> AgentNewPathIndicies = _navigationManager.AgentDataContainer.AgentNewPathIndicies.AsArray();
            NativeArray<int> AgentCurPathIndicies = _navigationManager.AgentDataContainer.AgentCurPathIndicies.AsArray();
            NativeArray<int> AgentFlockIndexArray = _navigationManager.AgentDataContainer.AgentFlockIndicies.AsArray();
            _islandFieldProcessors = islandFieldProcessors;
            NativeArray<UnsafeList<DijkstraTile>> targetSectorIntegrations = _pathContainer.TargetSectorIntegrationList.AsArray();
            NativeArray<PathLocationData> pathLocationDataArray = _pathContainer.PathLocationDataList.AsArray();
            NativeArray<PathFlowData> pathFlowDataArray = _pathContainer.PathFlowDataList.AsArray();
            NativeArray<PathState> pathStateArray = _pathContainer.ExposedPathStateList.AsArray();
            NativeArray<PathDestinationData> pathDestinationDataArray = _pathContainer.PathDestinationDataList.AsArray();
            NativeArray<PathRoutineData> pathRoutineDataArray = _pathContainer.PathRoutineDataList.AsArray();
            NativeArray<UnsafeList<PathSectorState>> pathSectorStateTables = _pathContainer.PathSectorStateTableList.AsArray();
            NativeArray<SectorBitArray> pathSectorBitArrays = _pathContainer.PathSectorBitArrays.AsArray();
            NativeArray<int> pathFlockIndexArray = _pathContainer.PathFlockIndicies.AsArray();
            NativeArray<int> pathSubscriberCountArray = _pathContainer.PathSubscriberCounts.AsArray();

            FlockIndexSubmissionJob flockSubmission = new FlockIndexSubmissionJob()
            {
                InitialPathRequestCount = requestedPaths.Length,
                AgentFlockIndexArray = AgentFlockIndexArray,
                AgentNewPathIndexArray = AgentNewPathIndicies,
                InitialPathRequests = requestedPaths.AsArray(),
                FlockList = _navigationManager.FlockDataContainer.FlockList,
                UnusedFlockIndexList = _navigationManager.FlockDataContainer.UnusedFlockIndexList,
            };
            flockSubmission.Schedule().Complete();

            //Self targeting fix
            DynamicPathRequestSelfTargetingFixJob selfTargetingFix = new DynamicPathRequestSelfTargetingFixJob()
            {
                AgentNewPathIndicies = AgentNewPathIndicies,
                InitialPathRequests = requestedPaths.AsArray(),
            };
            JobHandle selfTargetingFixHandle = selfTargetingFix.Schedule();
            if (FlowFieldUtilities.DebugMode) { selfTargetingFixHandle.Complete(); }

            //Agent task cleaning
            _agentPathTaskList.Length = agentDataArray.Length;
            NativeArrayCleaningJob<PathTask> agentTaskCleaning = new NativeArrayCleaningJob<PathTask>()
            {
                Array = _agentPathTaskList.AsArray(),
            };
            JobHandle agentTaskCleaningHandle = agentTaskCleaning.Schedule();
            if (FlowFieldUtilities.DebugMode) { agentTaskCleaningHandle.Complete(); }

            //Routine data reset
            PathRoutineDataResetJob routineDataReset = new PathRoutineDataResetJob()
            {
                PathOrganizationDataArray = pathRoutineDataArray,
            };
            JobHandle routineDataResetHandle = routineDataReset.Schedule(islandFieldHandleAsDependency);
            if (FlowFieldUtilities.DebugMode) { routineDataResetHandle.Complete(); }

            //Set agent indicies to start moving
            AgentsToStartMovingSetJob agentsToStartMovingSet = new AgentsToStartMovingSetJob()
            {
                AgentNewPathIndicies = AgentNewPathIndicies,
                AgentsToStartMoving = _agentIndiciesToStartMoving,
            };
            JobHandle agentsToStartMovingSetHandle = agentsToStartMovingSet.Schedule(JobHandle.CombineDependencies(selfTargetingFixHandle, routineDataResetHandle));
            if (FlowFieldUtilities.DebugMode) { agentsToStartMovingSetHandle.Complete(); }

            //Check edited paths
            JobHandle sectorEditCheckHandle = agentsToStartMovingSetHandle;
            if (editedSectorBitArray.Length != 0)
            {
                PathSectorEditCheckJob sectorEditCheck = new PathSectorEditCheckJob()
                {
                    PathStateArray = pathStateArray,
                    FieldEditSectorIDArray = editedSectorBitArray,
                    PathSectorIDArray = pathSectorBitArrays,
                    PathDestinationDataArray = pathDestinationDataArray,
                    PathRoutineDataArray = pathRoutineDataArray,
                };
                sectorEditCheckHandle = sectorEditCheck.Schedule(pathRoutineDataArray.Length, 64, agentsToStartMovingSetHandle);
                if (FlowFieldUtilities.DebugMode) { sectorEditCheckHandle.Complete(); }
            }

            //Routine data setting
            PathRoutineDataCalculationJob routineDataCalculation = new PathRoutineDataCalculationJob()
            {
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                TileSize = FlowFieldUtilities.TileSize,
                SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                FieldColAmount = FlowFieldUtilities.FieldColAmount,
                FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                FieldMaxXExcluding = FlowFieldUtilities.FieldMaxXExcluding,
                FieldMaxYExcluding = FlowFieldUtilities.FieldMaxYExcluding,
                FieldMinXIncluding = FlowFieldUtilities.FieldMinXIncluding,
                FieldMinYIncluding = FlowFieldUtilities.FieldMinYIncluding,
                PathStateArray = pathStateArray,
                TargetSectorIntegrations = targetSectorIntegrations,
                AgentDataArray = agentDataArray,
                PathDestinationDataArray = pathDestinationDataArray,
                PathFlowDataArray = pathFlowDataArray,
                PathLocationDataArray = pathLocationDataArray,
                PathOrganizationDataArray = pathRoutineDataArray,
                IslandFieldProcessors = _islandFieldProcessors,
                CostFields = costFieldCosts,
            };
            JobHandle routineDataCalculationHandle = routineDataCalculation.Schedule(pathRoutineDataArray.Length, 64, sectorEditCheckHandle);
            if (FlowFieldUtilities.DebugMode) { routineDataCalculationHandle.Complete(); _pathConstructionTester.RoutineDataCalculationTest(routineDataCalculation); }

            //Current path reconstruction submission
            CurrentPathReconstructionDeterminationJob reconstructionDetermination = new CurrentPathReconstructionDeterminationJob()
            {
                PathFlockIndexArray = pathFlockIndexArray,
                AgentCurPathIndicies = AgentCurPathIndicies,
                AgentNewPathIndicies = AgentNewPathIndicies,
                AgentDataArray = agentDataArray,
                PathRequests = requestedPaths,
                PathStateArray = pathStateArray,
                PathDestinationDataArray = pathDestinationDataArray,
                PathRoutineDataArray = pathRoutineDataArray,
                FlockIndexToPathRequestIndex = _flockIndexToPathRequestIndex,
            };
            JobHandle reconstructionDeterminationHandle = reconstructionDetermination.Schedule(JobHandle.CombineDependencies(routineDataCalculationHandle, agentTaskCleaningHandle, selfTargetingFixHandle));
            if (FlowFieldUtilities.DebugMode) { reconstructionDeterminationHandle.Complete(); }

            //Current path update submission
            CurrentPathUpdateDeterminationJob updateDetermination = new CurrentPathUpdateDeterminationJob()
            {
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                TileSize = FlowFieldUtilities.TileSize,
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                CurrentPathSourceCount = _currentPathSourceCount,
                AgentCurrentPathIndicies = AgentCurPathIndicies,
                AgentDataArray = agentDataArray,
                AgentNewPathIndicies = AgentNewPathIndicies,
                AgentPathTasks = _agentPathTaskList.AsArray(),
                PathSectorStateTableArray = pathSectorStateTables,
                PathFlowDataArray = pathFlowDataArray,
                PathLocationDataArray = pathLocationDataArray,
                PathRoutineDataArray = pathRoutineDataArray,
            };
            JobHandle updateDeterminationHandle = updateDetermination.Schedule(reconstructionDeterminationHandle);
            if (FlowFieldUtilities.DebugMode) { updateDeterminationHandle.Complete(); }

            //Set path requested agents
            AgentsWithNewPathJob agentsToUnsubCurPathSubmission = new AgentsWithNewPathJob()
            {
                AgentNewPathIndicies = AgentNewPathIndicies,
                AgentsWithNewPath = _agentIndiciesToUnsubCurPath,
            };
            JobHandle agentsToUnsubCurPathSubmissionHandle = agentsToUnsubCurPathSubmission.Schedule(updateDeterminationHandle);
            if (FlowFieldUtilities.DebugMode) { agentsToUnsubCurPathSubmissionHandle.Complete(); }

            //Agent looking for path flag submission
            AgentLookingForPathSubmissionJob agentLookingForPathSubmission = new AgentLookingForPathSubmissionJob()
            {
                TileSize = FlowFieldUtilities.TileSize,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                AgentDataArray = agentDataArray,
                AgentNewPathIndicies = AgentNewPathIndicies,
                CostFields = costFieldCosts,
                AgentsLookingForPath = AgentsLookingForPath,
                AgentsLookingForPathRequestRecords = AgentsLookingForPathRecords,
                ReadyAgentsLookingForPathRequestRecords = _readyAgentsLookingForPathRecords,
                InitialPathRequests = requestedPaths,
                ReadyAgentsLookingForPath = _readyAgentsLookingForPath,
            };
            JobHandle agentLookingForPathSubmissionHandle = agentLookingForPathSubmission.Schedule(agentsToUnsubCurPathSubmissionHandle);
            if (FlowFieldUtilities.DebugMode) { agentLookingForPathSubmissionHandle.Complete(); }

            FlockToPathHashMapConstructionJob flockToPathConstruction = new FlockToPathHashMapConstructionJob()
            {
                PathStates = pathStateArray,
                AgentFlockIndicies = AgentFlockIndexArray,
                FlockListLength = _navigationManager.FlockDataContainer.FlockList.Length,
                ReadyAgentsLookingForPath = _readyAgentsLookingForPath,
                PathFlockIndicies = pathFlockIndexArray,
                FlockSlices = _hashMapFlockSlices,
                PathIndicies = _hashMapPathIndicies,
            };
            JobHandle flockToPathConstructionHandle = flockToPathConstruction.Schedule(agentLookingForPathSubmissionHandle);
            if (FlowFieldUtilities.DebugMode) { flockToPathConstructionHandle.Complete(); }

            //Agent looking for path check
            AgentLookingForPathCheckJob agentLookingForPathCheck = new AgentLookingForPathCheckJob()
            {
                TileSize = FlowFieldUtilities.TileSize,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                FlockToPathHashmap = new FlockToPathHashMap()
                {
                    FlockSlices = _hashMapFlockSlices,
                    PathIndicies = _hashMapPathIndicies,
                },
                AgentDataArray = agentDataArray,
                AgentFlockIndicies = AgentFlockIndexArray,
                ReadyAgentsLookingForPath = _readyAgentsLookingForPath,
                ReadyAgentsLookingForPathRequestRecords = _readyAgentsLookingForPathRecords,
                InitialPathRequests = requestedPaths,
                IslandFieldProcessors = islandFieldProcessors,
                PathDestinationDataArray = pathDestinationDataArray,
                PathRoutineDataArray = pathRoutineDataArray,
                PathSubscriberCounts = pathSubscriberCountArray,
                AgentNewPathIndicies = AgentNewPathIndicies,
                FlockIndexToPathRequestIndex = _flockIndexToPathRequestIndex,
                AgentIndiciesToSubExistingPath = _agentIndiciesToSubExistingPath,
            };
            JobHandle agentLookingForPathCheckHandle = agentLookingForPathCheck.Schedule(flockToPathConstructionHandle);
            if (FlowFieldUtilities.DebugMode) { agentLookingForPathCheckHandle.Complete(); }

            //Set path requested agents
            AgentsWithNewPathJob agentsToSubNewPathSubmission = new AgentsWithNewPathJob()
            {
                AgentNewPathIndicies = AgentNewPathIndicies,
                AgentsWithNewPath = _agentIndiciesToSubNewPath,
            };
            JobHandle agentsToSubNewPathSubmissionHandle = agentsToSubNewPathSubmission.Schedule(agentLookingForPathCheckHandle);
            if (FlowFieldUtilities.DebugMode) { agentsToSubNewPathSubmissionHandle.Complete(); }

            //Initial request to offset derived request
            PathRequestOffsetDerivationJob offsetDerivation = new PathRequestOffsetDerivationJob()
            {
                TileSize = FlowFieldUtilities.TileSize,
                AgentDataArray = agentDataArray,
                InitialPathRequests = requestedPaths,
                DerivedPathRequests = _offsetDerivedPathRequests,
                NewAgentPathIndicies = AgentNewPathIndicies,
            };
            JobHandle offsetDerivationHandle = offsetDerivation.Schedule(agentsToSubNewPathSubmissionHandle);
            if (FlowFieldUtilities.DebugMode) { offsetDerivationHandle.Complete(); }

            //Offset derived request to final request
            PathRequestIslandDerivationJob islandDerivation = new PathRequestIslandDerivationJob()
            {
                TileSize = FlowFieldUtilities.TileSize,
                AgentDataArray = agentDataArray,
                DerivedPathRequests = _offsetDerivedPathRequests,
                FinalPathRequests = _finalPathRequests,
                IslandFieldProcesorsPerOffset = _islandFieldProcessors,
                NewAgentPathIndicies = AgentNewPathIndicies,
                PathRequestSourceCount = _pathRequestSourceCount,
            };
            JobHandle islandDerivationHandle = islandDerivation.Schedule(offsetDerivationHandle);
            if (FlowFieldUtilities.DebugMode) { islandDerivationHandle.Complete(); }

            //Request destination expansions
            NativeArray<JobHandle> handles = new NativeArray<JobHandle>(_FinalPathRequestExpansionJobCount, Allocator.Temp);
            for (int i = 0; i < _FinalPathRequestExpansionJobCount; i++)
            {
                FinalPathRequestDestinationExpansionJob destinationExpansion = new FinalPathRequestDestinationExpansionJob()
                {
                    JobIndex = i,
                    TotalJobCount = _FinalPathRequestExpansionJobCount,
                    SectorColAmount = FlowFieldUtilities.SectorColAmount,
                    SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                    SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                    SectorRowAmount = FlowFieldUtilities.SectorRowAmount,
                    FieldColAmount = FlowFieldUtilities.FieldColAmount,
                    FieldRowAmount = FlowFieldUtilities.FieldRowAmount,
                    TileSize = FlowFieldUtilities.TileSize,
                    FieldMinXIncluding = FlowFieldUtilities.FieldMinXIncluding,
                    FieldMinYIncluding = FlowFieldUtilities.FieldMinYIncluding,
                    FieldMaxXExcluding = FlowFieldUtilities.FieldMaxXExcluding,
                    FieldMaxYExcluding = FlowFieldUtilities.FieldMaxYExcluding,
                    FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                    FinalPathRequests = _finalPathRequests,
                    IslandFieldProcessors = _islandFieldProcessors,
                    CostFields = costFieldCosts,
                };
                JobHandle destinationExpansionHandle = destinationExpansion.Schedule(islandDerivationHandle);
                handles[i] = destinationExpansionHandle;
                if (FlowFieldUtilities.DebugMode) { destinationExpansionHandle.Complete(); }
            }
            JobHandle combinedExpansionHanlde = JobHandle.CombineDependencies(handles);

            //Request source submissions
            FinalPathRequestSourceSubmitJob sourceSubmit = new FinalPathRequestSourceSubmitJob()
            {
                Sources = _sourcePositions,
                AgentNewPathIndicies = AgentNewPathIndicies,
                AgentCurPathIndicies = AgentCurPathIndicies,
                AgentDataArray = agentDataArray,
                FinalPathRequests = _finalPathRequests,
                PathRequestSourceCount = _pathRequestSourceCount,
                CurrentPathSourceCount = _currentPathSourceCount,
                AgentTasks = _agentPathTaskList.AsArray(),
                PathStateArray = pathStateArray,
                PathRoutineDataArray = pathRoutineDataArray,
            };
            JobHandle sourceSubmitHandle = sourceSubmit.Schedule(JobHandle.CombineDependencies(combinedExpansionHanlde, islandDerivationHandle));
            if (FlowFieldUtilities.DebugMode) { sourceSubmitHandle.Complete(); }

            _pathfindingTaskOrganizationHandle.Add(sourceSubmitHandle);
        }
        void CompletePathEvaluation()
        {
            _pathfindingTaskOrganizationHandle[0].Complete();
            _pathfindingTaskOrganizationHandle.Clear();

            if (_islandFieldProcessors.IsCreated)
            {
                _islandFieldProcessors.Dispose();
            }

            //SET PATH INDICIES OF REQUESTED PATHS
            for (int i = 0; i < _finalPathRequests.Length; i++)
            {
                FinalPathRequest currentpath = _finalPathRequests[i];
                if (!currentpath.IsValid()) { continue; }
                NativeSlice<float2> pathSources = new NativeSlice<float2>(_sourcePositions.AsArray(), currentpath.SourcePositionStartIndex, currentpath.SourceCount);
                int newPathIndex = _pathContainer.CreatePath(currentpath);
                RequestPipelineInfoWithHandle requestInfo = new RequestPipelineInfoWithHandle(new JobHandle(), newPathIndex, i);
                _portalTravesalScheduler.SchedulePortalTraversalFor(requestInfo, pathSources);
                _newPathIndicies.Add(newPathIndex);
                currentpath.PathIndex = newPathIndex;
                _finalPathRequests[i] = currentpath;
            }

            //SCHEDULE PATH ADDITIONS AND FLOW REQUESTS
            NativeArray<PathRoutineData> pathRoutineDataArray = _pathContainer.PathRoutineDataList.AsArray();
            for (int i = 0; i < pathRoutineDataArray.Length; i++)
            {
                PathRoutineData curPathRoutineData = pathRoutineDataArray[i];
                if (curPathRoutineData.Task == 0 && curPathRoutineData.DestinationState != DynamicDestinationState.Moved) { continue; }
                NativeSlice<float2> flowRequestSources = new NativeSlice<float2>(_sourcePositions.AsArray(), curPathRoutineData.FlowRequestSourceStart, curPathRoutineData.FlowRequestSourceCount);
                NativeSlice<float2> pathAdditionSources = new NativeSlice<float2>(_sourcePositions.AsArray(), curPathRoutineData.PathAdditionSourceStart, curPathRoutineData.PathAdditionSourceCount);
                PathPipelineInfoWithHandle pathInfo = new PathPipelineInfoWithHandle(new JobHandle(), i, curPathRoutineData.DestinationState);
                bool pathAdditionRequested = (curPathRoutineData.Task & PathTask.PathAdditionRequest) == PathTask.PathAdditionRequest;
                bool flowRequested = (curPathRoutineData.Task & PathTask.FlowRequest) == PathTask.FlowRequest;
                bool destinationMoved = curPathRoutineData.DestinationState == DynamicDestinationState.Moved;
                if (pathAdditionRequested)
                {
                    _additionPortalTraversalScheduler.SchedulePortalTraversalFor(pathInfo, pathAdditionSources);
                    _expandedPathIndicies.Add(pathInfo.PathIndex);
                }
                else if (flowRequested)
                {
                    _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(pathInfo, new JobHandle(), flowRequestSources);
                }
                else if (destinationMoved)
                {
                    _dynamicAreaScheduler.ScheduleDynamicArea(pathInfo);
                    _losIntegrationScheduler.ScheduleLOS(pathInfo);
                    _destinationUpdatedPathIndicies.Add(pathInfo.PathIndex);
                }
            }

            TryComplete();
        }

        internal void TryComplete()
        {
            if (_pathfindingTaskOrganizationHandle.Count != 0)
            {
                if (_pathfindingTaskOrganizationHandle[0].IsCompleted)
                {
                    CompletePathEvaluation();
                    _pathfindingTaskOrganizationHandle.Clear();
                }
            }
            if (_pathfindingTaskOrganizationHandle.Count == 0)
            {
                _portalTravesalScheduler.TryComplete(_finalPathRequests, _sourcePositions.AsArray());
                _additionPortalTraversalScheduler.TryComplete(_sourcePositions.AsArray());
                _requestedSectorCalculationScheduler.TryComplete();
            }
        }
        internal void ForceComplete()
        {
            if (_pathfindingTaskOrganizationHandle.Count != 0)
            {
                CompletePathEvaluation();
                _pathfindingTaskOrganizationHandle.Clear();
            }
            _dynamicAreaScheduler.ForceComplete();
            _portalTravesalScheduler.ForceComplete(_finalPathRequests, _sourcePositions.AsArray());
            _additionPortalTraversalScheduler.ForceComplete(_sourcePositions.AsArray());
            _requestedSectorCalculationScheduler.ForceComplete();
            _pathContainer.ExposeBuffers(_destinationUpdatedPathIndicies.AsArray(), _newPathIndicies.AsArray(), _expandedPathIndicies.AsArray());
        }
        internal void TransferNewPathsToCurPaths()
        {
            //TRANSFER NEW PATH INDICIES TO CUR PATH INDICIES
            NewPathToCurPathTransferJob newPathToCurPathTransferJob = new NewPathToCurPathTransferJob()
            {
                AgentCurPathIndicies = _navigationManager.AgentDataContainer.AgentCurPathIndicies.AsArray(),
                AgentNewPathIndicies = _navigationManager.AgentDataContainer.AgentNewPathIndicies.AsArray(),
                PathSubscribers = _navigationManager.PathDataContainer.PathSubscriberCounts.AsArray(),
                FinalPathRequests = _finalPathRequests.AsArray(),
                AgentsToTrySubNewPath = _agentIndiciesToSubNewPath.AsArray(),
                AgentsToTryUnsubCurPath = _agentIndiciesToUnsubCurPath.AsArray(),
                AgentIndiciesToSubExistingPath = _agentIndiciesToSubExistingPath.AsArray(),
            };

            AgentStartMovingJob agentStartMoving = new AgentStartMovingJob()
            {
                AgentDataArray = _navigationManager.AgentDataContainer.AgentDataList.AsArray(),
                AgentDestinationReachedArray = _navigationManager.AgentDataContainer.AgentDestinationReachedArray.AsArray(),
                AgentIndiciesToStartMoving = _agentIndiciesToStartMoving.AsArray(),
            };

            JobHandle newPathToCurPathTransferHandle = newPathToCurPathTransferJob.Schedule();
            JobHandle agentStartMovingHandle = agentStartMoving.Schedule();
            JobHandle.CompleteAll(ref newPathToCurPathTransferHandle, ref agentStartMovingHandle);
        }
    }


}