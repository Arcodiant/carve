using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>T-135's three node types: worley, fbm and warp, worked by hand, tiling, size-invariant, and each shown failing on a broken copy.</summary>
[Trait("Tier", "unit")]
public class WorleyFbmWarpTests
{
    // ---- running a graph --------------------------------------------------------------------------------------

    private static TextureGraph Graph(string nodes, int size, string source, uint seed = 7)
    {
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": {{size}}, "seed": {{seed}}, "nodes": { {{nodes}}, "o": { "type": "output", "inputs": { "in": "{{source}}" }, "params": { "role": "base_colour" } } } }""");
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    /// <summary>The first channel of <paramref name="source"/> (a node, or "node.port") as the tiles hold it, row-major, evaluated in tiles of <paramref name="tileSize"/> (0: whole).</summary>
    private static float[] Run(string nodes, int size, string source, int tileSize = 0, int channel = 0)
    {
        var g = Graph(nodes, size, source);
        var result = new float[size * size];
        new GraphEvaluator(g).Evaluate(OutputRole.BaseColour, size, tile =>
        {
            for (var y = tile.Rect.Y; y < tile.Rect.Bottom; y++)
                for (var x = tile.Rect.X; x < tile.Rect.Right; x++)
                {
                    var (r, gr, b) = tile.Colour(x, y);
                    result[y * size + x] = channel == 0 ? r : channel == 1 ? gr : b;
                }
        }, tileSize == 0 ? size : tileSize);
        return result;
    }

    private static float H(int ix, int iy, uint seed) => (TestNodes.Hash(ix, iy, seed) & 0xFFFF) / 65535f;

    private const uint Seed = 7;   // the graph's seed in these tests; every node's own seed is 0 unless it says

    // ---- worley -----------------------------------------------------------------------------------------------

    private delegate (float F1, float Edge, float Random) WorleyFn(int x, int y, int size, int cx, int cy, float jitter, bool manhattan, uint seed);

    /// <summary>A copy of the worley sample, with a wrap, the jitter, the edge's sign or the metric optionally broken.</summary>
    private static (float, float, float) WorleyCopy(int x, int y, int size, int cx, int cy, float jitter, bool manhattan, uint seed,
        bool wrap = true, bool useJitter = true, bool flipEdge = false, bool useMetric = true)
    {
        float px = (x + 0.5f) * cx / size, py = (y + 0.5f) * cy / size;
        int ix = (int)MathF.Floor(px), iy = (int)MathF.Floor(py);
        float f1 = float.MaxValue, f2 = float.MaxValue;
        float random = 0;
        for (var j = iy - 1; j <= iy + 1; j++)
            for (var i = ix - 1; i <= ix + 1; i++)
            {
                uint h = TestNodes.Hash(wrap ? ((i % cx) + cx) % cx : i, wrap ? ((j % cy) + cy) % cy : j, seed);
                var k = useJitter ? jitter : 1f;
                float ox = 0.5f + ((h & 0xFFFF) / 65535f - 0.5f) * k, oy = 0.5f + ((h >> 16) / 65535f - 0.5f) * k;
                float dx = i + ox - px, dy = j + oy - py;
                var d = manhattan && useMetric ? MathF.Abs(dx) + MathF.Abs(dy) : MathF.Sqrt(dx * dx + dy * dy);
                if (d < f1)
                {
                    f2 = f1; f1 = d;
                    random = (TestNodes.Hash(wrap ? ((i % cx) + cx) % cx : i, wrap ? ((j % cy) + cy) % cy : j, seed ^ 0x5BD1E995u) & 0xFFFF) / 65535f;
                }
                else if (d < f2) f2 = d;
            }
        return (f1, flipEdge ? f1 - f2 : f2 - f1, random);
    }

    private static void CheckWorley(WorleyFn sample)
    {
        const int Size = 32;
        // cells [4, 4] at 32 texels: a cell is 8 texels. With jitter 0 the points are at the cell centres, (k + 0.5) cells.
        // texel (0, 0) is at (0.0625, 0.0625) cells, 0.4375 from the centre of cell 0 in each axis.
        Assert.Equal(MathF.Sqrt(2) * 0.4375f, sample(0, 0, Size, 4, 4, 0, false, Seed).F1, 5);
        // texel (3, 3) is at (0.4375, 0.4375): 0.0625 from the centre in each axis. The nearest other centres are 1 away less that.
        Assert.Equal(MathF.Sqrt(2) * 0.0625f, sample(3, 3, Size, 4, 4, 0, false, Seed).F1, 5);
        // texel (7, 3): at (0.9375, 0.4375): cell 0's centre is (0.4375, 0.0625) away, cell 1's (0.5625, 0.0625): edge = f2 - f1 (a sign flip shows here)
        var (f1, edge, _) = sample(7, 3, Size, 4, 4, 0, false, Seed);
        Assert.Equal(MathF.Sqrt(0.4375f * 0.4375f + 0.0625f * 0.0625f), f1, 5);
        Assert.Equal(MathF.Sqrt(0.5625f * 0.5625f + 0.0625f * 0.0625f) - f1, edge, 5);
        // texels 7 and 8 are mirror images about the boundary: the edge is the same, and it is 0 only on the boundary itself
        Assert.Equal(edge, sample(8, 3, Size, 4, 4, 0, false, Seed).Edge, 5);
        // the metric: manhattan at (0, 0) is 0.4375 + 0.4375 (catches a euclidean one)
        Assert.Equal(0.875f, sample(0, 0, Size, 4, 4, 0, true, Seed).F1, 5);
        // random is one hash per cell: the same over a cell, and the hash of that cell at the seed's other half
        var cell = (TestNodes.Hash(1, 2, Seed ^ 0x5BD1E995u) & 0xFFFF) / 65535f;
        Assert.Equal(cell, sample(11, 20, Size, 4, 4, 0, false, Seed).Random, 6);
        Assert.Equal(cell, sample(8, 16, Size, 4, 4, 0, false, Seed).Random, 6);
        // the jitter: a point placed by the hash. Texel (11, 20) is in cell (1, 2) at (1.4375, 2.5625); with jitter 1 the point is at the hash's own offsets
        uint h = TestNodes.Hash(1, 2, Seed);
        float ox = (h & 0xFFFF) / 65535f, oy = (h >> 16) / 65535f;
        var near = sample(11, 20, Size, 4, 4, 1, false, Seed).F1;
        Assert.True(near <= MathF.Sqrt((1 + ox - 1.4375f) * (1 + ox - 1.4375f) + (2 + oy - 2.5625f) * (2 + oy - 2.5625f)) + 1e-5f, "the nearest point is no further than the cell's own");
        var half = sample(11, 20, Size, 4, 4, 0.5f, false, Seed);
        Assert.NotEqual(sample(11, 20, Size, 4, 4, 0, false, Seed).F1, half.F1);      // catches the jitter ignored
        Assert.NotEqual(half.F1, sample(11, 20, Size, 4, 4, 1, false, Seed).F1);
        // tiling: one image to the left or below is the other side of the image
        foreach (var (a, b) in new[] { (-1, Size - 1), (Size, 0) })
            for (var y = 0; y < Size; y += 5)
            {
                var l = sample(a, y, Size, 4, 4, 1, false, Seed);
                var r = sample(b, y, Size, 4, 4, 1, false, Seed);
                Assert.Equal(r.F1, l.F1, 4);
                Assert.Equal(r.Edge, l.Edge, 4);
                Assert.Equal(r.Random, l.Random, 6);
                var t = sample(y, a, Size, 4, 4, 1, false, Seed);
                var u = sample(y, b, Size, 4, 4, 1, false, Seed);
                Assert.Equal(u.F1, t.F1, 4);
                Assert.Equal(u.Random, t.Random, 6);
            }
    }

    [Fact]
    public void WorleyNodeHoldsTheHandWorkedValuesThroughTheEvaluatorInEveryOutput()
    {
        const string Nodes = """ "t": { "type": "worley", "params": { "cells": [4, 4], "jitter": 0 } } """;
        const int Size = 32;
        var f1 = Run(Nodes, Size, "t.f1");
        var edge = Run(Nodes, Size, "t.edge");
        var random = Run(Nodes, Size, "t.random");
        Assert.Equal(MathF.Sqrt(2) * 0.4375f, f1[0], 5);
        Assert.Equal(MathF.Sqrt(2) * 0.0625f, f1[3 * Size + 3], 5);
        Assert.Equal(MathF.Sqrt(0.5625f * 0.5625f + 0.0625f * 0.0625f) - MathF.Sqrt(0.4375f * 0.4375f + 0.0625f * 0.0625f), edge[3 * Size + 7], 5);
        Assert.Equal(edge[3 * Size + 7], edge[3 * Size + 8], 5);
        Assert.Equal((TestNodes.Hash(1, 2, Seed ^ 0x5BD1E995u) & 0xFFFF) / 65535f, random[20 * Size + 11], 6);
        // the node's own seed is an offset on the graph's
        var seeded = Run(""" "t": { "type": "worley", "params": { "cells": [4, 4], "jitter": 0, "seed": 5 } } """, Size, "t.random");
        Assert.Equal((TestNodes.Hash(1, 2, (Seed + 5) ^ 0x5BD1E995u) & 0xFFFF) / 65535f, seeded[20 * Size + 11], 6);
    }

    [Fact]
    public void WorleyIsTheSameInAnyTileSizeAndTilesAndKeepsItsPicture()
    {
        const string Nodes = """ "t": { "type": "worley", "params": { "cells": [5, 3], "jitter": 0.8 } } """;
        foreach (var port in new[] { "t.f1", "t.edge", "t.random" })
        {
            var whole = Run(Nodes, 64, port);
            foreach (var side in new[] { 8, 16 }) Assert.Equal(whole, Run(Nodes, 64, port, side));
        }
        // size-invariant: texel (4k..4k+3) at 1024 and its parent texel at 256 are within one texel's travel of each other. f1 changes by at most
        // the distance moved, 1.5 texels of 1024 across, times the 5 cells; edge (f2 - f1) by twice that.
        var small = Run(Nodes, 256, "t.f1");
        var big = Run(Nodes, 1024, "t.f1");
        var smallEdge = Run(Nodes, 256, "t.edge");
        var bigEdge = Run(Nodes, 1024, "t.edge");
        var smallRandom = Run(Nodes, 256, "t.random");
        var bigRandom = Run(Nodes, 1024, "t.random");
        var same = 0;
        for (var y = 0; y < 1024; y++)
            for (var x = 0; x < 1024; x++)
            {
                var s = (y / 4) * 256 + x / 4;
                Assert.InRange(Math.Abs(big[y * 1024 + x] - small[s]), 0, 2 * 5 * 1.5 / 1024 * 1.5);
                Assert.InRange(Math.Abs(bigEdge[y * 1024 + x] - smallEdge[s]), 0, 4 * 5 * 1.5 / 1024 * 1.5);
                if (bigRandom[y * 1024 + x] == smallRandom[s]) same++;
            }
        Assert.True(same > 1024 * 1024 * 0.97, $"{same} of the 1024 x 1024 texels have their parent texel's cell");
    }

    [Fact]
    public void WorleyEdgeIsZeroOnTheBoundaryAndPositiveInsideAndCellsHoldOneRandomEach()
    {
        const int Size = 128;
        var edge = Run(""" "t": { "type": "worley", "params": { "cells": [8, 8], "jitter": 1 } } """, Size, "t.edge");
        var random = Run(""" "t": { "type": "worley", "params": { "cells": [8, 8], "jitter": 1 } } """, Size, "t.random");
        Assert.All(edge, v => Assert.True(v >= 0));
        // each texel next to a texel of another cell has a small edge, one far from every boundary does not
        for (var y = 1; y < Size - 1; y++)
            for (var x = 1; x < Size - 1; x++)
                if (random[y * Size + x] != random[y * Size + x + 1]) Assert.True(edge[y * Size + x] < 0.2f, $"edge {edge[y * Size + x]} beside a boundary at ({x}, {y})");
        Assert.True(edge.Max() > 0.6f);
        Assert.InRange(random.Distinct().Count(), 40, 64);   // at most one value per cell, and most of the 64 cells own a texel
    }

    // ---- fbm --------------------------------------------------------------------------------------------------

    private delegate float FbmFn(int x, int y, int size, int cx, int cy, int octaves, float gain, bool gradient, string style, uint graphSeed, int nodeSeed);

    private static float SmoothRef(int x, int y, int size, int cx, int cy, uint seed)
    {
        float px = (x + 0.5f) * cx / size, py = (y + 0.5f) * cy / size;
        int ix = (int)MathF.Floor(px), iy = (int)MathF.Floor(py);
        float fx = px - ix, fy = py - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float L(int i, int j) => H(((i % cx) + cx) % cx, ((j % cy) + cy) % cy, seed);
        var top = L(ix, iy) + (L(ix + 1, iy) - L(ix, iy)) * fx;
        var bottom = L(ix, iy + 1) + (L(ix + 1, iy + 1) - L(ix, iy + 1)) * fx;
        return top + (bottom - top) * fy;
    }

    private static readonly (float X, float Y)[] Dirs = [(1, 0), (-1, 0), (0, 1), (0, -1), (0.70710678f, 0.70710678f), (-0.70710678f, 0.70710678f), (0.70710678f, -0.70710678f), (-0.70710678f, -0.70710678f)];

    /// <summary>A copy of the fractal sum with its normalisation, its weights, its styles or its gradient lattice's wrap optionally broken (value basis wraps through the real noise octave).</summary>
    private static float FbmCopy(int x, int y, int size, int cx, int cy, int octaves, float gain, bool gradient, string style, uint graphSeed, int nodeSeed,
        bool normalise = true, bool reverseWeights = false, bool swapStyles = false, bool wrap = true)
    {
        float sum = 0, total = 0;
        for (var o = 0; o < octaves; o++)
        {
            var seed = graphSeed + (uint)nodeSeed + (uint)o * 7919u;
            int cellsX = cx << o, cellsY = cy << o;
            float n;
            if (!gradient) n = SmoothRef(x, y, size, cellsX, cellsY, seed);
            else
            {
                float px = (x + 0.5f) * cellsX / size, py = (y + 0.5f) * cellsY / size;
                int ix = (int)MathF.Floor(px), iy = (int)MathF.Floor(py);
                float fx = px - ix, fy = py - iy;
                float Corner(int dx, int dy)
                {
                    int i = ix + dx, j = iy + dy;
                    var d = Dirs[TestNodes.Hash(wrap ? ((i % cellsX) + cellsX) % cellsX : i, wrap ? ((j % cellsY) + cellsY) % cellsY : j, seed) & 7];
                    return d.X * (fx - dx) + d.Y * (fy - dy);
                }
                float F(float t) => t * t * t * (t * (t * 6 - 15) + 10);
                float u = F(fx), v = F(fy);
                var top = Corner(0, 0) + (Corner(1, 0) - Corner(0, 0)) * u;
                var bottom = Corner(0, 1) + (Corner(1, 1) - Corner(0, 1)) * u;
                n = Math.Clamp((top + (bottom - top) * v) * 0.70710678f + 0.5f, 0f, 1f);
            }
            var st = swapStyles ? (style == "ridged" ? "billow" : style == "billow" ? "ridged" : style) : style;
            var value = st == "ridged" ? 1 - MathF.Abs(2 * n - 1) : st == "billow" ? MathF.Abs(2 * n - 1) : n;
            var w = 1f;
            for (var k = 0; k < (reverseWeights ? octaves - 1 - o : o); k++) w *= gain;
            sum += w * value; total += w;
        }
        return normalise ? sum / total : sum;
    }

    private static void CheckFbm(FbmFn fbm)
    {
        const int Size = 32;
        // one octave, plain, value basis: the octave itself (cells [4, 4], seed 7)
        Assert.Equal(SmoothRef(5, 9, Size, 4, 4, Seed), fbm(5, 9, Size, 4, 4, 1, 0.5f, false, "plain", Seed, 0), 5);
        // two octaves, gain 0.5: octave 0 at weight 1 and octave 1 (cells doubled, seed stepped by 7919) at weight 0.5, over 1.5 (catches weights the wrong way round)
        var two = (SmoothRef(5, 9, Size, 4, 4, Seed) + 0.5f * SmoothRef(5, 9, Size, 8, 8, Seed + 7919u)) / 1.5f;
        Assert.Equal(two, fbm(5, 9, Size, 4, 4, 2, 0.5f, false, "plain", Seed, 0), 5);
        // gain 0 leaves only the first octave
        Assert.Equal(SmoothRef(5, 9, Size, 4, 4, Seed), fbm(5, 9, Size, 4, 4, 3, 0, false, "plain", Seed, 0), 5);
        // a node's seed is an offset on the graph's
        Assert.Equal(SmoothRef(5, 9, Size, 4, 4, Seed + 3), fbm(5, 9, Size, 4, 4, 1, 0.5f, false, "plain", Seed, 3), 5);
        // the styles on the same n (a single octave): ridged 1 - |2n - 1|, billow |2n - 1| (catches them swapped)
        var n = SmoothRef(5, 9, Size, 4, 4, Seed);
        Assert.Equal(1 - MathF.Abs(2 * n - 1), fbm(5, 9, Size, 4, 4, 1, 0.5f, false, "ridged", Seed, 0), 5);
        Assert.Equal(MathF.Abs(2 * n - 1), fbm(5, 9, Size, 4, 4, 1, 0.5f, false, "billow", Seed, 0), 5);
        // gradient basis at texel centres of a lattice as fine as the image (fx = fy = 0.5, the fade 0.5): the mean of the four corner dots, stretched
        // to 0..1 by 0.7071. Texel (2, 3) of 8: corners (2, 3), (3, 3), (2, 4), (3, 4), the lattice 8 across.
        float Dot(int i, int j, float dx, float dy) { var d = Dirs[TestNodes.Hash(i % 8, j % 8, Seed) & 7]; return d.X * dx + d.Y * dy; }
        var mean = (Dot(2, 3, .5f, .5f) + Dot(3, 3, -.5f, .5f) + Dot(2, 4, .5f, -.5f) + Dot(3, 4, -.5f, -.5f)) / 4;
        Assert.Equal(mean * 0.70710678f + 0.5f, fbm(2, 3, 8, 8, 8, 1, 0.5f, true, "plain", Seed, 0), 5);
        // normalised to 0..1 at full gain and eight octaves, in every style and basis (catches the missing division), and not flat
        foreach (var gradient in new[] { false, true })
            foreach (var style in new[] { "plain", "ridged", "billow" })
            {
                float lo = 2, hi = -1;
                for (var y = 0; y < 32; y++)
                    for (var x = 0; x < 32; x++)
                    {
                        var v = fbm(x * 3, y * 3, 96, 3, 3, 8, 1, gradient, style, Seed, 0);
                        lo = Math.Min(lo, v); hi = Math.Max(hi, v);
                    }
                Assert.True(lo >= 0 && hi <= 1, $"{style} {(gradient ? "gradient" : "value")}: {lo} to {hi}");
                Assert.True(hi - lo > 0.1f, $"{style} {(gradient ? "gradient" : "value")} is flat: {lo} to {hi}");
            }
        // tiling: one image to the left or above is the other side of the image, in both bases and with every octave doubling the cells
        foreach (var gradient in new[] { false, true })
            for (var k = 0; k < Size; k += 3)
            {
                Assert.Equal(fbm(Size - 1, k, Size, 4, 3, 3, 0.6f, gradient, "plain", Seed, 0), fbm(-1, k, Size, 4, 3, 3, 0.6f, gradient, "plain", Seed, 0), 4);
                Assert.Equal(fbm(k, Size - 1, Size, 4, 3, 3, 0.6f, gradient, "plain", Seed, 0), fbm(k, -1, Size, 4, 3, 3, 0.6f, gradient, "plain", Seed, 0), 4);
            }
    }

    [Fact]
    public void FbmNodeHoldsTheHandWorkedValuesThroughTheEvaluator()
    {
        var one = Run(""" "t": { "type": "fbm", "params": { "cells": [4, 4], "octaves": 1 } } """, 32, "t");
        Assert.Equal(SmoothRef(5, 9, 32, 4, 4, Seed), one[9 * 32 + 5], 5);
        var two = Run(""" "t": { "type": "fbm", "params": { "cells": [4, 4], "octaves": 2, "gain": 0.5 } } """, 32, "t");
        Assert.Equal((SmoothRef(5, 9, 32, 4, 4, Seed) + 0.5f * SmoothRef(5, 9, 32, 8, 8, Seed + 7919u)) / 1.5f, two[9 * 32 + 5], 5);
        var ridged = Run(""" "t": { "type": "fbm", "params": { "cells": [4, 4], "octaves": 1, "style": "ridged" } } """, 32, "t");
        Assert.Equal(1 - MathF.Abs(2 * SmoothRef(5, 9, 32, 4, 4, Seed) - 1), ridged[9 * 32 + 5], 5);
        var gradient = Run(""" "t": { "type": "fbm", "params": { "cells": [64, 64], "octaves": 1, "basis": "gradient" } } """, 64, "t");
        Assert.Equal(FbmCopy(2, 3, 64, 64, 64, 1, 0.5f, true, "plain", Seed, 0), gradient[3 * 64 + 2], 6);
    }

    [Fact]
    public void FbmIsTheSameInAnyTileSizeAndSizeInvariant()
    {
        foreach (var basis in new[] { "value", "gradient" })
        {
            var nodes = $$""" "t": { "type": "fbm", "params": { "cells": [3, 2], "octaves": 4, "gain": 0.6, "basis": "{{basis}}", "style": "billow" } } """;
            var whole = Run(nodes, 64, "t");
            foreach (var side in new[] { 8, 32 }) Assert.Equal(whole, Run(nodes, 64, "t", side));
            // the same uv at 256 and 1024: the finest octave has 24 cells across and varies by at most 2 per cell, so moving 1.5 of 1024 texels changes it by little
            var small = Run(nodes, 256, "t");
            var big = Run(nodes, 1024, "t");
            double worst = 0;
            for (var y = 0; y < 1024; y++)
                for (var x = 0; x < 1024; x++) worst = Math.Max(worst, Math.Abs(big[y * 1024 + x] - small[(y / 4) * 256 + x / 4]));
            Assert.InRange(worst, 0, 0.06);
        }
    }

    // ---- warp -------------------------------------------------------------------------------------------------

    private static void CheckWarpOffset(Func<float, float, bool, float, float, float, int, (float Dx, float Dy)> offset)
    {
        // size 64, amount 0.125: 8 texels at a channel of 1 (and -8 at 0, nothing at 0.5)
        Assert.Equal((8f, -8f), offset(1, 0, true, 0.125f, 1, 0, 64));                // colour by: R is u, G is v (catches the signs, and u and v swapped)
        Assert.Equal((-8f, 8f), offset(0, 1, true, 0.125f, 1, 0, 64));
        Assert.Equal((0f, 0f), offset(.5f, .5f, true, 0.125f, 1, 0, 64));
        Assert.Equal((8f, 0f), offset(1, 0, false, 0.125f, 1, 0, 64));                // grey by along angle 0 (u)
        var (dx, dy) = offset(1, 0, false, 0.125f, 0, 1, 64);                         // along angle 90 (v, down): catches cos and sin swapped
        Assert.Equal((0f, 8f), (dx, dy));
        var (qx, qy) = offset(0, 0, false, 0.125f, 0.70710678f, 0.70710678f, 64);
        Assert.Equal(-8 * 0.70710678f, qx, 4); Assert.Equal(-8 * 0.70710678f, qy, 4);
    }

    private static void CheckBilinear(Func<float, float, float, float, float, float, float> bilinear)
    {
        Assert.Equal(4.5f, bilinear(1, 3, 5, 9, .5f, .5f), 6);                        // the mean of the four
        Assert.Equal(1.5f, bilinear(1, 3, 5, 9, .25f, 0), 6);                         // a quarter of the way from a to b (catches fx and fy swapped)
        Assert.Equal(2f, bilinear(1, 3, 5, 9, 0, .25f), 6);                           // a quarter of the way from a to c
        Assert.Equal(1f, bilinear(1, 3, 5, 9, 0, 0), 6);
    }

    private static void CheckWrappedRead(Func<int, int, int> wrap)
    {
        Assert.Equal(63, wrap(-1, 64));                                               // catches no wrap, a clamp
        Assert.Equal(0, wrap(64, 64));
        Assert.Equal(56, wrap(-8, 64));
    }

    [Fact]
    public void WarpOffsetBilinearAndWrapHoldTheHandWorkedValues()
    {
        CheckWarpOffset(WarpNode.Offset);
        CheckBilinear(TileRead.Bilinear);
        CheckWrappedRead(Tile.Wrap);
    }

    private const string Source = """ "src": { "type": "noise", "params": { "cells": [8, 8], "octaves": 2 } } """;

    private static string Const(string id, double v) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"\"{id}\": {{ \"type\": \"levels\", \"inputs\": {{ \"in\": \"src\" }}, \"params\": {{ \"out_low\": {v}, \"out_high\": {v} }} }}");

    [Fact]
    public void WarpWithAmountZeroIsItsInputAndWithAHalfGreyByToo()
    {
        const int Size = 64;
        var input = Run(Source, Size, "src");
        var zero = Run($$"""{{Source}}, {{Const("by", 1)}}, "t": { "type": "warp", "inputs": { "in": "src", "by": "by" }, "params": { "amount": 0 } } """, Size, "t");
        Assert.Equal(input, zero);
        var half = Run($$"""{{Source}}, {{Const("by", 0.5)}}, "t": { "type": "warp", "inputs": { "in": "src", "by": "by" }, "params": { "amount": 0.2 } } """, Size, "t");
        Assert.Equal(input, half);
    }

    [Fact]
    public void WarpWithAConstantByIsAShiftOfItsInputWrappingAtEveryEdge()
    {
        const int Size = 64;
        var input = Run(Source, Size, "src");
        int At(int x, int y) => Tile.Wrap(y, Size) * Size + Tile.Wrap(x, Size);
        // grey by 1 along angle 0: out(x, y) = in(x + 8, y), 0.125 of 64
        var along = Run($$"""{{Source}}, {{Const("by", 1)}}, "t": { "type": "warp", "inputs": { "in": "src", "by": "by" }, "params": { "amount": 0.125 } } """, Size, "t");
        // grey by 0 along angle 90: out(x, y) = in(x, y - 8)
        var down = Run($$"""{{Source}}, {{Const("by", 0)}}, "t": { "type": "warp", "inputs": { "in": "src", "by": "by" }, "params": { "amount": 0.125, "angle": 90 } } """, Size, "t");
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                Assert.Equal(input[At(x + 8, y)], along[y * Size + x], 5);
                Assert.Equal(input[At(x, y - 8)], down[y * Size + x], 4);
            }
        // a half texel: amount 0.5 / 64 at by 1 averages a texel with its right neighbour (the wrap included at x = 63)
        var halfTexel = Run($$"""{{Source}}, {{Const("by", 1)}}, "t": { "type": "warp", "inputs": { "in": "src", "by": "by" }, "params": { "amount": 0.0078125 } } """, Size, "t");
        for (var x = 0; x < Size; x++)
            Assert.Equal((input[5 * Size + x] + input[5 * Size + (x + 1) % Size]) / 2, halfTexel[5 * Size + x], 5);
    }

    [Fact]
    public void WarpByAColourShiftsInUAndVAndKeepsAColourInput()
    {
        const int Size = 64;
        // by "#ff0000": R 1, G 0: out(x, y) = in(x + 8, y - 8). The input is the two noises as colour: red from one, green from another
        var nodes = $$"""
            {{Source}}, "g": { "type": "noise", "params": { "cells": [5, 3], "seed": 4 } },
            "red": { "type": "colour", "params": { "colour": "#ff0000" } }, "green": { "type": "colour", "params": { "colour": "#00ff00" } },
            "r": { "type": "blend", "inputs": { "a": "red", "b": "src" }, "params": { "mode": "multiply" } },
            "gg": { "type": "blend", "inputs": { "a": "green", "b": "g" }, "params": { "mode": "multiply" } },
            "rg": { "type": "blend", "inputs": { "a": "r", "b": "gg" }, "params": { "mode": "add" } },
            "t": { "type": "warp", "inputs": { "in": "rg", "by": "red" }, "params": { "amount": 0.125 } }
            """;
        var inR = Run(nodes, Size, "rg", 0, 0);
        var inG = Run(nodes, Size, "rg", 0, 1);
        var outR = Run(nodes, Size, "t", 0, 0);
        var outG = Run(nodes, Size, "t", 0, 1);
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var s = Tile.Wrap(y - 8, Size) * Size + Tile.Wrap(x + 8, Size);
                Assert.Equal(inR[s], outR[y * Size + x], 5);
                Assert.Equal(inG[s], outG[y * Size + x], 5);
            }
        Assert.Contains(outG, v => v > 0.1f);   // the green really is there: a colour in gives a colour out
    }

    [Fact]
    public void WarpAsksItsInputForTheOutputWidenedByTheAmountAndItsByForTheOutput()
    {
        var node = new WarpNode();
        var read = TexGraphFile.Read("""{ "version": 1, "size": 64, "seed": 0, "nodes": { "c": { "type": "colour" }, "t": { "type": "warp", "inputs": { "in": "c", "by": "c" }, "params": { "amount": 0.1 } } } }""");
        var p = new NodeParameters(node, read.Graph!.Nodes["t"].Params);
        var rect = new TexelRect(16, 32, 8, 8);
        Assert.Equal(rect.Inflate(9), node.InputRect("in", rect, p, 64));              // 0.1 of 64 is 6.4, up to 7, and 2 for the bilinear read
        Assert.Equal(rect, node.InputRect("by", rect, p, 64));
        Assert.Equal(PortKind.Colour, node.OutputKind("out", new Dictionary<string, PortKind> { ["in"] = PortKind.Colour, ["by"] = PortKind.Grey }, p));
        Assert.Equal(PortKind.Grey, node.OutputKind("out", new Dictionary<string, PortKind> { ["in"] = PortKind.Grey, ["by"] = PortKind.Colour }, p));
    }

    [Fact]
    public void WarpIsTheSameInAnyTileSizeAndSizeInvariantAtItsWidestReach()
    {
        const string Nodes = """
            "src": { "type": "worley", "params": { "cells": [4, 4] } },
            "u": { "type": "fbm", "params": { "cells": [3, 3], "octaves": 3, "seed": 1 } },
            "t": { "type": "warp", "inputs": { "in": "src.edge", "by": "u" }, "params": { "amount": 0.25, "angle": 30 } }
            """;
        var whole = Run(Nodes, 64, "t");
        foreach (var side in new[] { 4, 16 }) Assert.Equal(whole, Run(Nodes, 64, "t", side));
        var small = Run(Nodes, 256, "t");
        var big = Run(Nodes, 1024, "t");
        double worst = 0;
        for (var y = 0; y < 1024; y++)
            for (var x = 0; x < 1024; x++) worst = Math.Max(worst, Math.Abs(big[y * 1024 + x] - small[(y / 4) * 256 + x / 4]));
        // the warped edge moves by up to a quarter image over the fbm's 0..1; one parent texel of difference at 256 is a displacement of 1 texel
        // (4 at 1024) and a slope in the offset: the picture is the same to within a fifth of its range
        Assert.InRange(worst, 0, 0.2);
    }

    // ---- each node failing on a broken copy ----------------------------------------------------------------------

    [Fact]
    public void EveryNodeIsCaughtOnABrokenCopy()
    {
        // the real nodes pass
        CheckWorley(WorleyNode.Sample);
        CheckFbm(FbmNode.Value);
        CheckWarpOffset(WarpNode.Offset);
        CheckBilinear(TileRead.Bilinear);
        CheckWrappedRead(Tile.Wrap);
        // and so do the copies, unbroken
        CheckWorley((x, y, s, cx, cy, j, m, seed) => WorleyCopy(x, y, s, cx, cy, j, m, seed));
        CheckFbm((x, y, s, cx, cy, o, g, gr, st, gs, ns) => FbmCopy(x, y, s, cx, cy, o, g, gr, st, gs, ns));

        // the table: what was broken, and that the check that catches it fails
        var breaks = new (string Name, Action Check)[]
        {
            ("worley: no wrap of the cells", () => CheckWorley((x, y, s, cx, cy, j, m, seed) => WorleyCopy(x, y, s, cx, cy, j, m, seed, wrap: false))),
            ("worley: the jitter ignored", () => CheckWorley((x, y, s, cx, cy, j, m, seed) => WorleyCopy(x, y, s, cx, cy, j, m, seed, useJitter: false))),
            ("worley: edge as f1 - f2", () => CheckWorley((x, y, s, cx, cy, j, m, seed) => WorleyCopy(x, y, s, cx, cy, j, m, seed, flipEdge: true))),
            ("worley: manhattan measured as euclidean", () => CheckWorley((x, y, s, cx, cy, j, m, seed) => WorleyCopy(x, y, s, cx, cy, j, m, seed, useMetric: false))),
            ("fbm: not divided by the weights", () => CheckFbm((x, y, s, cx, cy, o, g, gr, st, gs, ns) => FbmCopy(x, y, s, cx, cy, o, g, gr, st, gs, ns, normalise: false))),
            ("fbm: the octave weights reversed", () => CheckFbm((x, y, s, cx, cy, o, g, gr, st, gs, ns) => FbmCopy(x, y, s, cx, cy, o, g, gr, st, gs, ns, reverseWeights: true))),
            ("fbm: ridged and billow swapped", () => CheckFbm((x, y, s, cx, cy, o, g, gr, st, gs, ns) => FbmCopy(x, y, s, cx, cy, o, g, gr, st, gs, ns, swapStyles: true))),
            ("fbm: the gradient lattice not wrapped", () => CheckFbm((x, y, s, cx, cy, o, g, gr, st, gs, ns) => FbmCopy(x, y, s, cx, cy, o, g, gr, st, gs, ns, wrap: false))),
            ("warp: the offset's sign flipped", () => CheckWarpOffset((r, g, c, a, co, si, s) => { var d = WarpNode.Offset(r, g, c, a, co, si, s); return (-d.Dx, -d.Dy); })),
            ("warp: u and v swapped", () => CheckWarpOffset((r, g, c, a, co, si, s) => { var d = WarpNode.Offset(r, g, c, a, co, si, s); return (d.Dy, d.Dx); })),
            ("warp: the angle's cos and sin swapped", () => CheckWarpOffset((r, g, c, a, co, si, s) => WarpNode.Offset(r, g, c, a, si, co, s))),
            ("warp: bilinear weights swapped", () => CheckBilinear((a, b, c, d, fx, fy) => TileRead.Bilinear(a, b, c, d, fy, fx))),
            ("warp: reads clamped instead of wrapping", () => CheckWrappedRead((v, n) => Math.Clamp(v, 0, n - 1))),
        };
        foreach (var (name, check) in breaks)
            Assert.True(Throws(check), $"the broken copy '{name}' was not caught");
    }

    private static bool Throws(Action check)
    {
        try { check(); return false; }
        catch (Xunit.Sdk.XunitException) { return true; }
    }
}
