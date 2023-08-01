using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Path_Finding_System.Scripts.Debuggers.Editor_Debuggers
{
    internal class EditorAgentWaypointDebugger
    {
        PathfindingManager _pathfindingManager;
        public EditorAgentWaypointDebugger(PathfindingManager pathfindingManager)
        {
            _pathfindingManager = pathfindingManager;
        }

        public void Debug(FlowFieldAgent agent)
        {
            Waypoint wp = _pathfindingManager.AgentDataContainer.AgentDataList[agent.AgentDataIndex].waypoint;
            Vector3 agentPos = agent.transform.position;
            Vector3 wpPos = new Vector3(wp.position.x, agentPos.y, wp.position.y);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(wpPos, 0.3f);
            Gizmos.color = Color.black;
            Gizmos.DrawLine(wpPos, agentPos);
        }
    }
}
