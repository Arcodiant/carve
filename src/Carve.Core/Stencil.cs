using System.Numerics;

namespace Carve.Core;

/// <summary>
/// A weighted sum of elements (vertex ids, or the corner indices of one face), divided by <see cref="Divisor"/>: how a
/// new point is made from the ones it came from (T-71). Its terms are kept in the order they were first added, so every
/// sum it gives is repeatable bit for bit.
/// </summary>
public sealed class Stencil
{
    private readonly List<int> _order = [];
    private readonly Dictionary<int, double> _terms = [];

    /// <summary>A stencil of no terms, dividing by <paramref name="divisor"/> (1 for a plain weighted sum).</summary>
    public Stencil(double divisor = 1)
    {
        if (!double.IsFinite(divisor) || divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor), divisor, "a divisor is finite and above 0");
        Divisor = divisor;
    }

    /// <summary>What the weighted sum is divided by: 1, or the count of an average summed first (subdivide's face mean).</summary>
    public double Divisor { get; }

    /// <summary>The terms in the order they were first added.</summary>
    public IReadOnlyList<(int Element, double Weight)> Terms => _order.Select(v => (v, _terms[v])).ToArray();

    /// <summary>A stencil of one element, weight 1: that element exactly.</summary>
    public static Stencil Of(int element)
    {
        var s = new Stencil();
        s.Add(element, 1);
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

    public void Add(int element, double weight)
    {
        if (_terms.TryGetValue(element, out var had)) _terms[element] = had + weight;
        else
        {
            _order.Add(element);
            _terms[element] = weight;
        }
    }

    /// <summary>Adds every term of <paramref name="other"/>, scaled.</summary>
    public void Add(Stencil other, double scale)
    {
        foreach (var v in other._order) Add(v, other._terms[v] * scale);
    }

    /// <summary>This stencil over positions, summed in double and rounded once; one term gives that position exactly.</summary>
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
