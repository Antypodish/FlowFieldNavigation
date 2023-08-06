using Mono.Cecil;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEditor;
using UnityEngine;
using UnityEngine.Jobs;

internal class jobtest : MonoBehaviour
{
    public GameObject position;
    public GameObject desired;

    
    private void OnDrawGizmos()
    {/*
        float3 pos = position.transform.position;
        float3 des = desired.transform.position;

        float3 desiredDir = des - pos;
        desiredDir = math.normalize(desiredDir);

        float3 perp1 = new float3(1, desiredDir.y, (-desiredDir.x) / desiredDir.z);
        float3 perp2 = new float3(-1, desiredDir.y, desiredDir.x / desiredDir.z);
        perp1 += pos;
        perp2 += pos;
        desired.transform.position = desiredDir + pos;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(position.transform.position, desired.transform.position);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(position.transform.position, perp1);
        Gizmos.DrawLine(position.transform.position, perp2);*/
    }
}