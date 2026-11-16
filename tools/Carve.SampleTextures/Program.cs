using Carve.SampleTextures;

// Usage: dotnet run --project tools/Carve.SampleTextures [output folder]
// With no argument it writes samples/textures beside Carve.slnx.
var folder = args.Length > 0 ? args[0] : Path.Combine(RepoRoot.Find(), "samples", "textures");
Directory.CreateDirectory(folder);
foreach (var (name, rgba) in TextureSet.Build())
{
    var png = Png.Encode(rgba, TextureSet.Size, TextureSet.Size);
    File.WriteAllBytes(Path.Combine(folder, name), png);
    Console.Error.WriteLine($"{name}: {png.Length} bytes");
}
return 0;
