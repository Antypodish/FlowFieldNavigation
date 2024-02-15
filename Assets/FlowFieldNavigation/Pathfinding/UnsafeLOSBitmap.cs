using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using System.Drawing;

namespace FlowFieldNavigation
{

    internal struct UnsafeLOSBitmap
    {
        UnsafeList<byte> _bytes;
        internal int _size;
        internal int Length
        {
            get { return _bytes.Length; }
        }
        internal int BitLength { get { return _size; } }
        internal bool IsCreated { get { return _bytes.IsCreated; } }
        internal UnsafeLOSBitmap(int size, Allocator allocator, NativeArrayOptions option = NativeArrayOptions.UninitializedMemory)
        {
            int byteCount = size / 8 + math.select(0, 1, size % 8 > 0);
            _size = size;
            _bytes = new UnsafeList<byte>(byteCount, allocator, option);
            _bytes.Length = byteCount;
        }
        internal int GetByteIndex(int bitIndex) => bitIndex / 8;
        internal int GetBitRank(int bitIndex) => bitIndex % 8;
        internal void Resize(int newLength, NativeArrayOptions option)
        {
            int byteCount = newLength / 8 + math.select(0, 1, newLength % 8 > 0);
            _size = newLength;
            _bytes.Resize(byteCount, option);
        }
        internal void SetByte(int byteIndex, byte newBits)
        {
            _bytes[byteIndex] = newBits;
        }
        internal void SetByteOfBit(int bitIndex, byte newBits)
        {
            int byteIndex = bitIndex / 8;
            _bytes[byteIndex] = newBits;
        }
        internal void SetBitsOfByteUntil(int byteIndex, int untilBitRankIncluding, byte byteToSet)
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
        internal void SetBitsOfByteStartingFrom(int byteIndex, int startingFromBitRank, byte byteToSet)
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
        internal bool IsLOS(int integrationIndex)
        {
            int byteIndex = integrationIndex / 8;
            int bitRank = integrationIndex % 8;
            int mask = 1 << bitRank;
            return (_bytes[byteIndex] & mask) == mask;
        }
        internal void Dispose()
        {
            _bytes.Dispose();
        }
    }


}