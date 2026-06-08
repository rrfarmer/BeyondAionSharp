namespace Aion.GameServer.Model.Geometry;

/// <summary>
/// Java parity: model/geometry/Polygon2D — float-coordinate polygon (Apache Licensed).
/// Java implementation backed by java.awt.GeneralPath (WIND_EVEN_ODD); C# uses equivalent
/// ray-casting (even-odd) point-in-polygon algorithm and explicit edge-intersection for intersects.
/// Public fields xpoints/ypoints/npoints preserved for direct field access by PolyArea.
/// Methods not used in game logic (getPolyline2D, getPolygon, getPathIterator, getBounds) are
/// TODO-backlog; rendering/path-iteration paths have no callers in the server.
/// </summary>
public class Polygon2D : ICloneable
{
    /// <summary>Java parity: public int npoints.</summary>
    public int npoints;

    /// <summary>Java parity: public float[] xpoints.</summary>
    public float[] xpoints;

    /// <summary>Java parity: public float[] ypoints.</summary>
    public float[] ypoints;

    // Bounding box cache for fast reject
    private float _minX, _minY, _maxX, _maxY;
    private bool _boundsValid;

    /// <summary>Java parity: Polygon2D() — empty polygon with initial capacity 4.</summary>
    public Polygon2D()
    {
        xpoints = new float[4];
        ypoints = new float[4];
    }

    /// <summary>Java parity: Polygon2D(float[], float[], int).</summary>
    public Polygon2D(float[] xpoints, float[] ypoints, int npoints)
    {
        if (npoints > xpoints.Length || npoints > ypoints.Length)
            throw new IndexOutOfRangeException("npoints > xpoints.Length || npoints > ypoints.Length");
        this.npoints = npoints;
        this.xpoints = new float[npoints];
        this.ypoints = new float[npoints];
        Array.Copy(xpoints, this.xpoints, npoints);
        Array.Copy(ypoints, this.ypoints, npoints);
        RecalculateBounds();
    }

    /// <summary>Java parity: Polygon2D(int[], int[], int) — from integer arrays.</summary>
    public Polygon2D(int[] xpoints, int[] ypoints, int npoints)
    {
        if (npoints > xpoints.Length || npoints > ypoints.Length)
            throw new IndexOutOfRangeException("npoints > xpoints.Length || npoints > ypoints.Length");
        this.npoints = npoints;
        this.xpoints = new float[npoints];
        this.ypoints = new float[npoints];
        for (int i = 0; i < npoints; i++)
        {
            this.xpoints[i] = xpoints[i];
            this.ypoints[i] = ypoints[i];
        }
        RecalculateBounds();
    }

    /// <summary>Java parity: reset().</summary>
    public void Reset()
    {
        npoints = 0;
        _boundsValid = false;
    }

    /// <summary>Java parity: addPoint(float, float).</summary>
    public void AddPoint(float x, float y)
    {
        if (npoints == xpoints.Length)
        {
            Array.Resize(ref xpoints, npoints * 2);
            Array.Resize(ref ypoints, npoints * 2);
        }
        xpoints[npoints] = x;
        ypoints[npoints] = y;
        npoints++;
        _boundsValid = false;
    }

    /// <summary>
    /// Java parity: contains(double, double) — point-in-polygon using even-odd (ray-casting) rule,
    /// matching Java GeneralPath.WIND_EVEN_ODD behaviour.
    /// </summary>
    public bool Contains(double x, double y) => Contains((float)x, (float)y);

    /// <summary>Java parity: contains(float, float) — ray-casting even-odd algorithm.</summary>
    public bool Contains(float x, float y)
    {
        if (npoints <= 2) return false;
        EnsureBounds();
        if (x < _minX || x > _maxX || y < _minY || y > _maxY) return false;

        bool inside = false;
        for (int i = 0, j = npoints - 1; i < npoints; j = i++)
        {
            float xi = xpoints[i], yi = ypoints[i];
            float xj = xpoints[j], yj = ypoints[j];
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>
    /// Java parity: intersects(double, double, double, double) — rectangle-polygon intersection.
    /// Checks bounding box, then polygon vertices inside rect, rect vertices inside polygon,
    /// then edge-edge crossings.
    /// </summary>
    public bool Intersects(double x, double y, double w, double h)
    {
        if (npoints <= 0) return false;
        EnsureBounds();
        // Quick reject
        if (_maxX < x || _minX > x + w || _maxY < y || _minY > y + h) return false;

        // Any polygon vertex inside rectangle?
        for (int i = 0; i < npoints; i++)
        {
            if (xpoints[i] >= x && xpoints[i] <= x + w && ypoints[i] >= y && ypoints[i] <= y + h)
                return true;
        }
        // Any rectangle corner inside polygon?
        if (Contains((float)x, (float)y) ||
            Contains((float)(x + w), (float)y) ||
            Contains((float)x, (float)(y + h)) ||
            Contains((float)(x + w), (float)(y + h)))
            return true;

        // Edge-edge crossings
        float rx0 = (float)x,       ry0 = (float)y;
        float rx1 = (float)(x + w), ry1 = (float)y;
        float rx2 = (float)(x + w), ry2 = (float)(y + h);
        float rx3 = (float)x,       ry3 = (float)(y + h);
        float[] recX = { rx0, rx1, rx2, rx3 };
        float[] recY = { ry0, ry1, ry2, ry3 };

        for (int ri = 0, rj = 3; ri < 4; rj = ri++)
        {
            for (int pi = 0, pj = npoints - 1; pi < npoints; pj = pi++)
            {
                if (SegmentsIntersect(recX[ri], recY[ri], recX[rj], recY[rj],
                                      xpoints[pi], ypoints[pi], xpoints[pj], ypoints[pj]))
                    return true;
            }
        }
        return false;
    }

    private static bool SegmentsIntersect(float x1, float y1, float x2, float y2,
                                           float x3, float y3, float x4, float y4)
    {
        float d1x = x2 - x1, d1y = y2 - y1;
        float d2x = x4 - x3, d2y = y4 - y3;
        float cross = d1x * d2y - d1y * d2x;
        if (cross == 0) return false;
        float dx = x3 - x1, dy = y3 - y1;
        float t = (dx * d2y - dy * d2x) / cross;
        float u = (dx * d1y - dy * d1x) / cross;
        return t >= 0 && t <= 1 && u >= 0 && u <= 1;
    }

    private void EnsureBounds()
    {
        if (_boundsValid || npoints == 0) return;
        _minX = _maxX = xpoints[0];
        _minY = _maxY = ypoints[0];
        for (int i = 1; i < npoints; i++)
        {
            if (xpoints[i] < _minX) _minX = xpoints[i];
            else if (xpoints[i] > _maxX) _maxX = xpoints[i];
            if (ypoints[i] < _minY) _minY = ypoints[i];
            else if (ypoints[i] > _maxY) _maxY = ypoints[i];
        }
        _boundsValid = true;
    }

    private void RecalculateBounds() { _boundsValid = false; EnsureBounds(); }

    public object Clone()
    {
        var pol = new Polygon2D();
        for (int i = 0; i < npoints; i++) pol.AddPoint(xpoints[i], ypoints[i]);
        return pol;
    }

    // TODO-backlog: getPolyline2D(), getPolygon(), getBounds(), getBounds2D(), getPathIterator() — rendering paths, no server callers
}
