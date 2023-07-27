using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public class WallProducer
{
    public NativeArray<int> TileToWallObject;
    public NativeList<float2> VertexSequence;
    public NativeList<WallObject> WallObjectList;
    public NativeList<Direction> EdgeDirections;
    public WallProducer() { }
    public void Produce(CostField costfieldWithOffset0, float tileSize, int fieldColAmount, int fieldRowAmount)
    {
        TileToWallObject = new NativeArray<int>(costfieldWithOffset0.CostsG.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        VertexSequence = new NativeList<float2>(Allocator.Persistent);
        WallObjectList = new NativeList<WallObject>(Allocator.Persistent);
        EdgeDirections = new NativeList<Direction>(Allocator.Persistent);
        WallColliderCalculationJob walCalJob = new WallColliderCalculationJob()
        {
            EdgeDirections = EdgeDirections,
            WallObjectList = WallObjectList,
            TileWallObjects = TileToWallObject,
            VertexSequence = VertexSequence,
            TileSize = tileSize,
            Costs = costfieldWithOffset0.CostsG,
            FieldColAmount = fieldColAmount,
            FieldRowAmount = fieldRowAmount,
        };
        walCalJob.Schedule().Complete();
    }
}
