public struct DirectionData
{
    public int N;
    public int E;
    public int S;
    public int W;
    public int NE;
    public int SE;
    public int SW;
    public int NW;

    public DirectionData(int index, int rowAmount, int colAmount)
    {
        Index2 form2D = new Index2(index / colAmount, index % colAmount);
        Index2 n = new Index2(form2D.R + 1, form2D.C);
        Index2 e = new Index2(form2D.R, form2D.C + 1);
        Index2 s = new Index2(form2D.R - 1, form2D.C);
        Index2 w = new Index2(form2D.R, form2D.C - 1);
        Index2 ne = new Index2(form2D.R + 1, form2D.C + 1);
        Index2 se = new Index2(form2D.R - 1, form2D.C + 1);
        Index2 sw = new Index2(form2D.R - 1, form2D.C - 1);
        Index2 nw = new Index2(form2D.R + 1, form2D.C - 1);

        N = OutOfBounds(n) ? index : (n.R * colAmount) + n.C;
        E = OutOfBounds(e) ? index : (e.R * colAmount) + e.C;
        S = OutOfBounds(s) ? index : (s.R * colAmount) + s.C;
        W = OutOfBounds(w) ? index : (w.R * colAmount) + w.C;
        NE = OutOfBounds(ne) ? index : (ne.R * colAmount) + ne.C;
        SE = OutOfBounds(se) ? index : (se.R * colAmount) + se.C;
        SW = OutOfBounds(sw) ? index : (sw.R * colAmount) + sw.C;
        NW = OutOfBounds(nw) ? index : (nw.R * colAmount) + nw.C;

        bool OutOfBounds(Index2 index)
        {
            if (index.R < 0)
            {
                return true;
            }
            if (index.C < 0)
            {
                return true;
            }
            if (index.R >= rowAmount)
            {
                return true;
            }
            if (index.C >= colAmount)
            {
                return true;
            }
            return false;
        }
    }
}
