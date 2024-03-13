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
                DestinationOnInvalidIslandButNotReconstructedIndicies = new NativeList<int>(Allocator.TempJob),
                DestinationOnUnwalkableButNotReconstructedIndicies = new NativeList<int>(Allocator.TempJob),
                DestinationOutsideFieldBoundsIndicies = new NativeList<int>(Allocator.TempJob),
                PathBotReconstructedAndUpdatedIndicies = new NativeList<int>(Allocator.TempJob),
                RemovedPathUpdatedOrReconstructedIndicies = new NativeList<int>(Allocator.TempJob),
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
            if (destinationOnInvalidIslandButNotReconstructed)
            {
                debug += "destinationOnInvalidIslandButNotReconstructed\n";
                for(int i = 0; i < routineDataTest.DestinationOnInvalidIslandButNotReconstructedIndicies.Length; i++)
                {
                    debug += routineDataTest.DestinationOnInvalidIslandButNotReconstructedIndicies[i] + ", ";
                }
                debug += "\n";
            }
            if (destinationOnUnwalkableButNotReconstructed) 
            { 
                debug += "destinationOnUnwalkableButNotReconstructed\n";
                for (int i = 0; i < routineDataTest.DestinationOnUnwalkableButNotReconstructedIndicies.Length; i++)
                {
                    debug += routineDataTest.DestinationOnUnwalkableButNotReconstructedIndicies[i] + ", ";
                }
                debug += "\n";
            }
            if (destinationOutsideFieldBounds) 
            {
                debug += "destinationOutsideFieldBounds\n";
                for (int i = 0; i < routineDataTest.DestinationOutsideFieldBoundsIndicies.Length; i++)
                {
                    debug += routineDataTest.DestinationOutsideFieldBoundsIndicies[i] + ", ";
                }
                debug += "\n";
            }
            if (pathBotReconstructedAndUpdated) 
            {
                debug += "pathBotReconstructedAndUpdated\n";
                for (int i = 0; i < routineDataTest.PathBotReconstructedAndUpdatedIndicies.Length; i++)
                {
                    debug += routineDataTest.PathBotReconstructedAndUpdatedIndicies[i] + ", ";
                }
                debug += "\n";
            }
            if (removedPathUpdatedOrReconstructed) 
            {
                debug += "removedPathUpdatedOrReconstructed\n";
                for (int i = 0; i < routineDataTest.RemovedPathUpdatedOrReconstructedIndicies.Length; i++)
                {
                    debug += routineDataTest.RemovedPathUpdatedOrReconstructedIndicies[i] + ", ";
                }
                debug += "\n";
            }
            if (debug.Length > 0)
            {
                UnityEngine.Debug.Log(debug);
            }
            routineDataTest.DestinationOnInvalidIslandButNotReconstructed.Dispose();
            routineDataTest.DestinationOnUnwalkableButNotReconstructed.Dispose();
            routineDataTest.DestinationOutsideFieldBounds.Dispose();
            routineDataTest.PathBotReconstructedAndUpdated.Dispose();
            routineDataTest.RemovedPathUpdatedOrReconstructed.Dispose();
            routineDataTest.DestinationOnInvalidIslandButNotReconstructedIndicies.Dispose();
            routineDataTest.DestinationOnUnwalkableButNotReconstructedIndicies.Dispose();
            routineDataTest.DestinationOutsideFieldBoundsIndicies.Dispose();
            routineDataTest.PathBotReconstructedAndUpdatedIndicies.Dispose();
            routineDataTest.RemovedPathUpdatedOrReconstructedIndicies.Dispose();
        }
    }


}
