using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static PlasticGui.Configuration.OAuth.GetOauthProviders;

namespace FlowFieldNavigation
{
    internal class PathfindingPipeline
    {
        FlowFieldNavigationManager _navManager;

        PortalTraversalScheduler _portalTraversalScheduler;
        RequestedSectorCalculationScheduler _requestedSectorCalculationScheduler;
        LOSIntegrationScheduler _losIntegrationScheduler;
        DynamicAreaScheduler _dynamicAreaScheduler;
        FlowCalculationScheduler _flowCalculationScheduler;
        NativeArray<float2> _sources;
        NativeList<PortalTraversalRequest> PorTravRequestedPathList;
        NativeList<FlowRequest> FlowRequestedPathList;
        NativeList<LosRequest> LosRequestedPathList;
        NativeList<int> GoalUpdateRequestedPathList;
        PathfindingPipelineStateHandle _stateHandle;
        internal PathfindingPipeline(FlowFieldNavigationManager navManager)
        {
            _navManager = navManager;
            _losIntegrationScheduler = new LOSIntegrationScheduler(navManager);
            _requestedSectorCalculationScheduler = new RequestedSectorCalculationScheduler(navManager, _losIntegrationScheduler);
            _dynamicAreaScheduler = new DynamicAreaScheduler(navManager);
            _portalTraversalScheduler = new PortalTraversalScheduler(navManager, _requestedSectorCalculationScheduler);
            _flowCalculationScheduler = new FlowCalculationScheduler(navManager, _losIntegrationScheduler);
            PorTravRequestedPathList = new NativeList<PortalTraversalRequest>(Allocator.Persistent);
            FlowRequestedPathList = new NativeList<FlowRequest>(Allocator.Persistent);
            LosRequestedPathList = new NativeList<LosRequest>(Allocator.Persistent);
            GoalUpdateRequestedPathList = new NativeList<int>(Allocator.Persistent);
        }
        internal void Run(NativeArray<float2> sources, NativeList<int> expandedPathIndiciesOutput, NativeList<int> destinationUpdatedPathIndicies)
        {
            PorTravRequestedPathList.Clear();
            FlowRequestedPathList.Clear();
            LosRequestedPathList.Clear();
            GoalUpdateRequestedPathList.Clear();
            _sources = sources;

            //Get scheduling information
            NativeArray<PathRoutineData> pathRoutineDataArray = _navManager.PathDataContainer.PathRoutineDataList.AsArray();
            for (int i = 0; i < pathRoutineDataArray.Length; i++)
            {
                PathRoutineData curPathRoutineData = pathRoutineDataArray[i];
                if (curPathRoutineData.Task == 0 && curPathRoutineData.DestinationState != DynamicDestinationState.Moved) { continue; }

                int flowStart = math.select(curPathRoutineData.PathAdditionSourceStart, curPathRoutineData.FlowRequestSourceStart, curPathRoutineData.FlowRequestSourceCount != 0);
                int flowCount = curPathRoutineData.FlowRequestSourceCount + curPathRoutineData.PathAdditionSourceCount;
                Slice flowReqSlice = new Slice(flowStart, flowCount);
                Slice pathAdditionReqSlice = new Slice(curPathRoutineData.PathAdditionSourceStart, curPathRoutineData.PathAdditionSourceCount);
                bool pathAdditionRequested = (curPathRoutineData.Task & PathTask.PathAdditionRequest) == PathTask.PathAdditionRequest;
                bool flowRequested = (curPathRoutineData.Task & PathTask.FlowRequest) == PathTask.FlowRequest;
                bool destinationMoved = curPathRoutineData.DestinationState == DynamicDestinationState.Moved;

                if (pathAdditionRequested)
                {
                    PorTravRequestedPathList.Add(new PortalTraversalRequest(i, pathAdditionReqSlice));
                    expandedPathIndiciesOutput.Add(i);
                }
                if (flowRequested || pathAdditionRequested)
                {
                    FlowRequestedPathList.Add(new FlowRequest(i, flowReqSlice));
                }
                if(pathAdditionRequested || flowRequested || destinationMoved)
                {
                    LosRequestedPathList.Add(new LosRequest(i, curPathRoutineData.DestinationState));
                }
                if (destinationMoved)
                {
                    GoalUpdateRequestedPathList.Add(i);
                    destinationUpdatedPathIndicies.Add(i);
                }
            }
            NativeList<JobHandle> tempDependancyList = new NativeList<JobHandle>(Allocator.Temp);
            for(int i = 0; i < PorTravRequestedPathList.Length; i++)
            {
                PortalTraversalRequest req = PorTravRequestedPathList[i];
                JobHandle porTravHandle = _portalTraversalScheduler.SchedulePortalTraversalFor(req.PathIndex, req.SourceSlice);
                tempDependancyList.Add(porTravHandle);
            }
            JobHandle porTravCombinedHandle = JobHandle.CombineDependencies(tempDependancyList.AsArray());
            tempDependancyList.Clear();

            for(int i = 0; i < FlowRequestedPathList.Length; i++)
            {
                FlowRequest req = FlowRequestedPathList[i];
                NativeSlice<float2> flowSources = new NativeSlice<float2>(_sources, req.SourceSlice.Index, req.SourceSlice.Count);
                JobHandle flowHandle = _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(req.PathIndex, porTravCombinedHandle, flowSources);
                tempDependancyList.Add(flowHandle);
            }
            JobHandle flowCombinedHandle = JobHandle.CombineDependencies(tempDependancyList.AsArray());
            tempDependancyList.Clear();

            _stateHandle = new PathfindingPipelineStateHandle()
            {
                Handle = flowCombinedHandle,
                State = PathfindingPipelineState.Intermediate,
            };
        }
        internal void TryComplete()
        {
            Complete(false);
        }
        internal void ForceComplete()
        {
            Complete(true);
        }

        void Complete(bool forceComplete)
        {
            if (_stateHandle.State == PathfindingPipelineState.Intermediate && (_stateHandle.Handle.IsCompleted || forceComplete))
            {
                _stateHandle.Handle.Complete();

                NativeList<JobHandle> tempHandleList = new NativeList<JobHandle>(Allocator.Temp);
                //Schedule flow
                for (int i = 0; i < FlowRequestedPathList.Length; i++)
                {
                    FlowRequest req = FlowRequestedPathList[i];
                    JobHandle flowHandle = _flowCalculationScheduler.ScheduleFlow(req.PathIndex);
                    tempHandleList.Add(flowHandle);
                }
                JobHandle combinedFlowHandle = JobHandle.CombineDependencies(tempHandleList.AsArray());
                tempHandleList.Clear();

                //Schedule los
                for (int i = 0; i < LosRequestedPathList.Length; i++)
                {
                    LosRequest req = LosRequestedPathList[i];
                    JobHandle handle = _losIntegrationScheduler.ScheduleLOS(req.PathIndex, req.DestinationState, combinedFlowHandle);
                    tempHandleList.Add(handle);
                }

                //Schedule dynamic area
                for (int i = 0; i < GoalUpdateRequestedPathList.Length; i++)
                {
                    int pathIndex = GoalUpdateRequestedPathList[i];
                    JobHandle dynamicAreaHandle = _dynamicAreaScheduler.ScheduleDynamicArea(pathIndex);
                    tempHandleList.Add(dynamicAreaHandle);
                }
                JobHandle combinedFinalHandle = JobHandle.CombineDependencies(tempHandleList.AsArray());

                _stateHandle.State = PathfindingPipelineState.Final;
                _stateHandle.Handle = combinedFinalHandle;
            }
            if(_stateHandle.State == PathfindingPipelineState.Final && (_stateHandle.Handle.IsCompleted || forceComplete))
            {
                _stateHandle.Handle.Complete();
                _dynamicAreaScheduler.ForceComplete(GoalUpdateRequestedPathList);
                _flowCalculationScheduler.ForceComplete();
            }

        }
    }
}