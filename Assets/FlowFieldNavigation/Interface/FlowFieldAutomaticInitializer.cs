using UnityEngine;

namespace FlowFieldNavigation
{
    public class FlowFieldAutomaticInitializer : MonoBehaviour
    {
        [SerializeField] FlowFieldNavigationManager _navigationManager;
        [SerializeField] float BaseAgentSpatialGridSize;
        [SerializeField] float BaseTriangleSpatialGridSize;
        [SerializeField] float MaxSurfaceHeightDifference;
        [SerializeField] float TileSize;
        [SerializeField] float MaxWalkableHeight;
        [SerializeField] float VerticalVoxelSize;
        [SerializeField] float MaxAgentRadius;
        [SerializeField] int LineOfSightRange;

        private void Start()
        {
            BaseAgentSpatialGridSize = Mathf.Max(BaseAgentSpatialGridSize, 3f);
            BaseTriangleSpatialGridSize = Mathf.Max(BaseTriangleSpatialGridSize, 0.1f);
            VerticalVoxelSize = Mathf.Max(VerticalVoxelSize, 0.01f);
            MaxSurfaceHeightDifference = Mathf.Max(MaxSurfaceHeightDifference, VerticalVoxelSize);
            TileSize = Mathf.Max(TileSize, 0.25f);
            MaxAgentRadius = Mathf.Max(MaxAgentRadius, 0.2f);
            LineOfSightRange = Mathf.Max(LineOfSightRange, 0);
            MaxWalkableHeight = Mathf.Min(MaxWalkableHeight, float.MaxValue);

            FlowFieldSurface[] surfaces = FindObjectsOfType<FlowFieldSurface>();
            FlowFieldStaticObstacle[] staticObstacles = FindObjectsOfType<FlowFieldStaticObstacle>();

            if (surfaces.Length == 0) { return; }
            SimulationStartParametersStandard startParam = new SimulationStartParametersStandard(surfaces,
                staticObstacles,
                BaseAgentSpatialGridSize,
                BaseTriangleSpatialGridSize,
                MaxAgentRadius,
                MaxSurfaceHeightDifference,
                TileSize,
                VerticalVoxelSize,
                LineOfSightRange,
                MaxWalkableHeight);
            _navigationManager.Interface.StartSimulation(startParam);
        }


    }

}
