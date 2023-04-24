public struct DiagonalData
{
    //if -1: invalid
    public int upright;
    public int downright;
    public int downleft;
    public int upleft;

    public DiagonalData(int index, int size)
    {
        Index2 form2D = new Index2(index / size, index % size);
        Index2 upright = new Index2(form2D.R + 1, form2D.C + 1);
        Index2 downright = new Index2(form2D.R - 1, form2D.C + 1);
        Index2 downleft = new Index2(form2D.R - 1, form2D.C - 1);
        Index2 upleft = new Index2(form2D.R + 1, form2D.C - 1);

        this.upright = IsOutOfBounds(upright, size) ? index : (upright.R * size) + upright.C;
        this.downright = IsOutOfBounds(downright, size) ? index : (downright.R * size) + downright.C;
        this.downleft = IsOutOfBounds(downleft, size) ? index : (downleft.R * size) + downleft.C;
        this.upleft = IsOutOfBounds(upleft, size) ? index : (upleft.R * size) + upleft.C;

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
