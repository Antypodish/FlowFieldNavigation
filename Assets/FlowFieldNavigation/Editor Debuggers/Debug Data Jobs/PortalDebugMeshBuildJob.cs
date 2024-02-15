using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct PortalDebugMeshBuildJob : IJob
    {
        internal float TileSize;
        internal float2 FieldGridStartPos;
        internal float3 PortalDebugPrimitiveSize;
        [ReadOnly] internal TriangleSpatialHashGrid TriangleSpatialHashGrid;
        [ReadOnly] internal NativeArray<float3> HeightMeshVerts;
        [ReadOnly] internal NativeSlice<int> AlivePortalIndicies;
        [ReadOnly] internal NativeArray<PortalNode> PortalNodes;

        internal NativeList<Vector3> Verts;
        internal NativeList<int> Trigs;
        public void Execute()
        {
            for (int i = 0; i < AlivePortalIndicies.Length; i++)
            {
                int portalIndex = AlivePortalIndicies[i];
                PortalNode portalNode = PortalNodes[portalIndex];
                float2 portalPos2 = portalNode.GetPosition2(TileSize, FieldGridStartPos);
                float height = GetHeight(portalPos2) + PortalDebugPrimitiveSize.y;
                float3 portalPos3 = new float3(portalPos2.x, height, portalPos2.y);

                int ublIndex = Verts.Length;
                int ubrIndex = ublIndex + 1;
                int utlIndex = ublIndex + 2;
                int utrIndex = ublIndex + 3;
                int lblIndex = ublIndex + 4;
                int lbrIndex = ublIndex + 5;
                int ltlIndex = ublIndex + 6;
                int ltrIndex = ublIndex + 7;

                float3 lbl = portalPos3 - PortalDebugPrimitiveSize / 2;
                float3 lbr = lbl + new float3(PortalDebugPrimitiveSize.x, 0, 0);
                float3 ltl = lbl + new float3(0, 0, PortalDebugPrimitiveSize.z);
                float3 ltr = lbl + new float3(PortalDebugPrimitiveSize.x, 0, PortalDebugPrimitiveSize.z);
                float3 ubl = lbl + new float3(0, PortalDebugPrimitiveSize.y, 0);
                float3 ubr = lbl + new float3(PortalDebugPrimitiveSize.x, PortalDebugPrimitiveSize.y, 0);
                float3 utl = lbl + new float3(0, PortalDebugPrimitiveSize.y, PortalDebugPrimitiveSize.z);
                float3 utr = lbl + new float3(PortalDebugPrimitiveSize.x, PortalDebugPrimitiveSize.y, PortalDebugPrimitiveSize.z);

                Verts.Add(lbl);
                Verts.Add(lbr);
                Verts.Add(ltl);
                Verts.Add(ltr);
                Verts.Add(ubl);
                Verts.Add(ubr);
                Verts.Add(utl);
                Verts.Add(utr);

                Trigs.Add(ublIndex);
                Trigs.Add(utlIndex);
                Trigs.Add(ubrIndex);
                Trigs.Add(utlIndex);
                Trigs.Add(utrIndex);
                Trigs.Add(ubrIndex);

                Trigs.Add(lblIndex);
                Trigs.Add(ublIndex);
                Trigs.Add(lbrIndex);
                Trigs.Add(ublIndex);
                Trigs.Add(ubrIndex);
                Trigs.Add(lbrIndex);

                Trigs.Add(lbrIndex);
                Trigs.Add(ubrIndex);
                Trigs.Add(ltrIndex);
                Trigs.Add(ubrIndex);
                Trigs.Add(utrIndex);
                Trigs.Add(ltrIndex);

                Trigs.Add(ltlIndex);
                Trigs.Add(utlIndex);
                Trigs.Add(lblIndex);
                Trigs.Add(utlIndex);
                Trigs.Add(ublIndex);
                Trigs.Add(lblIndex);

                Trigs.Add(ltrIndex);
                Trigs.Add(utrIndex);
                Trigs.Add(ltlIndex);
                Trigs.Add(utrIndex);
                Trigs.Add(utlIndex);
                Trigs.Add(ltlIndex);
            }
        }
        float GetHeight(float2 pos)
        {
            float curHeight = float.MinValue;
            for (int i = 0; i < TriangleSpatialHashGrid.GetGridCount(); i++)
            {
                bool succesfull = TriangleSpatialHashGrid.TryGetIterator(pos, i, out TriangleSpatialHashGridIterator triangleGridIterator);
                if (!succesfull) { return 0; }
                while (triangleGridIterator.HasNext())
                {
                    NativeSlice<int> triangles = triangleGridIterator.GetNextRow();
                    for (int j = 0; j < triangles.Length; j += 3)
                    {
                        int v1Index = triangles[j];
                        int v2Index = triangles[j + 1];
                        int v3Index = triangles[j + 2];
                        float3 v13d = HeightMeshVerts[v1Index];
                        float3 v23d = HeightMeshVerts[v2Index];
                        float3 v33d = HeightMeshVerts[v3Index];
                        float2 v1 = new float2(v13d.x, v13d.z);
                        float2 v2 = new float2(v23d.x, v23d.z);
                        float2 v3 = new float2(v33d.x, v33d.z);

                        BarycentricCoordinates barCords = GetBarycentricCoordinatesForEachVectorInTheOrderUVW(v1, v2, v3, pos);
                        if (barCords.u < 0 || barCords.w < 0 || barCords.v < 0) { continue; }
                        float newHeight = v13d.y * barCords.u + v23d.y * barCords.v + v33d.y * barCords.w;
                        curHeight = math.select(curHeight, newHeight, newHeight > curHeight);
                    }
                }
            }
            return math.select(curHeight, 0, curHeight == float.MinValue);
        }
        BarycentricCoordinates GetBarycentricCoordinatesForEachVectorInTheOrderUVW(float2 a, float2 b, float2 c, float2 p)
        {
            float2 v0 = b - a, v1 = c - a, v2 = p - a;
            float d00 = math.dot(v0, v0);
            float d01 = math.dot(v0, v1);
            float d11 = math.dot(v1, v1);
            float d20 = math.dot(v2, v0);
            float d21 = math.dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            float u = 1.0f - v - w;
            return new BarycentricCoordinates()
            {
                v = v,
                u = u,
                w = w,
            };
        }
    }


}
