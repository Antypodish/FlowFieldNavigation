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
        NativeArray<float2> _sources;

        internal PathfindingPipeline(FlowFieldNavigationManager navManager)
        {
            _navManager = navManager;
            _losIntegrationScheduler = new LOSIntegrationScheduler(navManager);
            _requestedSectorCalculationScheduler = new RequestedSectorCalculationScheduler(navManager, _losIntegrationScheduler);
            _dynamicAreaScheduler = new DynamicAreaScheduler(navManager);
            _portalTraversalScheduler = new PortalTraversalScheduler(navManager, _requestedSectorCalculationScheduler);
        }

        internal void SetSources(NativeArray<float2> sources)
        {
            _sources = sources;
            _portalTraversalScheduler.SetSources(sources);
        }
        internal void SchedulePortalTraversalFor(int pathIndex, Slice pathfindingRequestSlice, Slice flowRequestSlice, DynamicDestinationState destinationState)
        {
            _portalTraversalScheduler.SchedulePortalTraversalFor(pathIndex, pathfindingRequestSlice, flowRequestSlice, destinationState);
        }
        internal void ScheduleRequestedSectorCalculation(int pathIndex, DynamicDestinationState destinationState, Slice flowRequesSlice)
        {
            NativeSlice<float2> flowRequestSources = new NativeSlice<float2>(_sources, flowRequesSlice.Index, flowRequesSlice.Count);
            _requestedSectorCalculationScheduler.ScheduleRequestedSectorCalculation(pathIndex, new JobHandle(), destinationState, flowRequestSources);
        }
        internal void UpdateDestination(PathPipelineInfoWithHandle pathInfo)
        {
            _dynamicAreaScheduler.ScheduleDynamicArea(pathInfo);
            _losIntegrationScheduler.ScheduleLOS(pathInfo);
        }

        internal void TryComplete()
        {
            _requestedSectorCalculationScheduler.TryComplete();
        }
        internal void ForceComplete()
        {
            _dynamicAreaScheduler.ForceComplete();
            _requestedSectorCalculationScheduler.ForceComplete();
        }
    }
}