using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>T-112's five node types: noise, gradient, levels, blend and height_to_normal, worked by hand, tiling, size-invariant, and each shown failing on a broken copy.</summary>
[Trait("Tier", "unit")]
public class FirstNodesTests
{
    // ---- running a graph --------------------------------------------------------------------------------------

    private const string Base = """ "g0": { "type": "gradient" }, """;

    private static TextureGraph Graph(string nodes, int size, uint seed = 7)
    {
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": {{size}}, "seed": {{seed}}, "nodes": { {{Base}} {{nodes}} } }""");
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    /// <summary>The floats of node "t" (greyscale, or colour taking the first channel) as the output role's band holds them, row-major.</summary>
    private static float[] Run(string nodes, int size, OutputRole role, int channel = 0, int? evalSize = null, uint seed = 7)
    {
        var roleName = OutputRoles.Name(role);
        var g = Graph(nodes + $$""", "o": { "type": "output", "inputs": { "in": "t" }, "params": { "role": "{{roleName}}" } }""", size, seed);
        var n = evalSize ?? size;
        var result = new float[n * n];
        new GraphEvaluator(g).Evaluate(role, n, (band, y0, rows) =>
        {
            for (var y = y0; y < y0 + rows; y++)
                for (var x = 0; x < n; x++)
                {
                    var (r, gr, b) = band.Colour(x, y);
                    result[y * n + x] = channel == 0 ? r : channel == 1 ? gr : b;
                }
        });
        return result;
    }

    private static float[] Grey(string nodes, int size, int? evalSize = null, uint seed = 7) => Run(nodes, size, OutputRole.Roughness, 0, evalSize, seed);

    private static string Const(string id, double v) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"\"{id}\": {{ \"type\": \"levels\", \"inputs\": {{ \"in\": \"g0\" }}, \"params\": {{ \"out_low\": {v}, \"out_high\": {v} }} }}");

    private static float H(int ix, int iy, uint seed) => (TestNodes.Hash(ix, iy, seed) & 0xFFFF) / 65535f;

    // ---- noise ------------------------------------------------------------------------------------------------

    [Fact]
    public void StepNoiseHoldsOneHashedValuePerCell()
    {
        // 32 texels, cells [4, 2]: a cell is 8 texels across and 16 down; the lattice value is T-77's hash of the cell at seed 7.
        var v = Grey("""
            "t": { "type": "noise", "params": { "cells": [4, 2], "interpolation": "step" } }
            """, 32);
        Assert.Equal(H(0, 0, 7), v[0], 6);
        Assert.Equal(H(1, 1, 7), v[17 * 32 + 9], 6);
        Assert.Equal(H(3, 1, 7), v[31 * 32 + 31], 6);
        for (var y = 16; y < 32; y++)
            for (var x = 8; x < 16; x++) Assert.Equal(H(1, 1, 7), v[y * 32 + x], 6);
    }

    [Fact]
    public void SmoothNoiseBlendsLatticeValuesWithASmoothstep()
    {
        // cells [16, 1] at 32 texels: texel 0 is a quarter of the way from lattice 0 to 1, texel 1 three quarters.
        // smoothstep(0.25) = 0.15625 and smoothstep(0.75) = 0.84375. One row of cells, so the vertical blend is between equals.
        var v = Grey("""
            "t": { "type": "noise", "params": { "cells": [16, 1] } }
            """, 32);
        float a = H(0, 0, 7), b = H(1, 0, 7);
        Assert.Equal(a + (b - a) * 0.15625f, v[0], 5);
        Assert.Equal(a + (b - a) * 0.84375f, v[1], 5);
    }

    [Fact]
    public void OctavesDoubleTheCellsHalveTheWeightAndNormalise()
    {
        var v = Grey("""
            "t": { "type": "noise", "params": { "cells": [4, 1], "interpolation": "step", "octaves": 2 } }
            """, 32);
        var expected = (H(0, 0, 7) + H(0, 0, 7 + 7919) / 2) / 1.5f;
        Assert.Equal(expected, v[0], 5);
        Assert.All(v, x => Assert.InRange(x, 0f, 1f));
    }

    [Fact]
    public void TheNodeSeedIsAnOffsetOnTheGraphSeed()
    {
        const string Node = """ "t": { "type": "noise", "params": { "cells": [8, 8], "seed": 3 } } """;
        const string Plain = """ "t": { "type": "noise", "params": { "cells": [8, 8] } } """;
        Assert.Equal(Grey(Plain, 32, seed: 7), Grey(Node, 32, seed: 4));
        Assert.NotEqual(Grey(Plain, 32, seed: 7), Grey(Node, 32, seed: 7));
    }

    // ---- gradient ---------------------------------------------------------------------------------------------

    [Fact]
    public void GradientIsSampledAtTexelCentres()
    {
        // 32 texels, repeat 8: texel centres at (x + 0.5) * 8 / 32 = 0.125, 0.375, 0.625, 0.875, then 0.125 again.
        var linear = Grey(""" "t": { "type": "gradient", "params": { "repeat": 8 } } """, 32);
        Assert.Equal([.125f, .375f, .625f, .875f, .125f, .375f, .625f, .875f], linear[..8]);
        // triangle: 1 - |2f - 1|: 0.25, 0.75, 0.75, 0.25
        var triangle = Grey(""" "t": { "type": "gradient", "params": { "repeat": 8, "shape": "triangle" } } """, 32);
        Assert.Equal([.25f, .75f, .75f, .25f, .25f, .75f, .75f, .25f], triangle[..8]);
        // along v the value depends on the row only
        var v = Grey(""" "t": { "type": "gradient", "params": { "axis": "v", "repeat": 8 } } """, 32);
        Assert.Equal(.375f, v[32 + 5]);
        Assert.Equal(.625f, v[2 * 32]);
        Assert.Equal(.375f, v[5 * 32 + 7]);
    }

    // ---- levels -----------------------------------------------------------------------------------------------

    [Fact]
    public void LevelsMapsClampsInvertsAndAppliesGamma()
    {
        Assert.Equal(0.3f, LevelsNode.Map(.375f, .25f, .75f, .2f, .6f, 1), 6);     // t = 0.25, out 0.2 + 0.4 * 0.25
        Assert.Equal(0.5f, LevelsNode.Map(.375f, .25f, .75f, .6f, .2f, 1), 6);     // inverted: 0.6 - 0.4 * 0.25
        Assert.Equal(0.6f, LevelsNode.Map(.9f, .25f, .75f, .2f, .6f, 1), 6);       // clamped above
        Assert.Equal(0.2f, LevelsNode.Map(.1f, .25f, .75f, .2f, .6f, 1), 6);       // clamped below
        Assert.Equal(0.5f, LevelsNode.Map(.25f, 0, 1, 0, 1, 2), 9);                // 0.25 ^ (1 / 2)
        Assert.Equal(0.09f, LevelsNode.Map(.3f, 0, 1, 0, 1, .5f), 7);              // 0.3 ^ 2
        Assert.Equal(1f, LevelsNode.Map(.5f, .5f, .5f, 0, 1, 1));                  // in_high = in_low: a step
        Assert.Equal(0f, LevelsNode.Map(.4f, .5f, .5f, 0, 1, 1));
    }

    [Fact]
    public void LevelsKeepsColourAndGivesTheDefaultsWhenNoParametersAreGiven()
    {
        var all = new[] { 0, 1, 2 }.Select(ch => Run(""" "c": { "type": "colour", "params": { "colour": "#ff8000" } }, "t": { "type": "levels", "inputs": { "in": "c" }, "params": { "out_low": 1, "out_high": 0 } } """, 32, OutputRole.BaseColour, ch)[0]).ToArray();
        Assert.Equal([0f, 1 - 128 / 255f, 1f], all);
        var same = Grey(""" "t": { "type": "levels", "inputs": { "in": "g0" } } """, 32);
        Assert.Equal(Grey(""" "t": { "type": "gradient" } """, 32), same);
    }

    // ---- blend ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("mix", 0.3f)]
    [InlineData("multiply", 0.24f)]
    [InlineData("add", 1.1f)]
    [InlineData("subtract", 0.5f)]
    [InlineData("min", 0.3f)]
    [InlineData("max", 0.8f)]
    public void BlendModesOnGreyscale(string mode, float expected)
    {
        var v = Grey($$"""{{Const("a", .8)}}, {{Const("b", .3)}}, "t": { "type": "blend", "inputs": { "a": "a", "b": "b" }, "params": { "mode": "{{mode}}" } } """, 32);
        Assert.Equal(expected, v[0], 6);
    }

    [Fact]
    public void BlendOpacityAndMaskMoveFromAToB()
    {
        // mix of a = 0.8 and b = 0.3 at opacity 0.5: 0.8 - 0.5 * 0.5 = 0.55
        var half = Grey($$"""{{Const("a", .8)}}, {{Const("b", .3)}}, "t": { "type": "blend", "inputs": { "a": "a", "b": "b" }, "params": { "opacity": 0.5 } } """, 32);
        Assert.Equal(0.55f, half[0], 6);
        // a mask of 0.125, 0.375, 0.625, 0.875 across 4 texels takes a where 0 and b where 1: 0.8 - 0.5 * mask
        var masked = Grey($$"""{{Const("a", .8)}}, {{Const("b", .3)}}, "m": { "type": "gradient", "params": { "repeat": 8 } }, "t": { "type": "blend", "inputs": { "a": "a", "b": "b", "mask": "m" } } """, 32);
        float[] want = [.7375f, .6125f, .4875f, .3625f];
        for (var i = 0; i < 4; i++) Assert.Equal(want[i], masked[i], 5);
    }

    [Fact]
    public void BlendIsColourIfEitherInputIsAndSpreadsGreyToGrey()
    {
        var nodes = $$"""{{Const("b", .5)}}, "a": { "type": "colour", "params": { "colour": "#ff0000" } }, "t": { "type": "blend", "inputs": { "a": "a", "b": "b" }, "params": { "mode": "multiply" } } """;
        Assert.Equal([.5f, 0f, 0f], new[] { 0, 1, 2 }.Select(ch => Run(nodes, 32, OutputRole.BaseColour, ch)[0]).ToArray());
        // greyscale only: a roughness output (greyscale) accepts it; colour would be a fault there
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": 32, "seed": 1, "nodes": { {{Base}} {{Const("b", .5)}}, "a": { "type": "colour" }, "t": { "type": "blend", "inputs": { "a": "a", "b": "b" } }, "o": { "type": "output", "inputs": { "in": "t" }, "params": { "role": "roughness" } } } }""");
        Assert.Single(read.Faults);
    }

    // ---- height_to_normal -------------------------------------------------------------------------------------

    [Fact]
    public void FlatHeightGivesTheUpNormal()
    {
        var rgba = OutputImage.Rgba8(new GraphEvaluator(Graph(Const("h", .4) + """, "t": { "type": "height_to_normal", "inputs": { "height": "h" }, "params": { "strength": 5 } }, "o": { "type": "output", "inputs": { "in": "t" }, "params": { "role": "normal" } } """, 32)), OutputRole.Normal, 32);
        for (var i = 0; i < rgba.Length; i += 4) Assert.Equal((128, 128, 255, 255), (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]));
    }

    [Fact]
    public void ARampInUTiltsRedDownAndARampDownTiltsGreenUp()
    {
        // A ramp in u of 64 texels: height (x + 0.5) / 64, so a central difference is 2/64 over 2 texels, a slope of 1 per
        // image width at strength 1. n = (-1, 0, 1) / sqrt 2 = (-0.7071, 0, 0.7071): bytes 127.5 -+ 90.15 -> 37, 128, 218.
        const string Normal = """ "t": { "type": "height_to_normal", "inputs": { "height": "g" }, "params": { "strength": 1 } } """;
        var u = OutputImage.Rgba8(new GraphEvaluator(Graph(""" "g": { "type": "gradient" }, """ + Normal + """, "o": { "type": "output", "inputs": { "in": "t" }, "params": { "role": "normal" } } """, 64)), OutputRole.Normal, 64);
        var i = (5 * 64 + 10) * 4;
        Assert.Equal((37, 128, 218), (u[i], u[i + 1], u[i + 2]));
        // the seam: texel 0 sees texel 63 across the wrap (height 0.992), so there the ramp falls and red rises
        Assert.True(u[(5 * 64) * 4] > 128, "wrap-around: the left neighbour of column 0 is column 63");
        var v = OutputImage.Rgba8(new GraphEvaluator(Graph(""" "g": { "type": "gradient", "params": { "axis": "v" } }, """ + Normal + """, "o": { "type": "output", "inputs": { "in": "t" }, "params": { "role": "normal" } } """, 64)), OutputRole.Normal, 64);
        i = (10 * 64 + 5) * 4;
        Assert.Equal((128, 218, 218), (v[i], v[i + 1], v[i + 2]));
    }

    [Fact]
    public void EveryEncodedNormalDecodesToUnitLength()
    {
        var g = Graph(""" "h": { "type": "noise", "params": { "cells": [6, 6], "octaves": 3 } }, "t": { "type": "height_to_normal", "inputs": { "height": "h" }, "params": { "strength": 0.3 } }, "o": { "type": "output", "inputs": { "in": "t" }, "params": { "role": "normal" } } """, 64);
        var rgba = OutputImage.Rgba8(new GraphEvaluator(g), OutputRole.Normal, 64);
        for (var i = 0; i < rgba.Length; i += 4)
        {
            double x = (rgba[i] - 127.5) / 127.5, y = (rgba[i + 1] - 127.5) / 127.5, z = (rgba[i + 2] - 127.5) / 127.5;
            Assert.InRange(Math.Sqrt(x * x + y * y + z * z), 0.99, 1.01);
        }
    }

    // ---- tiling -----------------------------------------------------------------------------------------------

    private static void AssertTiles(float[] img, int size)
    {
        float interiorX = 0, interiorY = 0, wrapX = 0, wrapY = 0;
        for (var y = 0; y < size; y++)
        {
            wrapX = MathF.Max(wrapX, MathF.Abs(img[y * size] - img[y * size + size - 1]));
            for (var x = 0; x < size - 1; x++) interiorX = MathF.Max(interiorX, MathF.Abs(img[y * size + x + 1] - img[y * size + x]));
        }
        for (var x = 0; x < size; x++)
        {
            wrapY = MathF.Max(wrapY, MathF.Abs(img[x] - img[(size - 1) * size + x]));
            for (var y = 0; y < size - 1; y++) interiorY = MathF.Max(interiorY, MathF.Abs(img[(y + 1) * size + x] - img[y * size + x]));
        }
        Assert.True(wrapX <= interiorX + 1e-6f, $"left and right columns: wrap step {wrapX} exceeds the largest interior step {interiorX}");
        Assert.True(wrapY <= interiorY + 1e-6f, $"top and bottom rows: wrap step {wrapY} exceeds the largest interior step {interiorY}");
    }

    public static TheoryData<string, string> Tiled() => new()
    {
        { "smooth noise", """ "t": { "type": "noise", "params": { "cells": [5, 3] } } """ },
        { "octave noise", """ "t": { "type": "noise", "params": { "cells": [3, 7], "octaves": 4, "seed": 9 } } """ },
        { "step noise", """ "t": { "type": "noise", "params": { "cells": [16, 16], "interpolation": "step" } } """ },
        { "triangle gradient", """ "t": { "type": "gradient", "params": { "repeat": 4, "shape": "triangle" } } """ },
        { "linear gradient (repeats)", """ "t": { "type": "gradient", "params": { "repeat": 4 } } """ },
        { "levels of noise", """ "n": { "type": "noise", "params": { "cells": [4, 4] } }, "t": { "type": "levels", "inputs": { "in": "n" }, "params": { "in_low": 0.2, "in_high": 0.9, "gamma": 1.6 } } """ },
        { "blend of noises", """ "n": { "type": "noise", "params": { "cells": [4, 4] } }, "m": { "type": "noise", "params": { "cells": [3, 5], "seed": 4 } }, "t": { "type": "blend", "inputs": { "a": "n", "b": "m" }, "params": { "mode": "add" } } """ },
    };

    [Theory]
    [MemberData(nameof(Tiled))]
    public void TheLeftAndRightColumnsAndTheTopAndBottomRowsContinueEachOther(string name, string nodes)
    {
        Assert.NotNull(name);
        AssertTiles(Grey(nodes, 64), 64);
    }

    [Fact]
    public void HeightToNormalTilesAcrossTheSeam()
    {
        foreach (var channel in new[] { 0, 1 })
        {
            var img = Run(""" "h": { "type": "noise", "params": { "cells": [4, 4] } }, "t": { "type": "height_to_normal", "inputs": { "height": "h" }, "params": { "strength": 0.5 } } """, 64, OutputRole.Normal, channel);
            AssertTiles(img, 64);
        }
    }

    // ---- size invariance --------------------------------------------------------------------------------------

    /// <summary>The 256 image's texel x sits at u = (x + 0.5) / 256, the 1024 image's texel 4x + 1 and 4x + 2 around it; they agree to within one step of the 256 image.</summary>
    private static void AssertSizeInvariant(string nodes, OutputRole role = OutputRole.Roughness, int channel = 0, float slack = 1f)
    {
        var small = Run(nodes, 256, role, channel);
        var large = Run(nodes, 256, role, channel, evalSize: 1024);
        float step = 0;
        for (var y = 0; y < 256; y++)
            for (var x = 0; x < 255; x++) step = MathF.Max(step, MathF.Abs(small[y * 256 + x + 1] - small[y * 256 + x]));
        for (var y = 0; y < 256; y++)
            for (var x = 0; x < 256; x++)
                Assert.True(MathF.Abs(small[y * 256 + x] - large[(4 * y + 2) * 1024 + 4 * x + 2]) <= step * slack + 1e-5f,
                    $"({x}, {y}): {small[y * 256 + x]} at 256, {large[(4 * y + 2) * 1024 + 4 * x + 2]} at 1024, one step {step}");
    }

    [Fact]
    public void ANodeGivesTheSameImageAtEverySize()
    {
        AssertSizeInvariant(""" "t": { "type": "noise", "params": { "cells": [8, 8], "octaves": 2 } } """);
        AssertSizeInvariant(""" "t": { "type": "noise", "params": { "cells": [8, 4], "interpolation": "step" } } """, slack: 0.0001f);   // equal, not just near
        AssertSizeInvariant(""" "t": { "type": "gradient", "params": { "repeat": 8, "shape": "triangle" } } """);
        AssertSizeInvariant(""" "n": { "type": "noise", "params": { "cells": [8, 8] } }, "t": { "type": "levels", "inputs": { "in": "n" }, "params": { "in_low": 0.3, "gamma": 2 } } """);
        AssertSizeInvariant(""" "n": { "type": "noise", "params": { "cells": [8, 8] } }, "m": { "type": "gradient", "params": { "axis": "v" } }, "t": { "type": "blend", "inputs": { "a": "n", "b": "m", "mask": "m" }, "params": { "mode": "multiply" } } """);
        // a normal from a smooth height: strength is per image width, so the tilt agrees; the central difference is read over one texel at each size
        AssertSizeInvariant(""" "h": { "type": "noise", "params": { "cells": [4, 4] } }, "t": { "type": "height_to_normal", "inputs": { "height": "h" }, "params": { "strength": 0.05 } } """, OutputRole.Normal, 0);
        AssertSizeInvariant(""" "h": { "type": "noise", "params": { "cells": [4, 4] } }, "t": { "type": "height_to_normal", "inputs": { "height": "h" }, "params": { "strength": 0.05 } } """, OutputRole.Normal, 1);
    }

    // ---- each node failing on a broken copy ------------------------------------------------------------------

    // A check is a function of one implementation: it passes the real one and throws for a broken one. The table says
    // which check catches each break.

    private delegate float OctaveFn(int x, int y, int size, int cellsX, int cellsY, bool smooth, uint seed);

    /// <summary>A copy of the noise octave, with its wrap-around or its smoothstep optionally broken.</summary>
    private static float OctaveCopy(int x, int y, int size, int cx, int cy, bool smooth, uint seed, bool wrap = true, bool smoothstep = true)
    {
        float px = (x + 0.5f) * cx / size, py = (y + 0.5f) * cy / size;
        int ix = (int)MathF.Floor(px), iy = (int)MathF.Floor(py);
        float L(int i, int j) => (NoiseNode.Hash(wrap ? i % cx : i, wrap ? j % cy : j, seed) & 0xFFFF) / 65535f;
        float fx = px - ix, fy = py - iy;
        if (smoothstep) { fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); }
        var top = L(ix, iy) + (L(ix + 1, iy) - L(ix, iy)) * fx;
        var bottom = L(ix, iy + 1) + (L(ix + 1, iy + 1) - L(ix, iy + 1)) * fx;
        return top + (bottom - top) * fy;
    }

    private static void CheckNoise(OctaveFn octave)
    {
        const int Size = 64;
        var img = new float[Size * Size];
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++) img[y * Size + x] = octave(x, y, Size, 5, 3, true, 7);
        AssertTiles(img, Size);                                                       // catches the missing wrap
        float a = H(0, 0, 7), b = H(1, 0, 7);                                        // cells [16, 1] at 32: texel 0 is a quarter of the way
        Assert.Equal(a + (b - a) * 0.15625f, octave(0, 0, 32, 16, 1, true, 7), 5);     // catches linear for smoothstep
    }

    private static void CheckGradient(Func<int, int, int, bool, float> value)
    {
        Assert.Equal(.125f, value(0, 8, 2, false));                                   // catches sampling at the texel corner
        Assert.Equal(.875f, value(3, 8, 2, false));
        Assert.Equal(.25f, value(0, 8, 2, true));                                     // catches the triangle turned upside down
        Assert.Equal(.75f, value(1, 8, 2, true));
    }

    private static void CheckLevels(Func<float, float, float, float, float, float, float> map)
    {
        Assert.Equal(0.3f, map(.375f, .25f, .75f, .2f, .6f, 1), 6);
        Assert.Equal(0.5f, map(.375f, .25f, .75f, .6f, .2f, 1), 6);                   // catches an inversion that does not invert
        Assert.Equal(0.6f, map(.9f, .25f, .75f, .2f, .6f, 1), 6);                     // catches the missing clamp
        Assert.Equal(0.5f, map(.25f, 0, 1, 0, 1, 2), 6);                              // catches gamma applied the other way
    }

    private static void CheckBlend(Func<string, float, float, float, float> apply)
    {
        Assert.Equal(.3f, apply("mix", .8f, .3f, 1), 6);
        Assert.Equal(.675f, apply("mix", .8f, .3f, .25f), 6);                          // catches opacity or mask inverted
        Assert.Equal(.5f, apply("subtract", .8f, .3f, 1), 6);                         // catches a - b flipped
        Assert.Equal(.3f, apply("min", .8f, .3f, 1), 6);                              // catches min and max swapped
        Assert.Equal(.8f, apply("max", .8f, .3f, 1), 6);
        Assert.Equal(.24f, apply("multiply", .8f, .3f, 1), 6);
        Assert.Equal(1.1f, apply("add", .8f, .3f, 1), 6);
    }

    private static void CheckNormal(Func<float, float, float, float, int, float, (float X, float Y, float Z)> normal)
    {
        var flat = normal(.4f, .4f, .4f, .4f, 64, 1);
        Assert.Equal((0f, 0f, 1f), flat);
        var right = normal(.4f, .4f + 2 / 64f, .4f, .4f, 64, 1);                      // height rising to the right
        Assert.Equal(-0.70710678f, right.X, 5);                                       // catches x flipped
        var down = normal(.4f, .4f, .4f, .4f + 2 / 64f, 64, 1);                       // height rising down the image
        Assert.Equal(0.70710678f, down.Y, 5);                                         // catches y flipped (glTF green up, rows down)
        Assert.InRange(Math.Sqrt(right.X * right.X + right.Y * right.Y + right.Z * right.Z), 0.99999, 1.00001);   // catches no normalisation
        var steep = normal(0, 1, 0, 0, 64, 1);
        Assert.InRange(Math.Sqrt(steep.X * steep.X + steep.Y * steep.Y + steep.Z * steep.Z), 0.99999, 1.00001);
    }

    /// <summary>The seam texel's normal from a ramp in u, its neighbours looked up by <paramref name="neighbour"/>: wrapping, or (broken) clamped.</summary>
    private static void CheckSeam(Func<int, int, int> neighbour)
    {
        const int Size = 64;
        float Height(int x) => (x + 0.5f) / Size;
        var n = HeightToNormalNode.Normal(Height(neighbour(-1, Size)), Height(neighbour(1, Size)), Height(0), Height(0), Size, 1);
        Assert.True(n.X > 0, "at the seam of a ramp the wrapped neighbour is the far end, so the slope reverses and x tilts positive");
    }

    [Fact]
    public void EveryNodeIsCaughtOnABrokenCopy()
    {
        // the real nodes pass
        CheckNoise(NoiseNode.Octave);
        CheckGradient(GradientNode.Value);
        CheckLevels(LevelsNode.Map);
        CheckBlend(BlendNode.Apply);
        CheckNormal(HeightToNormalNode.Normal);
        CheckSeam((dx, size) => Band.Wrap(dx, size));

        // the table: what was broken, and that the check that catches it fails
        var breaks = new (string Name, Action Check)[]
        {
            ("noise: no wrap-around of the lattice", () => CheckNoise((x, y, s, cx, cy, sm, seed) => OctaveCopy(x, y, s, cx, cy, sm, seed, wrap: false))),
            ("noise: linear instead of smoothstep", () => CheckNoise((x, y, s, cx, cy, sm, seed) => OctaveCopy(x, y, s, cx, cy, sm, seed, smoothstep: false))),
            ("gradient: sampled at the texel corner", () => CheckGradient((t, size, r, tri) => { var f = t * (float)r / size; f -= MathF.Floor(f); return tri ? 1 - MathF.Abs(2 * f - 1) : f; })),
            ("gradient: triangle upside down", () => CheckGradient((t, size, r, tri) => { var v = GradientNode.Value(t, size, r, tri); return tri ? 1 - v : v; })),
            ("levels: out_low and out_high swapped", () => CheckLevels((v, il, ih, ol, oh, g) => LevelsNode.Map(v, il, ih, oh, ol, g))),
            ("levels: not clamped", () => CheckLevels((v, il, ih, ol, oh, g) => ol + (oh - ol) * ((v - il) / (ih - il)))),
            ("levels: gamma the other way", () => CheckLevels((v, il, ih, ol, oh, g) => LevelsNode.Map(v, il, ih, ol, oh, 1 / g))),
            ("blend: mask taken the wrong way round", () => CheckBlend((m, a, b, k) => BlendNode.Apply(m, a, b, 1 - k))),
            ("blend: subtract flipped", () => CheckBlend((m, a, b, k) => m == "subtract" ? BlendNode.Apply(m, b, a, k) : BlendNode.Apply(m, a, b, k))),
            ("blend: min and max swapped", () => CheckBlend((m, a, b, k) => BlendNode.Apply(m == "min" ? "max" : m == "max" ? "min" : m, a, b, k))),
            ("height_to_normal: x sign flipped", () => CheckNormal((l, r, u, d, s, st) => { var n = HeightToNormalNode.Normal(l, r, u, d, s, st); return (-n.X, n.Y, n.Z); })),
            ("height_to_normal: y sign flipped", () => CheckNormal((l, r, u, d, s, st) => { var n = HeightToNormalNode.Normal(l, r, u, d, s, st); return (n.X, -n.Y, n.Z); })),
            ("height_to_normal: not normalised", () => CheckNormal((l, r, u, d, s, st) => (-(r - l) * 0.5f * s * st, (d - u) * 0.5f * s * st, 1f))),
            ("height_to_normal: no wrap-around at the seam", () => CheckSeam((dx, size) => Math.Clamp(dx, 0, size - 1))),
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
