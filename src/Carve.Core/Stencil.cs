using System.Numerics;

namespace Carve.Core;

/// <summary>
/// A weighted sum of elements (vertex ids, or the corner indices of one face), divided by <see cref="Divisor"/>: how a
/// new point is made from the ones it came from (T-71). Its terms are kept in the order they were first added, so every
/// sum it gives is repeatable bit for bit. Read only outside the kernel: build one with <see cref="Of(int)"/>,
/// <see cref="Lerp"/> or <see cref="Of(IEnumerable{ValueTuple{int, double}}, double, bool)"/>.
/// </summary>
/// <remarks>
/// A stencil of one term gives that element's value exactly, whatever its weight and divisor: a corner kept as it was.
/// The sum starts from 0 unless <see cref="SumsFromFirst"/> is set, when it starts from the first term's product, so a
/// midpoint of two negative zeros stays a negative zero (subdivide's UV midpoints, as before T-71).
/// </remarks>
public sealed class Stencil
{
    private readonly List<int> _order = [];
    private readonly Dictionary<int, double> _terms = [];

    internal Stencil(double divisor = 1, bool sumsFromFirst = false)
    {
        if (!double.IsFinite(divisor) || divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor), divisor, "a divisor is finite and above 0");
        Divisor = divisor;
        SumsFromFirst = sumsFromFirst;
    }

    /// <summary>What the weighted sum is divided by: 1, or the count of an average summed first (subdivide's face mean).</summary>
    public double Divisor { get; }

    /// <summary>Whether a sum starts from its first term rather than from 0 (the two differ only for a negative zero).</summary>
    public bool SumsFromFirst { get; }

    /// <summary>The terms in the order they were first added.</summary>
    public IReadOnlyList<(int Element, double Weight)> Terms => _order.Select(v => (v, _terms[v])).ToArray();

    /// <summary>A stencil of one element, weight 1: that element exactly.</summary>
    public static Stencil Of(int element)
    {
        var s = new Stencil();
        s.Add(element, 1);
        return s;
    }

    /// <summary>A stencil of these terms, in order (a repeated element's weights add), over <paramref name="divisor"/>.</summary>
    public static Stencil Of(IEnumerable<(int Element, double Weight)> terms, double divisor = 1, bool sumsFromFirst = false)
    {
        ArgumentNullException.ThrowIfNull(terms);
        var s = new Stencil(divisor, sumsFromFirst);
        foreach (var (element, weight) in terms) s.Add(element, weight);
        return s;
    }

    /// <summary>The lerp from <paramref name="a"/> to <paramref name="b"/> at <paramref name="t"/>: <c>(1 - t) a + t b</c>.</summary>
    public static Stencil Lerp(int a, int b, double t)
    {
        var s = new Stencil();
        s.Add(a, 1 - t);
        s.Add(b, t);
        return s;
    }

    internal void Add(int element, double weight)
    {
        if (_terms.TryGetValue(element, out var had)) _terms[element] = had + weight;
        else
        {
            _order.Add(element);
            _terms[element] = weight;
        }
    }

    /// <summary>Adds every term of <paramref name="other"/>, scaled.</summary>
    internal void Add(Stencil other, double scale)
    {
        foreach (var v in other._order) Add(v, other._terms[v] * scale);
    }

    /// <summary>This stencil over positions, summed in double from 0 and rounded once; one term gives that position exactly.</summary>
    public Vector3 Apply(IReadOnlyList<Vector3> verts)
    {
        if (_order.Count == 1) return verts[_order[0]];
        double x = 0, y = 0, z = 0;
        foreach (var v in _order)
        {
            var c = _terms[v];
            x += c * verts[v].X; y += c * verts[v].Y; z += c * verts[v].Z;
        }
        return new Vector3((float)(x / Divisor), (float)(y / Divisor), (float)(z / Divisor));
    }
}
