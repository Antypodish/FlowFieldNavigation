using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
public class IntegrationFieldFactory
{
    List<NativeList<IntegrationTile>> _integrationFieldContainer;

    public IntegrationFieldFactory(int initialSize)
    {
        _integrationFieldContainer = new List<NativeList<IntegrationTile>>(initialSize);
        for (int i = 0; i < initialSize; i++)
        {
            _integrationFieldContainer.Add(new NativeList<IntegrationTile>(Allocator.Persistent));
        }
    }
    public NativeList<IntegrationTile> GetIntegrationField(int length)
    {
        if (_integrationFieldContainer.Count == 0)
        {
            NativeList<IntegrationTile> field = new NativeList<IntegrationTile>(length, Allocator.Persistent);
            field.Length = length;
            IntegrationFieldResetJob resetJob = new IntegrationFieldResetJob()
            {
                IntegrationField = field,
            };
            resetJob.Schedule().Complete();
            return field;
        }
        else
        {
            NativeList<IntegrationTile> field = _integrationFieldContainer[0];
            field.Length = length;
            IntegrationFieldResetJob resetJob = new IntegrationFieldResetJob()
            {
                IntegrationField = field,
            };
            resetJob.Schedule().Complete();
            _integrationFieldContainer.RemoveAtSwapBack(0);
            return field;
        }
    }
    public void SendIntegrationField(NativeList<IntegrationTile> integrationField)
    {
        _integrationFieldContainer.Add(integrationField);
    }
}
