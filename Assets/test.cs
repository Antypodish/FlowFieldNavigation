using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEngine.UIElements;
using System;
public class testjob : MonoBehaviour
{
    private void Start()
    {

    }
    private void OnDrawGizmos()
    {/*
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] verts = mesh.vertices;
        int[] trigs = mesh.triangles;

        for(int i = 0; i < verts.Length; i++)
        {
            verts[i] = transform.TransformPoint(verts[i]);
        }
        Vector3 center = Vector3.zero;
        for(int i = 0; i < verts.Length; i++)
        {
            center += verts[i];
        }
        center = center / verts.Length;

        for(int i = 0; i < trigs.Length; i+=3)
        {
            int v1i = trigs[i];
            int v2i = trigs[i + 1];
            int v3i = trigs[i + 2];
            Vector3 v1 = verts[v1i];
            Vector3 v2 = verts[v3i];
            Vector3 v3 = verts[v2i];

            Vector3 trigCenter = (v1 + v2 + v3) / 3;
            Vector3 v1tov2 = v2-v1;
            Vector3 v2tov3 = v3-v1;
            Vector3 cross = Vector3.Cross(v1tov2, v2tov3);
            cross = cross.normalized * 0.2f;
            Vector3 normal = trigCenter + cross;
            Gizmos.DrawLine(trigCenter, normal);
        }*/
    }
    private void OnDrawGizmosSelected()
    {/*
        BoxCollider bc = GetComponent<BoxCollider>();
        Vector3 bcpos = bc.center;
        Vector3 bcSize = bc.size;

        Vector3 lbl = bcpos - bcSize / 2;
        Vector3 ltl = lbl + new Vector3(0, 0, bcSize.z);
        Vector3 ltr = lbl + new Vector3(bcSize.x, 0, bcSize.z);
        Vector3 lbr = lbl + new Vector3(bcSize.x, 0, 0);
        Vector3 ubl = lbl + new Vector3(0, bcSize.y, 0);
        Vector3 utl = ubl + new Vector3(0, 0, bcSize.z);
        Vector3 utr = ubl + new Vector3(bcSize.x, 0, bcSize.z);
        Vector3 ubr = ubl + new Vector3(bcSize.x, 0, 0);
        
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
        */
    }
}