using UnityEngine;
using Unity.Mathematics;
using UnityEngine.UIElements;

internal class FlowFieldStaticObstacle : MonoBehaviour
{
    public Vector3 Size;
    [HideInInspector] public bool CanBeDisposed { get; internal set; }
    
    internal void GetBoundaries(out float3x4 lowerFace, out float3x4 upperFace)
    {
        Vector3 position = transform.position;
        lowerFace.c0 = position - Size / 2;
        lowerFace.c1 = lowerFace.c0 + new float3(0, 0, Size.z);
        lowerFace.c2 = lowerFace.c0 + new float3(Size.x, 0, Size.z);
        lowerFace.c3 = lowerFace.c0 + new float3(Size.x, 0, 0);
        upperFace.c0 = lowerFace.c0 + new float3(0, Size.y, 0);
        upperFace.c1 = upperFace.c0 + new float3(0, 0, Size.z);
        upperFace.c2 = upperFace.c0 + new float3(Size.x, 0, Size.z);
        upperFace.c3 = upperFace.c0 + new float3(Size.x, 0, 0);
    }
    private void OnDrawGizmosSelected()
    {
        Vector3 position = transform.position;

        Vector3 lbl = position - Size / 2;
        Vector3 ltl = lbl + new Vector3(0, 0, Size.z);
        Vector3 ltr = lbl + new Vector3(Size.x, 0, Size.z);
        Vector3 lbr = lbl + new Vector3(Size.x, 0, 0);
        Vector3 ubl = lbl + new Vector3(0,Size.y, 0);
        Vector3 utl = ubl + new Vector3(0, 0, Size.z);
        Vector3 utr = ubl + new Vector3(Size.x, 0, Size.z);
        Vector3 ubr = ubl + new Vector3(Size.x, 0, 0);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(lbl, ltl);
        Gizmos.DrawLine(ltl, ltr);
        Gizmos.DrawLine(ltr, lbr);
        Gizmos.DrawLine(lbr, lbl);
        
        Gizmos.DrawLine(ubl, utl);
        Gizmos.DrawLine(utl, utr);
        Gizmos.DrawLine(utr, ubr);
        Gizmos.DrawLine(ubr, ubl);
        
        Gizmos.DrawLine(lbl, ubl);
        Gizmos.DrawLine(ltl, utl);
        Gizmos.DrawLine(ltr, utr);
        Gizmos.DrawLine(lbr, ubr);
    }
}