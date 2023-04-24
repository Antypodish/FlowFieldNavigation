using Unity.VisualScripting;
using UnityEngine;

public class WalkabilityData
{
    public float TileSize;
    public int TileAmount;
    public WalkabilityCell[][] WalkabilityMatrix;

    float _resolution;

    public WalkabilityData(float tileSize, int tileAmount, float resolution)
    {
        TileSize = tileSize;
        TileAmount = tileAmount;
        _resolution = resolution;

        InnitializeWalkabilityMatrix();
        SimulatePerlinNoise();

        void InnitializeWalkabilityMatrix()
        {
            WalkabilityMatrix = new WalkabilityCell[TileAmount][];
            for (int i = 0; i < TileAmount; i++)
            {
                WalkabilityMatrix[i] = new WalkabilityCell[TileAmount];
            }
        }
    }
    void SimulateFullyWalkable()
    {
        for (int r = 0; r < WalkabilityMatrix.Length; r++)
        {
            for (int c = 0; c < WalkabilityMatrix[r].Length; c++)
            {
                WalkabilityMatrix[r][c] = new WalkabilityCell
                {
                    Walkability = Walkability.Walkable,
                    CellPosition = new Vector3(c * TileSize, 0, r * TileSize)
                };
            }
        }
    }
    void SimulatePerlinNoise()
    {
        //innitialize noise map
        float[][] noiseMap = new float[TileAmount][];
        for(int r = 0; r < noiseMap.Length; r++)
        {
            noiseMap[r] = new float[TileAmount];
            for(int c = 0; c < noiseMap[r].Length; c++)
            {
                noiseMap[r][c] = Mathf.PerlinNoise((float)r / _resolution, (float)c / _resolution);
            }
        }

        //translate to walkability data
        for (int r = 0; r < WalkabilityMatrix.Length; r++)
        {
            for (int c = 0; c < WalkabilityMatrix[r].Length; c++)
            {
                Vector3 cellPosition = new Vector3(c * TileSize, 0, r * TileSize);
                Walkability walkability = noiseMap[r][c] < 0.3f ? Walkability.Unwalkable : Walkability.Walkable;
                WalkabilityMatrix[r][c] = new WalkabilityCell
                {
                    Walkability = walkability,
                    CellPosition = cellPosition
                };
            }
        }
    }
}
public struct WalkabilityCell
{
    public Vector3 CellPosition;
    public Walkability Walkability;
}
public enum Walkability : byte
{
    Unwalkable,
    Walkable
}