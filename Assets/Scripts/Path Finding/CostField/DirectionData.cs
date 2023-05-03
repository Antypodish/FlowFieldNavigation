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

    public DirectionData(int index, int size)
    {
        Index2 form2D = new Index2(index / size, index % size);
        Index2 n = new Index2(form2D.R + 1, form2D.C);
        Index2 e = new Index2(form2D.R, form2D.C + 1);
        Index2 s = new Index2(form2D.R - 1, form2D.C);
        Index2 w = new Index2(form2D.R, form2D.C - 1);
        Index2 ne = new Index2(form2D.R + 1, form2D.C + 1);
        Index2 se = new Index2(form2D.R - 1, form2D.C + 1);
        Index2 sw = new Index2(form2D.R - 1, form2D.C - 1);
        Index2 nw = new Index2(form2D.R + 1, form2D.C - 1);

        N = OutOfBounds(n, size) ? index : (n.R * size) + n.C;
        E = OutOfBounds(e, size) ? index : (e.R * size) + e.C;
        S = OutOfBounds(s, size) ? index : (s.R * size) + s.C;
        W = OutOfBounds(w, size) ? index : (w.R * size) + w.C;
        NE = OutOfBounds(ne, size) ? index : (ne.R * size) + ne.C;
        SE = OutOfBounds(se, size) ? index : (se.R * size) + se.C;
        SW = OutOfBounds(sw, size) ? index : (sw.R * size) + sw.C;
        NW = OutOfBounds(nw, size) ? index : (nw.R * size) + nw.C;

        bool OutOfBounds(Index2 index, int size)
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
