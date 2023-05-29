using JetBrains.Annotations;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

internal class LOSTester : MonoBehaviour
{
    [SerializeField] TerrainGenerator terrainGenerator;

    int[][] fieldMatrix;
    float tileSize;
    int fieldRowAmount;
    int fieldColAmount;

    float2 start;
    DirectionFlag startFlag = DirectionFlag.None;
    int2 startIndex;
    float2 end;
    DirectionFlag endFlag = DirectionFlag.None;
    int2 endIndex;
    private void Start()
    {
        tileSize = terrainGenerator.TileSize;
        fieldColAmount = terrainGenerator.ColumnAmount;
        fieldRowAmount = terrainGenerator.RowAmount;
        fieldMatrix = new int[terrainGenerator.RowAmount][];
        for(int i = 0; i < fieldMatrix.Length; i++)
        {
            fieldMatrix[i] = new int[terrainGenerator.ColumnAmount];
            for(int j = 0; j < fieldMatrix[i].Length; j++)
            {
                fieldMatrix[i][j] = 0;
            }
        }
    }
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                start = GetEndCorner(hit.point, out DirectionFlag flag);
                startFlag = flag;
                startIndex = new int2(Mathf.FloorToInt(hit.point.x / tileSize), Mathf.FloorToInt(hit.point.z / tileSize));
            }
        }
        if (Input.GetMouseButton(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                end = GetEndCorner(hit.point, out DirectionFlag flag);
                endFlag = flag;
                endIndex = new int2(Mathf.FloorToInt(hit.point.x / tileSize), Mathf.FloorToInt(hit.point.z / tileSize));
            }
        }
        //HELPERS
        float2 GetEndCorner(Vector3 pos, out DirectionFlag flag)
        {
            flag = DirectionFlag.None;
            int2 index = new int2(Mathf.FloorToInt(pos.x / tileSize), Mathf.FloorToInt(pos.z / tileSize));
            float2 posfloat2 = new float2(pos.x, pos.z);
            float2 indexPos = new float2(index.x * tileSize + tileSize / 2, index.y * tileSize + tileSize / 2);
            float2 upRight = indexPos + new float2(tileSize / 2, tileSize / 2);
            float2 downRight = indexPos + new float2(tileSize / 2, -tileSize / 2);
            float2 downLeft = indexPos + new float2(-tileSize / 2, -tileSize / 2);
            float2 upLeft = indexPos + new float2(-tileSize / 2, tileSize / 2);
            float upRightDistance = math.distance(posfloat2, upRight);
            float downRightDistance = math.distance(posfloat2, downRight);
            float downLeftDistance = math.distance(posfloat2, downLeft);
            float upLeftDistance = math.distance(posfloat2, upLeft);

            float2 minPos = float2.zero;
            float minDis = float.MaxValue;
            if(upRightDistance < minDis)
            {
                flag = DirectionFlag.NE;
                minDis = upRightDistance;
                minPos = upRight;
            }
            if (downRightDistance < minDis)
            {
                flag = DirectionFlag.SE;
                minDis = downRightDistance;
                minPos = downRight;
            }
            if (downLeftDistance < minDis)
            {
                flag = DirectionFlag.SW;
                minDis = downLeftDistance;
                minPos = downLeft;
            }
            if (upLeftDistance < minDis)
            {
                flag = DirectionFlag.NW;
                minPos = upLeft;
            }
            return minPos;

        }
    }
    List<int2> GetOffsets()
    {
        const int ts = 1;
        float2 p1 = start;
        float2 p2 = end;
        bool isYDecreasing = false;
        bool isXDecreasing = false;
        if(p2.x < p1.x)
        {
            isXDecreasing = true;
            float dif = p1.x - p2.x;
            p2.x = p2.x + dif * 2;
        }
        if (p2.y < p1.y)
        {
            isYDecreasing = true;
            float dif = p1.y - p2.y;
            p2.y = p2.y + dif * 2;
        }
        float2 p1Local = float2.zero;
        float2 p2Local = p2 - p1;
        float m = p2Local.y / p2Local.x;
        if(m == float.PositiveInfinity)
        {
            Debug.Log(m);
            List<int2> infinityIndex = new List<int2>();
            infinityIndex.Add(int2.zero);
            return infinityIndex;
        }
        List<float2> points = GetPoints();
        List<int2> indicies = GetIndicies();
        return indicies;

        //HELPERS
        List<float2> GetPoints()
        {
            List<float2> points = new List<float2>();
            for (int i = 0; i <= p2Local.x; i++)
            {
                float y = m * i;
                points.Add(new float2(i / ts, y / ts - 0.000001f));
            }
            return points;
        }
        List<int2> GetIndicies()
        {
            List<int2> indicies = new List<int2>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                float2 next = points[i + 1];
                float2 cur = points[i];
                int curx = (int)cur.x;
                int cury = (int)cur.y;
                int nexty = (int)next.y;
                for (int j = cury; j <= nexty; j++)
                {
                    int2 index = new int2(curx, j);
                    if (isYDecreasing)
                    {
                        index.y *= -1;
                    }
                    if (isXDecreasing)
                    {
                        index.x *= -1;
                    }
                    indicies.Add(index);
                }
            }
            return indicies;
        }
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        float3 start3 = new float3(start.x, 0f, start.y);
        float3 end3 = new float3(end.x, 0f, end.y);
        Gizmos.DrawSphere(start3, 0.3f);
        Gizmos.DrawSphere(end3, 0.3f);
        Gizmos.color = Color.black;
        Gizmos.DrawLine(start3, end3);
        Gizmos.color = Color.white;
        List<int2> offsets = GetOffsets();
        int2 startIndex = new int2(Mathf.FloorToInt(start.x / tileSize), Mathf.FloorToInt(start.y / tileSize));
        for(int i = 0; i < offsets.Count; i++)
        {
            int2 index = offsets[i] + startIndex;
            float3 pos = ToFloat3(index);
            Gizmos.DrawCube(pos, new Vector3(0.5f, 0.5f, 0.5f));
        }
    }

    float3 ToFloat3(int2 index)
    {
        return new float3(index.x * tileSize + tileSize / 2, 0f, index.y * tileSize + tileSize / 2);
    }
}
public enum DirectionFlag : byte
{
    None,
    NE,
    SE,
    SW,
    NW
}