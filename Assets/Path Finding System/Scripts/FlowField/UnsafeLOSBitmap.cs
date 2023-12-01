using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
public struct UnsafeLOSBitmap
{
    UnsafeList<byte> _bytes;

    public int Length
    {
        get { return _bytes.Length; }
    }

    public UnsafeLOSBitmap(int size, Allocator allocator, NativeArrayOptions option = NativeArrayOptions.UninitializedMemory)
    {
        int byteCount = size / 8 + math.select(0, 1, size % 8 > 0);
        _bytes = new UnsafeList<byte>(byteCount, allocator, option);
        _bytes.Length = byteCount;
    }

    public int GetByteIndex(int bitIndex) => bitIndex / 8;
    public int GetBitRank(int bitIndex) => bitIndex % 8;
    public void Resize(int newLength, NativeArrayOptions option)
    {
        _bytes.Resize(newLength, option);
    }
    public void SetByte(int byteIndex, byte newBits)
    {
        _bytes[byteIndex] = newBits;
    }
    public void SetByteOfBit(int bitIndex, byte newBits)
    {
        int byteIndex = bitIndex / 8;
        _bytes[byteIndex] = newBits;
    }
    public void SetBitsOfByteUntil(int byteIndex, int untilBitRankIncluding, byte byteToSet)
    {
        int byteToSetShiftCount = 7 - untilBitRankIncluding;
        int curByteShiftCount = untilBitRankIncluding + 1;

        byte curByte = _bytes[byteIndex];
        curByte = (byte)(curByte >> curByteShiftCount);
        curByte = (byte)(curByte << curByteShiftCount);
        byteToSet = (byte)(byteToSet << byteToSetShiftCount);
        byteToSet = (byte)(byteToSet >> byteToSetShiftCount);
        _bytes[byteIndex] = (byte)(curByte | byteToSet);
    }
    public void SetBitsOfByteStartingFrom(int byteIndex, int startingFromBitRank, byte byteToSet)
    {
        int curByteShiftCount = 8 - startingFromBitRank;
        int byteToSetShiftCount = startingFromBitRank;
        byte curByte = _bytes[byteIndex];
        curByte = (byte)(curByte << curByteShiftCount);
        curByte = (byte)(curByte >> curByteShiftCount);
        byteToSet = (byte)(byteToSet >> byteToSetShiftCount);
        byteToSet = (byte)(byteToSet << byteToSetShiftCount);
        _bytes[byteIndex] = (byte)(curByte | byteToSet);
    }
    public bool IsLOS(int integrationIndex)
    {
        int byteIndex = integrationIndex / 8;
        int bitRank = integrationIndex % 8;
        int mask = 1 << bitRank;
        return (_bytes[byteIndex] & mask) == mask;
    }
    public void Dispose()
    {
        _bytes.Dispose();
    }
}
