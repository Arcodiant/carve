using Patina.Graphs;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

// Usage: dotnet run --project tools/Patina.SampleGraphs [--canonical]
// Evaluates every samples/graphs/*.texgraph (beside Carve.slnx) at the graph's own size into samples/graphs/<name>/
// <role>.png, one PNG for each output the graph has. With --canonical it first rewrites each graph in the format's own
// canonical text (what TexGraphFile.Write gives). Each variant below, a graph with its exposed parameters set to other
// values (T-139, through GraphParameters.WithOverrides, as swatch and export set them), goes into
// samples/graphs/<name>/variants/<variant>/<role>.png. A graph using another as a node (T-140) resolves its path against
// its own folder (TexGraphFile.ReadFile gives the graph its folder).
var variants = new (string Graph, string Variant, (string Name, ParamValue Value)[] Values)[]
{
    ("bricks", "grey", [("mortar", ParamValue.Of(0.08)), ("tint", ParamValue.Of("#7a7a70"))]), // grey stone bricks
};
var root = FindRoot();
var folder = Path.Combine(root, "samples", "graphs");
foreach (var path in Directory.GetFiles(folder, "*" + TexGraphFile.Extension).Order(StringComparer.Ordinal))
{
    var read = TexGraphFile.ReadFile(path);
    if (!read.Loaded) { Console.Error.WriteLine($"{path}: {read.Error}"); return 1; }
    // A graph with an input lacking a default (T-140) is only used as a node: it has nothing of its own to evaluate.
    if (read.Faults.Count > 0 && read.Faults.All(f => f.OnItsOwn))
    {
        Console.Error.WriteLine($"{path}: skipped, an input has no default so it is only used as a node: {string.Join("; ", read.Faults.Select(f => f.Message))}");
        continue;
    }
    if (read.Faults.Count > 0)
    {
        foreach (var fault in read.Faults) Console.Error.WriteLine($"{path}: {fault.Message}");
        return 1;
    }
    var graph = read.Graph!;
    if (args.Contains("--canonical")) TexGraphFile.WriteFile(path, graph);
    var name = Path.GetFileNameWithoutExtension(path);
    WritePngs(graph, Path.Combine(folder, name), name);
    foreach (var (_, variant, values) in variants.Where(v => v.Graph == name))
    {
        var set = GraphParameters.WithOverrides(graph, values.ToDictionary(v => v.Name, v => v.Value));
        if (!set.Ok) { Console.Error.WriteLine($"{path} variant {variant}: {set.Error}"); return 1; }
        WritePngs(set.Graph!, Path.Combine(folder, name, "variants", variant), $"{name}/variants/{variant}");
    }
}
return 0;

static void WritePngs(TextureGraph graph, string outFolder, string label)
{
    var evaluator = new GraphEvaluator(graph);
    Directory.CreateDirectory(outFolder);
    // Each output's file: metallic is packed into roughness.png, so it has none of its own.
    foreach (var output in evaluator.Outputs.Where(OutputRoles.HasOwnFile))
    {
        var image = OutputImage.Make(evaluator, output, graph.Size);
        File.WriteAllBytes(Path.Combine(outFolder, OutputRoles.FileOf(output)), image.Png);
        Console.Error.WriteLine($"{label}/{OutputRoles.FileOf(output)}: {image.Png.Length} bytes");
    }
}

static string FindRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Carve.slnx"))) return dir.FullName;
    throw new DirectoryNotFoundException("Carve.slnx not found above the current folder");
}
