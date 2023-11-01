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

        }
    }
}
