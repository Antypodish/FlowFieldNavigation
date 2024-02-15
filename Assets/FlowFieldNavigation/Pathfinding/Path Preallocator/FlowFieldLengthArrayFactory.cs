using System.Collections.Generic;
using System.Net.Http.Headers;
using Unity.Collections;

namespace FlowFieldNavigation
{

    internal class FlowFieldLengthArrayFactory
    {
        List<NativeArray<int>> _flowFieldLengths;

        internal FlowFieldLengthArrayFactory()
        {
            _flowFieldLengths = new List<NativeArray<int>>();
        }

        internal NativeArray<int> GetFlowFieldLengthArray()
        {
            if (_flowFieldLengths.Count == 0) { return new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); }
            int index = _flowFieldLengths.Count - 1;
            NativeArray<int> array = _flowFieldLengths[index];
            _flowFieldLengths.RemoveAtSwapBack(index);
            return array;
        }
        internal void SendFlowFieldLengthArray(NativeArray<int> flowFieldLengthArray)
        {
            flowFieldLengthArray[0] = 0;
            _flowFieldLengths.Add(flowFieldLengthArray);
        }
    }


}