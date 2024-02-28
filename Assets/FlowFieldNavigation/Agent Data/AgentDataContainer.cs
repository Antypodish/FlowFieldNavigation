using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;


namespace FlowFieldNavigation
{
    internal class AgentDataContainer
    {
        FlowFieldNavigationManager _navigationManager;

        internal TransformAccessArray AgentTransforms;
        internal NativeList<AgentData> AgentDataList;
        internal NativeList<int> AgentReferanceIndicies;
        internal NativeList<float> AgentRadii;
        internal NativeList<bool> AgentUseNavigationMovementFlags;
        internal NativeList<bool> AgentDestinationReachedArray;
        internal NativeList<int> AgentFlockIndicies;
        internal NativeList<int> AgentNewPathIndicies;
        internal NativeList<int> AgentCurPathIndicies;
        public AgentDataContainer(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
            AgentTransforms = new TransformAccessArray(0);
            AgentDataList = new NativeList<AgentData>(Allocator.Persistent);
            AgentNewPathIndicies = new NativeList<int>(0, Allocator.Persistent);
            AgentCurPathIndicies = new NativeList<int>(0, Allocator.Persistent);
            AgentFlockIndicies = new NativeList<int>(Allocator.Persistent);
            AgentDestinationReachedArray = new NativeList<bool>(Allocator.Persistent);
            AgentUseNavigationMovementFlags = new NativeList<bool>(Allocator.Persistent);
            AgentRadii = new NativeList<float>(Allocator.Persistent);
            AgentUseNavigationMovementFlags = new NativeList<bool>(Allocator.Persistent);
            AgentReferanceIndicies = new NativeList<int>(Allocator.Persistent);
        }
        public void DisposeAll()
        {
            AgentTransforms.Dispose();
            AgentDataList.Dispose();
            AgentDestinationReachedArray.Dispose();
            AgentFlockIndicies.Dispose();
            AgentNewPathIndicies.Dispose();
            AgentCurPathIndicies.Dispose();
            AgentRadii.Dispose();
            AgentUseNavigationMovementFlags.Dispose();
            AgentReferanceIndicies.Dispose();
        }
    }
}