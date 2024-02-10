using UnityEngine;
using Unity.Mathematics;
using UnityEngine.UIElements;
using System;
using UnityEditor;
public class FlowFieldStaticObstacle : MonoBehaviour
{
    public Vector3 Size;
    [HideInInspector] public bool CanBeDisposed { get; internal set; }
    
    internal StaticObstacle GetBoundaries()
    {
        float3 lbl = - Size / 2;
        return new StaticObstacle()
        {
            LBL = lbl,
            LBR = lbl + new float3(Size.x, 0, 0),
            LTR = lbl + new float3(Size.x, 0, Size.z),
            LTL = lbl + new float3(0, 0, Size.z),
            UBL = lbl + new float3(0, Size.y, 0),
            UBR = lbl + new float3(Size.x, Size.y, 0),
            UTR = lbl + new float3(Size.x, Size.y, Size.z),
            UTL = lbl + new float3(0, Size.y, Size.z),
        };
    }
    private void OnDrawGizmosSelected()
    {
        Vector3 position = Vector3.zero;

        Vector3 lbl = position - Size / 2;
        Vector3 ltl = lbl + new Vector3(0, 0, Size.z);
        Vector3 ltr = lbl + new Vector3(Size.x, 0, Size.z);
        Vector3 lbr = lbl + new Vector3(Size.x, 0, 0);
        Vector3 ubl = lbl + new Vector3(0,Size.y, 0);
        Vector3 utl = ubl + new Vector3(0, 0, Size.z);
        Vector3 utr = ubl + new Vector3(Size.x, 0, Size.z);
        Vector3 ubr = ubl + new Vector3(Size.x, 0, 0);


        Vector3[] points = new Vector3[]
        {
            lbl, ltl, ltr, lbr, ubl, utl, utr, ubr
        };
        Vector3[] results = new Vector3[8];
        ReadOnlySpan<Vector3> span = new ReadOnlySpan<Vector3>(points, 0, points.Length);
        transform.TransformPoints(span, new Span<Vector3>(results, 0, results.Length));
        lbl = results[0];
        ltl = results[1];
        ltr = results[2];
        lbr = results[3];
        ubl = results[4];
        utl = results[5];
        utr = results[6];
        ubr = results[7];

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