using System.Numerics;
using System.Text.Json;
using Carve.LoadChecks;
using Carve.ModelViewer.Tests;
using Empyrean;
using Xunit;
using CarveFile = Carve.Core.CarveFile;
using CarveMaterial = Carve.Core.Material;
using CarveTexture = Carve.Core.MaterialTexture;
using FaceStyle = Carve.Core.FaceStyle;

namespace Carve.MaterialsLoad.Tests;

/// <summary>
/// What a game sees of the exported textured barrel, and what the viewer drew of it: the last step of suite <c>scenario-materials</c>. The run
/// folder comes from <c>CARVE_MATERIALS_RUN</c>, which <c>scripts/run-materials-scenario.ps1</c> sets; with it unset every test is skipped (the
/// script fails a run that skipped one).
/// </summary>
[Trait("Tier", "regression")]
public class MaterialsLoadTests
{
    private static readonly string? Run = Environment.GetEnvironmentVariable("CARVE_MATERIALS_RUN");

    private static string RunFolder => Run ?? throw new InvalidOperationException("CARVE_MATERIALS_RUN is unset");

    private const int M = 12, T = 80;

    private static CarveTexture Tex(string file)
    {
        var path = RunContent.SamplePath("textures/" + file);
        return CarveTexture.FromPng(path, File.ReadAllBytes(path), out var problem) ?? throw new InvalidOperationException($"{file}: {problem}");
    }

    /// <summary>The Scope's table, with the textures of <c>samples/textures/</c>.</summary>
    private static CarveMaterial[] Table() =>
    [
        new("staves", BaseColourTexture: Tex("staves-base.png"), NormalTexture: Tex("staves-normal.png"), OcclusionTexture: Tex("staves-occlusion.png")),
        new("iron", BaseColourTexture: Tex("iron-base.png"), MetallicRoughnessTexture: Tex("iron-mr.png"), Metallic: 1f, Roughness: 1f),
        new("end-grain", BaseColourTexture: Tex("end-grain-base.png"), NormalTexture: Tex("end-grain-normal.png")),
        new("rune-stave", BaseColourTexture: Tex("staves-base.png"), NormalTexture: Tex("staves-normal.png"), OcclusionTexture: Tex("staves-occlusion.png"), EmissiveTexture: Tex("rune-emissive.png"), Emissive: "#FF8020"),
    ];

    [SkippableOrRun]
    public void The_glb_loads_four_materials_with_every_field_and_texture_of_the_table_and_no_warning()
    {
        var model = RunContent.Loader(RunFolder).Load(new ContentPath("models/barrel.glb"));
        Assert.Empty(model.Warnings);
        var loaded = model.SubmeshMaterials.Select(m => m!).ToArray();
        Assert.Equal(4, loaded.Length);

        foreach (var want in Table())
        {
            var matches = loaded.Where(m => Same(want, m)).ToArray();
            Assert.True(matches.Length == 1, $"material '{want.Name}': {matches.Length} of the four loaded materials match every field and texture");
        }
    }

    private static bool Same(CarveMaterial want, ParsedMaterial got)
    {
        var emissive = FaceStyle.ToLinear(want.Emissive);
        return got.Pbr
            && got.BaseColor == FaceStyle.ToLinear(want.BaseColour)
            && got.Metallic == want.Metallic
            && got.Roughness == want.Roughness
            && got.EmissiveColor == new Vector3(emissive.X, emissive.Y, emissive.Z)
            && got.Occlusion == want.Occlusion
            && got.DoubleSided == want.DoubleSided
            && Bytes(want.BaseColourTexture, got.TextureBytes)
            && Bytes(want.MetallicRoughnessTexture, got.MetallicRoughnessTexture?.Bytes)
            && Bytes(want.NormalTexture, got.NormalTexture?.Bytes)
            && Bytes(want.EmissiveTexture, got.EmissiveTexture?.Bytes)
            && Bytes(want.OcclusionTexture, got.OcclusionTexture?.Bytes);
    }

    private static bool Bytes(Carve.Core.MaterialSlot? want, byte[]? got) => want is null ? got is null : got is not null && ((CarveTexture)want).Png.ToArray().SequenceEqual(got);

    /// <summary>A file read with its texture paths resolved against its own folder.</summary>
    private static Carve.Core.Model Read(string path)
    {
        var read = CarveFile.Read(File.ReadAllText(path), Path.GetDirectoryName(path));
        return read.Model ?? throw new InvalidOperationException($"{path}: {string.Join("; ", read.Errors)}");
    }

    [SkippableOrRun]
    public void The_saved_file_is_the_sample_by_content()
    {
        var saved = Read(Path.Combine(RunFolder, "barrel-textured.carve"));
        var sample = Read(RunContent.SamplePath("barrel-textured.carve"));
        Assert.Equal(
            CarveFile.Write(sample, Path.GetDirectoryName(RunContent.SamplePath("barrel-textured.carve"))!),
            CarveFile.Write(saved, RunFolder));
        Assert.Equal(4, sample.Materials.Count);
    }

    private static JsonElement View => JsonDocument.Parse(File.ReadAllText(Path.Combine(RunFolder, "viewer", "view.json"))).RootElement;

    [SkippableOrRun]
    public void The_viewer_reports_four_pbr_materials_with_the_tables_fields()
    {
        Assert.True(View.GetProperty("sky").GetBoolean());
        var reported = View.GetProperty("materials").EnumerateArray().ToArray();
        Assert.Equal(4, reported.Length);
        Assert.All(reported, m => Assert.True(m.GetProperty("pbr").GetBoolean()));

        foreach (var want in Table())
        {
            var matches = reported.Where(m => Reports(want, m)).ToArray();
            Assert.True(matches.Length == 1, $"material '{want.Name}': {matches.Length} of the viewer's four materials match");
        }
    }

    private static bool Reports(CarveMaterial want, JsonElement m)
    {
        float[] Floats(string key) => [.. m.GetProperty(key).EnumerateArray().Select(n => n.GetSingle())];
        var colour = FaceStyle.ToLinear(want.BaseColour);
        var emissive = FaceStyle.ToLinear(want.Emissive);
        bool Slot(string slot, Carve.Core.MaterialSlot? texture)
        {
            var s = m.GetProperty("textures").GetProperty(slot);
            return s.GetProperty("set").GetBoolean() == (texture is not null)
                && (texture is null || (s.GetProperty("width").GetInt32() == 256 && s.GetProperty("height").GetInt32() == 256));
        }

        return Floats("baseColor").SequenceEqual([colour.X, colour.Y, colour.Z, colour.W])
            && m.GetProperty("metallic").GetSingle() == want.Metallic
            && m.GetProperty("roughness").GetSingle() == want.Roughness
            && Floats("emissive").SequenceEqual([emissive.X, emissive.Y, emissive.Z])
            && m.GetProperty("occlusion").GetSingle() == want.Occlusion
            && m.GetProperty("doubleSided").GetBoolean() == want.DoubleSided
            && Slot("baseColor", want.BaseColourTexture) && Slot("metallicRoughness", want.MetallicRoughnessTexture)
            && Slot("normal", want.NormalTexture) && Slot("emissive", want.EmissiveTexture) && Slot("occlusion", want.OcclusionTexture);
    }

    private static int Max((int R, int G, int B) p) => Math.Max(p.R, Math.Max(p.G, p.B));

    private static bool Warm((int R, int G, int B) p) => p.R >= p.G + M && p.G >= p.B + M;

    /// <summary>
    /// The viewer's frames (tonemapped, its own light) by relations only, never values (P7): the rune's texels are warm and brighter than the plain
    /// wood of their face, rust is warm and clean iron is not and is lit, in both frames, at the sites the scenario worked from the geometry.
    /// </summary>
    [SkippableOrRun]
    public void The_viewer_frames_show_the_rune_the_rust_and_the_clean_iron_by_hue_and_order()
    {
        var sites = File.ReadAllLines(Path.Combine(RunFolder, "viewer-samples.tsv")).Select(l => l.Split('\t')).ToArray();
        foreach (var view in new[] { "viewer-front", "viewer-angle" })
        {
            var frame = new FramePixels(Path.Combine(RunFolder, "viewer", view + ".png"));
            (int R, int G, int B)[] Of(string look) => [.. sites.Where(s => s[0] == view && s[3] == look).Select(s => frame.Pixel(int.Parse(s[1]), int.Parse(s[2])))];
            var (clean, rust, on, off) = (Of("Clean"), Of("Rust"), Of("RuneOn"), Of("RuneOff"));

            Assert.True(clean.Length >= 8 && rust.Length >= 8, $"{view}: {clean.Length} clean and {rust.Length} rust samples, at least 8 each");
            Assert.All(clean, p => Assert.False(Warm(p) || Max(p) < T, $"{view}: clean iron {p} is warm or near-black"));
            Assert.All(rust, p => Assert.True(Warm(p), $"{view}: rust {p} is not ordered R > G > B"));
            Assert.True(on.Length >= 8 && off.Length >= 8, $"{view}: {on.Length} rune and {off.Length} plain samples, at least 8 each");
            Assert.All(on, p => Assert.True(Warm(p), $"{view}: rune {p} is not warm"));
            Assert.True(on.Min(Max) >= off.Max(Max) + M, $"{view}: the rune's least bright texel {on.Min(Max)} is not {M} above plain wood's brightest {off.Max(Max)}");

            // P5: the sites tell a v-flipped texture and a u-mirrored one apart (the scenario worked the flags from the geometry), at least 3 of each.
            foreach (var (what, looks) in new[] { ("iron", new[] { "Clean", "Rust" }), ("rune", new[] { "RuneOn" }) })
            {
                var of = sites.Where(s => s[0] == view && looks.Contains(s[3])).ToArray();
                int v = of.Count(s => s[5] == "True"), u = of.Count(s => s[6] == "True");
                Assert.True(v >= 3 && u >= 3, $"{view}: {what} sites that change class under a v-flip {v}, under a u-mirror {u}, at least 3 each");
            }
        }
    }
}

/// <summary>A fact that is skipped when the run folder is not given.</summary>
public sealed class SkippableOrRunAttribute : FactAttribute
{
    public SkippableOrRunAttribute()
    {
        if (Environment.GetEnvironmentVariable("CARVE_MATERIALS_RUN") is null)
        {
            Skip = "CARVE_MATERIALS_RUN is unset";
        }
    }
}
