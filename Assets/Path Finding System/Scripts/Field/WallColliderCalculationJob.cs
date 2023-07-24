using JetBrains.Annotations;
using System.Runtime.ConstrainedExecution;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
[BurstCompile]
public struct WallColliderCalculationJob : IJob
{
    public float TileSize;
    public int FieldColAmount;
    public int FieldRowAmount;

    public NativeArray<byte> Costs;
    public NativeArray<int> TileToWallObject;
    public NativeList<WallObject> WallObjectList;
    public NativeList<float2> VertexSequence;

    int _fieldTileAmount;
    public void Execute()
    {
        //VARIABLE CAPTURING
        _fieldTileAmount = FieldColAmount * FieldRowAmount;

        NativeArray<Edge> tileEdges = new NativeArray<Edge>(Costs.Length * 4, Allocator.Temp, NativeArrayOptions.ClearMemory);
        VertexSequence.Add(new float2(-1, -1));
        WallObjectList.Add(new WallObject());
        SetEdgesForEachTile(tileEdges);

        NativeList<Edge> edgeList = new NativeList<Edge>(Allocator.Temp);
        float2 startVertex;
        float2 lastVertex;
        for (int i = 0; i < Costs.Length; i++)
        {
            //GUARDS
            if (Costs[i] != byte.MaxValue) { continue; }
            if (TileToWallObject[i] != 0) { continue; }
            SetEdgeList(i);
            if(edgeList.IsEmpty) { continue; }

            //CREATE NEW WALL OBJECT
            WallObject newWallObject = new WallObject()
            {
                vertexStart = VertexSequence.Length,
                vertexLength = 0,
            };

            //SET INITIAL VERTICIES
            startVertex = edgeList[0].p1;
            lastVertex = edgeList[0].p2;
            VertexSequence.Add(startVertex);
            newWallObject.vertexLength++;
            VertexSequence.Add(lastVertex);
            newWallObject.vertexLength++;
            for (int j = 1; j < edgeList.Length; j++)
            {
                Edge newEdge = edgeList[j];
                if (Equals(newEdge.p1, lastVertex))
                {
                    lastVertex = newEdge.p2;
                    VertexSequence.Add(lastVertex);
                    newWallObject.vertexLength++;
                }
                else if (Equals(newEdge.p2, lastVertex))
                {
                    lastVertex = newEdge.p1;
                    VertexSequence.Add(lastVertex);
                    newWallObject.vertexLength++;
                }
            }

            //SET REMAINING VERTICIES
            int curIndex = i;
            while (!Equals(lastVertex, startVertex))
            {
                //GET NEIGHBOUR OF THE CURRENT TILE SUCH THAT NEW TILE HAS AN EDGE CONTAINING THE LAST VERTEX IN THE VERTEX SEQUENCE
                int commonVertexNeighbourIndex = GetNeighbourWithCommonVertex(curIndex, tileEdges, lastVertex);
                SetEdgeList(commonVertexNeighbourIndex);

                //TRY TO ADD ALL OF ITS EDGES
                int lastEdgeListLength = 0;
                while (!edgeList.IsEmpty && edgeList.Length != lastEdgeListLength)
                {
                    lastEdgeListLength = edgeList.Length;
                    for (int j = lastEdgeListLength - 1; j >= 0; j--)
                    {
                        Edge edge = edgeList[j];
                        if (Equals(edge.p1, lastVertex))
                        {
                            lastVertex = edge.p2;
                            VertexSequence.Add(lastVertex);
                            newWallObject.vertexLength++;
                            edgeList.RemoveAtSwapBack(j);
                            
                        }
                        else if (Equals(edge.p2, lastVertex))
                        {
                            lastVertex = edge.p1;
                            VertexSequence.Add(lastVertex);
                            newWallObject.vertexLength++;
                            edgeList.RemoveAtSwapBack(j);
                        }
                        if (Equals(lastVertex, startVertex)) { break; }
                    }
                    if (Equals(lastVertex, startVertex)) { break; }
                }
                //SET IT CURRENT TILE
                curIndex = commonVertexNeighbourIndex;
            }
            VertexSequence.RemoveAtSwapBack(VertexSequence.Length - 1);
            newWallObject.vertexLength--;

            MarkArea(i, WallObjectList.Length);
            WallObjectList.Add(newWallObject);
        }

        void SetEdgeList(int tileIndex)
        {
            edgeList.Clear();
            for (int i = tileIndex * 4; i < tileIndex * 4 + 4; i++)
            {
                if (tileEdges[i].dir != WallDirection.None) { edgeList.Add(tileEdges[i]); }
            }
        }
    }
    void MarkArea(int startIndex, int wallObjectIndex)
    {
        NativeQueue<int> markQueue = new NativeQueue<int>(Allocator.Temp);
        int fieldColAmount = FieldColAmount;
        int fieldTileAmount = FieldColAmount * FieldRowAmount;

         TileToWallObject[startIndex] = wallObjectIndex;

        markQueue.Enqueue(startIndex);
        while (!markQueue.IsEmpty())
        {
            int cur = markQueue.Dequeue();

            int n = cur + fieldColAmount;
            int e = cur + 1;
            int s = cur - fieldColAmount;
            int w = cur - 1;

            bool nOverflow = n >= fieldTileAmount;
            bool eOverflow = (e % fieldColAmount) == 0;
            bool sOverflow = s < 0;
            bool wOverflow = (cur % fieldColAmount) == 0;

            n = math.select(n, cur, nOverflow);
            e = math.select(e, cur, eOverflow);
            s = math.select(s, cur, sOverflow);
            w = math.select(w, cur, wOverflow);

            bool nAvailable = !nOverflow && Costs[n] == byte.MaxValue && TileToWallObject[n] != wallObjectIndex;
            bool eAvailable = !eOverflow && Costs[e] == byte.MaxValue && TileToWallObject[e] != wallObjectIndex;
            bool sAvailable = !sOverflow && Costs[s] == byte.MaxValue && TileToWallObject[s] != wallObjectIndex;
            bool wAvailable = !wOverflow && Costs[w] == byte.MaxValue && TileToWallObject[w] != wallObjectIndex;

            if (nAvailable)
            {
                TileToWallObject[n] = wallObjectIndex;
                markQueue.Enqueue(n);
            }
            if (eAvailable)
            {
                TileToWallObject[e] = wallObjectIndex;
                markQueue.Enqueue(e);
            }
            if (sAvailable)
            {
                TileToWallObject[s] = wallObjectIndex;
                markQueue.Enqueue(s);
            }
            if (wAvailable)
            {
                TileToWallObject[w] = wallObjectIndex;
                markQueue.Enqueue(w);
            }
        }
    }
    int GetNeighbourWithCommonVertex(int curIndex, NativeArray<Edge> tileEdges, float2 lastVertex)
    {
        int n = curIndex + FieldColAmount;
        int e = curIndex + 1;
        int s = curIndex - FieldColAmount;
        int w = curIndex - 1;
        int ne = n + 1;
        int se = s + 1;
        int sw = s - 1;
        int nw = n - 1;
        bool nOverflow = n >= _fieldTileAmount;
        bool eOverflow = (e % FieldColAmount) == 0;
        bool sOverflow = s < 0;
        bool wOverflow = (curIndex % FieldColAmount) == 0;
        bool nAvailable = false;
        bool eAvailable = false;
        bool sAvailable = false;
        bool wAvailable = false;
        bool neAvailable = false;
        bool seAvailable = false;
        bool swAvailable = false;
        bool nwAvailable = false;

        if (!nOverflow) { nAvailable = Costs[n] == byte.MaxValue; }
        if (!eOverflow) { eAvailable = Costs[e] == byte.MaxValue; }
        if (!sOverflow) { sAvailable = Costs[s] == byte.MaxValue; }
        if (!wOverflow) { wAvailable = Costs[w] == byte.MaxValue; }
        if (!(nOverflow || eOverflow)) { neAvailable = Costs[ne] == byte.MaxValue && (nAvailable || eAvailable); }
        if (!(sOverflow || eOverflow)) { seAvailable = Costs[se] == byte.MaxValue && (sAvailable || eAvailable); }
        if (!(sOverflow || wOverflow)) { swAvailable = Costs[sw] == byte.MaxValue && (sAvailable || wAvailable); }
        if (!(nOverflow || wOverflow)) { nwAvailable = Costs[nw] == byte.MaxValue && (nAvailable || wAvailable); }

        if (nAvailable)
        {
            for (int i = n * 4; i < n * 4 + 4; i++)
            {
                Edge edge = tileEdges[i];
                if (edge.dir == WallDirection.None) { break; }
                if (Equals(edge.p1, lastVertex) || Equals(edge.p2, lastVertex)) { return n; }
            }
        }
        if (eAvailable)
        {
            for (int i = e * 4; i < e * 4 + 4; i++)
            {
                Edge edge = tileEdges[i];
                if (edge.dir == WallDirection.None) { break; }
                if (Equals(edge.p1, lastVertex) || Equals(edge.p2, lastVertex)) { return e; }
            }
        }
        if (sAvailable)
        {
            for (int i = s * 4; i < s * 4 + 4; i++)
            {
                Edge edge = tileEdges[i];
                if (edge.dir == WallDirection.None) { break; }
                if (Equals(edge.p1, lastVertex) || Equals(edge.p2, lastVertex)) { return s; }
            }
        }
        if (wAvailable)
        {
            for (int i = w * 4; i < w * 4 + 4; i++)
            {
                Edge edge = tileEdges[i];
                if (edge.dir == WallDirection.None) { break; }
                if (Equals(edge.p1, lastVertex) || Equals(edge.p2, lastVertex)) { return w; }
            }
        }
        if (neAvailable)
        {
            for (int i = ne * 4; i < ne * 4 + 4; i++)
            {
                Edge edge = tileEdges[i];
                if (edge.dir == WallDirection.None)
                {
                    break;
                }
                if (Equals(edge.p1, lastVertex) || Equals(edge.p2, lastVertex)) { return ne; }
            }
        }
        if (seAvailable)
        {
            for (int i = se * 4; i < se * 4 + 4; i++)
            {
                Edge edge = tileEdges[i];
                if (edge.dir == WallDirection.None) { break; }
                if (Equals(edge.p1, lastVertex) || Equals(edge.p2, lastVertex)) { return se; }
            }
        }
        if (swAvailable)
        {
            for (int i = sw * 4; i < sw * 4 + 4; i++)
            {
                Edge edge = tileEdges[i];
                if (edge.dir == WallDirection.None) { break; }
                if (Equals(edge.p1, lastVertex) || Equals(edge.p2, lastVertex)) { return sw; }
            }
        }
        if (nwAvailable)
        {
            for (int i = nw * 4; i < nw * 4 + 4; i++)
            {
                Edge edge = tileEdges[i];
                if (edge.dir == WallDirection.None) { break; }
                if (Equals(edge.p1, lastVertex) || Equals(edge.p2, lastVertex)) { return nw; }
            }
        }
        return nw;
    }

    void SetEdgesForEachTile(NativeArray<Edge> tileEdges)
    {
        float halfTileSize = TileSize / 2;
        int fieldTileAmount = FieldColAmount * FieldRowAmount;
        for(int i = 0; i < Costs.Length; i++)
        {
            if (Costs[i]!= byte.MaxValue) { continue; }
            int2 i2d = new int2(i % FieldColAmount, i / FieldColAmount);
            float2 tilePos = new float2(TileSize / 2 + i2d.x * TileSize, TileSize / 2 + i2d.y * TileSize);

            int tileEdgeIndex = i * 4;

            int n1d = i + FieldColAmount;
            int e1d = i + 1;
            int s1d = i - FieldColAmount;
            int w1d = i - 1;

            bool nOverflow = n1d >= fieldTileAmount;
            bool eOverflow = (e1d % FieldColAmount) == 0;
            bool sOverflow = s1d < 0;
            bool wOverflow = (i % FieldColAmount) == 0;

            if (!nOverflow)
            {
                byte nCost = Costs[n1d];
                if(nCost == 1)
                {
                    float2 p1 = tilePos + new float2(-halfTileSize, halfTileSize);
                    float2 p2 = tilePos + new float2(halfTileSize, halfTileSize);
                    tileEdges[tileEdgeIndex] = new Edge()
                    {
                        p1 = p1,
                        p2 = p2,
                        dir = WallDirection.S
                    };
                    tileEdgeIndex++;
                }
            }
            if (!eOverflow)
            {
                byte eCost = Costs[e1d];
                if (eCost == 1)
                {
                    float2 p1 = tilePos + new float2(halfTileSize, halfTileSize);
                    float2 p2 = tilePos + new float2(halfTileSize, -halfTileSize);
                    tileEdges[tileEdgeIndex] = new Edge()
                    {
                        p1 = p1,
                        p2 = p2,
                        dir = WallDirection.W
                    };
                    tileEdgeIndex++;
                }
            }
            if (!sOverflow)
            {
                byte sCost = Costs[s1d];
                if (sCost == 1)
                {
                    float2 p1 = tilePos + new float2(halfTileSize, -halfTileSize);
                    float2 p2 = tilePos + new float2(-halfTileSize, -halfTileSize);
                    tileEdges[tileEdgeIndex] = new Edge()
                    {
                        p1 = p1,
                        p2 = p2,
                        dir = WallDirection.N
                    };
                    tileEdgeIndex++;
                }
            }
            if (!wOverflow)
            {
                byte wCost = Costs[w1d];
                if (wCost == 1)
                {
                    float2 p1 = tilePos + new float2(-halfTileSize, -halfTileSize);
                    float2 p2 = tilePos + new float2(-halfTileSize, halfTileSize);
                    tileEdges[tileEdgeIndex] = new Edge()
                    {
                        p1 = p1,
                        p2 = p2,
                        dir = WallDirection.E
                    };
                    tileEdgeIndex++;
                }
            }
        }
    }
    bool Equals(float2 p1, float2 p2)
    {
        return p1.x == p2.x && p1.y == p2.y;
    }
}
public struct Edge
{
    public float2 p1;
    public float2 p2;
    public WallDirection dir;
}
public struct WallObject
{
    public int vertexStart;
    public int vertexLength;
}
public struct SequenceIndexer
{
    public int start;
    public int length;
}
public enum WallDirection : byte
{
    None,
    N,
    E,
    S,
    W,
}