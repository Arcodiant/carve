using static Carve.Core.PlaneGeometry;

namespace Carve.Core;

/// <summary>
/// A constrained Delaunay triangulation in the plane (T-237, for <see cref="Tessellation"/>): points inserted one by one
/// into a super triangle with Lawson flips, constraint segments recovered by flipping the edges that cross them (Sloan),
/// the triangles inside the constraint loops found by parity (inside a loop and in no hole), and those refined by
/// splitting every unconstrained edge longer than a limit at its midpoint until none is.
/// </summary>
/// <remarks>
/// Triangles are kept counter-clockwise in a flat index list: triangle <c>t</c>'s corners are <c>_v[3t..3t+2]</c>, its edge
/// <c>k</c> runs from corner <c>k</c> to corner <c>k+1</c>, <c>_n</c> holds the triangle across it (-1 none) and <c>_c</c>
/// whether it is a constraint. Points 0..2 are the super triangle's. No triangle is ever removed, so ids hold.
/// </remarks>
internal sealed class ConstrainedDelaunay
{
    private readonly List<Vector2D> _p = [];
    private readonly List<int> _v = [];
    private readonly List<int> _n = [];
    private readonly List<bool> _c = [];
    private readonly List<bool> _inside = [];
    private readonly List<int> _vt = [];
    private readonly double _eps;
    private int _last;

    /// <summary>A triangulation ready for points within the box from (<paramref name="minX"/>, <paramref name="minY"/>) to (<paramref name="maxX"/>, <paramref name="maxY"/>).</summary>
    public ConstrainedDelaunay(double minX, double minY, double maxX, double maxY)
    {
        var size = Math.Max(Math.Max(maxX - minX, maxY - minY), 1e-30);
        // An orientation within this of zero is on the line: rounding noise at the box's scale.
        _eps = 1e-12 * size * size;
        var (cx, cy, r) = ((minX + maxX) / 2, (minY + maxY) / 2, size * 20);
        _p.AddRange([new(cx - r, cy - r), new(cx + r, cy - r), new(cx, cy + r)]);
        _vt.AddRange([0, 0, 0]);
        _v.AddRange([0, 1, 2]);
        _n.AddRange([-1, -1, -1]);
        _c.AddRange([false, false, false]);
        _inside.Add(false);
    }

    public int PointCount => _p.Count;

    public Vector2D Point(int i) => _p[i];

    private int TriangleCount => _v.Count / 3;

    private int V(int t, int k) => _v[3 * t + k % 3];

    private int N(int t, int k) => _n[3 * t + k % 3];

    /// <summary>Inserts <paramref name="p"/> and returns its index. A point on an existing point is refused.</summary>
    public int Add(Vector2D p)
    {
        var (t, edge) = Locate(p);
        var i = _p.Count;
        _p.Add(p);
        _vt.Add(t);
        if (edge >= 0) SplitEdge(t, edge, i);
        else SplitTriangle(t, i);
        return i;
    }

    /// <summary>Makes segment <paramref name="a"/>-<paramref name="b"/> an edge that no flip crosses.</summary>
    public void Constrain(int a, int b)
    {
        if (FindEdge(a, b) is not { } found)
        {
            var crossing = new Queue<(int, int)>();
            for (var t = 0; t < TriangleCount; t++)
                for (var k = 0; k < 3; k++)
                    if (V(t, k) < V(t, k + 1) && Crosses(V(t, k), V(t, k + 1), a, b)) crossing.Enqueue((V(t, k), V(t, k + 1)));
            var guard = 0;
            while (crossing.Count > 0)
            {
                if (++guard > 100_000) throw new InvalidOperationException("a constraint could not be recovered");
                var (u, w) = crossing.Dequeue();
                var (t, k) = FindEdge(u, w) ?? throw new InvalidOperationException("a crossing edge was lost");
                var (p, q) = (V(t, k + 2), Opposite(t, k));
                if (Orient(_p[p], _p[q], _p[u]) * Orient(_p[p], _p[q], _p[w]) >= 0)
                {
                    crossing.Enqueue((u, w)); // not convex yet: another flip will make it so
                    continue;
                }
                Flip(t, k, null);
                if (Crosses(p, q, a, b)) crossing.Enqueue((p, q));
            }
            found = FindEdge(a, b) ?? throw new InvalidOperationException("a constraint could not be recovered");
        }
        var (s, j) = found;
        _c[3 * s + j] = true;
        var o = N(s, j);
        if (o >= 0) _c[3 * o + EdgeIndex(o, V(s, j + 1), V(s, j))] = true;
    }

    /// <summary>Flips every edge that is not Delaunay and not a constraint, until none is.</summary>
    public void Legalize()
    {
        var stack = new Stack<(int, int)>();
        for (var t = 0; t < TriangleCount; t++)
            for (var k = 0; k < 3; k++) stack.Push((t, k));
        Legalize(stack);
    }

    /// <summary>Marks the triangles inside the constraint loops: those an odd number of constraints from the super triangle.</summary>
    public void Classify()
    {
        var depth = Enumerable.Repeat(-1, TriangleCount).ToArray();
        var queue = new LinkedList<int>();
        depth[_vt[0]] = 0;
        queue.AddFirst(_vt[0]);
        while (queue.Count > 0)
        {
            var t = queue.First!.Value;
            queue.RemoveFirst();
            for (var k = 0; k < 3; k++)
            {
                var u = N(t, k);
                if (u < 0) continue;
                var step = _c[3 * t + k] ? 1 : 0;
                if (depth[u] >= 0 && depth[u] <= depth[t] + step) continue;
                depth[u] = depth[t] + step;
                if (step == 0) queue.AddFirst(u); else queue.AddLast(u);
            }
        }
        for (var t = 0; t < TriangleCount; t++) _inside[t] = depth[t] % 2 == 1;
    }

    /// <summary>Splits every unconstrained edge inside that is longer than <paramref name="limit"/> at its midpoint, longest first, until none is.</summary>
    public void Refine(double limit)
    {
        var over = limit * (1 + 1e-9);
        for (var pass = 0; ; pass++)
        {
            var longEdges = new List<(double Length, int A, int B)>();
            for (var t = 0; t < TriangleCount; t++)
                for (var k = 0; k < 3; k++)
                {
                    var (a, b) = (V(t, k), V(t, k + 1));
                    if (!_inside[t] || _c[3 * t + k] || a > b) continue;
                    var d = _p[b] - _p[a];
                    var length = Math.Sqrt(d.X * d.X + d.Y * d.Y);
                    if (length > over) longEdges.Add((length, a, b));
                }
            if (longEdges.Count == 0) return;
            if (pass > 200) throw new InvalidOperationException("refinement did not settle");
            foreach (var (_, a, b) in longEdges.OrderByDescending(e => e.Length).ThenBy(e => e.A).ThenBy(e => e.B))
            {
                if (FindEdge(a, b) is not { } e) continue; // flipped away by an earlier split
                var i = _p.Count;
                _p.Add((_p[a] + _p[b]) * 0.5);
                _vt.Add(e.T);
                SplitEdge(e.T, e.K, i);
            }
        }
    }

    /// <summary>The triangles inside, counter-clockwise, by point index.</summary>
    public IEnumerable<(int A, int B, int C)> Inside()
    {
        for (var t = 0; t < TriangleCount; t++)
            if (_inside[t]) yield return (V(t, 0), V(t, 1), V(t, 2));
    }

    private (int T, int Edge) Locate(Vector2D p)
    {
        var t = _last;
        for (var step = 0; ; step++)
        {
            if (step > 4 * TriangleCount + 16) throw new InvalidOperationException("point location did not settle");
            var moved = -1;
            for (var j = 0; j < 3 && moved < 0; j++)
            {
                var k = (j + step) % 3; // a rotating first edge, so the walk cannot cycle
                if (Orient(_p[V(t, k)], _p[V(t, k + 1)], p) < -_eps) moved = k;
            }
            if (moved < 0) break;
            t = N(t, moved);
            if (t < 0) throw new InvalidOperationException("the point is outside the triangulation");
        }
        for (var k = 0; k < 3; k++)
        {
            var (a, b) = (_p[V(t, k)], _p[V(t, k + 1)]);
            if (a == p || b == p) throw new InvalidOperationException("a point was given twice");
            if (Math.Abs(Orient(a, b, p)) <= _eps) return (t, k);
        }
        return (t, -1);
    }

    private void Set(int t, int a, int b, int c, int na, int nb, int nc, bool ca, bool cb, bool cc)
    {
        if (t == TriangleCount)
        {
            _v.AddRange([a, b, c]);
            _n.AddRange([na, nb, nc]);
            _c.AddRange([ca, cb, cc]);
            _inside.Add(false);
        }
        else
        {
            (_v[3 * t], _v[3 * t + 1], _v[3 * t + 2]) = (a, b, c);
            (_n[3 * t], _n[3 * t + 1], _n[3 * t + 2]) = (na, nb, nc);
            (_c[3 * t], _c[3 * t + 1], _c[3 * t + 2]) = (ca, cb, cc);
        }
        _vt[a] = _vt[b] = _vt[c] = t;
        _last = t;
    }

    /// <summary>Points neighbour <paramref name="t"/>'s link to <paramref name="from"/> at <paramref name="to"/>.</summary>
    private void Relink(int t, int from, int to)
    {
        if (t < 0) return;
        for (var k = 0; k < 3; k++)
            if (_n[3 * t + k] == from) { _n[3 * t + k] = to; return; }
    }

    private void SplitTriangle(int t, int p)
    {
        var (a, b, c) = (V(t, 0), V(t, 1), V(t, 2));
        var (na, nb, nc) = (N(t, 0), N(t, 1), N(t, 2));
        var (ca, cb, cc) = (_c[3 * t], _c[3 * t + 1], _c[3 * t + 2]);
        var inside = _inside[t];
        var (t1, t2) = (TriangleCount, TriangleCount + 1);
        Set(t, a, b, p, na, t1, t2, ca, false, false);
        Set(t1, b, c, p, nb, t2, t, cb, false, false);
        Set(t2, c, a, p, nc, t, t1, cc, false, false);
        _inside[t1] = _inside[t2] = inside;
        Relink(nb, t, t1);
        Relink(nc, t, t2);
        Legalize(new Stack<(int, int)>([(t, 0), (t1, 0), (t2, 0)]));
    }

    private void SplitEdge(int t, int k, int p)
    {
        var (a, b, c) = (V(t, k), V(t, k + 1), V(t, k + 2));
        var (nbc, nca) = (N(t, k + 1), N(t, k + 2));
        var (cbc, cca, cf) = (_c[3 * t + (k + 1) % 3], _c[3 * t + (k + 2) % 3], _c[3 * t + k]);
        var u = N(t, k);
        if (u < 0) throw new InvalidOperationException("a point on the super triangle's rim");
        var j = EdgeIndex(u, b, a);
        var d = V(u, j + 2);
        var (nad, ndb) = (N(u, j + 1), N(u, j + 2));
        var (cad, cdb) = (_c[3 * u + (j + 1) % 3], _c[3 * u + (j + 2) % 3]);
        var (inT, inU) = (_inside[t], _inside[u]);
        var (t2, u2) = (TriangleCount, TriangleCount + 1);
        Set(t, p, b, c, u2, nbc, t2, cf, cbc, false);
        Set(t2, a, p, c, u, t, nca, cf, false, cca);
        Set(u, p, a, d, t2, nad, u2, cf, cad, false);
        Set(u2, b, p, d, t, u, ndb, cf, false, cdb);
        (_inside[t2], _inside[u2]) = (inT, inU);
        Relink(nca, t, t2);
        Relink(ndb, u, u2);
        Legalize(new Stack<(int, int)>([(t, 1), (t2, 2), (u, 1), (u2, 2)]));
    }

    /// <summary>Flips edge <paramref name="k"/> of <paramref name="t"/>, pushing the four outer edges onto <paramref name="stack"/> when given.</summary>
    private void Flip(int t, int k, Stack<(int, int)>? stack)
    {
        var (a, b, c) = (V(t, k), V(t, k + 1), V(t, k + 2));
        var u = N(t, k);
        var j = EdgeIndex(u, b, a);
        var d = V(u, j + 2);
        var (nbc, nca, nad, ndb) = (N(t, k + 1), N(t, k + 2), N(u, j + 1), N(u, j + 2));
        var (cbc, cca, cad, cdb) = (_c[3 * t + (k + 1) % 3], _c[3 * t + (k + 2) % 3], _c[3 * u + (j + 1) % 3], _c[3 * u + (j + 2) % 3]);
        Set(t, c, a, d, nca, nad, u, cca, cad, false);
        Set(u, d, b, c, ndb, nbc, t, cdb, cbc, false);
        Relink(nad, u, t);
        Relink(nbc, t, u);
        if (stack is null) return;
        stack.Push((t, 0));
        stack.Push((t, 1));
        stack.Push((u, 0));
        stack.Push((u, 1));
    }

    private void Legalize(Stack<(int, int)> stack)
    {
        var guard = 0;
        while (stack.Count > 0)
        {
            if (++guard > 1_000_000 + 64 * TriangleCount) throw new InvalidOperationException("legalization did not settle");
            var (t, k) = stack.Pop();
            var u = N(t, k);
            if (u < 0 || _c[3 * t + k]) continue;
            var (a, b, c) = (V(t, k), V(t, k + 1), V(t, k + 2));
            var d = V(u, EdgeIndex(u, b, a) + 2);
            if (InCircle(_p[a], _p[b], _p[c], _p[d])) Flip(t, k, stack);
        }
    }

    /// <summary>Whether <paramref name="d"/> is inside the circle through counter-clockwise <paramref name="a"/>, <paramref name="b"/>, <paramref name="c"/>, beyond rounding.</summary>
    private static bool InCircle(Vector2D a, Vector2D b, Vector2D c, Vector2D d)
    {
        var (ax, ay, bx, by, cx, cy) = (a.X - d.X, a.Y - d.Y, b.X - d.X, b.Y - d.Y, c.X - d.X, c.Y - d.Y);
        var (la, lb, lc) = (ax * ax + ay * ay, bx * bx + by * by, cx * cx + cy * cy);
        var (ta, tb, tc) = (la * (bx * cy - cx * by), lb * (cx * ay - ax * cy), lc * (ax * by - bx * ay));
        var bound = la * (Math.Abs(bx * cy) + Math.Abs(cx * by)) + lb * (Math.Abs(cx * ay) + Math.Abs(ax * cy)) + lc * (Math.Abs(ax * by) + Math.Abs(bx * ay));
        return ta + tb + tc > 1e-12 * bound;
    }

    /// <summary>Whether edge <paramref name="u"/>-<paramref name="w"/> properly crosses segment <paramref name="a"/>-<paramref name="b"/>.</summary>
    private bool Crosses(int u, int w, int a, int b)
    {
        if (u == a || u == b || w == a || w == b) return false;
        var (o1, o2) = (Orient(_p[a], _p[b], _p[u]), Orient(_p[a], _p[b], _p[w]));
        var (o3, o4) = (Orient(_p[u], _p[w], _p[a]), Orient(_p[u], _p[w], _p[b]));
        return ((o1 > _eps && o2 < -_eps) || (o1 < -_eps && o2 > _eps)) && ((o3 > _eps && o4 < -_eps) || (o3 < -_eps && o4 > _eps));
    }

    private int Opposite(int t, int k)
    {
        var u = N(t, k);
        return V(u, EdgeIndex(u, V(t, k + 1), V(t, k)) + 2);
    }

    /// <summary>The edge of <paramref name="t"/> from <paramref name="a"/> to <paramref name="b"/>.</summary>
    private int EdgeIndex(int t, int a, int b)
    {
        for (var k = 0; k < 3; k++)
            if (V(t, k) == a && V(t, k + 1) == b) return k;
        throw new InvalidOperationException("the triangles are not linked");
    }

    /// <summary>A triangle with edge <paramref name="a"/>-<paramref name="b"/> (either way) and that edge's index, found round <paramref name="a"/>.</summary>
    private (int T, int K)? FindEdge(int a, int b)
    {
        var start = _vt[a];
        // Round a one way, then (should the fan open on the rim) the other.
        foreach (var forward in new[] { true, false })
        {
            var t = start;
            do
            {
                var k = 0;
                while (V(t, k) != a) k++;
                if (V(t, k + 1) == b) return (t, k);
                if (V(t, k + 2) == b) return (t, (k + 2) % 3);
                t = forward ? N(t, k) : N(t, k + 2);
            } while (t >= 0 && t != start);
            if (t == start) break;
        }
        return null;
    }
}
