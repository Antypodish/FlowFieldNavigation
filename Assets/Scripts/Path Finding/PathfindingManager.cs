using System.Diagnostics;
using TMPro;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;
public class PathfindingManager : MonoBehaviour
{
    [SerializeField] TerrainGenerator _terrainGenerator;
    [SerializeField] int _maxCostfieldOffset;

    [HideInInspector] public static PathfindingJobScheduler JobScheduler;
    [HideInInspector] public CostFieldProducer CostFieldProducer;
    [HideInInspector] public PathProducer PathProducer;
    [HideInInspector] public NativeArray<Vector3> TilePositions;
    [HideInInspector] public float TileSize;
    [HideInInspector] public int RowAmount;
    [HideInInspector] public int ColumnAmount;
    [HideInInspector] public byte SectorTileAmount = 10;

    private void Start()
    {
        //!!!ORDER IS IMPORTANT!!!
        TileSize = _terrainGenerator.TileSize;
        RowAmount = _terrainGenerator.RowAmount;
        ColumnAmount = _terrainGenerator.ColumnAmount;
        JobScheduler = new PathfindingJobScheduler(this);
        CostFieldProducer = new CostFieldProducer(_terrainGenerator.WalkabilityData, SectorTileAmount);
        CostFieldProducer.StartCostFieldProduction(0, _maxCostfieldOffset, SectorTileAmount);
        PathProducer = new PathProducer(this);
        TilePositions = new NativeArray<Vector3>(RowAmount * ColumnAmount, Allocator.Persistent);
        CalculateTilePositions();
        CostFieldProducer.ForceCompleteCostFieldProduction();
    }
    private void Update()
    {
        JobScheduler.Update();
        PathProducer.Update();
    }
    private void LateUpdate()
    {

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
    public Path SetDestination(NativeArray<Vector3> sources, Vector3 target)
    {
        return PathProducer.ProducePath(sources, target, 0);
    }
    public void EditCost(Index2 bound1, Index2 bound2, byte newCost)
    {
        CostFieldEditJob[] editJobs = CostFieldProducer.GetEditJobs(new BoundaryData(bound1, bound2), newCost);
        JobScheduler.AddCostEditJob(editJobs);
    }
    public void SignalEditedSectors(NativeList<int> editedSectorIndicies)
    {
        PathProducer.MarkSectors(editedSectorIndicies);
    }
}
