using Unity.Collections;
namespace FlowFieldNavigation
{
    internal class PathUpdateSeedContainer
    {
        internal NativeList<PathUpdateSeed> UpdateSeeds;

        internal PathUpdateSeedContainer()
        {
            UpdateSeeds = new NativeList<PathUpdateSeed>(Allocator.Persistent);
        }
    }
}