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
    [HideInInspector] public int RowAmount;
    [HideInInspector] public int ColumnAmount;
    private void Start()
    {
        JobManager = new PathfindingJobManager();
        CostFieldProducer = new CostFieldProducer(_terrainGenerator.WalkabilityData);
        CostFieldProducer.StartCostFieldProduction(0, _maxCostfieldOffset, 10);

        TileSize = _terrainGenerator.TileSize;
        RowAmount = _terrainGenerator.RowAmount;
        ColumnAmount = _terrainGenerator.ColumnAmount;
        TilePositions = new NativeArray<Vector3>(RowAmount * ColumnAmount, Allocator.Persistent);
        CalculateTilePositions();
        CostFieldProducer.ForceCompleteCostFieldProduction();
    }
    private void Update()
    {
        JobManager.Update();
    }
    void CalculateTilePositions()
    {
        for (int r = 0; r < RowAmount; r++)
        {
            for (int c = 0; c < ColumnAmount; c++)
            {
                int index = r * ColumnAmount + c;
                TilePositions[index] = new Vector3(TileSize / 2 + c * TileSize, 0f, TileSize / 2 + r * TileSize);
            }
        }
    }

    public void SetUnwalkable(Index2 bound1, Index2 bound2, byte newCost)
    {
        int lowerRow = bound1.R < bound2.R ? bound1.R : bound2.R;
        int upperRow = bound1.R > bound2.R ? bound1.R : bound2.R;
        int leftmostCol = bound1.C < bound2.C ? bound1.C : bound2.C;
        int rightmostCol = bound1.C > bound2.C ? bound1.C : bound2.C;
        CostFieldEditJob[] editJobs = CostFieldProducer.GetEditJobs(new Index2(lowerRow, leftmostCol), new Index2(upperRow, rightmostCol), newCost);
        JobManager.AddCostEditJob(editJobs);
    }
}
