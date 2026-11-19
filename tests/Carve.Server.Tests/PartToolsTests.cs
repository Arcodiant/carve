extern alias carve;
using System.Numerics;
using System.Text.Json;
using Carve.Rover;
using DefinePartTool = carve::Carve.Server.DefinePartTool;
using GetPartsTool = carve::Carve.Server.GetPartsTool;
using KeepLevelTool = carve::Carve.Server.KeepLevelTool;

namespace Carve.Server.Tests;

/// <summary>T-93: define_part, get_parts, keep_level, the edits' part targeting and check's envelope warnings, on the rover, in process.</summary>
[Trait("Tier", "unit")]
public class PartToolsTests
{
    private static async Task<ToolHarness> StartAsync()
    {
        var carve = new ToolHarness();
        await carve.InitializeAsync();
        return carve;
    }

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static string Refused(JsonElement result)
    {
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private static float[] V(Vector3 v) => [v.X, v.Y, v.Z];

    /// <summary>define_part's arguments for one of RoverSpec's records, every field given.</summary>
    internal static string PartArguments(RoverSpec.PartSpec p)
    {
        var args = new Dictionary<string, object?>
        {
            ["part"] = p.Name,
            ["parent"] = p.Parent,
            ["translation"] = V(p.Translation),
            ["rotation"] = new[] { p.Rotation.X, p.Rotation.Y, p.Rotation.Z, p.Rotation.W },
            ["envelope"] = p.Envelope is { } e ? new { min = V(e.Min), max = V(e.Max) } : null,
            ["moves"] = p.Moves,
        };
        if (p.InstanceOf is not null) args["instance_of"] = p.InstanceOf;
        return JsonSerializer.Serialize(args);
    }

    internal static string MeshArguments(string name, RoverSpec.Shape shape) =>
        JsonSerializer.Serialize(new { name, vertices = shape.Positions.Select(V), faces = shape.Faces });

    /// <summary>The rover's blockout through the tools: the parts that are not instances, their geometry, then the instances.</summary>
    internal static async Task BuildBlockoutAsync(ToolHarness carve)
    {
        foreach (var p in RoverSpec.Tree.Where(p => p.InstanceOf is null))
            Assert.True(Ok(await carve.CallToolAsync("define_part", PartArguments(p))).GetProperty("created").GetBoolean());
        foreach (var (name, shape) in RoverSpec.Blockout) Ok(await carve.CallToolAsync("define_mesh", MeshArguments(name, shape)));
        foreach (var p in RoverSpec.Tree.Where(p => p.InstanceOf is not null)) Ok(await carve.CallToolAsync("define_part", PartArguments(p)));
    }

    private static async Task<JsonElement[]> PartsAsync(ToolHarness carve) =>
        [.. Ok(await carve.CallToolAsync("get_parts", "{}")).GetProperty("parts").EnumerateArray()];

    private static float[] Floats(JsonElement a) => [.. a.EnumerateArray().Select(x => x.GetSingle())];

    private static void Near(float[] expected, JsonElement actual) =>
        Assert.True(expected.Zip(Floats(actual)).All(z => MathF.Abs(z.First - z.Second) < 1e-5f), $"expected [{string.Join(", ", expected)}], got {actual}");

    [Fact]
    public async Task The_rover_built_through_the_tools_reads_back_as_RoverSpec_field_for_field()
    {
        await using var carve = await StartAsync();
        await BuildBlockoutAsync(carve);
        var parts = await PartsAsync(carve);

        Assert.Equal(RoverSpec.Tree.Select(p => p.Name), parts.Select(p => p.GetProperty("name").GetString()));
        foreach (var (spec, got) in RoverSpec.Tree.Zip(parts))
        {
            Assert.Equal(spec.Parent, got.GetProperty("parent").GetString());
            Assert.Equal(V(spec.Translation), Floats(got.GetProperty("translation")));
            Assert.Equal(new[] { spec.Rotation.X, spec.Rotation.Y, spec.Rotation.Z, spec.Rotation.W }, Floats(got.GetProperty("rotation")));
            Assert.Equal(new[] { 1f, 1f, 1f }, Floats(got.GetProperty("scale")));
            if (spec.Envelope is { } e)
            {
                Assert.Equal(V(e.Min), Floats(got.GetProperty("envelope").GetProperty("min")));
                Assert.Equal(V(e.Max), Floats(got.GetProperty("envelope").GetProperty("max")));
            }
            else Assert.Equal(JsonValueKind.Null, got.GetProperty("envelope").ValueKind);
            Assert.Equal(spec.InstanceOf, got.GetProperty("instance_of").GetString());
            Assert.Equal(spec.Moves, got.GetProperty("moves").GetBoolean());
            Assert.Equal(RoverSpec.GeometryParts.Contains(spec.Name), got.GetProperty("geometry").GetBoolean());
            var shown = spec.InstanceOf ?? spec.Name;
            var shape = RoverSpec.Blockout.GetValueOrDefault(shown);
            Assert.Equal(shape?.Positions.Length ?? 0, got.GetProperty("vertices").GetInt32());
            Assert.Equal(shape?.Faces.Length ?? 0, got.GetProperty("faces").GetInt32());
            Assert.Empty(got.GetProperty("levels").EnumerateArray());
            Assert.Empty(got.GetProperty("warnings").EnumerateArray());
            if (shape is null)
            {
                Assert.Equal(JsonValueKind.Null, got.GetProperty("local_bounds").ValueKind);
                Assert.Equal(JsonValueKind.Null, got.GetProperty("world_bounds").ValueKind);
            }
        }

        // World bounds at level 0, as T-91's As built gives them.
        var byName = parts.ToDictionary(p => p.GetProperty("name").GetString()!);
        void World(string name, float[] min, float[] max)
        {
            Near(min, byName[name].GetProperty("world_bounds").GetProperty("min"));
            Near(max, byName[name].GetProperty("world_bounds").GetProperty("max"));
        }
        World("Chassis", [-1, 0.4f, -0.6f], [1, 0.8f, 0.6f]);
        World("Cab", [0.1f, 0.8f, -0.5f], [0.9f, 1.4f, 0.5f]);
        World("Mast", [-0.65f, 0.8f, -0.05f], [-0.55f, 1.6f, 0.05f]);
        World("Lamp", [-0.6f, 1.5f, -0.1f], [-0.3f, 1.7f, 0.1f]);
        World("Wheel_FL", [0.3f, 0, 0.62f], [1.1f, 0.8f, 0.82f]);
        World("Wheel_FR", [0.3f, 0, -0.82f], [1.1f, 0.8f, -0.62f]);
        World("Wheel_RL", [-1.1f, 0, 0.62f], [-0.3f, 0.8f, 0.82f]);
        World("Wheel_RR", [-1.1f, 0, -0.82f], [-0.3f, 0.8f, -0.62f]);
        Near([-0.4f, -0.4f, -0.1f], byName["Wheel_FR"].GetProperty("local_bounds").GetProperty("min"));
    }

    [Fact]
    public async Task Get_parts_gives_the_worked_example_entry_and_keep_level_its_example_output()
    {
        await using var carve = await StartAsync();
        await BuildBlockoutAsync(carve);
        var kept = await carve.CallToolAsync("keep_level", KeepLevelTool.Example);
        Assert.EndsWith("\nOutput: " + TextOf(kept), KeepLevelTool.Text);
        var text = TextOf(await carve.CallToolAsync("get_parts", "{}"));
        Assert.True(GetPartsTool.ExampleEntry != "" && text.Contains(GetPartsTool.ExampleEntry, StringComparison.Ordinal), text);
        Assert.EndsWith("Output entry: " + GetPartsTool.ExampleEntry, GetPartsTool.Text);
    }

    [Fact]
    public async Task Define_parts_worked_example_runs_on_the_rover()
    {
        await using var carve = await StartAsync();
        foreach (var p in RoverSpec.Tree.Where(p => p.InstanceOf is null)) Ok(await carve.CallToolAsync("define_part", PartArguments(p)));
        foreach (var (name, shape) in RoverSpec.Blockout) Ok(await carve.CallToolAsync("define_mesh", MeshArguments(name, shape)));
        Assert.EndsWith("Example: " + DefinePartTool.Example, DefinePartTool.Text);
        var body = Ok(await carve.CallToolAsync("define_part", DefinePartTool.Example));
        Assert.True(body.GetProperty("created").GetBoolean());
        var part = body.GetProperty("part");
        Assert.Equal("Wheel_FL", part.GetProperty("instance_of").GetString());
        Assert.Equal("Axle_F", part.GetProperty("parent").GetString());
    }

    [Fact]
    public async Task An_existing_part_changes_only_the_fields_given_and_keeps_its_levels()
    {
        await using var carve = await StartAsync();
        await BuildBlockoutAsync(carve);
        Ok(await carve.CallToolAsync("keep_level", """{"level":0}"""));
        var body = Ok(await carve.CallToolAsync("define_part", """{"part":"Mast","rotation":[0,0.7071068,0,0.7071068]}"""));
        Assert.False(body.GetProperty("created").GetBoolean());
        var mast = body.GetProperty("part");
        Assert.Equal("Chassis", mast.GetProperty("parent").GetString());
        Assert.Equal(new[] { -0.6f, 0.8f, 0 }, Floats(mast.GetProperty("translation")));
        Assert.True(mast.GetProperty("moves").GetBoolean());
        Assert.Equal(JsonValueKind.Object, mast.GetProperty("envelope").ValueKind);
        Assert.Equal([0], mast.GetProperty("levels").EnumerateArray().Select(x => x.GetInt32()));
        // The lamp swings from +X to -Z with its mast.
        var lamp = (await PartsAsync(carve)).Single(p => p.GetProperty("name").GetString() == "Lamp");
        Near([-0.7f, 1.5f, -0.3f], lamp.GetProperty("world_bounds").GetProperty("min"));
        Near([-0.5f, 1.7f, 0], lamp.GetProperty("world_bounds").GetProperty("max"));

        // null clears the envelope and makes a part top-level.
        mast = Ok(await carve.CallToolAsync("define_part", """{"part":"Mast","envelope":null,"parent":null}""")).GetProperty("part");
        Assert.Equal(JsonValueKind.Null, mast.GetProperty("envelope").ValueKind);
        Assert.Equal(JsonValueKind.Null, mast.GetProperty("parent").ValueKind);
    }

    [Fact]
    public async Task Define_mesh_on_a_part_keeps_its_placement_and_levels_and_refuses_an_instance()
    {
        await using var carve = await StartAsync();
        await BuildBlockoutAsync(carve);
        Ok(await carve.CallToolAsync("keep_level", """{"level":0}"""));
        Ok(await carve.CallToolAsync("define_mesh", MeshArguments("Wheel_FL", RoverSpec.RefinedWheel)));
        var wheel = (await PartsAsync(carve)).Single(p => p.GetProperty("name").GetString() == "Wheel_FL");
        Assert.Equal("Axle_F", wheel.GetProperty("parent").GetString());
        Assert.Equal(32, wheel.GetProperty("vertices").GetInt32());
        Assert.Equal([0], wheel.GetProperty("levels").EnumerateArray().Select(x => x.GetInt32()));

        Assert.Contains("name: 'Wheel_FR' is an instance of 'Wheel_FL'",
            Refused(await carve.CallToolAsync("define_mesh", MeshArguments("Wheel_FR", RoverSpec.RefinedWheel))));
        // A new name is a top-level part at the origin, as before.
        var crate = Ok(await carve.CallToolAsync("define_mesh", MeshArguments("Crate", RoverSpec.Box(Vector3.Zero, Vector3.One))));
        var made = (await PartsAsync(carve)).Last();
        Assert.Equal("Crate", made.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, made.GetProperty("parent").ValueKind);
        Assert.Equal(new[] { 0f, 0, 0 }, Floats(made.GetProperty("translation")));
        Assert.Equal(8, crate.GetProperty("vertices").GetInt32());
    }

    [Fact]
    public async Task Keep_level_keeps_levels_per_part_and_an_equal_level_replaces()
    {
        await using var carve = await StartAsync();
        await BuildBlockoutAsync(carve);
        Ok(await carve.CallToolAsync("keep_level", """{"level":0}"""));
        Ok(await carve.CallToolAsync("define_mesh", MeshArguments("Wheel_FL", RoverSpec.RefinedWheel)));
        var body = Ok(await carve.CallToolAsync("keep_level", """{"level":1,"parts":["Wheel_FL","Cab"]}"""));
        Assert.Equal("""{"level":1,"kept":[{"part":"Cab","levels":[0,1]},{"part":"Wheel_FL","levels":[0,1]}]}""", body.GetRawText());
        Ok(await carve.CallToolAsync("keep_level", """{"level":1,"parts":["Cab"]}"""));
        var parts = await PartsAsync(carve);
        Assert.Equal([0, 1], parts.Single(p => p.GetProperty("name").GetString() == "Wheel_RR").GetProperty("levels").EnumerateArray().Select(x => x.GetInt32()));
        Assert.Equal([0], parts.Single(p => p.GetProperty("name").GetString() == "Mast").GetProperty("levels").EnumerateArray().Select(x => x.GetInt32()));
        Assert.Equal(carve.Store.Current.Model.Find("Cab"), carve.Store.Current.Model.FindPart("Cab")!.LevelAt(1)!.Mesh);
    }

    [Fact]
    public async Task An_edit_on_Wheel_FL_shows_in_the_world_bounds_of_all_four_wheels()
    {
        await using var carve = await StartAsync();
        await BuildBlockoutAsync(carve);
        string[] wheels = ["Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR"];
        var before = (await PartsAsync(carve)).Where(p => wheels.Contains(p.GetProperty("name").GetString())).ToArray();
        Assert.All(before, w => Assert.Equal(0.8f, w.GetProperty("world_bounds").GetProperty("max")[1].GetSingle(), 1e-5f));
        var (part, face, by) = RoverSpec.HubMove;
        Ok(await carve.CallToolAsync("move", JsonSerializer.Serialize(new { mesh = part, faces = new[] { face }, by = V(by) })));
        var after = (await PartsAsync(carve)).Where(p => wheels.Contains(p.GetProperty("name").GetString())).ToArray();
        Assert.Equal(4, after.Length);
        foreach (var (b, a) in before.Zip(after))
        {
            Assert.Equal(0.9f, a.GetProperty("world_bounds").GetProperty("max")[1].GetSingle(), 1e-5f);
            Assert.Equal(b.GetProperty("world_bounds").GetProperty("min").GetRawText(), a.GetProperty("world_bounds").GetProperty("min").GetRawText());
        }
    }

    [Fact]
    public async Task Check_on_the_rover_with_Axle_F_lowered_gives_exactly_the_envelope_warning()
    {
        await using var carve = await StartAsync();
        await BuildBlockoutAsync(carve);
        var quiet = Ok(await carve.CallToolAsync("check", """{"mesh":"Chassis"}"""));
        Assert.True(quiet.GetProperty("ok").GetBoolean());
        Assert.Empty(quiet.GetProperty("warnings").EnumerateArray());

        var lowered = RoverSpec.AxleLoweredTranslation;
        Ok(await carve.CallToolAsync("define_part", JsonSerializer.Serialize(new { part = "Axle_F", translation = V(lowered) })));
        const string warning = "'Axle_F': its envelope, placed in 'Chassis', goes below the envelope of 'Chassis' on y by 0.3 m";
        foreach (var mesh in new[] { "Chassis", "Wheel_FL" })
        {
            var body = Ok(await carve.CallToolAsync("check", JsonSerializer.Serialize(new { mesh })));
            Assert.False(body.GetProperty("ok").GetBoolean());
            Assert.Equal([warning], body.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()));
            Assert.Empty(body.GetProperty("problems").EnumerateArray());
        }
        var axle = (await PartsAsync(carve)).Single(p => p.GetProperty("name").GetString() == "Axle_F");
        Assert.Equal([warning], axle.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()));
    }

    [Theory]
    [InlineData("""{"part":"Cab","parent":"Axel_F"}""", "parent: no part named 'Axel_F'; the parts are")]
    [InlineData("""{"part":"Chassis","parent":"Lamp"}""", "parent: 'Chassis' would be its own ancestor")]
    [InlineData("""{"part":"Cab","rotation":[0,1,0,1]}""", "rotation: not a unit quaternion: its length is 1.41421")]
    [InlineData("""{"part":"Cab","rotation":[0,1,0]}""", "rotation: expected 4 numbers [x, y, z, w], got 3 element(s)")]
    [InlineData("""{"part":"Cab","scale":[1,-1,1]}""", "scale: each component must be > 0 (a mirroring scale is refused")]
    [InlineData("""{"part":"Cab","scale":[1,0,1]}""", "scale: each component must be > 0")]
    [InlineData("""{"part":"Cab","translation":[0,"up",0]}""", "translation[1]: expected a finite number, got a string")]
    [InlineData("""{"part":"Cab","envelope":{"min":[1,0,0],"max":[0,1,1]}}""", "envelope: min is above max on x")]
    [InlineData("""{"part":"Cab","envelope":{"min":[0,0,0]}}""", "envelope.max: required but missing")]
    [InlineData("""{"part":"Cab","envelope":{"min":[0,0,0],"max":[1,1,1],"mid":[0,0,0]}}""", "envelope.mid: an envelope takes only min and max")]
    [InlineData("""{"part":"Cab","envelope":[0,0,0]}""", "envelope: expected {min, max} or null, got an array")]
    [InlineData("""{"part":"Spare","instance_of":"Axle_F"}""", "instance_of: 'Axle_F' has no geometry")]
    [InlineData("""{"part":"Spare","instance_of":"Wheel_RL"}""", "instance_of: 'Wheel_RL' is itself an instance of 'Wheel_FL'; name 'Wheel_FL'")]
    [InlineData("""{"part":"Spare","instance_of":"Nope"}""", "instance_of: no part named 'Nope'")]
    [InlineData("""{"part":"Cab","instance_of":"Wheel_FL"}""", "instance_of: 'Cab' already exists with geometry of its own, and an existing part's instance_of does not change")]
    [InlineData("""{"part":"Axle_R","instance_of":"Wheel_FL"}""", "instance_of: 'Axle_R' already exists as an empty part, and an existing part's instance_of does not change; give a new name to make an instance")]
    [InlineData("""{"part":"Wheel_RL","instance_of":"Cab"}""", "instance_of: 'Wheel_RL' is already an instance of 'Wheel_FL'")]
    [InlineData("""{"part":"Spare","instance_of":"Spare"}""", "instance_of: 'Spare' cannot be an instance of itself")]
    [InlineData("""{"part":"a:b"}""", "part: a part name must not contain ':'")]
    [InlineData("""{"part":""}""", "part: a part name must not be empty")]
    [InlineData("""{"parent":"Chassis"}""", "part: required but missing")]
    [InlineData("""{"part":"Cab","moves":"yes"}""", "moves: expected true or false, got a string")]
    [InlineData("""{"part":"Cab","parent":3}""", "parent: expected a part name or null, got 3")]
    [InlineData("""{"part":"Cab","pivot":[0,0,0]}""", "pivot: not an argument of define_part")]
    public async Task Define_part_refuses_with_a_tool_error_naming_the_argument(string arguments, string expected)
    {
        await using var carve = await StartAsync();
        await BuildBlockoutAsync(carve);
        var version = carve.Store.Current.Version;
        var text = Refused(await carve.CallToolAsync("define_part", arguments));
        Assert.Contains(expected, text);
        Assert.StartsWith("define_part failed with", text);
        Assert.Equal(version, carve.Store.Current.Version);
    }

    [Theory]
    [InlineData("""{"level":0,"parts":["Wheel_FR"]}""", "parts[0]: 'Wheel_FR' is an instance of 'Wheel_FL'; keep a level of 'Wheel_FL', and every instance shows it")]
    [InlineData("""{"level":0,"parts":["Cab","Axle_F"]}""", "parts[1]: 'Axle_F' is an empty part with no geometry to keep as a level")]
    [InlineData("""{"level":0,"parts":["Nope"]}""", "parts[0]: no part named 'Nope'")]
    [InlineData("""{"level":0,"parts":["Cab","Cab"]}""", "parts[1]: 'Cab' is named twice")]
    [InlineData("""{"level":0,"parts":[]}""", "parts: empty;")]
    [InlineData("""{"level":-1}""", "level: -1 is negative")]
    [InlineData("""{"level":1.5}""", "level: expected a whole number, got 1.5")]
    [InlineData("""{"parts":["Cab"]}""", "level: required but missing")]
    [InlineData("""{"level":0,"part":"Cab"}""", "part: not an argument of keep_level")]
    [InlineData("""{"level":1}""", "level: the newest kept level of 'Chassis' is 2; a new level must be 2 or more (2 replaces it)")]
    public async Task Keep_level_refuses_with_a_tool_error(string arguments, string expected)
    {
        await using var carve = await StartAsync();
        await BuildBlockoutAsync(carve);
        Ok(await carve.CallToolAsync("keep_level", """{"level":2,"parts":["Chassis"]}"""));
        var version = carve.Store.Current.Version;
        var text = Refused(await carve.CallToolAsync("keep_level", arguments));
        Assert.Contains(expected, text);
        Assert.Equal(version, carve.Store.Current.Version);
    }

    [Fact]
    public async Task Keep_level_on_an_empty_model_says_there_is_no_mesh()
    {
        await using var carve = await StartAsync();
        Assert.Contains("there is no mesh yet", Refused(await carve.CallToolAsync("keep_level", """{"level":0}""")));
        Assert.Equal("""{"parts":[]}""", TextOf(await carve.CallToolAsync("get_parts", "{}")));
        Assert.Contains("x: not an argument of get_parts, which takes none", Refused(await carve.CallToolAsync("get_parts", """{"x":1}""")));
    }

    [Theory]
    [InlineData("move", """{"mesh":"Wheel_FR","faces":[0],"by":[0,1,0]}""", "mesh: 'Wheel_FR' is an instance of 'Wheel_FL'; edit 'Wheel_FL', and every instance follows")]
    [InlineData("move", """{"mesh":"Axle_F","faces":[0],"by":[0,1,0]}""", "mesh: 'Axle_F' is an empty part with no geometry")]
    [InlineData("select", """{"mesh":"Wheel_RR","kind":"faces","where":"normal.y > 0.9"}""", "mesh: 'Wheel_RR' is an instance of 'Wheel_FL'")]
    [InlineData("check", """{"mesh":"Axle_R"}""", "mesh: 'Axle_R' is an empty part with no geometry")]
    public async Task An_edit_on_an_instance_or_an_empty_is_refused_with_T91s_text(string tool, string arguments, string expected)
    {
        await using var carve = await StartAsync();
        await BuildBlockoutAsync(carve);
        Assert.Contains(expected, Refused(await carve.CallToolAsync(tool, arguments)));
    }

    [Fact]
    public async Task Edits_work_in_the_parts_own_space()
    {
        await using var carve = await StartAsync();
        await BuildBlockoutAsync(carve);
        // The Cab's top is at y 0.6 in its own space, 1.4 in the model's; select and move see 0.6.
        var top = Ok(await carve.CallToolAsync("move", """{"mesh":"Cab","faces":[3],"by":[0,0.1,0]}"""));
        Assert.Equal(0.7f, top.GetProperty("bounds").GetProperty("max")[1].GetSingle(), 1e-6f);
        var cab = (await PartsAsync(carve)).Single(p => p.GetProperty("name").GetString() == "Cab");
        Assert.Equal(1.5f, cab.GetProperty("world_bounds").GetProperty("max")[1].GetSingle(), 1e-5f);
        Assert.Equal(0.7f, cab.GetProperty("local_bounds").GetProperty("max")[1].GetSingle(), 1e-6f);
    }

    [Fact]
    public async Task Undo_steps_back_each_new_tool()
    {
        await using var carve = await StartAsync();
        await BuildBlockoutAsync(carve);
        var model = carve.Store.Current.Model;

        Ok(await carve.CallToolAsync("define_part", """{"part":"Spare","parent":"Chassis"}"""));
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Null(carve.Store.Current.Model.FindPart("Spare"));
        Assert.Same(model, carve.Store.Current.Model);

        Ok(await carve.CallToolAsync("define_part", """{"part":"Cab","translation":[0,2,0],"envelope":null}"""));
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Same(model, carve.Store.Current.Model);

        Ok(await carve.CallToolAsync("keep_level", """{"level":0}"""));
        Assert.Contains("keep_level", TextOf(await carve.CallToolAsync("undo", "{}")));
        Assert.Same(model, carve.Store.Current.Model);
        Assert.Empty(carve.Store.Current.Model.FindPart("Cab")!.Levels);

        // get_parts adds no step: undo after it steps back the instance made last.
        await PartsAsync(carve);
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.Null(carve.Store.Current.Model.FindPart("Wheel_RR"));
    }

    [Fact]
    public async Task Tools_list_gives_the_three_new_tools_with_their_schemas()
    {
        await using var carve = await StartAsync();
        var tools = (await carve.ListToolsAsync()).EnumerateArray().ToDictionary(t => t.GetProperty("name").GetString()!);
        void Schema(string name, string[] properties, string[] required)
        {
            var schema = tools[name].GetProperty("inputSchema");
            Assert.Equal(properties, schema.GetProperty("properties").EnumerateObject().Select(p => p.Name));
            Assert.Equal(required, schema.TryGetProperty("required", out var r) ? r.EnumerateArray().Select(x => x.GetString()!) : []);
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        }
        Schema("define_part", ["part", "parent", "translation", "rotation", "scale", "envelope", "instance_of", "moves"], ["part"]);
        Schema("get_parts", [], []);
        Schema("keep_level", ["level", "parts"], ["level"]);
        Assert.Equal(DefinePartTool.Text, tools["define_part"].GetProperty("description").GetString());
        Assert.Equal(GetPartsTool.Text, tools["get_parts"].GetProperty("description").GetString());
        Assert.Equal(KeepLevelTool.Text, tools["keep_level"].GetProperty("description").GetString());
        var define = tools["define_part"].GetProperty("inputSchema").GetProperty("properties");
        Assert.Equal(4, define.GetProperty("rotation").GetProperty("minItems").GetInt32());
        Assert.Equal("""["string","null"]""", define.GetProperty("parent").GetProperty("type").GetRawText());
        Assert.Equal("""["object","null"]""", define.GetProperty("envelope").GetProperty("type").GetRawText());
    }
}
