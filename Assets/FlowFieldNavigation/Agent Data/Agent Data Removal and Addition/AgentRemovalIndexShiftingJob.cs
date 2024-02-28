using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEngine.Jobs;

namespace FlowFieldNavigation
{
    [BurstCompile]
    internal struct AgentRemovalIndexShiftingJob : IJob
    {
        [ReadOnly] internal NativeArray<int> AgentIndiciesToRemove;

        internal NativeList<AgentData> AgentDataList;
        internal NativeList<float> AgentRadii;
        internal NativeList<bool> AgentDestinationReachedArray;
        internal NativeList<bool> AgentUseNavigationMovementFlags;
        internal NativeList<int> AgentFlockIndicies;
        internal NativeList<int> AgentNewPathIndicies;
        internal NativeList<int> AgentCurPathIndicies;
        internal NativeList<int> AgentReferanceIndiciesPerAgent;
        internal NativeArray<AgentDataReferance> AgentReferances;
        internal NativeList<int> RemovedAgentMarks;
        internal NativeList<IndexShiftingPair> IndexShiftingPairs;
        [WriteOnly] internal NativeReference<int> LengthAfterRemoval;
        public void Execute()
        {
            int agentsLength = AgentDataList.Length;
            int lengthAfterRemoval = AgentDataList.Length - AgentIndiciesToRemove.Length;
            LengthAfterRemoval.Value = lengthAfterRemoval;
            int toRemoveAgentListPointer = 0;
            for (int agentListPointer = lengthAfterRemoval; agentListPointer < agentsLength; agentListPointer++)
            {
                int removedMark = RemovedAgentMarks[agentListPointer];
                if (removedMark == -2) { continue; }
                int indexToShiftFrom = agentListPointer;
                int indexToShiftTowards = agentListPointer;
                //Iterate throuh AgentIndiciesToRemove to find an index to shift towards
                for (int i = toRemoveAgentListPointer; i < AgentIndiciesToRemove.Length; i++)
                {
                    int agentIndex = AgentIndiciesToRemove[i];
                    if (agentIndex >= lengthAfterRemoval) { continue; }
                    indexToShiftTowards = agentIndex;
                    toRemoveAgentListPointer = i + 1;
                    break;
                }
                IndexShiftingPair shiftPair = new IndexShiftingPair()
                {
                    Source = indexToShiftFrom,
                    Destination = indexToShiftTowards,
                };
                IndexShiftingPairs.Add(shiftPair);
                RemovedAgentMarks[indexToShiftFrom] = indexToShiftTowards;
                int referanceIndexOfSavedAgent = AgentReferanceIndiciesPerAgent[indexToShiftFrom];
                AgentReferances[referanceIndexOfSavedAgent] = new AgentDataReferance(indexToShiftTowards);

                AgentDataList[indexToShiftTowards] = AgentDataList[indexToShiftFrom];
                AgentDestinationReachedArray[indexToShiftTowards] = AgentDestinationReachedArray[indexToShiftFrom];
                AgentFlockIndicies[indexToShiftTowards] = AgentFlockIndicies[indexToShiftFrom];
                AgentNewPathIndicies[indexToShiftTowards] = AgentNewPathIndicies[indexToShiftFrom];
                AgentCurPathIndicies[indexToShiftTowards] = AgentCurPathIndicies[indexToShiftFrom];
                AgentRadii[indexToShiftTowards] = AgentRadii[indexToShiftFrom];
                AgentUseNavigationMovementFlags[indexToShiftTowards] = AgentUseNavigationMovementFlags[indexToShiftFrom];
                AgentReferanceIndiciesPerAgent[indexToShiftTowards] = AgentReferanceIndiciesPerAgent[indexToShiftFrom];
            }
            AgentDataList.Length = lengthAfterRemoval;
            AgentDestinationReachedArray.Length = lengthAfterRemoval;
            AgentFlockIndicies.Length = lengthAfterRemoval;
            AgentNewPathIndicies.Length = lengthAfterRemoval;
            AgentCurPathIndicies.Length = lengthAfterRemoval;
            AgentRadii.Length = lengthAfterRemoval;
            AgentUseNavigationMovementFlags.Length = lengthAfterRemoval;
            AgentReferanceIndiciesPerAgent.Length = lengthAfterRemoval;
        }
    }
    internal struct IndexShiftingPair
    {
        internal int Source;
        internal int Destination;
    }

}
