using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>blur</c>: input <c>in</c>, greyscale or colour, the same out. A box blur, wrapping so that it tiles. <c>radius</c> is a
/// part of the image's width (0 to 0.1), made a whole number of texels, <c>r = floor(radius * size + 0.5)</c>; a box pass
/// averages the <c>2r + 1</c> texels centred on each. <c>quality</c> <c>box</c> is one pass, <c>smooth</c> three (close to a
/// Gaussian of sigma about <c>r</c>). Separable: all the horizontal passes, then all the vertical ones, each a running sum,
/// so the cost a texel does not grow with the radius. <c>radius</c> 0 is the input.
/// <para>Arithmetic (recorded by T-137): each running sum is an integer, the texels held as fixed point (value * 2^32,
/// rounded), so a sum is exact in any order and from any start; a pass's result is the integer sum divided by the window
/// and 2^32 in <c>double</c> and rounded once to <c>float</c>. That makes a texel's value the same whatever tile it is
/// computed in, which a <c>float</c> or <c>double</c> running sum (it starts at a different texel in each tile) could not
/// promise to the last bit. Values may be negative or above 1.</para>
/// </summary>
public sealed class BlurNode : NodeType
{
    public const string TypeName = "blur";

    private const double Fixed = 4294967296.0; // 2^32

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [new("in", PortKind.Any)];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
    [
        ParamSpec.Float("radius", 0, 0.1, 0.01),
        ParamSpec.Enum("quality", "box", "box", "smooth"),
    ];

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => inputs["in"];

    /// <summary>The radius in whole texels.</summary>
    public static int Texels(double radius, int size) => (int)Math.Floor(radius * size + 0.5);

    public static int Passes(string quality) => quality == "smooth" ? 3 : 1;

    /// <summary>How far past the output a read reaches: a pass reaches <c>r</c>.</summary>
    public static int Reach(int texels, int passes) => texels * passes;

    public override TexelRect InputRect(string port, TexelRect output, NodeParameters parameters, int size) =>
        output.Inflate(Reach(Texels(parameters.Float("radius"), size), Passes(parameters.Text("quality"))));

    /// <summary>The input over <paramref name="rect"/> (logical texels, wrapping), <paramref name="channels"/> floats a texel.</summary>
    public static float[] Gather(TileRead input, TexelRect rect, int channels)
    {
        var buffer = new float[rect.Width * rect.Height * channels];
        var i = 0;
        for (var y = rect.Y; y < rect.Bottom; y++)
            for (var x = rect.X; x < rect.Right; x++)
            {
                var (r, g, b) = input.Colour(x, y);
                buffer[i++] = r;
                if (channels == 3) { buffer[i++] = g; buffer[i++] = b; }
            }
        return buffer;
    }

    /// <summary>
    /// Blurs <paramref name="buffer"/>, <paramref name="width"/> by <paramref name="height"/> texels, with
    /// <paramref name="passes"/> box passes of radius <paramref name="texels"/> each way. Each pass takes
    /// <c>2 * texels</c> texels off each axis, so the result is <c>width - 2 * texels * passes</c> by the same down.
    /// </summary>
    public static float[] Smooth(float[] buffer, int width, int height, int channels, int texels, int passes)
    {
        var window = 2 * texels + 1;
        var w = width;
        for (var p = 0; p < passes; p++) { buffer = Horizontal(buffer, w, height, channels, window); w -= 2 * texels; }
        var h = height;
        for (var p = 0; p < passes; p++) { buffer = Vertical(buffer, w, h, channels, window); h -= 2 * texels; }
        return buffer;
    }

    private static long Quantise(float v) => (long)Math.Floor(v * Fixed + 0.5);

    // Into `to` when given (its first w x height texels; RV-76: the separable passes work in the evaluator's scratch),
    // else a new array.
    private static float[] Horizontal(float[] from, int width, int height, int channels, int window, float[]? to = null)
    {
        var w = width - window + 1;
        to ??= new float[w * height * channels];
        var scale = window * Fixed;
        for (var y = 0; y < height; y++)
            for (var c = 0; c < channels; c++)
            {
                var row = y * width * channels + c;
                var sum = 0L;
                for (var k = 0; k < window; k++) sum += Quantise(from[row + k * channels]);
                var o = y * w * channels + c;
                to[o] = (float)(sum / scale);
                for (var x = 1; x < w; x++)
                {
                    sum += Quantise(from[row + (x + window - 1) * channels]) - Quantise(from[row + (x - 1) * channels]);
                    to[o + x * channels] = (float)(sum / scale);
                }
            }
        return to;
    }

    private static float[] Vertical(float[] from, int width, int height, int channels, int window, float[]? to = null)
    {
        var h = height - window + 1;
        var stride = width * channels;
        to ??= new float[stride * h];
        var scale = window * Fixed;
        var sums = new long[stride];
        for (var k = 0; k < window; k++)
            for (var i = 0; i < stride; i++) sums[i] += Quantise(from[k * stride + i]);
        for (var i = 0; i < stride; i++) to[i] = (float)(sums[i] / scale);
        for (var y = 1; y < h; y++)
        {
            var add = (y + window - 1) * stride; var drop = (y - 1) * stride; var o = y * stride;
            for (var i = 0; i < stride; i++)
            {
                sums[i] += Quantise(from[add + i]) - Quantise(from[drop + i]);
                to[o + i] = (float)(sums[i] / scale);
            }
        }
        return to;
    }

    /// <summary>T-155: the horizontal passes are the row pass, the vertical ones the column pass, each reaching <c>r</c> times the passes.</summary>
    public override SeparablePasses? Separable(NodeParameters parameters, int size)
    {
        var reach = Reach(Texels(parameters.Float("radius"), size), Passes(parameters.Text("quality")));
        return new SeparablePasses(reach, reach);
    }

    /// <summary>The horizontal passes over the tile's rows, read widened by the reach in x: exactly the rows <see cref="Evaluate"/>'s vertical passes start from.</summary>
    public override void EvaluateRows(NodeEvaluation e)
    {
        var input = e.Input("in")!;
        var output = e.Output();
        var channels = output.Channels;
        var texels = Texels(e.Parameters.Float("radius"), e.Size);
        var passes = Passes(e.Parameters.Text("quality"));
        var reach = Reach(texels, passes);
        var rect = e.Rect;
        int width = rect.Width + 2 * reach, stride = width * channels;
        // Two scratch arrays, each pass reading one and writing the other (RV-76: new arrays each pass were large-object
        // garbage, about 8 MB a row tile at 8192).
        float[] buffer = e.Scratch(stride * rect.Height), spare = e.Scratch(stride * rect.Height);
        for (var y = 0; y < rect.Height; y++) input.CopyRow(rect.Y + y, rect.X - reach, width, buffer.AsSpan(y * stride, stride));
        var window = 2 * texels + 1;
        for (var p = 0; p < passes; p++)
        {
            (buffer, spare) = (Horizontal(buffer, width, rect.Height, channels, window, spare), buffer);
            width -= 2 * texels;
        }
        buffer.AsSpan(0, output.Data.Length).CopyTo(output.Data);
    }

    /// <summary>The vertical passes over the row pass's tiles, read widened by the reach in y.</summary>
    public override void EvaluateColumns(NodeEvaluation e)
    {
        var rows = e.Input(RowsPort)!;
        var output = e.Output();
        var channels = output.Channels;
        var texels = Texels(e.Parameters.Float("radius"), e.Size);
        var passes = Passes(e.Parameters.Text("quality"));
        var reach = Reach(texels, passes);
        var rect = e.Rect;
        int height = rect.Height + 2 * reach, stride = rect.Width * channels;
        float[] buffer = e.Scratch(stride * height), spare = e.Scratch(stride * height);
        for (var y = 0; y < height; y++) rows.CopyRow(rect.Y - reach + y, rect.X, rect.Width, buffer.AsSpan(y * stride, stride));
        var window = 2 * texels + 1;
        for (var p = 0; p < passes; p++)
        {
            (buffer, spare) = (Vertical(buffer, rect.Width, height, channels, window, spare), buffer);
            height -= 2 * texels;
        }
        buffer.AsSpan(0, output.Data.Length).CopyTo(output.Data);
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var input = e.Input("in")!;
        var output = e.Output();
        var channels = output.Channels;
        var texels = Texels(e.Parameters.Float("radius"), e.Size);
        var passes = Passes(e.Parameters.Text("quality"));
        var rect = e.Rect.Inflate(Reach(texels, passes));
        var blurred = Smooth(Gather(input, rect, channels), rect.Width, rect.Height, channels, texels, passes);
        blurred.CopyTo(output.Data, 0); // after the passes the buffer is exactly e.Rect
    }
}
