using Patina.Graphs;

namespace Patina.Server;

/// <summary>A graph kept on the snapshot stack, with the label push_snapshot was given.</summary>
public sealed record Snapshot(string? Label, TextureGraph Graph);

/// <summary>
/// The session's one open graph, its path once save or open has given it one, and the snapshot stack. A graph is a
/// value, so a snapshot is the graph itself and popping it restores it byte for byte. Tools hold <see cref="Gate"/>
/// for the whole of a call, so a call sees and leaves one state. Nothing here is ever written to a file but the graph.
/// </summary>
public sealed class GraphStore
{
    private readonly List<Snapshot> _stack = [];

    public object Gate { get; } = new();

    /// <summary>The open graph; null until define_graph or open gives one.</summary>
    public TextureGraph? Graph { get; private set; }

    /// <summary>The file the open graph was last saved to or opened from; null for a defined graph not yet saved.</summary>
    public string? Path { get; private set; }

    public int Depth => _stack.Count;

    /// <summary>A new open graph (define_graph, open): the path is the given one and the stack is cleared.</summary>
    public void Replace(TextureGraph graph, string? path)
    {
        Graph = graph;
        Path = path;
        _stack.Clear();
    }

    /// <summary>The open graph after an edit; the path and the stack are kept.</summary>
    public void Edit(TextureGraph graph) => Graph = graph;

    public void Saved(string path) => Path = path;

    /// <summary>Copies the open graph onto the stack; the new depth.</summary>
    public int Push(string? label)
    {
        _stack.Add(new(label, Graph ?? throw new InvalidOperationException("no graph to push")));
        return _stack.Count;
    }

    /// <summary>Restores the graph on top of the stack and removes it; null on an empty stack.</summary>
    public Snapshot? Pop()
    {
        if (_stack.Count == 0) return null;
        var top = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        Graph = top.Graph;
        return top;
    }
}

/// <summary>What a tool resolves a relative file path against: <see cref="Directory"/>, or the working directory when null.</summary>
public sealed record ToolFolder(string? Directory);
