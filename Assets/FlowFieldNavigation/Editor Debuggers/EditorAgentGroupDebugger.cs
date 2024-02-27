using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace FlowFieldNavigation
{
    internal class EditorAgentGroupDebugger
    {
        FlowFieldNavigationManager _navigationManager;
        Color[] _colors;
        internal EditorAgentGroupDebugger(FlowFieldNavigationManager navigationManager)
        {
            _navigationManager = navigationManager;

            _colors = new Color[]{
            new Color(0,0,0),
            new Color(1,0,0),
            new Color(0,1,0),
            new Color(1,1,0),
            new Color(0,0,1),
            new Color(1,0,1),
            new Color(0,1,1),
            new Color(1,1,1),
        };
        }
    }

}

