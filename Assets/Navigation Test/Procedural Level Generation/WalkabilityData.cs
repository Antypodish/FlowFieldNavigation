using UnityEngine;
using FlowFieldNavigation;
public class WalkabilityData
{
    public float TileSize;
    public int RowAmount;
    public int ColAmount;
    public Walkability[][] WalkabilityMatrix;
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

        void InnitializeWalkabilityMatrix()
        {
            WalkabilityMatrix = new Walkability[RowAmount][];
            for (int i = 0; i < RowAmount; i++)
            {
                WalkabilityMatrix[i] = new Walkability[ColAmount];
            }
        }
    }
    void SimulateFullyWalkable()
    {
        for (int r = 0; r < WalkabilityMatrix.Length; r++)
        {
            for (int c = 0; c < WalkabilityMatrix[r].Length; c++)
            {
                WalkabilityMatrix[r][c] = Walkability.Walkable;
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
                noiseMap[r][c] = Mathf.PerlinNoise(r / _resolution, c / _resolution);
            }
        }

        //translate to walkability data
        for (int r = 0; r < WalkabilityMatrix.Length; r++)
        {
            for (int c = 0; c < WalkabilityMatrix[r].Length; c++)
            {
                Walkability walkability = noiseMap[r][c] < 0.3f ? Walkability.Unwalkable : Walkability.Walkable;
                WalkabilityMatrix[r][c] = walkability;
            }
        }
    }
}