using Unity.Collections;
using Unity.Jobs;

namespace FlowFieldNavigation
{
    internal class PathConstructionTester
    {
        public void RoutineDataCalculationTest(PathRoutineDataCalculationJob routineDataCalculationJob)
        {
            PathRoutineDataCalculationTest routineDataTest = new PathRoutineDataCalculationTest()
            {
                FieldGridStartPos = FlowFieldUtilities.FieldGridStartPosition,
                SectorColAmount = FlowFieldUtilities.SectorColAmount,
                SectorMatrixColAmount = FlowFieldUtilities.SectorMatrixColAmount,
                SectorTileAmount = FlowFieldUtilities.SectorTileAmount,
                TileSize = FlowFieldUtilities.TileSize,
                FieldMaxXExcluding = FlowFieldUtilities.FieldMaxXExcluding,
                FieldMaxYExcluding = FlowFieldUtilities.FieldMaxYExcluding,
                FieldMinXIncluding = FlowFieldUtilities.FieldMinXIncluding,
                FieldMinYIncluding = FlowFieldUtilities.FieldMinYIncluding,
                PathStates = routineDataCalculationJob.PathStateArray,
                CostFields = routineDataCalculationJob.CostFields,
                IslandFieldProcessors = routineDataCalculationJob.IslandFieldProcessors,
                PathDestinationData = routineDataCalculationJob.PathDestinationDataArray,
                PathRoutineData = routineDataCalculationJob.PathOrganizationDataArray,
                DestinationOnInvalidIslandButNotReconstructed = new NativeReference<bool>(false, Allocator.TempJob),
                DestinationOnUnwalkableButNotReconstructed = new NativeReference<bool>(false, Allocator.TempJob),
                DestinationOutsideFieldBounds = new NativeReference<bool>(false, Allocator.TempJob),
                PathBotReconstructedAndUpdated = new NativeReference<bool>(false, Allocator.TempJob),
                RemovedPathUpdatedOrReconstructed = new NativeReference<bool>(false, Allocator.TempJob),
            };
            routineDataTest.Schedule().Complete();
            bool destinationOnInvalidIslandButNotReconstructed = routineDataTest.DestinationOnInvalidIslandButNotReconstructed.Value;
            bool destinationOnUnwalkableButNotReconstructed = routineDataTest.DestinationOnUnwalkableButNotReconstructed.Value;
            bool destinationOutsideFieldBounds = routineDataTest.DestinationOutsideFieldBounds.Value;
            bool pathBotReconstructedAndUpdated = routineDataTest.PathBotReconstructedAndUpdated.Value;
            bool removedPathUpdatedOrReconstructed = routineDataTest.RemovedPathUpdatedOrReconstructed.Value;
            string debug = "";
            if (destinationOnInvalidIslandButNotReconstructed) { debug += "destinationOnInvalidIslandButNotReconstructed\n"; }
            if (destinationOnUnwalkableButNotReconstructed) { debug += "destinationOnUnwalkableButNotReconstructed\n"; }
            if (destinationOutsideFieldBounds) { debug += "destinationOutsideFieldBounds\n"; }
            if (pathBotReconstructedAndUpdated) { debug += "pathBotReconstructedAndUpdated\n"; }
            if (removedPathUpdatedOrReconstructed) { debug += "removedPathUpdatedOrReconstructed\n"; }
            if (debug.Length > 0)
            {
                UnityEngine.Debug.Log(debug);
            }
            routineDataTest.DestinationOnInvalidIslandButNotReconstructed.Dispose();
            routineDataTest.DestinationOnUnwalkableButNotReconstructed.Dispose();
            routineDataTest.DestinationOutsideFieldBounds.Dispose();
            routineDataTest.PathBotReconstructedAndUpdated.Dispose();
            routineDataTest.RemovedPathUpdatedOrReconstructed.Dispose();
        }
    }


}
