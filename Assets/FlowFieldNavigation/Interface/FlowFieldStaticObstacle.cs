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
    private void OnDrawGizmos()
    {
        Vector3 position = Vector3.zero;

        Vector3 lbl = position - Size / 2;
        Vector3 ltl = lbl + new Vector3(0, 0, Size.z);
        Vector3 ltr = lbl + new Vector3(Size.x, 0, Size.z);
        Vector3 lbr = lbl + new Vector3(Size.x, 0, 0);
        Vector3 ubl = lbl + new Vector3(0, Size.y, 0);
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

        Vector3 leftFaceNormal = Vector3.Cross(ltl - lbl, utl - lbl).normalized;
        Vector3 rightFaceNormal = Vector3.Cross(lbr - ltr, ubr - ltr).normalized;
        Vector3 topFaceNormal = Vector3.Cross(ubl - ubr, utl - ubr).normalized;
        Vector3 botFaceNormal = Vector3.Cross(ltl - lbr, lbl - lbr).normalized;
        Vector3 frontFaceNormal = Vector3.Cross(lbl - lbr, ubl - lbr).normalized;
        Vector3 backFaceNormal = Vector3.Cross(utl - ltr, ltl - ltr).normalized;

        Vector3 leftFaceCenter = (ubl + lbl + utl + ltl) / 4;
        Vector3 rightFaceCenter = (ubr + lbr + utr + ltr) / 4;
        Vector3 topFaceCenter = (ubl + utl + utr + ubr) / 4;
        Vector3 botFaceCenter = (lbl + ltl + ltr + lbr) / 4;
        Vector3 frontFaceCenter = (ubl + lbl + ubr + lbr) / 4;
        Vector3 backFaceCenter = (utl + ltl + utr + ltr) / 4;
        Gizmos.DrawSphere(leftFaceCenter, 0.1f);
        Gizmos.DrawSphere(rightFaceCenter, 0.1f);
        Gizmos.DrawSphere(topFaceCenter, 0.1f);
        Gizmos.DrawSphere(botFaceCenter, 0.1f);
        Gizmos.DrawSphere(frontFaceCenter, 0.1f);
        Gizmos.DrawSphere(backFaceCenter, 0.1f);
        Gizmos.DrawLine(leftFaceCenter, leftFaceCenter + leftFaceNormal);
        Gizmos.DrawLine(rightFaceCenter, rightFaceCenter + rightFaceNormal);
        Gizmos.DrawLine(topFaceCenter, topFaceCenter + topFaceNormal);
        Gizmos.DrawLine(botFaceCenter, botFaceCenter + botFaceNormal);
        Gizmos.DrawLine(frontFaceCenter, frontFaceCenter + frontFaceNormal);
        Gizmos.DrawLine(backFaceCenter, backFaceCenter + backFaceNormal);

        Handles.Label(leftFaceCenter, (Mathf.Acos(Vector3.Dot(leftFaceNormal, Vector3.up)) * Mathf.Rad2Deg).ToString());
        Handles.Label(rightFaceCenter, (Mathf.Acos(Vector3.Dot(rightFaceNormal, Vector3.up)) * Mathf.Rad2Deg).ToString());
        Handles.Label(topFaceCenter, (Mathf.Acos(Vector3.Dot(topFaceNormal, Vector3.up)) * Mathf.Rad2Deg).ToString());
        Handles.Label(botFaceCenter, (Mathf.Acos(Vector3.Dot(botFaceNormal, Vector3.up)) * Mathf.Rad2Deg).ToString());
        Handles.Label(frontFaceCenter, (Mathf.Acos(Vector3.Dot(frontFaceNormal, Vector3.up)) * Mathf.Rad2Deg).ToString());
        Handles.Label(backFaceCenter, (Mathf.Acos(Vector3.Dot(backFaceNormal, Vector3.up)) * Mathf.Rad2Deg).ToString());
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