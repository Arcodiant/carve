using Xunit;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Tests;

/// <summary>T-134: the metallic and height roles and named outputs: their packing at chosen texels, their order, and their faults.</summary>
[Trait("Tier", "unit")]
public class RolesAndNamedOutputsTests
{
    private static TextureGraph Graph(string nodes, int size = 32)
    {
        var read = TexGraphFile.Read($$"""{ "version": 1, "size": {{size}}, "seed": 0, "nodes": { {{nodes}} } }""");
        Assert.Null(read.Error);
        return read.Graph!;
    }

    /// <summary>A greyscale constant <paramref name="v"/>: a gradient levelled to one value.</summary>
    private static string Const(string id, double v) =>
        $$""" "{{id}}_g": { "type": "gradient" }, "{{id}}": { "type": "levels", "inputs": { "in": "{{id}}_g" }, "params": { "out_low": {{v}}, "out_high": {{v}} } } """;

    private static string Out(string id, string from, string param) =>
        $$""" "{{id}}": { "type": "output", "inputs": { "in": "{{from}}" }, "params": { {{param}} } } """;

    private static IReadOnlyList<GraphFault> Faults(string nodes) => GraphValidator.Validate(Graph(nodes));

    // metallic 1 across u from 0 at texel 0: the gradient's texel x is (x + 0.5) / 32.
    private static readonly string Plate = string.Join(",",
        """ "ramp": { "type": "gradient" } """,
        Const("rough", 0.6),
        """ "tint": { "type": "colour", "params": { "colour": "#336699" } } """,
        Const("h", 0.2),
        Out("o_rough", "rough", "\"role\": \"roughness\""),
        Out("o_metal", "ramp", "\"role\": \"metallic\""),
        Out("o_height", "h", "\"role\": \"height\""),
        Out("o_edge", "ramp", "\"name\": \"edge\""),
        Out("o_tint", "tint", "\"name\": \"a_tint\""));

    [Fact]
    public void Metallic_is_packed_into_roughness_s_blue_and_alone_is_grey()
    {
        var evaluator = new GraphEvaluator(Graph(Plate));
        var file = OutputImage.FileRgba8(evaluator, "roughness", 32);
        // Texel 5: metallic (5.5 / 32) * 255 = 43.83 -> 44; roughness 0.6 * 255 = 153.
        Assert.Equal([255, 153, 44, 255], file.AsSpan(5 * 4, 4).ToArray());
        Assert.Equal([255, 153, 44, 255], file.AsSpan((7 * 32 + 5) * 4, 4).ToArray());
        // Texel 31: (31.5 / 32) * 255 = 251.02 -> 251.
        Assert.Equal([255, 153, 251, 255], file.AsSpan(31 * 4, 4).ToArray());
        // The roughness column alone keeps B 0; metallic alone is grey.
        Assert.Equal([255, 153, 0, 255], OutputImage.Rgba8(evaluator, OutputRole.Roughness, 32).AsSpan(5 * 4, 4).ToArray());
        Assert.Equal([44, 44, 44, 255], OutputImage.Rgba8(evaluator, OutputRole.Metallic, 32).AsSpan(5 * 4, 4).ToArray());
        Assert.Equal(Png.Encode(file, 32, 32), OutputImage.Make(evaluator, OutputRole.Roughness, 32).Png);
        Assert.Throws<ArgumentException>(() => OutputImage.Make(evaluator, OutputRole.Metallic, 32));
    }

    [Fact]
    public void Without_metallic_roughness_s_blue_stays_zero()
    {
        var evaluator = new GraphEvaluator(Graph(string.Join(",", Const("rough", 0.6), Out("o", "rough", "\"role\": \"roughness\""))));
        Assert.Equal(OutputImage.Rgba8(evaluator, OutputRole.Roughness, 32), OutputImage.FileRgba8(evaluator, "roughness", 32));
        Assert.Equal([255, 153, 0, 255], OutputImage.FileRgba8(evaluator, "roughness", 32)[..4]);
    }

    [Fact]
    public void Height_and_named_outputs_are_packed_at_chosen_texels()
    {
        var evaluator = new GraphEvaluator(Graph(Plate));
        // height 0.2 * 255 = 51 in R, G and B.
        Assert.Equal([51, 51, 51, 255], OutputImage.Make(evaluator, OutputRole.Height, 32).Rgba.AsSpan((9 * 32 + 3) * 4, 4).ToArray());
        // A greyscale named output is spread to grey; texel 16: (16.5 / 32) * 255 = 131.48 -> 131.
        var edge = OutputImage.Make(evaluator, "edge", 32);
        Assert.Equal("edge", edge.Output);
        Assert.Equal([131, 131, 131, 255], edge.Rgba.AsSpan(16 * 4, 4).ToArray());
        // A colour one is sRGB as given.
        Assert.Equal([0x33, 0x66, 0x99, 255], OutputImage.Make(evaluator, "a_tint", 32).Rgba.AsSpan((20 * 32 + 30) * 4, 4).ToArray());
        Assert.Equal("edge.png", OutputRoles.FileOf("edge"));
        Assert.Equal("roughness.png", OutputRoles.FileOf("metallic"));
        Assert.Equal("height.png", OutputRoles.FileOf("height"));
    }

    [Fact]
    public void Outputs_come_in_role_order_then_named_a_to_z()
    {
        var all = string.Join(",", Plate,
            """ "c": { "type": "colour", "params": { "colour": "#808080" } } """,
            """ "n": { "type": "colour", "params": { "colour": "#8080ff" } } """,
            Out("o_occ", "h", "\"role\": \"occlusion\""), Out("o_n", "n", "\"role\": \"normal\""), Out("o_base", "c", "\"role\": \"base_colour\""));
        var evaluator = new GraphEvaluator(Graph(all));
        Assert.Equal(["base_colour", "normal", "roughness", "metallic", "occlusion", "height", "a_tint", "edge"], evaluator.Outputs);
        Assert.Equal(["a_tint", "edge"], evaluator.Named);
        Assert.Equal(OutputRoles.All, evaluator.Roles);
        Assert.Equal(PortKind.Colour, evaluator.KindOf("a_tint"));
        Assert.Equal(PortKind.Grey, evaluator.KindOf("edge"));
    }

    [Fact]
    public void Metallic_without_roughness_is_a_fault_naming_the_output()
    {
        var fault = Assert.Single(Faults(string.Join(",", """ "ramp": { "type": "gradient" } """, Out("shine", "ramp", "\"role\": \"metallic\""))));
        Assert.Equal(("shine", "role"), (fault.Node, fault.Parameter));
        Assert.StartsWith("node 'shine' parameter 'role': metallic is packed with roughness, so the graph needs a roughness output too", fault.Message);
    }

    [Fact]
    public void Metallic_and_height_take_greyscale()
    {
        var c = """ "c": { "type": "colour", "params": { "colour": "#808080" } } """;
        var fault = Assert.Single(Faults(string.Join(",", c, Out("h", "c", "\"role\": \"height\""))));
        Assert.Equal("node 'h' input 'in': role height takes greyscale, but it is given colour", fault.Message);
    }

    [Fact]
    public void A_name_and_a_role_together_two_outputs_one_name_and_a_role_s_name_are_faults()
    {
        var ramp = """ "ramp": { "type": "gradient" } """;
        var both = Assert.Single(Faults(string.Join(",", ramp, Out("o", "ramp", "\"role\": \"height\", \"name\": \"edge\""))));
        Assert.Equal(("o", "name"), (both.Node, both.Parameter));
        Assert.Equal("node 'o' parameter 'name': an output has a role or a name, not both; drop role to name an extra image, or name to give a role", both.Message);

        var twice = Assert.Single(Faults(string.Join(",", ramp, Out("a", "ramp", "\"name\": \"edge\""), Out("b", "ramp", "\"name\": \"edge\""))));
        Assert.Equal("node 'b' parameter 'name': edge is already the name of output 'a'; a graph has one output per name", twice.Message);

        var role = Assert.Single(Faults(string.Join(",", ramp, Out("a", "ramp", "\"name\": \"height\""))));
        Assert.Equal("node 'a' parameter 'name': height is a role; give it as role, or name the output something else", role.Message);

        var bad = Assert.Single(Faults(string.Join(",", ramp, Out("a", "ramp", "\"name\": \"Edge\""))));
        Assert.Equal("node 'a' parameter 'name' is \"Edge\", but must be a name, [a-z][a-z0-9_]* (lower case letters, digits and _, starting with a letter)", bad.Message);

        var height = Assert.Single(Faults(string.Join(",", ramp, Out("a", "ramp", "\"role\": \"height\""), Out("b", "ramp", "\"role\": \"height\""))));
        Assert.Equal("node 'b' parameter 'role': height is already the role of output 'a'; a graph has one output per role", height.Message);
    }
}
