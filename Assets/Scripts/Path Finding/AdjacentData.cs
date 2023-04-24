public struct AdjacentData
{
    //if -1: invalid
    public int up;
    public int right;
    public int down;
    public int left;

    public AdjacentData(int index, int size)
    {
        Index2 form2D = new Index2(index / size, index % size);
        Index2 up = new Index2(form2D.R + 1, form2D.C);
        Index2 right = new Index2(form2D.R, form2D.C + 1);
        Index2 down = new Index2(form2D.R - 1, form2D.C);
        Index2 left = new Index2(form2D.R, form2D.C - 1);

        this.up = IsOutOfBounds(up, size) ? index : (up.R * size) + up.C;
        this.right = IsOutOfBounds(right, size) ? index : (right.R * size) + right.C;
        this.down = IsOutOfBounds(down, size) ? index : (down.R * size) + down.C;
        this.left = IsOutOfBounds(left, size) ? index : (left.R * size) + left.C;

        bool IsOutOfBounds(Index2 index, int size)
        {
            if (index.R < 0)
            {
                return true;
            }
            if (index.C < 0)
            {
                return true;
            }
            if (index.R >= size)
            {
                return true;
            }
            if (index.C >= size)
            {
                return true;
            }
            return false;
        }
    }
}
