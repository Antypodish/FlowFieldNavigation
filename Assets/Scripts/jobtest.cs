using Mono.Cecil;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEditor;
using UnityEngine;
using UnityEngine.Jobs;

internal class jobtest : MonoBehaviour
{
    private void Update()
    {
        GL.Begin(GL.LINES);
        GL.Color(Color.white);
        GL.Vertex3(0, 0, 0);
        GL.Vertex3(1, 0, 0);
        GL.End();
    }
}
