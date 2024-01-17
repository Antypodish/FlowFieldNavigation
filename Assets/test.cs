using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
public class test : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnDrawGizmos()
    {/*
        Vector3[] verts = gameObject.GetComponent<MeshFilter>().mesh.vertices;
        int[] trigs = gameObject.GetComponent<MeshFilter>().mesh.triangles;
        float3 pos = gameObject.transform.position;
        float3 scale = gameObject.transform.localScale;
        quaternion rotation = transform.rotation;

        Gizmos.color = Color.blue;
        for (int i = 0; i < trigs.Length; i += 3)
        {
            float3 v1 = verts[trigs[i]];
            float3 v2 = verts[trigs[i + 1]];
            float3 v3 = verts[trigs[i + 2]];
            v1 = pos + math.rotate(rotation, v1 * scale);
            v2 = pos + math.rotate(rotation, v2 * scale);
            v3 = pos + math.rotate(rotation, v3 * scale);
            float3 normal = math.cross(v3 - v2, v1 - v2);

            if(math.dot(normal, new float3(0, 1f, 0)) > 0)
            {
                Gizmos.DrawSphere(v1, 0.2f);
                Gizmos.DrawSphere(v2, 0.2f);
                Gizmos.DrawSphere(v3, 0.2f);
                Gizmos.color = Color.red;
                float3 normalStart = (v1 + v2 + v3) / 3;
                normal *= 0.2f;
                float3 normalEnd = normalStart + normal;
                Gizmos.DrawLine(normalStart, normalEnd);
            }
            
            Gizmos.color = Color.red;
            float3 normalStart = (v1 + v2 + v3) / 3;
            float3 normalEnd = normalStart + normal;
            Gizmos.DrawLine(normalStart, normalEnd);

            Gizmos.DrawSphere(normalEnd, 0.2f);
            UnityEngine.Debug.Log();
            
        }*/
    }
}
