namespace Patina.Graphs.Nodes;

/// <summary>
/// The node types a graph may use, by name. It is immutable: <see cref="BuiltIn"/> holds this package's types, and
/// <see cref="With"/> gives a new registry with a type made elsewhere (M-29's mesh nodes from Carve), so no global
/// state changes and the package knows nothing of what is added to it.
/// </summary>
public sealed class NodeRegistry
{
    /// <summary>The built-in types, one line each.</summary>
    public static NodeRegistry BuiltIn { get; } = new(
    [
        new ColourNode(),
        new OutputNode(),
        new NoiseNode(),
        new GradientNode(),
        new LevelsNode(),
        new BlendNode(),
        new HeightToNormalNode(),
        new TilesNode(),
        new StripesNode(),
        new ShapeNode(),
        new WorleyNode(),
        new FbmNode(),
        new WarpNode(),
        new BlurNode(),
        new GradientMapNode(),
        new InputNode(),
        new NestedGraphNode(),
        new TransformNode(),
        new DistanceNode(),
    ]);

    private readonly IReadOnlyDictionary<string, NodeType> _types;

    public NodeRegistry(IEnumerable<NodeType> types)
    {
        var map = new SortedDictionary<string, NodeType>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            if (!GraphNames.IsValid(type.Name)) throw new ArgumentException($"a type's name is {GraphNames.Pattern}, not '{type.Name}'");
            if (!map.TryAdd(type.Name, type)) throw new ArgumentException($"type '{type.Name}' is registered twice");
            foreach (var port in type.Inputs.Select(p => p.Name).Concat(type.Outputs))
                if (!GraphNames.IsValid(port)) throw new ArgumentException($"type '{type.Name}': a port's name is {GraphNames.Pattern}, not '{port}'");
            if (type.Inputs.Select(p => p.Name).Distinct().Count() != type.Inputs.Count) throw new ArgumentException($"type '{type.Name}' names an input twice");
            if (type.Parameters.Select(p => p.Name).Distinct().Count() != type.Parameters.Count) throw new ArgumentException($"type '{type.Name}' names a parameter twice");
        }
        _types = map;
    }

    /// <summary>A registry with these types and <paramref name="types"/> too; a name already registered is refused.</summary>
    public NodeRegistry With(params NodeType[] types) => new(_types.Values.Concat(types));

    /// <summary>The names, in ordinal order, as a fault lists them.</summary>
    public IReadOnlyList<string> Names => [.. _types.Keys];

    public NodeType? Find(string name) => _types.TryGetValue(name, out var t) ? t : null;
}
