using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FlowFieldNavigation
{

    [BurstCompile]
    internal struct HeightDebugMeshBuildJob : IJob
    {
        internal int StartTrig;
        internal int TrigCount;
        [ReadOnly] internal NativeArray<float3> HeightMeshVerts;
        [ReadOnly] internal NativeArray<int> HeightMeshTrigs;
        internal NativeList<Vector3> Verts;
        internal NativeList<int> Trigs;
        public void Execute()
        {
            for (int i = StartTrig; i < StartTrig + TrigCount; i += 3)
            {
                int v1OldIndex = HeightMeshTrigs[i];
                int v2OldIndex = HeightMeshTrigs[i + 1];
                int v3OldIndex = HeightMeshTrigs[i + 2];

                float3 v1 = HeightMeshVerts[v1OldIndex];
                float3 v2 = HeightMeshVerts[v2OldIndex];
                float3 v3 = HeightMeshVerts[v3OldIndex];

                Trigs.Add(Verts.Length);
                Trigs.Add(Verts.Length + 1);
                Trigs.Add(Verts.Length + 2);
                Verts.Add(v1);
                Verts.Add(v2);
                Verts.Add(v3);
            }
        }
    }

}