extern alias carve;
using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-94: render's part and level argument rules and get_mesh's placement and level, on the rover, in process (no device needed).</summary>
[Trait("Tier", "unit")]
public class RenderPartArgumentTests
{
    private static async Task<ToolHarness> RoverAsync(bool refine = false)
    {
        var carve = new ToolHarness();
        await carve.InitializeAsync();
        await RoverTools.BuildAsync(carve.CallToolAsync, refine);
        return carve;
    }

    private static string Refused(JsonElement result)
    {
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    [Fact]
    public async Task A_part_the_model_lacks_is_refused_with_the_parts_listed()
    {
        await using var carve = await RoverAsync();
        var text = Refused(await carve.CallToolAsync("render", """{"part":"Axle_X"}"""));
        Assert.Contains("part: there is no part named 'Axle_X'; the parts are 'Chassis'", text);
        Assert.Contains("'Wheel_RR'", text);
    }

    [Theory]
    [InlineData("""{"level":-1}""", "level: -1 is not a level; a kept level is 0 or more (keep_level)")]
    [InlineData("""{"level":0,"pose":{}}""", "level: not with pose; a kept level is drawn as it was kept, at rest")]
    [InlineData("""{"level":0,"mode":"uvs","mesh":"Cab"}""", "level: not with mode 'uvs'; the layout is the live mesh's")]
    [InlineData("""{"part":"Cab","mode":"uvs","mesh":"Cab"}""", "part: not with mode 'uvs'; the layout is one mesh's, named by mesh")]
    public async Task A_level_below_0_or_with_what_moves_it_is_refused(string arguments, string expected)
    {
        await using var carve = await RoverAsync();
        Assert.Contains(expected, Refused(await carve.CallToolAsync("render", arguments)));
    }

    [Fact]
    public async Task A_mesh_outside_the_parts_subtree_is_refused_with_the_subtree_listed()
    {
        await using var carve = await RoverAsync();
        var text = Refused(await carve.CallToolAsync("render", """{"part":"Axle_F","mesh":"Cab","labels":"faces"}"""));
        Assert.Contains("mesh: 'Cab' is not drawn with part 'Axle_F'; the parts under it are 'Axle_F', 'Wheel_FL'", text);
        Assert.Contains("'Wheel_FR'", text);
        Assert.DoesNotContain("'Wheel_RL'", text);
        // The rear wheels are an instance's subtree away: Wheel_FL is not under Axle_R either.
        Assert.Contains("mesh: 'Wheel_FL' is not drawn with part 'Axle_R'", Refused(await carve.CallToolAsync("render", """{"part":"Axle_R","mesh":"Wheel_FL"}""")));
    }

    [Fact]
    public async Task Get_mesh_gives_the_parts_parent_and_world_placement_and_keeps_part_local_coordinates()
    {
        await using var carve = await RoverAsync();
        var wheel = Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"Wheel_FL"}"""));
        Assert.Equal("Axle_F", wheel.GetProperty("parent").GetString());
        var world = wheel.GetProperty("world");
        Assert.Equal([0.7f, 0.4f, 0.72f], world.GetProperty("translation").EnumerateArray().Select(v => v.GetSingle()), new Near());
        Assert.Equal([0f, 0f, 0f, 1f], world.GetProperty("rotation").EnumerateArray().Select(v => v.GetSingle()), new Near());
        Assert.Equal([1f, 1f, 1f], world.GetProperty("scale").EnumerateArray().Select(v => v.GetSingle()), new Near());
        // Part-local: the wheel's own bounds, centred on its axle, not where it is placed.
        Assert.Equal(-0.4f, wheel.GetProperty("bounds").GetProperty("min")[0].GetSingle(), 4);
        Assert.False(wheel.TryGetProperty("level", out _));

        var chassis = Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"Chassis"}"""));
        Assert.Equal(JsonValueKind.Null, chassis.GetProperty("parent").ValueKind);
        Assert.Equal([0f, 0f, 0f], chassis.GetProperty("world").GetProperty("translation").EnumerateArray().Select(v => v.GetSingle()), new Near());
    }

    /// <summary>RV-71: a crate under an empty scaled [2,1,1], turned by <paramref name="degrees"/> about Y.</summary>
    private static async Task<JsonElement> CrateUnderScaleAsync(ToolHarness carve, float degrees)
    {
        await carve.CallToolAsync("define_mesh", PartToolsTests.MeshArguments("Crate", Carve.Rover.RoverSpec.Box(System.Numerics.Vector3.Zero, System.Numerics.Vector3.One)));
        Ok(await carve.CallToolAsync("define_part", """{"part":"Base","scale":[2,1,1]}"""));
        var turn = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, degrees * MathF.PI / 180);
        Ok(await carve.CallToolAsync("define_part", JsonSerializer.Serialize(new { part = "Crate", parent = "Base", rotation = new[] { turn.X, turn.Y, turn.Z, turn.W } })));
        return Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"Crate"}"""));
    }

    private const string Shear = "world: the transform of part 'Crate' is not a translation, rotation and scale, since a non-uniform scale above a rotation makes a shear, "
        + "so world only approximates where the part is drawn; give that ancestor a uniform scale, or turn the part so the scale's axes stay its own";

    [Fact]
    public async Task Get_mesh_warns_when_world_is_a_shear_that_no_translation_rotation_and_scale_holds()
    {
        await using var carve = new ToolHarness();
        await carve.InitializeAsync();
        var crate = await CrateUnderScaleAsync(carve, 30);
        Assert.Contains(Shear, crate.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()));
        Assert.True(crate.TryGetProperty("world", out _));
    }

    [Fact]
    public async Task Get_mesh_gives_no_world_warning_when_the_transform_is_exact()
    {
        await using var carve = new ToolHarness();
        await carve.InitializeAsync();
        // A quarter turn keeps the scale's axes: the world is a translation, rotation and scale.
        var crate = await CrateUnderScaleAsync(carve, 90);
        Assert.DoesNotContain(crate.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.StartsWith("world:", StringComparison.Ordinal));
        await using var rover = await RoverAsync();
        // Every rover part, under the shared test export uses (RV-72).
        foreach (var mesh in Carve.Rover.RoverSpec.Build().Meshes.Select(m => m.Name))
            Assert.DoesNotContain(Ok(await rover.CallToolAsync("get_mesh", JsonSerializer.Serialize(new { mesh }))).GetProperty("warnings").EnumerateArray(),
                w => w.GetString()!.StartsWith("world:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Get_mesh_level_reads_the_newest_kept_level_at_or_below_it_and_names_both()
    {
        await using var carve = await RoverAsync(refine: true);
        var live = Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"Wheel_FL"}"""));
        Assert.Equal(64, live.GetProperty("vertices").GetArrayLength());
        var zero = Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"Wheel_FL","level":0}"""));
        Assert.Equal(16, zero.GetProperty("vertices").GetArrayLength());
        Assert.Equal(0, zero.GetProperty("level").GetProperty("given").GetInt32());
        Assert.Equal(0, zero.GetProperty("level").GetProperty("shown").GetInt32());
        // The Mast is kept at level 0 only: level 5 shows it.
        var mast = Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"Mast","level":5}"""));
        Assert.Equal(5, mast.GetProperty("level").GetProperty("given").GetInt32());
        Assert.Equal(0, mast.GetProperty("level").GetProperty("shown").GetInt32());

        Assert.Contains("level: -1 is not a level", Refused(await carve.CallToolAsync("get_mesh", """{"mesh":"Cab","level":-1}""")));
        await carve.CallToolAsync("define_mesh", PartToolsTests.MeshArguments("Crate", Carve.Rover.RoverSpec.Box(System.Numerics.Vector3.Zero, System.Numerics.Vector3.One)));
        Assert.Contains("level: 'Crate' has no kept level; keep_level keeps one", Refused(await carve.CallToolAsync("get_mesh", """{"mesh":"Crate","level":0}""")));
    }

    private sealed class Near : IEqualityComparer<float>
    {
        public bool Equals(float a, float b) => Math.Abs(a - b) < 1e-5f;
        public int GetHashCode(float v) => 0;
    }
}
