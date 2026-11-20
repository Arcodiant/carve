using System.Text.Json;
using StbImageSharp;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>
/// render_glb samples a file's textures as render samples a material's (T-124, ruling 1 of T-78, interim until engine need
/// 8): a face textured with texel noise, much finer than the frame's pixels, is drawn by both and held to D-6's frame rule.
/// With the loader's default (no mip maps, so Nearest) the noise survives in render_glb's frame and the rule fails.
/// </summary>
[Trait("Tier", "regression")]
public sealed class RenderGlbSamplingTests() : FileToolTests("carve-render-glb-sampling")
{
    private const int Texels = 1024;

    private static readonly string Frames =
        Directory.CreateDirectory(Path.Combine(Paths.RepoRoot, "artifacts", "render-glb-sampling-tests")).FullName;

    /// <summary>Each texel black or white by an integer hash of its index: no System.Random, the same file every run.</summary>
    private static (byte R, byte G, byte B) Noise(int x, int y)
    {
        var h = (uint)(y * Texels + x) * 0x9E3779B1u;
        h ^= h >> 15;
        h *= 0x85EBCA77u;
        h ^= h >> 13;
        var v = (h & 1) == 0 ? (byte)0 : (byte)255;
        return (v, v, v);
    }

    private static ImageResult Frame(JsonElement result, string keepAs)
    {
        Assert.False(IsError(result), IsError(result) ? TextOf(result) : "");
        var png = Convert.FromBase64String(result.GetProperty("content")[0].GetProperty("data").GetString()!);
        File.WriteAllBytes(Path.Combine(Frames, keepAs + ".png"), png);
        return ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
    }

    [Fact]
    public async Task A_fine_texture_is_drawn_by_render_glb_as_render_draws_it()
    {
        var (process, _) = await StartProcessAsync(mesh: null);
        using var carve = process;
        File.WriteAllBytes(Path.Combine(Root, "noise.png"), TestPng.Encode(Texels, Texels, Noise));
        Ok(await carve.CallToolAsync("define_mesh", MaterialRenderTests.Box("mesh", 0)));
        Ok(await carve.CallToolAsync("set_uvs", MaterialRenderTests.FrontUvs("mesh")));
        Ok(await carve.CallToolAsync("define_material", """{"name":"noise","base_colour_texture":"noise.png"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"faces":[1],"material":"noise"}"""));
        Ok(await carve.CallToolAsync("export", """{"path":"noise.glb"}"""));

        var render = Frame(await carve.CallToolAsync("render", """{"view":"+z"}"""), "noise-render");
        var glb = Frame(await carve.CallToolAsync("render_glb", """{"path":"noise.glb","view":"+z"}"""), "noise-render-glb");

        // D-6's frame rule: fails past 0.5 % of the pixels differing or an 8-connected patch past 50.
        var (differing, patch) = RenderGlbTargetsTests.Measure(render, glb);
        var limit = render.Width * render.Height * 0.005;
        Assert.True(differing <= limit && patch <= 50,
            $"render_glb against render on a texel-noise face: {differing} px differ (limit {limit}), largest patch {patch} (limit 50)");
    }
}
