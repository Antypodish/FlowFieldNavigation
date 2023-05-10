using UnityEngine;

public class WalkabilityData
{
    public float TileSize;
    public int RowAmount;
    public int ColAmount;
    public WalkabilityCell[][] WalkabilityMatrix;
    float _resolution;

    public WalkabilityData(float tileSize, int rowAmount, int colAmount, float resolution, SimulationState simulationState)
    {
        RowAmount = rowAmount;
        ColAmount = colAmount;
        TileSize = tileSize;
        _resolution = resolution;

        InnitializeWalkabilityMatrix();

        if(simulationState == SimulationState.FullWalkable) { SimulateFullyWalkable(); }
        if(simulationState == SimulationState.PerlinNoise) { SimulatePerlinNoise(); }
        SetEdgesUnwalkable();

        void InnitializeWalkabilityMatrix()
        {
            WalkabilityMatrix = new WalkabilityCell[RowAmount][];
            for (int i = 0; i < RowAmount; i++)
            {
                WalkabilityMatrix[i] = new WalkabilityCell[ColAmount];
            }
        }
        void SetEdgesUnwalkable()
        {
            for(int c = 0; c < colAmount; c++)
            {
                WalkabilityMatrix[0][c].Walkability = Walkability.Unwalkable;
                WalkabilityMatrix[rowAmount - 1][c].Walkability = Walkability.Unwalkable;
            }
            for (int r = 0; r < rowAmount; r++)
            {
                WalkabilityMatrix[r][0].Walkability = Walkability.Unwalkable;
                WalkabilityMatrix[r][colAmount - 1].Walkability = Walkability.Unwalkable;
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
        float[][] noiseMap = new float[RowAmount][];
        for(int r = 0; r < noiseMap.Length; r++)
        {
            noiseMap[r] = new float[ColAmount];
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