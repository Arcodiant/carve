using Patina.Graphs.Evaluation;
using Patina.Graphs.Nodes;
using Xunit;

namespace Patina.Graphs.Tests;

/// <summary>
/// Graphs used as nodes (T-140): the <c>graph</c> node expanded inline at validation, its nodes renamed
/// <c>&lt;node&gt;/&lt;id&gt;</c>, and <c>input</c> nodes as its ports. The expansion equals the same nodes written by hand
/// (a warp among them, T-153), a parameter set on the node equals the same override through T-139, every fault, paths
/// from two folders, a three-file cycle, and a table of broken copies and what caught each.
/// </summary>
[Trait("Tier", "unit")]
public sealed class NestedGraphTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "patina-t140-" + Guid.NewGuid().ToString("N"));

    public NestedGraphTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    /// <summary>A filter: input <c>in</c> (colour, no default) warped by noise and mixed toward a colour under a mask, <c>amount</c> the mix's opacity.</summary>
    private const string Inner = """
        {
          "version": 2,
          "size": 64,
          "seed": 0,
          "parameters": {
            "amount": { "value": 0.5, "drives": ["mix.opacity"] }
          },
          "nodes": {
            "c": { "type": "colour", "params": { "colour": "#102030" } },
            "in": { "type": "input", "params": { "kind": "colour", "name": "in" } },
            "mix": { "type": "blend", "inputs": { "a": "warped", "b": "c", "mask": "n" }, "params": { "mode": "mix" } },
            "n": { "type": "noise", "params": { "cells": [8, 8] } },
            "out": { "type": "output", "inputs": { "in": "mix" } },
            "warped": { "type": "warp", "inputs": { "by": "n", "in": "in" }, "params": { "amount": 0.05 } }
          }
        }
        """;

    private const string Outer = """
        {
          "version": 2,
          "size": 64,
          "seed": 5,
          "nodes": {
            "g": { "type": "graph", "inputs": { "in": "src" }, "params": { "amount": 0.3, "path": "inner.texgraph" } },
            "n": { "type": "noise", "params": { "cells": [4, 4], "seed": 9 } },
            "out": { "type": "output", "inputs": { "in": "g" } },
            "rough": { "type": "output", "inputs": { "in": "n" }, "params": { "role": "roughness" } },
            "src": { "type": "colour", "params": { "colour": "#c08040" } }
          }
        }
        """;

    /// <summary>The outer graph's nodes and the inner graph's, written out by hand in one graph (ids changed, opacity set, in wired to src).</summary>
    private static string ByHand(string mixA = "warped", string opacity = "0.3", string noise = "inner_n", string mixB = "c") => $$"""
        {
          "version": 1,
          "size": 64,
          "seed": 5,
          "nodes": {
            "c": { "type": "colour", "params": { "colour": "#102030" } },
            "inner_n": { "type": "noise", "params": { "cells": [8, 8] } },
            "mix": { "type": "blend", "inputs": { "a": "{{mixA}}", "b": "{{mixB}}", "mask": "{{noise}}" }, "params": { "mode": "mix", "opacity": {{opacity}} } },
            "n": { "type": "noise", "params": { "cells": [4, 4], "seed": 9 } },
            "out": { "type": "output", "inputs": { "in": "mix" } },
            "rough": { "type": "output", "inputs": { "in": "n" }, "params": { "role": "roughness" } },
            "src": { "type": "colour", "params": { "colour": "#c08040" } },
            "warped": { "type": "warp", "inputs": { "by": "{{noise}}", "in": "src" }, "params": { "amount": 0.05 } }
          }
        }
        """;

    private string Put(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private static TextureGraph Load(string path)
    {
        var read = TexGraphFile.ReadFile(path);
        Assert.True(read.Loaded, read.Error?.ToString());
        return read.Graph!;
    }

    private static TextureGraph Text(string text)
    {
        var read = TexGraphFile.Read(text);
        Assert.True(read.Loaded, read.Error?.ToString());
        Assert.Empty(read.Faults);
        return read.Graph!;
    }

    private static byte[] Image(TextureGraph graph, string output, int? tile = null) =>
        OutputImage.Rgba8(new GraphEvaluator(graph), output, 64, tile);

    private IReadOnlyList<GraphFault> FaultsOf(string outer)
    {
        Put("inner.texgraph", Inner);
        return GraphValidator.Validate(Load(Put("outer.texgraph", outer)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(16)]
    [InlineData(32)]
    public void The_expansion_equals_the_same_nodes_written_by_hand_a_warp_among_them(int? tile)
    {
        Put("inner.texgraph", Inner);
        var outer = Load(Put("outer.texgraph", Outer));
        Assert.Empty(GraphValidator.Validate(outer));
        var hand = Text(ByHand());
        Assert.Equal(Image(hand, "base_colour", tile), Image(outer, "base_colour", tile));
        Assert.Equal(Image(hand, "roughness", tile), Image(outer, "roughness", tile));
        Assert.Equal(["base_colour", "roughness"], new GraphEvaluator(outer).Outputs);
    }

    /// <summary>RV-76: the outer graph's size rules, not the nested file's: an inner graph of 32 under an outer one of 64 equals the hand-written copy at 64.</summary>
    [Fact]
    public void A_nested_file_of_another_size_takes_the_outer_graphs_size()
    {
        Put("inner.texgraph", Inner.Replace("\"size\": 64", "\"size\": 32"));
        var outer = Load(Put("outer.texgraph", Outer));
        Assert.Empty(GraphValidator.Validate(outer));
        var hand = Text(ByHand());
        Assert.Equal(Image(hand, "base_colour"), Image(outer, "base_colour"));
        Assert.Equal(Image(hand, "base_colour", 16), Image(outer, "base_colour", 16));
    }

    /// <summary>RV-76: an outer node whose id is one expansion also makes (<c>g/c</c> beside graph node <c>g</c>, whose file has <c>c</c>) is faulted for its id, not put in the nested node's place; so is the same one level down.</summary>
    [Fact]
    public void An_outer_node_with_an_expanded_id_is_faulted_not_put_in_the_nested_nodes_place()
    {
        var clash = Outer.Replace("\"n\": { \"type\": \"noise\"", "\"g/c\": { \"type\": \"colour\", \"params\": { \"colour\": \"#ffffff\" } },\n    \"n\": { \"type\": \"noise\"");
        Assert.Contains("\"g/c\"", clash);
        var faults = FaultsOf(clash);
        var fault = Assert.Single(faults);
        Assert.Equal("g/c", fault.Node);
        Assert.StartsWith("node 'g/c': an id is", fault.Message);

        // One level down: the inner file holds the clash, and the outer graph using it is faulted through it.
        Put("inner.texgraph", Inner);
        Put("middle.texgraph", clash.Replace("\"seed\": 5,", "\"seed\": 5,"));
        var top = Load(Put("top.texgraph", Outer.Replace("\"path\": \"inner.texgraph\"", "\"path\": \"middle.texgraph\"").Replace("\"amount\": 0.3, ", "")));
        Assert.Contains(GraphValidator.Validate(top), f => f.Node == "g/g/c" && f.Message.Contains("an id is", StringComparison.Ordinal));
    }

    /// <summary>RV-76: a graph node's path is relative to the using graph's file (T-140): absolute, drive-relative, from-the-root and UNC paths are faults, and nothing is opened for them.</summary>
    [Fact]
    public void A_rooted_path_is_a_fault_and_is_not_opened()
    {
        var b = ((char)92).ToString();
        var innerFull = Put("inner.texgraph", Inner);
        string[] rooted = [innerFull, "C:inner", "/inner", b + "inner", b + b + "server" + b + "share" + b + "x", "//server/share/x"];
        foreach (var path in rooted)
        {
            Assert.False(ParamSpec.IsRelativePath(path), path);
            Assert.Null(GraphValidator.ResolvePath(Load(Put("outer.texgraph", Outer)), path));
            var json = path.Replace(b, b + b);
            var faults = GraphValidator.Validate(Load(Put("outer.texgraph", Outer.Replace("\"inner.texgraph\"", "\"" + json + "\""))));
            var fault = Assert.Single(faults);
            Assert.Equal(("g", "path"), (fault.Node, fault.Parameter));
            Assert.Contains("relative to this graph's file", fault.Message);
            Assert.DoesNotContain("no file at", fault.Message);
        }
        Assert.True(ParamSpec.IsRelativePath(".." + b + "x"));
        Assert.True(ParamSpec.IsRelativePath("sub/x.texgraph"));
    }

    /// <summary>
    /// RV-76: nine files each using the one below <c>fan</c> times. A fan of 2 (about 1,500 nodes) validates; a fan of 4
    /// would take in 4^8 uses, so it is a fault naming <see cref="GraphValidator.MaxNodes"/>, found at once rather than hanging.
    /// </summary>
    [Theory]
    [InlineData(2, false)]
    [InlineData(4, true)]
    [InlineData(10, true)]
    public void Fan_out_past_the_node_cap_is_a_fault_not_a_hang(int fan, bool faulted)
    {
        Put("f0.texgraph", """
            { "version": 1, "size": 64, "seed": 0, "nodes": { "c": { "type": "colour" }, "out": { "type": "output", "inputs": { "in": "c" } } } }
            """);
        for (var i = 1; i <= 8; i++)
        {
            var uses = string.Join(", ", Enumerable.Range(1, fan).Select(k => $"\"u{k}\": {{ \"type\": \"graph\", \"params\": {{ \"path\": \"f{i - 1}\" }} }}"));
            Put($"f{i}.texgraph", $$"""{ "version": 2, "size": 64, "seed": 0, "nodes": { {{uses}}, "out": { "type": "output", "inputs": { "in": "u1" } } } }""");
        }
        var top = Load(Path.Combine(_dir, "f8.texgraph"));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var faults = GraphValidator.Validate(top);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(30), $"validation took {watch.Elapsed}");
        if (!faulted) { Assert.Empty(faults); return; }
        Assert.NotEmpty(faults);
        Assert.Contains(faults, f => f.Parameter == "path" && f.Message.Contains($"past {GraphValidator.MaxNodes}", StringComparison.Ordinal));
    }

    /// <summary>Broken copies of the expansion, each written by hand as the expansion would have made it, and what tells it from the real one.</summary>
    public static TheoryData<string, string> BrokenCopies => new()
    {
        // A nested node renamed without its prefix clashes with the parent's 'n' (the parent's noise, cells 4, seed 9, takes its place).
        { "a renamed node clashing with a parent's id", ByHand(noise: "n") },
        { "a parameter not passed down (amount left at the blend's own opacity 1)", ByHand(opacity: "1") },
        { "an input wired to the wrong port (the warped input into the mix's b, the colour into its a)", ByHand(mixA: "c", mixB: "warped") },
    };

    [Theory]
    [MemberData(nameof(BrokenCopies))]
    public void Each_broken_copy_differs_from_the_expansion_the_byte_for_byte_test_catches_it(string what, string broken)
    {
        Put("inner.texgraph", Inner);
        var outer = Load(Put("outer.texgraph", Outer));
        Assert.NotEqual(Image(Text(broken), "base_colour"), Image(outer, "base_colour"));
        Assert.False(string.IsNullOrEmpty(what));
    }

    [Fact]
    public void A_parameter_set_on_the_node_equals_the_same_override_through_T139()
    {
        File.Copy(Path.Combine(Samples.Folder, "bricks.texgraph"), Path.Combine(_dir, "bricks.texgraph"));
        var outer = Load(Put("wall.texgraph", """
            {
              "version": 2,
              "size": 256,
              "seed": 21,
              "nodes": {
                "base": { "type": "output", "inputs": { "in": "wall.base_colour" } },
                "wall": { "type": "graph", "params": { "mortar": 0.06, "path": "bricks" } }
              }
            }
            """));
        Assert.Empty(GraphValidator.Validate(outer));
        var bricks = Load(Path.Combine(_dir, "bricks.texgraph"));
        var set = GraphParameters.WithOverrides(bricks, new Dictionary<string, ParamValue> { ["mortar"] = ParamValue.Of(0.06) }).Graph!;
        Assert.Equal(OutputImage.Rgba8(new GraphEvaluator(set), "base_colour", 256), OutputImage.Rgba8(new GraphEvaluator(outer), "base_colour", 256));
    }

    /// <summary>The person's ruling (2026-11-20): an outer exposed parameter drives a value on a graph node, "wall.mortar".</summary>
    private static string PassThrough(string parameter) => $$"""
        {
          "version": 2,
          "size": 256,
          "seed": 21,
          "parameters": {
            {{parameter}}
          },
          "nodes": {
            "base": { "type": "output", "inputs": { "in": "wall.base_colour" } },
            "wall": { "type": "graph", "params": { "path": "bricks" } }
          }
        }
        """;

    private TextureGraph BricksWith(double mortar)
    {
        var bricks = Load(Path.Combine(_dir, "bricks.texgraph"));
        return GraphParameters.WithOverrides(bricks, new Dictionary<string, ParamValue> { ["mortar"] = ParamValue.Of(mortar) }).Graph!;
    }

    [Fact]
    public void An_outer_parameter_drives_a_graph_nodes_parameter_with_the_nested_range()
    {
        // The worked case: old-wall's kind of graph exposing mortar, which drives bricks' own mortar (0 to 0.45, its layout's gap) through the node.
        File.Copy(Path.Combine(Samples.Folder, "bricks.texgraph"), Path.Combine(_dir, "bricks.texgraph"));
        var outer = Load(Put("wall.texgraph", PassThrough("\"mortar\": { \"value\": 0.06, \"drives\": [\"wall.mortar\"] }")));
        Assert.Empty(GraphValidator.Validate(outer));
        var mortar = GraphParameters.Of(outer)["mortar"];
        Assert.Equal((ParamKind.Float, 0.0, 0.45), (mortar.Kind, mortar.Range.Min, mortar.Range.Max));
        Assert.Equal(OutputImage.Rgba8(new GraphEvaluator(BricksWith(0.06)), "base_colour", 256), OutputImage.Rgba8(new GraphEvaluator(outer), "base_colour", 256));

        // An override on the outer graph reaches bricks, equal to the same override on bricks itself.
        var set = GraphParameters.WithOverrides(outer, new Dictionary<string, ParamValue> { ["mortar"] = ParamValue.Of(0.12) });
        Assert.Null(set.Error);
        Assert.Equal(OutputImage.Rgba8(new GraphEvaluator(BricksWith(0.12)), "base_colour", 256), OutputImage.Rgba8(new GraphEvaluator(set.Graph!), "base_colour", 256));
        Assert.Equal("parameters['mortar']: is 0.5, but must be a number from 0 to 0.45",
            GraphParameters.WithOverrides(outer, new Dictionary<string, ParamValue> { ["mortar"] = ParamValue.Of(0.5) }).Error);

        // The node's own value for a driven parameter is not written, and the graph reads back the same.
        var written = TexGraphFile.Write(outer);
        Assert.DoesNotContain("\"mortar\": 0.06 }", written);
        Assert.Equal(written, TexGraphFile.Write(Load(Put("again.texgraph", written))));
    }

    [Fact]
    public void A_pass_through_parameter_takes_the_narrowest_range_of_what_it_drives()
    {
        // amount drives inner's amount (0 to 1, itself driving a blend's opacity) on two graph nodes, and max narrows it again.
        Put("inner.texgraph", Inner);
        var outer = Load(Put("outer.texgraph", """
            {
              "version": 2,
              "size": 64,
              "seed": 5,
              "parameters": {
                "amount": { "value": 0.2, "drives": ["g.amount", "h.amount"], "max": 0.4 }
              },
              "nodes": {
                "g": { "type": "graph", "inputs": { "in": "src" }, "params": { "amount": 0.9, "path": "inner.texgraph" } },
                "h": { "type": "graph", "inputs": { "in": "g" }, "params": { "path": "inner.texgraph" } },
                "out": { "type": "output", "inputs": { "in": "h" } },
                "src": { "type": "colour", "params": { "colour": "#c08040" } }
              }
            }
            """));
        Assert.Empty(GraphValidator.Validate(outer));
        var amount = GraphParameters.Of(outer)["amount"];
        Assert.Equal((0.0, 0.4), (amount.Range.Min, amount.Range.Max));
        // g's own 0.9 is ignored: both nodes take 0.2.
        var byHand = Load(Put("byhand.texgraph", """
            {
              "version": 2,
              "size": 64,
              "seed": 5,
              "nodes": {
                "g": { "type": "graph", "inputs": { "in": "src" }, "params": { "amount": 0.2, "path": "inner.texgraph" } },
                "h": { "type": "graph", "inputs": { "in": "g" }, "params": { "amount": 0.2, "path": "inner.texgraph" } },
                "out": { "type": "output", "inputs": { "in": "h" } },
                "src": { "type": "colour", "params": { "colour": "#c08040" } }
              }
            }
            """));
        Assert.Empty(byHand.Parameters);
        Assert.Equal(Image(byHand, "base_colour"), Image(outer, "base_colour"));
    }

    public static TheoryData<string, string, string> PassThroughFaults => new()
    {
        { "a value outside the nested graph's range", "\"mortar\": { \"value\": 0.5, \"drives\": [\"wall.mortar\"] }",
            "parameters['mortar']: is 0.5, but must be a number from 0 to 0.45" },
        { "a parameter the nested graph does not have", "\"mortar\": { \"value\": 0.06, \"drives\": [\"wall.mortr\"] }",
            "parameters['mortar']: drives 'wall.mortr', but 'wall', a graph node using bricks.texgraph, has no parameter 'mortr'; its parameters are path, " },
        { "the graph node's path", "\"mortar\": { \"value\": 0.06, \"drives\": [\"wall.path\"] }",
            "parameters['mortar']: drives 'wall.path', a path, which a graph parameter cannot drive; " },
        { "two kinds", "\"mortar\": { \"value\": 0.06, \"drives\": [\"wall.mortar\", \"wall.tint\"] }",
            "parameters['mortar']: drives 'wall.mortar', a number, and 'wall.tint', a colour; the parameters it drives are all of one kind" },
        { "a range left empty by min", "\"mortar\": { \"value\": 0.6, \"drives\": [\"wall.mortar\"], \"min\": 0.5 }",
            "parameters['mortar']: the parameters it drives have no value in common: wall.mortar is a number from 0 to 0.45; and it gives min 0.5" },
    };

    [Theory]
    [MemberData(nameof(PassThroughFaults))]
    public void A_pass_through_parameter_is_faulted_as_T139s_drives_are(string what, string parameter, string start)
    {
        File.Copy(Path.Combine(Samples.Folder, "bricks.texgraph"), Path.Combine(_dir, "bricks.texgraph"));
        var fault = Assert.Single(GraphValidator.Validate(Load(Put("wall.texgraph", PassThrough(parameter)))));
        Assert.Null(fault.Node);
        Assert.StartsWith(start, fault.Message);
        Assert.False(string.IsNullOrEmpty(what));
    }

    [Fact]
    public void Drives_through_a_cycle_of_files_end_and_the_cycle_is_named()
    {
        string Use(string next) => $$"""{ "version": 2, "size": 64, "seed": 0, "parameters": { "p": { "value": 1, "drives": ["u.p"] } }, "nodes": { "o": { "type": "output", "inputs": { "in": "u" } }, "u": { "type": "graph", "params": { "path": "{{next}}" } } } }""";
        Put("b.texgraph", Use("c.texgraph"));
        Put("c.texgraph", Use("a.texgraph"));
        var faults = GraphValidator.Validate(Load(Put("a.texgraph", Use("b.texgraph"))));
        Assert.Contains(faults, f => f.Message.Contains("a cycle through files: b.texgraph -> c.texgraph -> a.texgraph -> b.texgraph"));
    }

    [Fact]
    public void Relative_paths_resolve_from_each_graphs_own_folder()
    {
        Put(Path.Combine("lib", "inner.texgraph"), Inner);
        var one = Load(Put(Path.Combine("a", "outer.texgraph"), Outer.Replace("\"inner.texgraph\"", "\"../lib/inner.texgraph\"")));
        var two = Load(Put(Path.Combine("b", "deep", "outer.texgraph"), Outer.Replace("\"inner.texgraph\"", "\"../../lib/inner\"")));
        Assert.Empty(GraphValidator.Validate(one));
        Assert.Empty(GraphValidator.Validate(two));
        Assert.Equal(Image(one, "base_colour"), Image(two, "base_colour"));
        Assert.Equal(Path.Combine(_dir, "lib", "inner.texgraph"), GraphValidator.Uses(two).Single().File);
    }

    [Fact]
    public void An_input_lacking_a_default_stops_the_graph_on_its_own_only()
    {
        var inner = Load(Put("inner.texgraph", Inner));
        var fault = Assert.Single(GraphValidator.Validate(inner));
        Assert.True(fault.OnItsOwn);
        Assert.Equal("node 'in' parameter 'default': input 'in' has no default, so the graph cannot be evaluated on its own; give it a default, or use the graph as a graph node that connects 'in'", fault.Message);
        Assert.Throws<GraphFaultException>(() => new GraphEvaluator(inner));
    }

    [Fact]
    public void An_input_with_a_default_evaluates_on_its_own_and_is_written_as_version_2()
    {
        var graph = Text(Inner.Replace("\"kind\": \"colour\", \"name\": \"in\"", "\"default\": \"#ff0000\", \"kind\": \"colour\", \"name\": \"in\""));
        Assert.StartsWith("{\n  \"version\": 2,", TexGraphFile.Write(graph));
        Assert.Equal(64 * 64 * 4, Image(graph, "base_colour").Length);
        var version1 = TexGraphFile.Write(graph).Replace("\"version\": 2", "\"version\": 1");
        Assert.Contains("version 2", TexGraphFile.Read(version1).Error!.Message);
    }

    public static TheoryData<string, string, string?, string?, string> Faults => new()
    {
        { "a value out of the nested graph's range", Outer.Replace("\"amount\": 0.3", "\"amount\": 1.5"), "g", "amount",
            "nodes['g'].params['amount']: is 1.5, but must be a number from 0 to 1 (inner.texgraph, parameter 'amount')" },
        { "an unknown parameter", Outer.Replace("\"amount\": 0.3", "\"amont\": 0.3"), "g", "amont",
            "nodes['g'].params['amont']: inner.texgraph has no such parameter; its parameters are amount" },
        { "an unknown input", Outer.Replace("\"in\": \"src\"", "\"in\": \"src\", \"base\": \"src\""), "g", null,
            "node 'g' input 'base': inner.texgraph has no such input; its inputs are in (colour)" },
        { "an input lacking a default left unconnected", Outer.Replace("\"inputs\": { \"in\": \"src\" }, ", ""), "g", null,
            "node 'g' input 'in': inner.texgraph's input 'in' has no default, so a graph node using it must connect it, and it is not" },
        { "an unknown output port", Outer.Replace("\"in\": \"g\"", "\"in\": \"g.height\""), "out", null,
            "node 'out' input 'in': names 'g.height', but inner.texgraph has no output 'height'; its outputs are g.base_colour" },
        { "a kind mismatch on an input", Outer.Replace("\"in\": \"src\"", "\"in\": \"n\""), "g", null,
            "node 'g' input 'in': takes colour, but it is given greyscale (inner.texgraph's input 'in')" },
        { "a missing path", Outer.Replace("\"path\": \"inner.texgraph\"", "\"path\": \"\""), "g", "path",
            "node 'g' parameter 'path' is \"\", but must be a path to a .texgraph file, relative to this graph's file" },
        { "a source naming an expanded node past the graph node", Outer.Replace("\"in\": \"g\"", "\"in\": \"g/mix\""), "out", null,
            "node 'out' input 'in': 'g/mix' is not a source; a source is \"<id>\" or \"<id>.<port>\"" },
    };

    [Theory]
    [MemberData(nameof(Faults))]
    public void Each_fault_names_its_node_and_says_what_would_be_right(string what, string outer, string? node, string? parameter, string message)
    {
        var faults = FaultsOf(outer);
        var fault = Assert.Single(faults);
        Assert.Equal((node, parameter, message), (fault.Node, fault.Parameter, fault.Message));
        Assert.False(string.IsNullOrEmpty(what));
    }

    [Fact]
    public void A_missing_file_lists_the_texgraph_files_in_its_folder()
    {
        var faults = FaultsOf(Outer.Replace("\"path\": \"inner.texgraph\"", "\"path\": \"iner\""));
        var fault = Assert.Single(faults);
        Assert.Equal($"node 'g' parameter 'path': no file at {Path.Combine(_dir, "iner.texgraph")}; the .texgraph files in {_dir} (2) are: inner.texgraph, outer.texgraph", fault.Message);
    }

    [Fact]
    public void A_fault_inside_a_nested_graph_names_the_path_from_the_top_and_its_file()
    {
        Put("inner.texgraph", Inner.Replace("\"cells\": [8, 8]", "\"cells\": [8, 8], \"octaves\": 99"));
        var fault = Assert.Single(GraphValidator.Validate(Load(Put("outer.texgraph", Outer))));
        Assert.Equal(("g/n", "octaves", Path.Combine(_dir, "inner.texgraph")), (fault.Node, fault.Parameter, fault.File));
        // The person's ruling (2026-11-20): the graph node's own over-range form, nodes['<path>'].params['<name>'], and the file.
        Assert.StartsWith("nodes['g/n'].params['octaves']: is 99, but must be a whole number from 1 to ", fault.Message);
        Assert.EndsWith(" (inner.texgraph)", fault.Message);
    }

    [Fact]
    public void A_three_file_cycle_names_the_chain()
    {
        string Use(string next) => $$"""{ "version": 2, "size": 64, "seed": 0, "nodes": { "o": { "type": "output", "inputs": { "in": "u" } }, "u": { "type": "graph", "params": { "path": "{{next}}" } } } }""";
        Put("b.texgraph", Use("c.texgraph"));
        Put("c.texgraph", Use("a.texgraph"));
        var faults = GraphValidator.Validate(Load(Put("a.texgraph", Use("b.texgraph"))));
        var fault = Assert.Single(faults);
        // The top graph's own file is not in the chain (a graph knows its folder, not its name), so the cycle is found one file further down.
        Assert.Equal("u/u/u/u", fault.Node);
        Assert.Equal("nodes['u/u/u/u'].params['path']: uses b.texgraph, a cycle through files: b.texgraph -> c.texgraph -> a.texgraph -> b.texgraph (each file uses the next as a node) (a.texgraph)", fault.Message);
    }

    [Fact]
    public void Nesting_deeper_than_8_is_a_fault_and_8_is_not()
    {
        string Use(string next) => $$"""{ "version": 2, "size": 64, "seed": 0, "nodes": { "o": { "type": "output", "inputs": { "in": "u" } }, "u": { "type": "graph", "params": { "path": "{{next}}" } } } }""";
        Put("leaf.texgraph", """{ "version": 1, "size": 64, "seed": 0, "nodes": { "c": { "type": "colour" }, "o": { "type": "output", "inputs": { "in": "c" } } } }""");
        for (var i = 1; i <= 9; i++) Put($"f{i}.texgraph", Use(i == 1 ? "leaf.texgraph" : $"f{i - 1}.texgraph"));
        Assert.Empty(GraphValidator.Validate(Load(Path.Combine(_dir, "f8.texgraph"))));
        var fault = Assert.Single(GraphValidator.Validate(Load(Path.Combine(_dir, "f9.texgraph"))));
        Assert.Contains($"nesting deeper than {GraphValidator.MaxDepth} files", fault.Message);
    }

    [Fact]
    public void A_file_without_graph_or_input_nodes_is_unchanged_by_the_expansion()
    {
        foreach (var path in Directory.GetFiles(Samples.Folder, "*.texgraph"))
        {
            var graph = Load(path);
            Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), TexGraphFile.Write(graph));
            if (graph.Nodes.Values.Any(n => n.Type is NestedGraphNode.TypeName or InputNode.TypeName)) continue;
            Assert.Empty(GraphValidator.Uses(graph));
        }
    }

    [Fact]
    public void Uses_gives_the_file_parameters_inputs_and_outputs()
    {
        Put("inner.texgraph", Inner);
        var use = Assert.Single(GraphValidator.Uses(Load(Put("outer.texgraph", Outer))));
        Assert.Equal(("g", Path.Combine(_dir, "inner.texgraph"), true), (use.Node, use.File, use.Expanded));
        Assert.Equal(ParamValue.Of(0.3), use.Parameters["amount"].Value);
        Assert.Equal([new NestedGraphInput("in", Imaging.PortKind.Colour, false)], use.Inputs);
        Assert.Equal(["base_colour"], use.Outputs);
    }
}

internal static class Samples
{
    public static string Folder
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Carve.slnx"))) return Path.Combine(dir.FullName, "samples", "graphs");
            throw new DirectoryNotFoundException("Carve.slnx not found above the test folder");
        }
    }
}
