using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
public struct UnsafeLOSBitmap
{
    UnsafeList<byte> _bytes;
    int _bitCount;
    public int BitCount { get { return _bitCount; } }
    public int ByteCount { get { return _bytes.Length; } }
    public UnsafeLOSBitmap(int size, Allocator allocator, NativeArrayOptions option = NativeArrayOptions.UninitializedMemory)
    {
        int byteCount = size / 8 + math.select(0, 1, size % 8 > 0);
        _bytes = new UnsafeList<byte>(byteCount, allocator, option);
        _bytes.Length = byteCount;
        _bitCount = size;
    }

    public int GetByteIndex(int bitIndex) => bitIndex / 8;
    public int GetBitRank(int bitIndex) => bitIndex % 8;

    public void SetByte(int byteIndex, byte newBits)
    {
        _bytes[byteIndex] = newBits;
    }
    public void SetByteOfBit(int bitIndex, byte newBits)
    {
        int byteIndex = bitIndex / 8;
        _bytes[byteIndex] = newBits;
    }
    public void SetBitsOfByteUntil(int byteIndex, int untilBitRank, byte byteToSet)
    {
        byte curByte = _bytes[byteIndex];
        curByte = (byte)(curByte >> untilBitRank);
        curByte = (byte)(curByte << untilBitRank);
        byteToSet = (byte)(byteToSet << untilBitRank);
        byteToSet = (byte)(byteToSet >> untilBitRank);
        _bytes[byteIndex] = (byte)(curByte | byteToSet);
    }
    public void SetBitsOfByteAfter(int byteIndex, int afterBitRank, byte byteToSet)
    {
        byte curByte = _bytes[byteIndex];
        curByte = (byte)(curByte << afterBitRank);
        curByte = (byte)(curByte >> afterBitRank);
        byteToSet = (byte)(byteToSet >> afterBitRank);
        byteToSet = (byte)(byteToSet << afterBitRank);
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
