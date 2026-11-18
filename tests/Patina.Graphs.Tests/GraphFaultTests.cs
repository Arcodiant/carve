using Xunit;

namespace Patina.Graphs.Tests;

[Trait("Tier", "unit")]
public class GraphFaultTests
{
    // One graph planting every fault, so the list must hold them all at once.
    private const string Faulty = """
        {
          "version": 1, "size": 256, "seed": 7,
          "nodes": {
            "Bad-Id": { "type": "colour" },
            "a": { "type": "nosie" },
            "b": { "type": "colour", "params": { "colour": "#zzzzzz", "shade": 1 } },
            "c": { "type": "output", "inputs": { "in": "grain_2", "extra": "b" }, "params": { "role": "normal" } },
            "d": { "type": "output", "params": { "role": "roughness" } },
            "e": { "type": "output", "inputs": { "in": "b" }, "params": { "role": "roughness" } },
            "f": { "type": "blur", "inputs": { "in": "g" } },
            "g": { "type": "blur", "inputs": { "in": "f" } },
            "h": { "type": "output", "inputs": { "in": "s.x" }, "params": { "role": "occlusion" } },
            "i": { "type": "output", "inputs": { "in": "s" }, "params": { "role": "base_colour" } },
            "k": { "type": "hash", "params": { "offset": 1.5, "cells": [0, 9000] } },
            "s": { "type": "split", "inputs": { "in": "k" } },
            "t": { "type": "blur", "inputs": { "in": "out_x" } },
            "out_x": { "type": "output", "inputs": { "in": "t" } }
          }
        }
        """;

    private static readonly IReadOnlyList<GraphFault> Faults = Read();

    private static IReadOnlyList<GraphFault> Read()
    {
        var read = TexGraphFile.Read(Faulty, TestNodes.Registry);
        Assert.True(read.Loaded, read.Error?.ToString()); // a graph with reference faults still loads
        return read.Faults;
    }

    private static GraphFault Find(string node, string? port, string? parameter, string says)
    {
        var fault = Assert.Single(Faults, f => f.Node == node && f.Port == port && f.Parameter == parameter && f.Message.Contains(says));
        Assert.Contains($"'{node}'", fault.Message);
        if (port is not null) Assert.Contains($"input '{port}'", fault.Message);
        if (parameter is not null) Assert.Contains($"parameter '{parameter}'", fault.Message);
        return fault;
    }

    [Fact]
    public void ABadIdIsNamed() => Find("Bad-Id", null, null, "[a-z][a-z0-9_]*");

    [Fact]
    public void AnUnknownTypeListsTheTypes() =>
        Find("a", null, null, "unknown type 'nosie'; the types are blend, blur, colour, gradient, hash, height_to_normal, levels, noise, output, split");

    [Fact]
    public void AnUnknownParameterListsTheTypesParameters() => Find("b", null, "shade", "its parameters are colour");

    [Fact]
    public void AWrongParameterGivesWhatItMustBe()
    {
        Find("b", null, "colour", "must be a colour \"#rrggbb\"");
        Find("k", null, "offset", "is 1.5, but must be a whole number from 0 to 1000");
        Find("k", null, "cells", "is [0, 9000], but must be two whole numbers [x, y], each from 1 to 4096");
    }

    [Fact]
    public void AnInputNamingNoNodeListsTheIds() =>
        Find("c", "in", null, "names 'grain_2', which is not a node; the nodes are Bad-Id, a, b, c, d, e, f, g, h, i, k, out_x, s, t");

    [Fact]
    public void AnInputPortTheTypeHasNotListsItsInputs() => Find("c", "extra", null, "output has no such input; its inputs are in");

    [Fact]
    public void AMissingRequiredInputIsNamed() => Find("d", "in", null, "needs it connected");

    [Fact]
    public void ASourcePortThatDoesNotExistListsThePorts()
    {
        Find("h", "in", null, "'s' has no port 'x'; its output ports are r, g");
        Find("i", "in", null, "several output ports, so name one: s.r, s.g");
    }

    [Fact]
    public void ACycleNamesTheNodesOnIt()
    {
        Find("f", "in", null, "on a cycle, f -> g -> f");
        Find("t", "in", null, "names 'out_x', a node of type output, which has no output port"); // an output can be no one's input
    }

    [Fact]
    public void AKindMismatchNamesBothEnds()
    {
        Find("e", "in", null, "role roughness takes greyscale, but it is given colour");
        Find("s", "in", null, "takes colour, but 'k' gives greyscale");
    }

    [Fact]
    public void TwoOutputsWithOneRoleNameBoth()
    {
        Find("e", null, "role", "roughness is already the role of output 'd'");
        Find("out_x", null, "role", "base_colour is already the role of output 'i'"); // role left to its default
    }

    [Fact]
    public void EveryFaultIsReportedFromOneGraph()
    {
        Assert.Equal(17, Faults.Count);
        Assert.All(Faults, f => Assert.NotNull(f.Node));
    }

    [Fact]
    public void NoOutputAtAllIsTheOneFaultWithNoNode()
    {
        var read = TexGraphFile.Read("""{ "version": 1, "size": 256, "seed": 7, "nodes": { "wood": { "type": "colour" } } }""");
        var fault = Assert.Single(read.Faults);
        Assert.Null(fault.Node);
        Assert.Contains("no output", fault.Message);
        Assert.Contains("base_colour, normal, roughness, occlusion", fault.Message);
    }

    [Fact]
    public void ASelfLoopIsACycle()
    {
        var read = TexGraphFile.Read("""{ "version": 1, "size": 256, "seed": 7, "nodes": { "loop": { "type": "blur", "inputs": { "in": "loop" } }, "o": { "type": "output", "inputs": { "in": "loop" } } } }""", TestNodes.Registry);
        var fault = Assert.Single(read.Faults);
        Assert.Equal(("loop", "in"), (fault.Node, fault.Port));
        Assert.Contains("loop -> loop", fault.Message);
    }

    [Fact]
    public void AGoodGraphHasNoFault()
    {
        var read = TexGraphFile.Read("""{ "version": 1, "size": 256, "seed": 7, "nodes": { "wood": { "type": "colour", "params": { "colour": "#aa7040" } }, "out": { "type": "output", "inputs": { "in": "wood" } } } }""");
        Assert.Empty(read.Faults);
    }

    [Theory]
    [InlineData(""" "l": { "type": "levels", "inputs": { "in": "x" } }, "o": { "type": "output", "inputs": { "in": "l" } } """)]
    [InlineData(""" "c": { "type": "colour" }, "l": { "type": "blend", "inputs": { "a": "x", "b": "c" } }, "o": { "type": "output", "inputs": { "in": "l" } } """)]
    [InlineData(""" "c": { "type": "colour" }, "l": { "type": "blend", "inputs": { "a": "c", "b": "x" } }, "o": { "type": "output", "inputs": { "in": "l" } } """)]
    public void AnInputFromANodeOfUnknownTypeIsOnlyThatNodesFault(string nodes)
    {
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": 32, "seed": 7, "nodes": { "x": { "type": "nosie" }, {{nodes}} } }""");
        Assert.True(read.Loaded);
        var fault = Assert.Single(read.Faults);
        Assert.Equal("x", fault.Node);
        Assert.Contains("unknown type 'nosie'", fault.Message);
    }

    [Fact]
    public void AnUnknownRoleIsNotAlsoCountedAsTheDefaultRole()
    {
        var read = TexGraphFile.Read("""{ "version": 1, "size": 32, "seed": 7, "nodes": { "c": { "type": "colour" }, "a": { "type": "output", "inputs": { "in": "c" } }, "b": { "type": "output", "inputs": { "in": "c" }, "params": { "role": "albedo" } } } }""");
        var fault = Assert.Single(read.Faults);
        Assert.Equal(("b", "role"), (fault.Node, fault.Parameter));
        Assert.Contains("\"albedo\"", fault.Message);
    }
}
