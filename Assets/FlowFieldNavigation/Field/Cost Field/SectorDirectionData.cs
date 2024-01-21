using Unity.Mathematics;
internal struct SectorDirectionData
{
    internal byte N;
    internal byte E;
    internal byte S;
    internal byte W;
    internal byte NE;
    internal byte SE;
    internal byte SW;
    internal byte NW;

    internal SectorDirectionData(byte index, int sectorTileAmount)
    {
        Index2 form2D = new Index2(index / sectorTileAmount, index % sectorTileAmount);
        Index2 n = new Index2(form2D.R + 1, form2D.C);
        Index2 e = new Index2(form2D.R, form2D.C + 1);
        Index2 s = new Index2(form2D.R - 1, form2D.C);
        Index2 w = new Index2(form2D.R, form2D.C - 1);
        Index2 ne = new Index2(form2D.R + 1, form2D.C + 1);
        Index2 se = new Index2(form2D.R - 1, form2D.C + 1);
        Index2 sw = new Index2(form2D.R - 1, form2D.C - 1);
        Index2 nw = new Index2(form2D.R + 1, form2D.C - 1);

        N = (byte)(OutOfBounds(n) ? index : (n.R * sectorTileAmount) + n.C);
        E = (byte)(OutOfBounds(e) ? index : (e.R * sectorTileAmount) + e.C);
        S = (byte)(OutOfBounds(s) ? index : (s.R * sectorTileAmount) + s.C);
        W = (byte)(OutOfBounds(w) ? index : (w.R * sectorTileAmount) + w.C);
        NE = (byte)(OutOfBounds(ne) ? index : (ne.R * sectorTileAmount) + ne.C);
        SE = (byte)(OutOfBounds(se) ? index : (se.R * sectorTileAmount) + se.C);
        SW = (byte)(OutOfBounds(sw) ? index : (sw.R * sectorTileAmount) + sw.C);
        NW = (byte)(OutOfBounds(nw) ? index : (nw.R * sectorTileAmount) + nw.C);

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
            if (index.R >= sectorTileAmount)
            {
                return true;
            }
            if (index.C >= sectorTileAmount)
            {
                return true;
            }
            return false;
        }
    }
    internal string ToString()
    {
        return "{"+N+", " + NE + ", " + E + ", " + SE + ", " + S + ", " + SW + ", " + W + ", " + NW + "}";
    }
}