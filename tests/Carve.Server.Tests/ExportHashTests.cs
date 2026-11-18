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
        // M-19's bytes, not main's 259F5B54...: T-84's shoulder fix changed the soft gardener. 7ac5790 (M-19 before M-7) writes these
        // same 64 hex digits. The person's ruling of 2026-11-17 ("Accept all", M-19 row 8), as the M-6 pin was re-pinned on 2026-11-16.
        Assert.Equal("F67B98974F443A7BA59A8694E547FCD2416C3303232025E2A4319C468237BD63", Hash(SkinnedExport.Write(model, "gardener-soft").Bytes));
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
