using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;
using UnityEngine.Jobs;

namespace FlowFieldNavigation
{
    internal class EditorHoldGroundDebugger
    {
        FlowFieldNavigationManager _navigationManager;
        internal EditorHoldGroundDebugger(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }

        internal void Debug()
        {
            Gizmos.color = Color.yellow;

            NativeArray<AgentData> agents = _navigationManager.AgentDataContainer.AgentDataList.AsArray();
            TransformAccessArray transforms = _navigationManager.AgentDataContainer.AgentTransforms;

            for (int i = 0; i < agents.Length; i++)
            {
                if ((agents[i].Status & AgentStatus.HoldGround) != AgentStatus.HoldGround) { continue; }
                Gizmos.DrawCube(transforms[i].position, new Vector3(0.2f, 0.2f, 0.2f));
            }
        }
    }

}

