using JetBrains.Annotations;
using System.Collections.Generic;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

internal class LOSTester : MonoBehaviour
{
    [SerializeField] TerrainGenerator _terrainGenerator;

    int[][] _fieldMatrix;
    float _tileSize;
    int _fieldRowAmount;
    int _fieldColAmount;

    Index2 _startIndex;
    Index2 _endIndex;
    private void Start()
    {
        _tileSize = _terrainGenerator.TileSize;
        _fieldColAmount = _terrainGenerator.ColumnAmount;
        _fieldRowAmount = _terrainGenerator.RowAmount;
        _fieldMatrix = new int[_terrainGenerator.RowAmount][];
        for(int i = 0; i < _fieldMatrix.Length; i++)
        {
            _fieldMatrix[i] = new int[_terrainGenerator.ColumnAmount];
            for(int j = 0; j < _fieldMatrix[i].Length; j++)
            {
                _fieldMatrix[i][j] = 0;
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
                _startIndex = new Index2(Mathf.FloorToInt(hit.point.z / _tileSize), Mathf.FloorToInt(hit.point.x / _tileSize));
            }
        }
        if (Input.GetMouseButton(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                _endIndex = new Index2(Mathf.FloorToInt(hit.point.z / _tileSize), Mathf.FloorToInt(hit.point.x / _tileSize));
            }
        }
    }
    private void OnDrawGizmos()
    {
        List<int> offsets = GetOffsets(_startIndex, _endIndex, _fieldColAmount);
        int startFlat = Index2.ToIndex(_startIndex, _fieldColAmount);
        int endFlat = Index2.ToIndex(_endIndex, _fieldColAmount);
        DrawLine();
        Gizmos.color = Color.white;
        for (int i = 0; i < offsets.Count; i++)
        {
            int curFlat = startFlat + offsets[i];
            Index2 curIndex = Index2.ToIndex2(curFlat, _fieldColAmount);
            Vector3 pos = new Vector3(curIndex.C * _tileSize + _tileSize / 2, 0.1f, curIndex.R * _tileSize + _tileSize / 2);
            Gizmos.DrawCube(pos, new Vector3(0.45f, 0.45f, 0.45f));
        }
        void DrawLine()
        {
            float startZ = 0;
            float startX = 0;
            float endZ = 0;
            float endX = 0;
            if (_startIndex.R < _endIndex.R)
            {
                startZ = _startIndex.R / _tileSize;
                endZ = _endIndex.R / _tileSize + _tileSize;
            }
            else
            {
                startZ = _startIndex.R / _tileSize + _tileSize;
                endZ = _endIndex.R / _tileSize;
            }
            if (_startIndex.C < _endIndex.C)
            {
                startX = _startIndex.C / _tileSize;
                endX = _endIndex.C / _tileSize + _tileSize;
            }
            else
            {
                startX = _startIndex.C / _tileSize + _tileSize;
                endX = _endIndex.C / _tileSize;
            }
            Vector3 startPos = new Vector3(startX, 0.1f, startZ);
            Vector3 endPos = new Vector3(endX, 0.1f, endZ);
            Gizmos.color = Color.black;
            Gizmos.DrawLine(startPos, endPos);
        }
    }
    Index2[] GetAdjustedIndicies()
    {
        float startR = 0;
        float startC = 0;
        float endR = 0;
        float endC = 0;

        Index2[] adjustedIndicies = new Index2[2];
        Index2 adjustedStart = _startIndex;
        Index2 adjustedEnd = _endIndex;
        if (_startIndex.R < _endIndex.R)
        {
            startR = _startIndex.R / _tileSize;
            endR = _endIndex.R / _tileSize + _tileSize;
        }
        else
        {
            startR = _startIndex.R / _tileSize + _tileSize;
            endR = _endIndex.R / _tileSize;
        }
        if (_startIndex.C < _endIndex.C)
        {
            startC = _startIndex.C / _tileSize;
            endC = _endIndex.C / _tileSize + _tileSize;
        }
        else
        {
            startC = _startIndex.C / _tileSize + _tileSize;
            endC = _endIndex.C / _tileSize;
        }
        return adjustedIndicies;
    }
    List<int> GetOffsets(Index2 start, Index2 end, int fieldColAmount)
    {
        List<int> offsets = new List<int>();
        List<Index2> indicies = new List<Index2>();
        List<Index2> newIndicies = new List<Index2>();

        int colAmount = Mathf.Abs(start.C - end.C) + 1;
        int rowAmount = Mathf.Abs(start.R - end.R) + 1;
        float m = (float) rowAmount / (float) colAmount;
        SetIndicies();
        SetOffsets();
        return offsets;

        void SetIndicies()
        {
            indicies.Add(new Index2(0, 0));
            for (int i = 0; i < colAmount - 1; i++)
            {
                float y = m * (i + 1);
                int rNew = Mathf.FloorToInt(y);
                indicies.Add(new Index2(rNew, i));
            }
            float yLast = m * colAmount;
            int rNewLast = Mathf.FloorToInt(yLast) - 1;
            indicies.Add(new Index2(rNewLast, colAmount - 1));

            for(int i = 1; i < indicies.Count; i++)
            {
                Index2 prev = indicies[i - 1];
                Index2 cur = indicies[i];
                for(int j = prev.R; j <= cur.R; j++)
                {
                    newIndicies.Add(new Index2(j, cur.C));
                }
            }
        }
        void SetOffsets()
        {
            int horizontalFactor = start.C < end.C ? 1 : -1;
            int verticalFactor = start.R < end.R ? 1 : -1;
            for(int i = 0; i < newIndicies.Count; i++)
            {
                Index2 index = newIndicies[i];
                int offset = index.R * verticalFactor * fieldColAmount + index.C * horizontalFactor;
                offsets.Add(offset);
            }
        }
    }
    
}