namespace Patina.Graphs;

/// <summary>
/// One thing wrong with a graph that still loads: the node it is on, the input port or the parameter it concerns
/// (at most one of the two), and a message that names them itself and says what would be right (the types there are,
/// the ids there are, the range), so a server can return the list as it stands. <see cref="Node"/> is null only for
/// the one fault of the whole graph, that it has no output.
/// </summary>
public sealed record GraphFault(string? Node, string? Port, string? Parameter, string Message)
{
    public override string ToString() => Message;
}

/// <summary>Why a file could not be loaded at all: it is not JSON, has not the file's shape, or has a version this build does not know. Line and column are 1-based.</summary>
public sealed record GraphLoadError(int Line, int Column, string Message)
{
    public override string ToString() => $"line {Line}, column {Column}: {Message}";
}

/// <summary>The outcome of <see cref="TexGraphFile.Read"/>: a graph and its faults, or the load error that stopped it.</summary>
public sealed record TexGraphReadResult(TextureGraph? Graph, GraphLoadError? Error, IReadOnlyList<GraphFault> Faults)
{
    public bool Loaded => Graph is not null;
}

/// <summary>Thrown when a graph with faults is given to something that needs it whole, such as the evaluator.</summary>
public sealed class GraphFaultException(IReadOnlyList<GraphFault> faults)
    : InvalidOperationException("the graph has faults: " + string.Join("; ", faults.Select(f => f.Message)))
{
    public IReadOnlyList<GraphFault> Faults { get; } = faults;
}
