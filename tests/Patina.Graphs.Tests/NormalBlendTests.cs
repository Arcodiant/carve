using Patina.Graphs.Checks;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>T-224's normal_blend: reoriented normal mapping worked by hand, its identities through a graph, unit length everywhere, and a table of broken copies.</summary>
[Trait("Tier", "unit")]
public class NormalBlendTests
{
    private delegate (float X, float Y, float Z) BlendFn((float X, float Y, float Z) b, (float X, float Y, float Z) d, float strength);

    private const int Size = 64;

    // A flat height gives (0, 0, 1); a noise height a bump; an fbm another.
    private const string Nodes = """
        "g": { "type": "gradient" },
        "flat_h": { "type": "levels", "inputs": { "in": "g" }, "params": { "out_low": 0.5, "out_high": 0.5 } },
        "flat": { "type": "height_to_normal", "inputs": { "height": "flat_h" } },
        "bump_h": { "type": "noise", "params": { "cells": [4, 4], "octaves": 3 } },
        "bump": { "type": "height_to_normal", "inputs": { "height": "bump_h" }, "params": { "strength": 0.2 } },
        "wave_h": { "type": "fbm", "params": { "cells": [3, 3] } },
        "wave": { "type": "height_to_normal", "inputs": { "height": "wave_h" }, "params": { "strength": 0.15 } }
        """;

    /// <summary>The normal output's vectors, row-major, for a graph whose node "t" goes to the normal role.</summary>
    private static (float X, float Y, float Z)[] Normals(string t, int tile = 0)
    {
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": {{Size}}, "seed": 3, "nodes": { {{Nodes}}, "t": {{t}}, "o": { "type": "output", "inputs": { "in": "t" }, "params": { "role": "normal" } } } }""");
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        var result = new (float, float, float)[Size * Size];
        new GraphEvaluator(read.Graph!).Evaluate(OutputRole.Normal, Size, tileOut =>
        {
            for (var y = tileOut.Rect.Y; y < tileOut.Rect.Bottom; y++)
                for (var x = tileOut.Rect.X; x < tileOut.Rect.Right; x++)
                    result[y * Size + x] = tileOut.Colour(x, y);
        }, tile == 0 ? null : tile);
        return result;
    }

    private static string Blend(string b, string d, double strength = 1) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $$"""{ "type": "normal_blend", "inputs": { "base": "{{b}}", "detail": "{{d}}" }, "params": { "strength": {{strength}} } }""");

    // The detail and the base on their own, as "t".
    private const string Bump = """{ "type": "height_to_normal", "inputs": { "height": "bump_h" }, "params": { "strength": 0.2 } }""";
    private const string Wave = """{ "type": "height_to_normal", "inputs": { "height": "wave_h" }, "params": { "strength": 0.15 } }""";

    private static void Near((float X, float Y, float Z) expected, (float X, float Y, float Z) actual, double tolerance = 1e-5)
    {
        Assert.Equal(expected.X, actual.X, tolerance);
        Assert.Equal(expected.Y, actual.Y, tolerance);
        Assert.Equal(expected.Z, actual.Z, tolerance);
    }

    private static void AllNear((float X, float Y, float Z)[] expected, (float X, float Y, float Z)[] actual)
    {
        for (var i = 0; i < expected.Length; i++) Near(expected[i], actual[i]);
    }

    // ---- through a graph --------------------------------------------------------------------------------------

    [Fact]
    public void AFlatBaseReturnsTheDetail() => AllNear(Normals(Bump), Normals(Blend("flat", "bump")));

    [Fact]
    public void AFlatDetailReturnsTheBase() => AllNear(Normals(Wave), Normals(Blend("wave", "flat")));

    [Fact]
    public void StrengthZeroReturnsTheBase() => AllNear(Normals(Wave), Normals(Blend("wave", "bump", 0)));

    [Fact]
    public void TheOutputIsUnitLengthAndPassesTheNormalRule()
    {
        foreach (var strength in new[] { 1.0, 0.6, 0.25 })
        {
            foreach (var (x, y, z) in Normals(Blend("wave", "bump", strength)))
                Assert.InRange(Math.Sqrt(x * x + y * y + z * z), 0.9999, 1.0001);
        }
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": {{Size}}, "seed": 3, "nodes": { {{Nodes}}, "t": {{Blend("wave", "bump", 0.6)}}, "o": { "type": "output", "inputs": { "in": "t" }, "params": { "role": "normal" } } } }""");
        Assert.Empty(new NormalNotUnitRule().Run(CheckSet.OfGraph(new GraphEvaluator(read.Graph!), Size)));
    }

    [Fact]
    public void TheSameAtEveryTileSize()
    {
        var whole = Normals(Blend("wave", "bump", 0.7));
        foreach (var tile in new[] { 16, 32 }) Assert.Equal(whole, Normals(Blend("wave", "bump", 0.7), tile));
    }

    // ---- by hand ----------------------------------------------------------------------------------------------

    [Fact]
    public void ATiltedDetailOnATiltedBaseIsWorkedByHand() => CheckByHand(NormalBlendNode.Blend);

    /// <summary>
    /// Base tilted along x, (0.6, 0, 0.8). RNM: t = base + (0, 0, 1), u = detail with x and y negated, r = t dot(t, u) / t.z - u.
    /// A detail tilted along y, (0, 0.6, 0.8): t = (0.6, 0, 1.8), u = (0, -0.6, 0.8), dot 1.44, r = 0.8 t - u = (0.48, 0.6, 0.64).
    /// The same tilt again, (0.6, 0, 0.8): u = (-0.6, 0, 0.8), dot 1.08, r = 0.6 t - u = (0.96, 0, 0.28), the angle doubled.
    /// At strength 0.5 the detail (0.6, 0, 0.8) is (0.3, 0, 0.9) / 0.948683; on a flat base that is the output.
    /// A base short of unit length, 0.9 (0.6, 0, 0.8), under a flat detail: k = 1, r = t - u = 0.9 (0.6, 0, 0.8), renormalised.
    /// </summary>
    private static void CheckByHand(BlendFn blend)
    {
        Near((0.48f, 0.6f, 0.64f), blend((0.6f, 0, 0.8f), (0, 0.6f, 0.8f), 1));
        Near((0.96f, 0, 0.28f), blend((0.6f, 0, 0.8f), (0.6f, 0, 0.8f), 1));
        Near((0.3162278f, 0, 0.9486833f), blend((0, 0, 1), (0.6f, 0, 0.8f), 0.5f));
        Near((0.6f, 0, 0.8f), blend((0.6f, 0, 0.8f), (0, 0.6f, 0.8f), 0));
        Near((0.6f, 0, 0.8f), blend((0.54f, 0, 0.72f), (0, 0, 1), 1));
    }

    /// <summary>RNM written out with no renormalisation, for the broken copies.</summary>
    private static (float, float, float) Raw((float X, float Y, float Z) b, (float X, float Y, float Z) d, float s, float sign = -1)
    {
        float ux = sign * d.X * s, uy = sign * d.Y * s, uz = 1 + (d.Z - 1) * s, tz = b.Z + 1;
        var k = (b.X * ux + b.Y * uy + tz * uz) / tz;
        return (b.X * k - ux, b.Y * k - uy, tz * k - uz);
    }

    [Fact]
    public void EveryBrokenCopyIsCaught()
    {
        static (float, float, float) Unit(float x, float y, float z) { var l = MathF.Sqrt(x * x + y * y + z * z); return (x / l, y / l, z / l); }
        var breaks = new (string Name, BlendFn Blend)[]
        {
            ("inputs swapped", (b, d, s) => NormalBlendNode.Blend(d, b, s)),
            ("UDN (xy added) in place of RNM", (b, d, s) => { var e = NormalBlendNode.Blend((0, 0, 1), d, s); return Unit(b.X + e.X, b.Y + e.Y, b.Z); }),
            ("detail's x and y not negated", (b, d, s) => { var (x, y, z) = Raw(b, d, s, sign: 1); return Unit(x, y, z); }),
            ("strength ignored", (b, d, s) => NormalBlendNode.Blend(b, d, 1)),
            ("not renormalised", (b, d, s) => Raw(b, d, s)),
        };
        foreach (var (name, blend) in breaks)
            Assert.True(Throws(() => CheckByHand(blend)), $"the broken copy '{name}' was not caught");
    }

    private static bool Throws(Action check)
    {
        try { check(); return false; }
        catch (Xunit.Sdk.XunitException) { return true; }
    }
}
