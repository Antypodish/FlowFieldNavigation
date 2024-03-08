using Unity.Mathematics;

namespace FlowFieldNavigation
{

    internal struct Index2
    {
        internal int R;
        internal int C;

        internal Index2(int row, int column)
        {
            R = row;
            C = column;
        }
        public static bool operator ==(Index2 index1, Index2 index2)
        {
            return index1.R == index2.R && index1.C == index2.C;
        }
        public static bool operator !=(Index2 index1, Index2 index2)
        {
            return index1.R != index2.R || index1.C != index2.C;
        }
        public static implicit operator int2(Index2 index)
        {
            return new int2(index.C, index.R);
        }
        public override string ToString()
        {
            return "[" + R + ", " + C + "]";
        }
    }
}
