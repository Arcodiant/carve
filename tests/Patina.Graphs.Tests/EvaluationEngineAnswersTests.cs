using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;
using Patina.Graphs.Nodes;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// RV-76's findings on the evaluation engine (T-173): what the cache counts is what it keeps reachable, the dropped
/// arrays and a node's scratch with its tiles; a read of one port of a node of several pins that port alone; a cap of
/// <see cref="long.MaxValue"/> evaluates; blur's separable order makes close to the fewest tiles; and a read of several
/// rectangles finds a row in any that holds it.
/// </summary>
[Trait("Tier", "unit")]
public class EvaluationEngineAnswersTests
{
    /// <summary>A pointwise grey node that works in scratch of a length of its own at every tile, then writes 0.5.</summary>
    private sealed class Scratchy : NodeType
    {
        public static long Floats(TexelRect rect) => 1000 + rect.X + 7L * rect.Y;
        public override string Name => "scratchy";
        public override IReadOnlyList<InputPort> Inputs => [];
        public override IReadOnlyList<ParamSpec> Parameters => [];
        public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => PortKind.Grey;
        public override void Evaluate(NodeEvaluation e)
        {
            var scratch = e.Scratch((int)Floats(e.Rect));
            Assert.All(scratch, v => Assert.Equal(0f, v));
            scratch.AsSpan().Fill(1f);
            e.Output().Data.AsSpan().Fill(0.5f);
        }
    }

    private static readonly NodeRegistry Registry = NodeRegistry.BuiltIn.With(new Scratchy());

    private static TextureGraph Graph(string nodes, int size = 256)
    {
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": {{size}}, "seed": 173, "nodes": { {{nodes}} } }""", Registry);
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

    private const string Blurred = """
        "f": { "type": "fbm", "params": { "cells": [8, 8], "octaves": 3 } },
        "g": { "type": "gradient_map", "inputs": { "in": "f" }, "params": { "positions": [0, 1], "colours": ["#203040", "#e0c080"] } },
        "b": { "type": "blur", "inputs": { "in": "g" }, "params": { "radius": 0.05, "quality": "smooth" } },
        "o": { "type": "output", "inputs": { "in": "b" }, "params": { "role": "base_colour" } }
        """;

    [Fact]
    public void Dropped_arrays_and_scratch_are_counted_in_what_is_held()
    {
        // With no cache one grey tile is held at a time, but a node's scratch and the arrays waiting to be taken again
        // are reachable too: LastHeldBytes counts them. Each tile's scratch here is a length of its own, so it is kept
        // for the next tile, then waits with the dropped arrays, at most 4 MiB of them.
        var evaluator = new GraphEvaluator(Graph("""
            "s": { "type": "scratchy" },
            "o": { "type": "output", "inputs": { "in": "s" }, "params": { "role": "occlusion" } }
            """), Registry);
        evaluator.Evaluate(OutputRole.Occlusion, 1024, _ => { }, 64, 0);
        const long tile = 64 * 64 * sizeof(float);
        var largest = Scratchy.Floats(new TexelRect(1024 - 64, 1024 - 64, 64, 64)) * sizeof(float);
        Assert.Equal(tile, evaluator.LastRun!.PeakBytes);
        Assert.InRange(evaluator.LastHeldBytes, tile + largest, tile + 2 * largest + (4L << 20));
    }

    [Fact]
    public void A_strong_minify_of_one_port_of_tiles_pins_that_port_alone()
    {
        // At 512 in tiles of 32, transform at scale 0.25 reads its whole input for every tile. tiles has four ports,
        // six floats a texel (6 MiB at 512); only mask is read, 1 MiB.
        const int size = 512;
        var g = Graph("""
            "t": { "type": "tiles", "params": { "count": [4, 4], "gap": 0.06, "bevel": 0 } },
            "m": { "type": "transform", "inputs": { "in": "t.mask" }, "params": { "tile": [16, 16], "scale": 0.25 } },
            "o": { "type": "output", "inputs": { "in": "m" }, "params": { "role": "height" } }
            """, size);
        var want = Run(g, OutputRole.Height, size).Rgba;
        var (rgba, run) = Run(g, OutputRole.Height, size, 32, 0);
        Assert.True(want.AsSpan().SequenceEqual(rgba));
        const long mask = (long)size * size * sizeof(float);
        Assert.True(run.PeakBytes <= mask + (2L << 20) / 8, $"held {run.PeakBytes} bytes against the mask's {mask}");
    }

    [Fact]
    public async Task A_cap_of_long_MaxValue_evaluates()
    {
        // BandTiles cast to int after its clamp, so this cap made the band -2 and the call looped for ever.
        var g = Graph(Blurred);
        var want = Run(g, OutputRole.BaseColour, 256).Rgba;
        var got = await Task.Run(() => Run(g, OutputRole.BaseColour, 256, 128, long.MaxValue).Rgba, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        Assert.True(want.AsSpan().SequenceEqual(got));
    }

    /// <summary>The most times over the fewest a separable blur under a tight cap may make its tiles.</summary>
    public const double MostBlurRemakes = 2.1;

    [Fact]
    public void A_blur_under_a_tight_cap_makes_close_to_the_fewest_tiles()
    {
        // At 1024 in tiles of 64 (16 x 16) under 4 MiB, a smooth blur at 0.05 reaches 154 texels. The fewest tiles is
        // each made once: f, g, the row pass and the blur, 4 x 256; T-173 measured 1,840, the inputs remade under the
        // edges of the narrowed bands. Without the row warm-up it made 2,716, without the cold list 48,620 and without
        // the band's narrowing 11,407: the row tiles flush each other (T-155: 21 for every output tile at 8192).
        var (_, run) = Run(Graph(Blurred), OutputRole.BaseColour, 1024, 64, 4L << 20);
        const int fewest = 4 * 16 * 16;
        TestContext.Current.SendDiagnosticMessage($"blur at 1024 in tiles of 64 under 4 MiB: {run.TilesEvaluated} tiles against {fewest}, held {run.PeakBytes}");
        Assert.True(run.TilesEvaluated <= fewest * MostBlurRemakes, $"made {run.TilesEvaluated} tiles against the fewest {fewest}");
        Assert.True(run.PeakBytes <= 4L << 20, $"held {run.PeakBytes} bytes");
    }

    [Fact]
    public void A_separable_blur_allocates_little_past_its_cache()
    {
        // Blur's passes worked in new arrays, large objects a tile: the evaluation allocated 397 MiB at 1024 in tiles of
        // 64 under 4 MiB. In the evaluator's scratch they are taken again: T-173 measured 18 MiB.
        var g = Graph(Blurred);
        Run(g, OutputRole.BaseColour, 256, 64, 4L << 20);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Run(g, OutputRole.BaseColour, 1024, 64, 4L << 20);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        TestContext.Current.SendDiagnosticMessage($"blur at 1024 in tiles of 64 under 4 MiB allocated {allocated / 1048576.0:0.0} MiB");
        Assert.True(allocated <= 64L << 20, $"allocated {allocated} bytes");
    }

    [Fact]
    public void A_row_past_the_first_rectangle_is_read_from_a_later_one_that_holds_it()
    {
        var tile = new Tile(PortKind.Grey, new TexelRect(0, 0, 32, 32));
        for (var i = 0; i < tile.Data.Length; i++) tile.Data[i] = i;
        var read = new TileRead(PortKind.Grey, 64, 32, [(new TexelRect(0, 0, 8, 1), new[] { tile }), (new TexelRect(0, 0, 32, 1), new[] { tile })]);
        Assert.Equal([4f, 5, 6, 7, 8, 9, 10, 11], read.Row(0, 4, 8).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => read.Row(0, 30, 4));
    }
}
