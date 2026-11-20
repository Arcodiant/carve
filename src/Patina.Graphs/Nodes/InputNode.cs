using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

namespace Patina.Graphs.Nodes;

/// <summary>
/// <c>input</c> (T-140, format version 2): one of the graph's image inputs, a port of the <c>graph</c> node that uses it.
/// Parameters <c>name</c> (<c>[a-z][a-z0-9_]*</c>, the port's name), <c>kind</c> <c>grey</c> or <c>colour</c>, and an
/// optional <c>default</c> (a number from 0 to 1 for grey, <c>"#rrggbb"</c> for colour). One output port, <c>out</c>,
/// of its kind. On its own the graph evaluates the default; a graph with an input lacking one cannot be evaluated on its
/// own (<see cref="GraphFault.OnItsOwn"/>) but still saves and is used as a node.
/// <para>When the graph is used as a node, expansion (<see cref="GraphValidator"/>) wires what the outer graph gives the
/// port to <see cref="ValuePort"/>, and the node passes it through unchanged; a file never wires that port itself (a
/// fault). Its kind is checked against <c>kind</c>, so a mismatch is named on the outer node's input.</para>
/// </summary>
public sealed class InputNode : NodeType
{
    public const string TypeName = "input";

    /// <summary>The port expansion wires to the outer graph's source; never written in a file.</summary>
    public const string ValuePort = "value";

    public override string Name => TypeName;

    public override IReadOnlyList<InputPort> Inputs { get; } = [new(ValuePort, PortKind.Any, Required: false)];

    public override IReadOnlyList<ParamSpec> Parameters { get; } =
        [ParamSpec.Identifier("name"), ParamSpec.Enum("kind", "grey", "grey", "colour"), ParamSpec.GreyOrColour("default")];

    public static PortKind KindOf(NodeParameters parameters) => parameters.Text("kind") == "colour" ? PortKind.Colour : PortKind.Grey;

    /// <summary>The input's port name, or null when the node gives none (a fault).</summary>
    public static string? PortName(NodeParameters parameters) => parameters.Has("name") ? parameters.Text("name") : null;

    /// <summary>What is wrong with <c>default</c> for this <c>kind</c>, or null (also when there is none).</summary>
    public static string? CheckDefault(NodeParameters parameters)
    {
        if (!parameters.Has("default")) return null;
        var value = parameters.Value("default");
        var kind = KindOf(parameters);
        if (kind == PortKind.Grey && value.Kind != ParamValueKind.Number)
            return $"is {value}, but the input is grey, so its default is a number from 0 to 1";
        if (kind == PortKind.Colour && value.Kind != ParamValueKind.Text)
            return $"is {value}, but the input is colour, so its default is a colour \"#rrggbb\"";
        return null;
    }

    public override PortKind OutputKind(string port, IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters) => KindOf(parameters);

    public override IEnumerable<(string Port, string Message)> CheckInputKinds(IReadOnlyDictionary<string, PortKind> inputs, NodeParameters parameters)
    {
        var takes = KindOf(parameters);
        if (inputs.TryGetValue(ValuePort, out var gives) && gives != takes)
            yield return (ValuePort, $"takes {PortKinds.Name(takes)}, but it is given {PortKinds.Name(gives)}");
    }

    public override void Evaluate(NodeEvaluation e)
    {
        var output = e.Output();
        var colour = KindOf(e.Parameters) == PortKind.Colour;
        if (e.Input(ValuePort) is { } given)
        {
            for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
                for (var x = e.Rect.X; x < e.Rect.Right; x++)
                    if (colour) { var (r, g, b) = given.Colour(x, y); output.SetColour(x, y, r, g, b); }
                    else output.SetGrey(x, y, given.Grey(x, y));
            return;
        }
        if (!e.Parameters.Has("default")) throw new InvalidOperationException($"input '{e.NodeId}' has no default and nothing connected");
        var value = e.Parameters.Value("default");
        float fr, fg, fb;
        if (colour)
        {
            ParamSpec.TryParseColour(value.Text!, out var c);
            (fr, fg, fb) = (c.R / 255f, c.G / 255f, c.B / 255f);
        }
        else fr = fg = fb = (float)value.Number;
        for (var y = e.Rect.Y; y < e.Rect.Bottom; y++)
        {
            var row = output.Row(y);
            if (!colour) { row.Fill(fr); continue; }
            for (var i = 0; i < row.Length; i += 3) { row[i] = fr; row[i + 1] = fg; row[i + 2] = fb; }
        }
    }
}
