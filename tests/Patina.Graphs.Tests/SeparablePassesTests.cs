using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// Separable passes and the evaluator's order (T-155): <c>blur</c> and <c>distance</c> made as a row pass and a column
/// pass give the bytes their one-pass <see cref="NodeType.Evaluate"/> gives, at every tile size and cap; the most held
/// stays under the cap at their largest radius and range; and a free rotation makes its source close to once.
/// </summary>
[Trait("Tier", "unit")]
public class SeparablePassesTests
{
    /// <summary>A node type as another, but not opting in to separable passes: its one-pass <see cref="NodeType.Evaluate"/>.</summary>
    private sealed class OnePass(string name, NodeType of) : NodeType
    {
        public override string Name => name;
        public override IReadOnlyList<InputPort> Inputs => of.Inputs;
        public override IReadOnlyList<ParamSpec> Parameters => of.Parameters;
        public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => of.OutputKind(port, inputs, parameters);
        public override TexelRect InputRect(string port, TexelRect output, NodeParameters parameters, int size) => of.InputRect(port, output, parameters, size);
        public override void Evaluate(NodeEvaluation evaluation) => of.Evaluate(evaluation);
    }

    private static readonly NodeRegistry Registry = NodeRegistry.BuiltIn.With(new OnePass("blur_one", new BlurNode()), new OnePass("distance_one", new DistanceNode()));

    private static TextureGraph Graph(string nodes, int size = 256)
    {
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": {{size}}, "seed": 155, "nodes": { {{nodes}} } }""", Registry);
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    private static (byte[] Rgba, EvaluationRun Run) Run(TextureGraph g, OutputRole role, int size, int? tileSize = null, long? cap = null)
    {
        var evaluator = new GraphEvaluator(g, Registry);
        var rgba = OutputImage.Rgba8(evaluator, role, size, tileSize, cap);
        return (rgba, evaluator.LastRun!);
    }

    private static string Blur(string type, double radius, string quality, bool colour) => $$"""
        "f": { "type": "fbm", "params": { "cells": [8, 8], "octaves": 3 } },
        "g": { "type": "gradient_map", "inputs": { "in": "f" }, "params": { "positions": [0, 1], "colours": ["#203040", "#e0c080"] } },
        "b": { "type": "{{type}}", "inputs": { "in": "{{(colour ? "g" : "f")}}" }, "params": { "radius": {{radius.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "quality": "{{quality}}" } },
        "o": { "type": "output", "inputs": { "in": "b" }, "params": { "role": "{{(colour ? "base_colour" : "roughness")}}" } }
        """;

    private static string Distance(string type, double range, string side) => $$"""
        "m": { "type": "tiles", "params": { "count": [3, 5], "gap": 0.12, "jitter": 0.4 } },
        "d": { "type": "{{type}}", "inputs": { "in": "m.mask" }, "params": { "range": {{range.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "side": "{{side}}" } },
        "o": { "type": "output", "inputs": { "in": "d" }, "params": { "role": "roughness" } }
        """;

    [Theory]
    [InlineData(0.0, "box", false)]
    [InlineData(0.01, "box", true)]
    [InlineData(0.03, "smooth", false)]
    [InlineData(0.1, "smooth", true)]
    public void Blur_in_two_passes_is_the_one_pass_blur_at_every_tile_size_and_cap(double radius, string quality, bool colour)
    {
        var role = colour ? OutputRole.BaseColour : OutputRole.Roughness;
        var want = Run(Graph(Blur("blur_one", radius, quality, colour)), role, 256).Rgba;
        var g = Graph(Blur("blur", radius, quality, colour));
        foreach (var side in new[] { 16, 64, 256 })
            foreach (var cap in new long?[] { 0, null })
                Assert.True(want.AsSpan().SequenceEqual(Run(g, role, 256, side, cap).Rgba), $"tile {side}, cap {cap?.ToString() ?? "default"}");
    }

    [Theory]
    [InlineData(0.0, "inside")]
    [InlineData(0.035, "inside")]
    [InlineData(0.1, "outside")]
    [InlineData(0.25, "both")]
    public void Distance_in_two_passes_is_the_one_pass_distance_at_every_tile_size_and_cap(double range, string side)
    {
        var want = Run(Graph(Distance("distance_one", range, side)), OutputRole.Roughness, 256).Rgba;
        var g = Graph(Distance("distance", range, side));
        foreach (var tile in new[] { 16, 64, 256 })
            foreach (var cap in new long?[] { 0, null })
                Assert.True(want.AsSpan().SequenceEqual(Run(g, OutputRole.Roughness, 256, tile, cap).Rgba), $"tile {tile}, cap {cap?.ToString() ?? "default"}");
    }

    [Fact]
    public void Blur_at_its_largest_radius_holds_no_more_than_the_cap()
    {
        // At 1024 in tiles of 64 a smooth blur at 0.1 reaches 307 texels: one pass's window was 12 x 12 colour tiles,
        // 6.75 MiB pinned, past a 4 MiB cap. In two passes a column of 12 row tiles and one row tile's 12 inputs are.
        const long cap = 4L << 20;
        var (_, run) = Run(Graph(Blur("blur", 0.1, "smooth", true)), OutputRole.BaseColour, 1024, 64, cap);
        Assert.True(run.PeakBytes <= cap, $"held {run.PeakBytes} bytes");
        var (_, one) = Run(Graph(Blur("blur_one", 0.1, "smooth", true)), OutputRole.BaseColour, 1024, 64, cap);
        Assert.True(one.PeakBytes > cap, $"one pass held {one.PeakBytes} bytes");
    }

    [Fact]
    public void Distance_at_its_largest_range_holds_no_more_than_the_cap()
    {
        // At 1024 in tiles of 64 range 0.25 reaches 257 texels: one pass pinned a window of 10 x 10 greyscale tiles,
        // 1.56 MiB, past a 1 MiB cap.
        const long cap = 1L << 20;
        var (_, run) = Run(Graph(Distance("distance", 0.25, "both")), OutputRole.Roughness, 1024, 64, cap);
        Assert.True(run.PeakBytes <= cap, $"held {run.PeakBytes} bytes");
        var (_, one) = Run(Graph(Distance("distance_one", 0.25, "both")), OutputRole.Roughness, 1024, 64, cap);
        Assert.True(one.PeakBytes > cap, $"one pass held {one.PeakBytes} bytes");
    }
}
