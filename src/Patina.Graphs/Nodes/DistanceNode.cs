using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>distance</c>: input <c>in</c>, a greyscale mask (a texel is inside at 0.5 or more); output, greyscale, each texel's
/// distance to the mask's edge as a fraction of <c>range</c> (0..0.25 of the image width), clamped to 1. The distance is
/// from the texel's centre to the nearest texel centre on the other side of the edge, so a texel next to the edge is
/// one texel away. <c>side</c> <c>inside</c> measures inside texels (outside ones are 0), <c>outside</c> the outside ones
/// (inside ones are 0), <c>both</c> every texel. Range 0 gives 1 for every texel measured and 0 elsewhere.
/// <para>
/// Exact Euclidean within the window <c>range</c> widens, wrapping, so it tiles: the input rectangle is the output's
/// widened by the range in texels, rounded up, and 1. A texel further than the range from the other side is clamped
/// to 1, so nothing outside the window can change a value. Worked in two passes (Felzenszwalb and Huttenlocher's):
/// along each row of the window, the distance in u to the nearest texel of the other side, searched outward from the
/// tile only as far as the range; then down each column of the tile, the lower envelope of the parabolas those row
/// distances make. Every distance is a whole number of texels squared until its square root, so any tile size gives
/// the same bytes.
/// </para>
/// </summary>
public sealed class DistanceNode : NodeType
{
    public const string TypeName = "distance";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Grey)];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.Float("range", 0, 0.25, 0.05),
        ParamSpec.Enum("side", "inside", "inside", "outside", "both"),
    ];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;

    /// <summary>The range in texels.</summary>
    public static double RangeTexels(float range, int size) => (double)range * size;

    /// <summary>How far past the output the window reaches: the range in texels rounded up, and 1.</summary>
    public static int Reach(float range, int size) => (int)Math.Ceiling(RangeTexels(range, size)) + 1;

    public override TexelRect InputRect(string port, TexelRect output, NodeParameters parameters, int size) =>
        output.Inflate(Reach(parameters.Float("range"), size));

    /// <summary>A row distance beyond the reach: no texel of that side within the window.</summary>
    private const int None = int.MaxValue;

    /// <summary>
    /// T-155: pass 1 is the row pass and pass 2 the column pass, each reaching the window's reach. The row pass's tile
    /// holds, for each texel, the u distance to the nearest texel of the other side within the reach, positive for an
    /// inside texel and negative for an outside one (infinite, with that sign, when there is none). Capped at the
    /// reach, it is the same in every tile; every distance under the range is still found, so the bytes are
    /// <see cref="Evaluate"/>'s.
    /// </summary>
    public override SeparablePasses? Separable(NodeParameters parameters, int size)
    {
        var reach = Reach(parameters.Float("range"), size);
        return new SeparablePasses(reach, reach);
    }

    public override void EvaluateRows(NodeEvaluation e)
    {
        var input = e.Input("in")!;
        var output = e.Output();
        var rect = e.Rect;
        var reach = Reach(e.Parameters.Float("range"), e.Size);
        var width = rect.Width + 2 * reach;
        var row = new float[width];
        var left = new int[width];
        for (var y = rect.Y; y < rect.Bottom; y++)
        {
            input.CopyRow(y, rect.X - reach, width, row);
            // the last window index at or before each of the other side, for the tile's columns: from the left, then the right
            int lastIn = int.MinValue / 2, lastOut = int.MinValue / 2;
            for (var i = 0; i < reach + rect.Width; i++)
            {
                if (row[i] >= 0.5f) lastIn = i; else lastOut = i;
                if (i >= reach) left[i] = row[i] >= 0.5f ? i - lastOut : i - lastIn;
            }
            int nextIn = int.MaxValue / 2, nextOut = int.MaxValue / 2;
            var to = output.Row(y);
            for (var i = width - 1; i >= reach; i--)
            {
                var inside = row[i] >= 0.5f;
                if (inside) nextIn = i; else nextOut = i;
                if (i >= reach + rect.Width) continue;
                var d = Math.Min(left[i], inside ? nextOut - i : nextIn - i);
                var v = d > reach ? float.PositiveInfinity : d;
                to[i - reach] = inside ? v : -v;
            }
        }
    }

    public override void EvaluateColumns(NodeEvaluation e)
    {
        var rowsRead = e.Input(RowsPort)!;
        var output = e.Output();
        var rect = e.Rect;
        var side = e.Parameters.Text("side");
        var reach = Reach(e.Parameters.Float("range"), e.Size);
        var rangeTexels = RangeTexels(e.Parameters.Float("range"), e.Size);
        bool wantInside = side != "outside", wantOutside = side != "inside";
        var rows = rect.Height + 2 * reach;
        var buffer = e.Scratch(rows * rect.Width); // the evaluator's, not large-object garbage a tile (RV-76)
        for (var r = 0; r < rows; r++) rowsRead.CopyRow(rect.Y - reach + r, rect.X, rect.Width, buffer.AsSpan(r * rect.Width, rect.Width));
        var toOut = new int[rows];
        var toIn = new int[rows];
        var f = new double[rows];
        var outOf = new double[rect.Height];
        var inOf = new double[rect.Height];
        var envelope = new Envelope(rows);
        for (var c = 0; c < rect.Width; c++)
        {
            for (var q = 0; q < rows; q++)
            {
                var v = buffer[q * rect.Width + c];
                var d = float.IsInfinity(v) ? None : (int)Math.Abs(v);
                toOut[q] = v > 0 ? d : 0;
                toIn[q] = v > 0 ? 0 : d;
            }
            if (wantInside) envelope.Column(toOut, 0, 1, rows, reach, f, outOf);
            if (wantOutside) envelope.Column(toIn, 0, 1, rows, reach, f, inOf);
            for (var y = 0; y < rect.Height; y++)
            {
                var self = buffer[(reach + y) * rect.Width + c] > 0;
                double d2 = self ? (wantInside ? outOf[y] : -1) : (wantOutside ? inOf[y] : -1);
                output.Row(rect.Y + y)[c] = d2 < 0 ? 0f : Value(d2, rangeTexels);
            }
        }
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var range = e.Parameters.Float("range");
        var side = e.Parameters.Text("side");
        var input = e.Input("in")!;
        var output = e.Output();
        var rect = e.Rect;
        var reach = Reach(range, e.Size);
        var rangeTexels = RangeTexels(range, e.Size);
        bool wantInside = side != "outside", wantOutside = side != "inside";

        // the window's rows, Y - reach to Bottom + reach - 1; for each, the u distance from each of the tile's columns
        // to the nearest outside texel (toOut, measured for inside texels) and the nearest inside texel (toIn)
        int rows = rect.Height + 2 * reach, top = rect.Y - reach;
        var toOut = wantInside ? new int[rows * rect.Width] : null;
        var toIn = wantOutside ? new int[rows * rect.Width] : null;
        var inside = new bool[rect.Width + 2 * reach];
        int x0 = rect.X - reach, end = rect.Width + 2 * reach;
        for (var r = 0; r < rows; r++)
        {
            // the tile's own run, then runs outward on each side only until each side wanted has been met (or the window ends)
            var y = top + r;
            int lo = reach, hi = reach + rect.Width;
            ReadRow(input, y, x0, lo, hi, rect.Width, rect.X, inside);
            bool needOut = toOut != null, needIn = toIn != null;
            while (lo > 0 && Wants(inside, lo, reach, needOut, needIn))
            {
                var from = Math.Max(0, lo - rect.Width);
                ReadRow(input, y, x0, from, lo, rect.Width, rect.X, inside);
                lo = from;
            }
            var right = reach + rect.Width;
            while (hi < end && Wants(inside, right, hi, needOut, needIn))
            {
                var to = Math.Min(end, hi + rect.Width);
                ReadRow(input, y, x0, hi, to, rect.Width, rect.X, inside);
                hi = to;
            }
            if (toOut != null) RowDistance(inside, lo, hi, reach, rect.Width, false, toOut.AsSpan(r * rect.Width, rect.Width));
            if (toIn != null) RowDistance(inside, lo, hi, reach, rect.Width, true, toIn.AsSpan(r * rect.Width, rect.Width));
        }

        // down each column of the tile: the squared distance over the window's rows
        var f = new double[rows];
        var outOf = toOut != null ? new double[rect.Height] : null;
        var inOf = toIn != null ? new double[rect.Height] : null;
        var envelope = new Envelope(rows);
        for (var c = 0; c < rect.Width; c++)
        {
            if (toOut != null) envelope.Column(toOut, c, rect.Width, rows, reach, f, outOf!);
            if (toIn != null) envelope.Column(toIn, c, rect.Width, rows, reach, f, inOf!);
            for (var y = 0; y < rect.Height; y++)
            {
                var self = input.Grey(rect.X + c, rect.Y + y) >= 0.5f;
                double d2 = self ? (wantInside ? outOf![y] : -1) : (wantOutside ? inOf![y] : -1);
                output.Row(rect.Y + y)[c] = d2 < 0 ? 0f : Value(d2, rangeTexels);
            }
        }
    }

    /// <summary>A squared distance in texels as the output's value: the distance over the range, clamped to 1.</summary>
    public static float Value(double d2, double rangeTexels)
    {
        if (double.IsPositiveInfinity(d2)) return 1f;
        var d = Math.Sqrt(d2);
        return d >= rangeTexels ? 1f : (float)(d / rangeTexels);
    }

    /// <summary>Whether window indices <paramref name="from"/> to to - 1, read so far on one side of the tile, still lack a wanted kind of texel.</summary>
    private static bool Wants(bool[] inside, int from, int to, bool needOut, bool needIn)
    {
        bool seenOut = false, seenIn = false;
        for (var i = from; i < to && !((seenOut || !needOut) && (seenIn || !needIn)); i++)
            if (inside[i]) seenIn = true; else seenOut = true;
        return (needOut && !seenOut) || (needIn && !seenIn);
    }

    /// <summary>Row <paramref name="y"/>'s window indices <paramref name="from"/> to to - 1 (logical x = x0 + index), inside or not, read a tile's run at a time.</summary>
    private static void ReadRow(TileRead input, int y, int x0, int from, int to, int tileWidth, int tileX, bool[] inside)
    {
        var x = x0 + from;
        while (x < x0 + to)
        {
            // runs end on the tile grid, which the output rectangle's own X lies on
            var next = tileX + (int)Math.Floor((double)(x - tileX) / tileWidth + 1) * tileWidth;
            var n = Math.Min(next, x0 + to) - x;
            var span = input.Row(y, x, n);
            for (var i = 0; i < n; i++) inside[x - x0 + i] = span[i] >= 0.5f;
            x += n;
        }
    }

    /// <summary>
    /// For each of the tile's columns (window index reach to reach + width - 1), the u distance to the nearest texel whose
    /// insideness is <paramref name="target"/>, among window indices lo to hi - 1 (those read), or <see cref="None"/>.
    /// </summary>
    private static void RowDistance(bool[] inside, int lo, int hi, int reach, int width, bool target, Span<int> to)
    {
        // the nearest target at or left of each column, then at or right of it
        var last = None;
        for (var i = reach - 1; i >= lo; i--)
            if (inside[i] == target) { last = i; break; }
        for (var k = 0; k < width; k++)
        {
            var i = reach + k;
            if (inside[i] == target) last = i;
            to[k] = last == None ? None : i - last;
        }
        var nextAt = None;
        for (var i = reach + width; i < hi; i++)
            if (inside[i] == target) { nextAt = i; break; }
        for (var k = width - 1; k >= 0; k--)
        {
            var i = reach + k;
            if (inside[i] == target) nextAt = i;
            if (nextAt != None && nextAt - i < to[k]) to[k] = nextAt - i;
        }
    }

    /// <summary>Felzenszwalb and Huttenlocher's lower envelope of parabolas down one column, reused across columns.</summary>
    private sealed class Envelope(int rows)
    {
        private readonly int[] _v = new int[rows];
        private readonly double[] _z = new double[rows + 1];

        /// <summary>
        /// Column <paramref name="c"/> of <paramref name="to"/>: f(q) = to(q)^2 over the window's rows; writes the lower
        /// envelope min over q of (p - q)^2 + f(q) at the tile's rows p (window rows reach to reach + height - 1) into
        /// <paramref name="d2"/>, positive infinity where no row has a target.
        /// </summary>
        public void Column(int[] to, int c, int width, int rows, int reach, double[] f, double[] d2)
        {
            var k = -1;
            for (var q = 0; q < rows; q++)
            {
                var t = to[q * width + c];
                if (t == None) continue;
                f[q] = (double)t * t;
                if (k < 0) { k = 0; _v[0] = q; _z[0] = double.NegativeInfinity; _z[1] = double.PositiveInfinity; continue; }
                double s;
                while (true)
                {
                    var v = _v[k];
                    s = (f[q] + (double)q * q - (f[v] + (double)v * v)) / (2.0 * (q - v));
                    if (s <= _z[k] && k > 0) { k--; continue; }
                    break;
                }
                k++;
                _v[k] = q; _z[k] = s; _z[k + 1] = double.PositiveInfinity;
            }
            if (k < 0) { Array.Fill(d2, double.PositiveInfinity); return; }
            var j = 0;
            for (var y = 0; y < d2.Length; y++)
            {
                var p = reach + y;
                while (_z[j + 1] < p) j++;
                var dv = p - _v[j];
                d2[y] = (double)dv * dv + f[_v[j]];
            }
        }
    }
}
