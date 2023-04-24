using UnityEngine;

internal class TileDebugger
{
    TerrainGenerator _terrainGenerator;

    internal TileDebugger(TerrainGenerator terrainGenerator)
    {
        _terrainGenerator = terrainGenerator;
    }

    internal void DebugTiles()
    {
        Gizmos.color = Color.black;
        float tileSize = _terrainGenerator.TileSize;
        int tileAmount = _terrainGenerator.TileAmount;
        float yOffset = .01f;

        for (int r = 0; r < tileAmount + 1; r++)
        {
            Vector3 start = new Vector3(0f, yOffset, r * tileSize);
            Vector3 end = new Vector3(tileSize * tileAmount, yOffset, r * tileSize);
            Gizmos.DrawLine(start, end);
        }
        for (int c = 0; c < tileAmount + 1; c++)
        {
            Vector3 start = new Vector3(c * tileSize, yOffset, 0f);
            Vector3 end = new Vector3(c * tileSize, yOffset, tileAmount * tileSize);
            Gizmos.DrawLine(start, end);
        }
    }
}