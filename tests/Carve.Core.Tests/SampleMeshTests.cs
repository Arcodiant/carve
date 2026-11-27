using Xunit;

namespace Carve.Core.Tests;

/// <summary>
/// T-197: fps-arms' cage and tool sources, copied into <c>samples/</c> byte for byte (fps-arms <c>main</c> at 0813e3a,
/// <c>work/</c>), read with the counts measured at M-23's planning.
/// </summary>
[Trait("Tier", "unit")]
public class SampleMeshTests
{
    [Theory]
    [InlineData("arms-cage.carve", 600, 590)]
    [InlineData("cutter-src.carve", 232, 170)]
    [InlineData("toolkit-src.carve", 200, 146)]
    [InlineData("scanner-src.carve", 124, 84)]
    public void Each_sample_reads_with_its_counts(string file, int vertices, int faces)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "samples", file))) dir = dir.Parent!;
        var read = CarveFile.Read(File.ReadAllText(Path.Combine(dir.FullName, "samples", file)));
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        var mesh = Assert.Single(read.Model!.Meshes).Mesh;
        Assert.Equal((vertices, faces), (mesh.LiveVertexCount, mesh.LiveFaceCount));
    }
}
