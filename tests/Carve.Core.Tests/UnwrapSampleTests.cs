using Xunit;
using Carve.Core;

namespace Carve.Core.Tests;

/// <summary>
/// samples/barrel.carve (T-56) is <see cref="TestMeshes.SeamedBarrel"/> unwrapped with the defaults, written by
/// <see cref="CarveFile.Write(PolygonMesh)"/>; kept equal to what Core makes, as exact text. Set
/// <c>CARVE_WRITE_BARREL=1</c> to write it again.
/// </summary>
public class UnwrapSampleTests
{
    private static string SamplePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "samples", "gatehouse.carve"))) dir = dir.Parent!;
        return Path.Combine(dir.FullName, "samples", "barrel.carve");
    }

    public static string Made()
    {
        var (result, errors) = Unwrap.Run(TestMeshes.SeamedBarrel());
        Assert.Empty(errors);
        return CarveFile.Write(result!.Mesh).ReplaceLineEndings("\n");
    }

    [Fact]
    public void The_barrel_sample_equals_what_core_makes()
    {
        var made = Made();
        if (Environment.GetEnvironmentVariable("CARVE_WRITE_BARREL") == "1") File.WriteAllText(SamplePath(), made);
        var text = File.ReadAllText(SamplePath()).ReplaceLineEndings("\n");
        Assert.Equal(made, text);

        var read = CarveFile.Read(text);
        Assert.True(read.Model is not null, string.Join("; ", read.Errors));
        Assert.Equal(5, read.Version);
        var mesh = read.Model!.Meshes.Single().Mesh;
        Assert.Equal(34, mesh.Uvs!.MappedFaces.Count);
        Assert.Equal(TestMeshes.BarrelSeams.Length, mesh.Seams.Count);
        Assert.Equal(made, CarveFile.Write(read.Model).ReplaceLineEndings("\n"));
    }
}
