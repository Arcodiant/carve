using System.Security.Cryptography;
using Carve.Core;

namespace Carve.Server.Tests;

/// <summary>
/// T-76's byte-identical guard, committed (RV-47): the soft gardener's skinned export and a static export with colour faces
/// (the gatehouse, whose door is a colour) are the bytes written before M-7 (SHA-256, taken at <c>5bc4559</c>, main before
/// materials), so a change to the colour primitives fails here.
/// </summary>
[Trait("Tier", "unit")]
public class ExportHashTests
{
    private static Model Sample(string file)
    {
        var path = Path.Combine(Paths.RepoRoot, "samples", file);
        var read = CarveFile.Read(File.ReadAllText(path), Path.GetDirectoryName(path));
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        return read.Model!;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    [Fact]
    public void The_soft_gardeners_skinned_export_is_the_bytes_written_before_materials()
    {
        var model = Sample("gardener-soft.carve");
        Assert.Equal("259F5B541BA81994C926175AD001172BA3A48DC83FC351E83B54AC2AC9912D1B", Hash(SkinnedExport.Write(model, "gardener-soft").Bytes));
    }

    [Fact]
    public void A_static_export_with_colour_faces_is_the_bytes_written_before_materials()
    {
        var model = Sample("gatehouse.carve");
        Assert.Contains(model.Meshes, m => m.Mesh.Style is { Colours.Count: > 0 });
        var mesh = model.Combined!;
        Assert.Equal("43A4CC0AF6963602D5705111B196CFA88CC52777DDC2D2CC1FECB3525F755BB4", Hash(GlbExport.Write(mesh, Shading.Triangles(mesh), "gatehouse", model.Materials)));
    }
}
