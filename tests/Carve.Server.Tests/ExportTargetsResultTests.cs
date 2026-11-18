using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-107: <c>export</c>'s result names the morph targets it wrote, in glTF target order, and says nothing of targets for a model with none.</summary>
[Trait("Tier", "unit")]
public sealed class ExportTargetsResultTests() : TempFolderTests("carve-export-targets")
{
    private async Task<ToolHarness> StartAsync()
    {
        var carve = new ToolHarness(workingDirectory: Root);
        try
        {
            await carve.InitializeAsync();
            var tools = await carve.ListToolsAsync();
            Assert.False(IsError(await carve.CallToolAsync("define_mesh", ToolList.Cube(tools))));
            return carve;
        }
        catch
        {
            await carve.DisposeAsync();
            throw;
        }
    }

    [Fact]
    public async Task A_model_with_targets_names_them_in_order_and_one_without_says_nothing_of_targets()
    {
        await using var carve = await StartAsync();
        var plain = await carve.CallToolAsync("export", """{"path":"plain.glb"}""");
        Assert.False(IsError(plain), TextOf(plain));
        Assert.DoesNotContain("morph", TextOf(plain));

        Assert.False(IsError(await carve.CallToolAsync("define_target", """{"name":"Girth","positions":{"0":[-0.5,0,0]}}""")));
        Assert.False(IsError(await carve.CallToolAsync("define_target", """{"name":"Slim","positions":{"1":[0.5,0,0]}}""")));
        var withTargets = await carve.CallToolAsync("export", """{"path":"dials.glb"}""");
        Assert.False(IsError(withTargets), TextOf(withTargets));
        Assert.Contains("2 morph target(s) 'Girth', 'Slim' in glTF target order", TextOf(withTargets));

        // The names are the file's: what the result says is what the .glb holds, in that order.
        var parsed = GlbExportTests.Parser.Parse(File.ReadAllBytes(Path.Combine(Root, "dials.glb")), Empyrean.ModelFormat.Gltf);
        Assert.Equal(["Girth", "Slim"], parsed.MorphTargets!.TargetNames);
    }

    [Fact]
    public async Task A_skinned_export_names_the_targets_and_a_piece_names_only_its_own()
    {
        // RV-64: the skinned and piece result lines go through Skinned, not the static path above.
        using var carve = await SkinnedExportToolTests.StartChainAsync(Root);
        Assert.False(IsError(await carve.CallToolAsync("define_target", """{"mesh":"upper","name":"Bend","positions":{"0":[0.3,1,0]}}""")));

        var skinned = await carve.CallToolAsync("export", """{"path":"chain.glb"}""");
        Assert.False(IsError(skinned), TextOf(skinned));
        Assert.Contains("skinned", TextOf(skinned));
        Assert.Contains("1 morph target(s) 'Bend' in glTF target order", TextOf(skinned));

        var upper = await carve.CallToolAsync("export", """{"path":"chain.upper.glb","mode":"piece","meshes":["upper"]}""");
        Assert.False(IsError(upper), TextOf(upper));
        Assert.Contains("as a piece", TextOf(upper));
        Assert.Contains("1 morph target(s) 'Bend' in glTF target order", TextOf(upper));

        var lower = await carve.CallToolAsync("export", """{"path":"chain.lower.glb","mode":"piece","meshes":["lower"]}""");
        Assert.False(IsError(lower), TextOf(lower));
        Assert.DoesNotContain("morph", TextOf(lower));
    }
}
