using Unity.Collections;
using UnityEngine;

public class PathfindingManager : MonoBehaviour
{
    [SerializeField] TerrainGenerator _terrainGenerator;
    [SerializeField] int _maxCostfieldOffset;

    [HideInInspector] public static PathfindingJobManager JobManager;
    [HideInInspector] public CostFieldProducer CostFieldProducer;
    [HideInInspector] public NativeArray<Vector3> TilePositions;
    [HideInInspector] public float TileSize;
    [HideInInspector] public int TileAmount;
    private void Start()
    {
        CostFieldProducer = new CostFieldProducer(_terrainGenerator.WalkabilityData);
        CostFieldProducer.StartCostFieldProduction(0, _maxCostfieldOffset, 10);

        TileSize = _terrainGenerator.TileSize;
        TileAmount = _terrainGenerator.TileAmount;
        TilePositions = new NativeArray<Vector3>(TileAmount * TileAmount, Allocator.Persistent);
        CalculateTilePositions();
        CostFieldProducer.ForceCompleteCostFieldProduction();
    }
    void CalculateTilePositions()
    {
        for (int r = 0; r < TileAmount; r++)
        {
            for (int c = 0; c < TileAmount; c++)
            {
                int index = r * TileAmount + c;
                TilePositions[index] = new Vector3(TileSize / 2 + c * TileSize, 0f, TileSize / 2 + r * TileSize);
            }
        }
    }
    public void SetUnwalkable(Index2 bound1, Index2 bound2)
    {
        CostFieldProducer.SetUnwalkable(bound1, bound2);
    }
}
