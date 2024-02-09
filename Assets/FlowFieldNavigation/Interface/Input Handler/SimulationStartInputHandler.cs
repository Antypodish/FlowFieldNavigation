using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;

internal class SimulationStartInputHandler
{
    internal SimulationInputs HandleInput(SimulationStartParameters simStartParam, Allocator allocator)
    {
        GetNavgiationSurface(simStartParam.NavigationSurfaces, out NativeArray<float3> navSurfaceVerticies, out NativeArray<int> navSurfaceTriangles, allocator);
        
        NativeArray<StaticObstacle> staticObstacles = GetStaticObstacles(simStartParam.StaticObstacles, allocator);
        
        GetSurfaceMeshStartAndEndPositions(navSurfaceVerticies, out float2 surfaceStartPos, out float2 surfaceEndPos);
        float2 fieldStartPos = math.select(simStartParam.FieldStartPositionXZ, surfaceStartPos, simStartParam.FieldStartPositionXZ == SimulationStartParameters.InvalidFieldStartPos);
        float2 fieldEndPos = math.select(simStartParam.FieldEndPositionXZ, surfaceEndPos, simStartParam.FieldEndPositionXZ == SimulationStartParameters.InvalidFieldStartPos);
        
        int colAmount = simStartParam.ColumnCount;
        int rowAmount = simStartParam.RowCount;
        if(colAmount == 0 || rowAmount == 0)
        {
            GetRowAndColAmount(fieldStartPos, fieldEndPos, simStartParam.TileSize, out colAmount, out rowAmount);
        }

        NativeArray<byte> baseCostField = GetBaseCostField(simStartParam.WalkabilityData, colAmount, rowAmount, allocator);

        return new SimulationInputs()
        {
            BaseAgentSpatialGridSize = simStartParam.BaseAgentSpatialGridSize,
            MaxCostFieldOffset = simStartParam.MaxCostFieldOffset,
            MaxSurfaceHeightDifference = simStartParam.MaxSurfaceHeightDifference,
            TileSize = simStartParam.TileSize,
            NavigationSurfaceVerticies = navSurfaceVerticies,
            NavigationSurfaceTriangles = navSurfaceTriangles,
            StaticObstacles = staticObstacles,
            FieldStartPositionXZ = fieldStartPos,
            RowCount = rowAmount,
            ColumnCount = colAmount,
            MaxWalkableHeight = simStartParam.MaxWalkableHeight,
            VerticalVoxelSize = simStartParam.VerticalVoxelSize,
            BaseCostField = baseCostField,
        };
    }
    NativeArray<byte> GetBaseCostField(Walkability[][] walkabilityData, int colCount, int rowCount, Allocator allocator)
    {
        NativeArray<byte> costField = new NativeArray<byte>(colCount * rowCount, allocator);
        if(walkabilityData == null)
        {
            for(int i = 0; i < costField.Length; i++) { costField[i] = 1; }
            return costField;
        }
        for(int r = 0; r < walkabilityData.Length; r++)
        {
            for (int c = 0; c < walkabilityData[0].Length; c++)
            {
                int index1d = r * colCount + c;
                costField[index1d] = (byte)math.select(1, byte.MaxValue, walkabilityData[r][c] == Walkability.Unwalkable);
            }
        }
        return costField;
    }
    void GetRowAndColAmount(float2 fieldStartPos, float2 fieldEndPos, float tileSize, out int colAmount, out int rowAmount)
    {
        float2 delta = fieldEndPos - fieldStartPos;
        int2 colAndRowAmounts = (int2)math.ceil(delta / tileSize);
        colAmount = colAndRowAmounts.x;
        rowAmount = colAndRowAmounts.y;
    }
    void GetSurfaceMeshStartAndEndPositions(NativeArray<float3> meshVerticies, out float2 startPos, out float2 endPos)
    {
        MeshBoundsJob boundJob = new MeshBoundsJob()
        {
            Verticies = meshVerticies,
            MeshEndPos = new NativeReference<float2>(0, Allocator.TempJob),
            MeshStartPos = new NativeReference<float2>(0, Allocator.TempJob),
        };
        boundJob.Schedule().Complete();
        startPos = boundJob.MeshStartPos.Value;
        endPos = boundJob.MeshEndPos.Value;
        boundJob.MeshStartPos.Dispose();
        boundJob.MeshEndPos.Dispose();
    }
    NativeArray<StaticObstacle> GetStaticObstacles(FlowFieldStaticObstacle[] staticObstacles, Allocator allocator)
    {
        NativeArray<StaticObstacle> obstacleOut = new NativeArray<StaticObstacle>(staticObstacles.Length, allocator);
        for (int i = 0; i < staticObstacles.Length; i++)
        {
            FlowFieldStaticObstacle obstacle = staticObstacles[i];
            StaticObstacle inputBounds = obstacle.GetBoundaries();
            Transform inputTransform = obstacle.transform;
            Vector3 lbl = inputBounds.LBL;
            Vector3 ltl = inputBounds.LTL;
            Vector3 ltr = inputBounds.LTR;
            Vector3 lbr = inputBounds.LBR;
            Vector3 ubl = inputBounds.UBL;
            Vector3 utl = inputBounds.UTL;
            Vector3 utr = inputBounds.UTR;
            Vector3 ubr = inputBounds.UBR;

            obstacleOut[i] = new StaticObstacle()
            {
                LBL = inputTransform.TransformPoint(lbl),
                LTL = inputTransform.TransformPoint(ltl),
                LTR = inputTransform.TransformPoint(ltr),
                LBR = inputTransform.TransformPoint(lbr),
                UBL = inputTransform.TransformPoint(ubl),
                UTL = inputTransform.TransformPoint(utl),
                UTR = inputTransform.TransformPoint(utr),
                UBR = inputTransform.TransformPoint(ubr),
            };
            obstacle.CanBeDisposed = true;
        }
        return obstacleOut;
    }
    void GetNavgiationSurface(FlowFieldSurface[] surfaceBehaviours, out NativeArray<float3> verticies, out NativeArray<int> triangles, Allocator allocator)
    {
        NativeList<float3> vertexList = new NativeList<float3>(allocator);
        NativeList<int> triangleList = new NativeList<int>(allocator);
        int vertexStart = 0;
        for (int i = 0; i < surfaceBehaviours.Length; i++)
        {
            FlowFieldSurface surface = surfaceBehaviours[i];
            if (surface == null) { continue; }

            GameObject surfaceObject = surface.gameObject;
            Transform surfaceTransform = surfaceObject.transform;
            MeshFilter surfaceMeshFilter = surfaceObject.GetComponent<MeshFilter>();
            if (surfaceMeshFilter == null) { continue; }

            Mesh surfaceMesh = surfaceMeshFilter.mesh;
            if (surfaceMesh == null) { continue; }

            Vector3[] meshVerticies = surfaceMesh.vertices;
            int[] meshTriangles = surfaceMesh.triangles;

            float3 position = surfaceTransform.position;
            float3 scale = surfaceTransform.localScale;
            quaternion rotation = surfaceTransform.rotation;

            for (int j = 0; j < meshVerticies.Length; j++)
            {
                vertexList.Add(position + math.rotate(rotation, meshVerticies[j] * scale));
            }
            for (int j = 0; j < meshTriangles.Length; j++)
            {
                triangleList.Add(vertexStart + meshTriangles[j]);
            }
            vertexStart += meshVerticies.Length;
        }
        verticies = vertexList.AsArray();
        triangles = triangleList.AsArray();
    }
}
internal struct SimulationInputs
{
    internal float BaseAgentSpatialGridSize;
    internal int MaxCostFieldOffset;
    internal float MaxSurfaceHeightDifference;
    internal float TileSize;
    internal int RowCount;
    internal int ColumnCount;
    internal float MaxWalkableHeight;
    public float VerticalVoxelSize;
    internal Vector2 FieldStartPositionXZ;
    internal NativeArray<byte> BaseCostField;
    internal NativeArray<float3> NavigationSurfaceVerticies;
    internal NativeArray<int> NavigationSurfaceTriangles;
    internal NativeArray<StaticObstacle> StaticObstacles;

    internal void Dispose()
    {
        BaseCostField.Dispose();
        NavigationSurfaceTriangles.Dispose();
        NavigationSurfaceVerticies.Dispose();
        StaticObstacles.Dispose();
    }
}