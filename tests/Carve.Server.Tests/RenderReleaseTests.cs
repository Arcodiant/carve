using System.Globalization;
using System.Text.RegularExpressions;

namespace Carve.Server.Tests;

/// <summary>
/// The real <c>carve</c> releases the textures and materials no model uses once it draws without them (RV-47, RV-48): one
/// face's material redefined between renders, plain, textured from one PNG, from another, then the UV layout drawn (no
/// material, but the model still has it), then plain again, with the engine's own counts read after each by the tiny scenario
/// (<c>Tiny:Mode=release</c>), since the product shows them through no tool.
/// </summary>
[Trait("Tier", "regression")]
public sealed partial class RenderReleaseTests : IDisposable
{
    private static readonly TimeSpan Run = TimeSpan.FromSeconds(120);

    private readonly string _scratch = Directory.CreateTempSubdirectory("carve-release-tests-").FullName;

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    [Fact]
    public async Task A_redefined_material_is_released_with_its_texture_once_the_mesh_draws_the_new_one()
    {
        var a = Path.Combine(_scratch, "a.png");
        var b = Path.Combine(_scratch, "b.png");
        File.WriteAllBytes(a, TestPng.Marker);
        File.Copy(Path.Combine(Paths.RepoRoot, "tests", "Carve.Server.Tests", "Fixtures", "swatch.png"), b);

        using var carve = new CarveProcess(
        [
            $"--Carve:Extensions:0={Paths.DebugExtensionDll}",
            $"--Carve:Extensions:1={Paths.TinyScenarioDll}",
            $"--Carve:Debug:RunFolder={Path.Combine(_scratch, "run")}",
            "--Tiny:Mode=release",
            $"--Tiny:TextureA={a}",
            $"--Tiny:TextureB={b}",
        ]);
        Assert.True(0 == await carve.WaitForExitAsync(Run), carve.Stderr);

        var counts = ResidencyLine().Matches(carve.Stderr)
            .Select(m => (Textures: Int(m, "textures"), Table: Int(m, "table"))).ToArray();
        Assert.True(counts.Length == 5, carve.Stderr);
        var plain = counts[0];
        // The textured material adds its one texture; redefined from the other PNG, the first texture and material go, and
        // each material takes the table entry the last one gave back.
        Assert.Equal(plain.Textures + 1, counts[1].Textures);
        Assert.Equal(plain.Textures + 1, counts[2].Textures);
        Assert.Equal(plain.Table, counts[1].Table);
        Assert.Equal(plain.Table, counts[2].Table);
        // The UV layout makes things of its own, so it is compared only with what follows it: while it draws no material,
        // the one the model has is kept with its texture, and redefined plain, that texture goes.
        Assert.Equal(counts[3].Textures - 1, counts[4].Textures);
        Assert.Equal(counts[3].Table, counts[4].Table);
    }

    private static int Int(Match m, string group) => int.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"tiny: residency \d textures (?<textures>\d+) table (?<table>\d+)")]
    private static partial Regex ResidencyLine();
}
