using Patina.Graphs;
using Patina.Graphs.Evaluation;
using Patina.Graphs.Imaging;

// Usage: dotnet run --project tools/Patina.SampleGraphs [--canonical]
// Evaluates every samples/graphs/*.texgraph (beside Carve.slnx) at the graph's own size into samples/graphs/<name>/
// <role>.png, one PNG for each output the graph has. With --canonical it first rewrites each graph in the format's own
// canonical text (what TexGraphFile.Write gives).
var root = FindRoot();
var folder = Path.Combine(root, "samples", "graphs");
foreach (var path in Directory.GetFiles(folder, "*" + TexGraphFile.Extension).Order(StringComparer.Ordinal))
{
    var read = TexGraphFile.ReadFile(path);
    if (!read.Loaded) { Console.Error.WriteLine($"{path}: {read.Error}"); return 1; }
    if (read.Faults.Count > 0)
    {
        foreach (var fault in read.Faults) Console.Error.WriteLine($"{path}: {fault.Message}");
        return 1;
    }
    var graph = read.Graph!;
    if (args.Contains("--canonical")) TexGraphFile.WriteFile(path, graph);
    var evaluator = new GraphEvaluator(graph);
    var outFolder = Path.Combine(folder, Path.GetFileNameWithoutExtension(path));
    Directory.CreateDirectory(outFolder);
    // Each output's file: metallic is packed into roughness.png, so it has none of its own.
    foreach (var output in evaluator.Outputs.Where(o => o != OutputRoles.Name(OutputRole.Metallic)))
    {
        var image = OutputImage.Make(evaluator, output, graph.Size);
        File.WriteAllBytes(Path.Combine(outFolder, OutputRoles.FileOf(output)), image.Png);
        Console.Error.WriteLine($"{Path.GetFileName(outFolder)}/{OutputRoles.FileOf(output)}: {image.Png.Length} bytes");
    }
}
return 0;

static string FindRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Carve.slnx"))) return dir.FullName;
    throw new DirectoryNotFoundException("Carve.slnx not found above the current folder");
}
