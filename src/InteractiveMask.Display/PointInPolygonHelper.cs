using InteractiveMask.Detection;

namespace InteractiveMask.Display;

/// <summary>
/// Ray-casting point-in-polygon test shared by tile detection filtering and the
/// ROI editor. Linear in polygon size, allocation-free.
/// </summary>
public static class PointInPolygonHelper
{
    /// <summary>
    /// Returns true when <paramref name="x"/> / <paramref name="y"/> lie inside
    /// <paramref name="polygon"/> (edges inclusive via standard ray-cast parity).
    /// The polygon is implicitly closed.
    /// </summary>
    public static bool Contains(int x, int y, IReadOnlyList<PolygonPoint> polygon)
    {
        int n = polygon.Count;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            if ((pi.Y > y) != (pj.Y > y))
            {
                double xIntercept = pj.X + (double)(y - pj.Y) * (pi.X - pj.X) / (pi.Y - pj.Y);
                if (x < xIntercept) inside = !inside;
            }
        }
        return inside;
    }
}
