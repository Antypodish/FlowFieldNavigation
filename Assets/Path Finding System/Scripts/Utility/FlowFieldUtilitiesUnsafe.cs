using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

public static class FlowFieldUtilitiesUnsafe
{
    public static UnsafeListReadOnly<byte> ToUnsafeListRedonly(NativeArray<byte> array)
    {
        UnsafeListReadOnly<byte> list;
        unsafe
        {
            byte* arrayPtr = (byte*)array.GetUnsafePtr();
            int arrayLength = array.Length;
            list = new UnsafeListReadOnly<byte>(arrayPtr, arrayLength);
        }
        return list;
    }
}
