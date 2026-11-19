using System.Numerics;
using System.Text.Json;
using Carve.LoadChecks;
using Carve.ModelViewer.Tests;
using Carve.Rover;
using Empyrean;
using Xunit;
using CarveFile = Carve.Core.CarveFile;
using CarveModel = Carve.Core.Model;
using FaceStyle = Carve.Core.FaceStyle;
using MeshReport = Carve.Core.MeshReport;

namespace Carve.PartsLoad.Tests;

/// <summary>
/// What a game sees of the exported rover, and what the viewer drew of it: the last step of suite <c>scenario-parts</c>. The run folder comes from
/// <c>CARVE_PARTS_RUN</c>, which <c>scripts/run-parts-scenario.ps1</c> sets; with it unset every test is skipped (the script fails a run that skipped one).
/// </summary>
[Trait("Tier", "regression")]
public class PartsLoadTests
{
    private static readonly string? Run = Environment.GetEnvironmentVariable("CARVE_PARTS_RUN");

    private static string RunFolder => Run ?? throw new InvalidOperationException("CARVE_PARTS_RUN is unset");

    private static readonly ContentPath Glb = new("models/rover.glb");

    private static readonly CarveModel Rover = RoverSpec.Build();

    private static ParsedModel Split() => RunContent.Loader(RunFolder).Load(Glb, new ModelParseOptions(SplitParts: true));

    private static int Triangles(ParsedPartMesh mesh) => mesh.MeshData.Indices!.Length / 3;

    private static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 1e-5f;

    // T-95's part checks, on the file the tools wrote and not the one Core wrote.

    [SkippableOrRun]
    public void The_split_load_keeps_the_root_the_mast_and_four_wheels_in_pre_order_with_no_warnings()
    {
        var parsed = Split();
        Assert.Empty(parsed.Warnings ?? []);
        var parts = parsed.Parts!.Parts;
        Assert.Equal(["rover", "Mast", "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR"], parts.Select(p => p.Name));
        Assert.Equal([-1, 0, 0, 0, 0, 0], parts.Select(p => p.ParentIndex));
    }

    [SkippableOrRun]
    public void Each_moving_parts_translation_rotation_and_scale_are_its_composed_ones()
    {
        var parts = Split().Parts!.Parts;
        foreach (var name in new[] { "Mast", "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" })
        {
            var part = parts.Single(p => p.Name == name);
            Assert.True(Matrix4x4.Decompose(Rover.WorldTransform(name)!.Value, out var scale, out var rotation, out var translation));
            Assert.True(Near(translation, part.Translation), $"{name}: {part.Translation} against {translation}");
            Assert.True(MathF.Abs(Quaternion.Dot(rotation, part.Rotation)) > 1 - 1e-6f, $"{name}: {part.Rotation} against {rotation}");
            Assert.True(Near(scale, part.Scale), $"{name}: scale {part.Scale}");
        }
    }

    [SkippableOrRun]
    public void The_front_left_wheel_the_mast_and_the_root_sit_where_the_sketch_puts_them()
    {
        var parts = Split().Parts!.Parts;
        Assert.True(Near(new Vector3(0.7f, 0.4f, 0.72f), parts.Single(p => p.Name == "Wheel_FL").Translation));
        Assert.True(Near(new Vector3(-0.6f, 0.8f, 0f), parts.Single(p => p.Name == "Mast").Translation));
        Assert.Equal(Quaternion.Identity, parts.Single(p => p.Name == "rover").Rotation);
        Assert.Equal(Vector3.Zero, parts.Single(p => p.Name == "rover").Translation);
    }

    [SkippableOrRun]
    public void The_four_wheels_share_one_mesh_and_each_part_has_the_spec_triangles()
    {
        var split = Split().Parts!;
        var parts = split.Parts;
        var wheel = parts.Single(p => p.Name == "Wheel_FL").MeshIndex;
        Assert.All(parts.Where(p => p.Name.StartsWith("Wheel_", StringComparison.Ordinal)), p => Assert.Equal(wheel, p.MeshIndex));
        Assert.Equal(3, split.Meshes.Length);
        Assert.Equal(40, Triangles(split.Meshes[parts.Single(p => p.Name == "rover").MeshIndex]));
        Assert.Equal(40, Triangles(split.Meshes[parts.Single(p => p.Name == "Mast").MeshIndex]));
        Assert.Equal(124, Triangles(split.Meshes[wheel]));
    }

    private static (Vector3 Min, Vector3 Max) BoundsOf(ParsedModel parsed)
    {
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var v in parsed.MeshData.Vertices)
        {
            min = Vector3.Min(min, v.Position);
            max = Vector3.Max(max, v.Position);
        }

        return (min, max);
    }

    [SkippableOrRun]
    public void The_merged_load_has_Combineds_bounds_and_576_triangles_and_the_split_loads_merged_mesh_is_the_same()
    {
        var report = MeshReport.Create(Rover.Combined!);
        foreach (var parsed in new[] { RunContent.Loader(RunFolder).Load(Glb), Split() })
        {
            Assert.Empty(parsed.Warnings ?? []);
            var (min, max) = BoundsOf(parsed);
            Assert.True(Near(report.BoundsMin, min), $"{min} against {report.BoundsMin}");
            Assert.True(Near(report.BoundsMax, max), $"{max} against {report.BoundsMax}");
            Assert.Equal(576, parsed.MeshData.Indices!.Length / 3);
        }
    }

    [SkippableOrRun]
    public void The_file_holds_the_five_agreed_colours_as_materials()
    {
        var loaded = RunContent.Loader(RunFolder).Load(Glb).SubmeshMaterials.Select(m => m!).ToArray();
        foreach (var hex in new[] { RoverSpec.Olive, RoverSpec.Sand, RoverSpec.DarkGrey, RoverSpec.LightGrey, RoverSpec.Yellow })
            Assert.Contains(loaded, m => m.BaseColor == FaceStyle.ToLinear(hex));
    }

    // The sample.

    private static CarveModel Read(string path)
    {
        var read = CarveFile.Read(File.ReadAllText(path), Path.GetDirectoryName(path));
        return read.Model ?? throw new InvalidOperationException($"{path}: {string.Join("; ", read.Errors)}");
    }

    [SkippableOrRun]
    public void The_saved_file_is_the_sample_by_content_and_the_sample_is_the_specs_model()
    {
        var samplePath = RunContent.SamplePath("rover.carve");
        var dir = Path.GetDirectoryName(samplePath)!;
        var sample = CarveFile.Write(Read(samplePath), dir);
        Assert.Equal(sample, CarveFile.Write(Read(Path.Combine(RunFolder, "parts", "rover.carve")), dir));
        Assert.Equal(sample, CarveFile.Write(Rover, dir));
        Assert.Contains("\"carve\": 7", File.ReadAllText(samplePath));
    }

    // The viewer.

    private static JsonElement View => JsonDocument.Parse(File.ReadAllText(Path.Combine(RunFolder, "viewer", "view.json"))).RootElement;

    [SkippableOrRun]
    public void The_viewer_loaded_the_rover_with_Combineds_bounds_and_the_two_agreed_views()
    {
        var view = View;
        var report = MeshReport.Create(Rover.Combined!);
        Assert.Equal("models/rover.glb", view.GetProperty("model").GetString());
        Assert.Equal(576, view.GetProperty("triangles").GetInt32());
        Assert.True(view.GetProperty("sky").GetBoolean());
        float[] Floats(JsonElement e) => [.. e.EnumerateArray().Select(n => n.GetSingle())];
        var bounds = view.GetProperty("bounds");
        Assert.True(Near(report.BoundsMin, new Vector3(Floats(bounds.GetProperty("min")))), $"{bounds}");
        Assert.True(Near(report.BoundsMax, new Vector3(Floats(bounds.GetProperty("max")))), $"{bounds}");
        Assert.Equal(["viewer-front", "viewer-angle"], view.GetProperty("views").EnumerateArray().Select(v => v.GetProperty("name").GetString()));
    }

    /// <summary>
    /// The viewer lists one material per submesh of its merged load, and a node instance contributes its mesh's primitives again:
    /// 6 mesh instances (rover, Mast, 4 wheels sharing one mesh) of 2 primitives each are 12 entries for a file of 6 materials.
    /// </summary>
    [SkippableOrRun]
    public void The_viewer_lists_a_material_per_submesh_so_the_four_wheel_instances_repeat_the_wheel_pair()
    {
        var listed = View.GetProperty("materials").EnumerateArray().Select(m => m.GetProperty("baseColor").ToString()).ToArray();
        Assert.Equal(12, listed.Length);
        Assert.Equal(6, listed.Distinct().Count());
        var loaded = RunContent.Loader(RunFolder).Load(Glb).SubmeshMaterials!;
        Assert.Equal(12, loaded.Length);
    }

    private enum Look { None, Olive, Sand, Dark, Yellow }

    /// <summary>
    /// The viewer's pixels (tonemapped, lit by its sun and the sky) by relations between the channels, never by value: the lens is warm and
    /// has little blue, the chassis is green over red over blue, the cab is a pale warm sand, the wheels are dark and a little blue.
    /// </summary>
    private static Look Classify((int R, int G, int B) p)
    {
        var (r, g, b) = p;
        var max = Math.Max(r, Math.Max(g, b));
        if (b * 2 < g && r >= g) return Look.Yellow;
        if (g >= r + 12 && g >= b + 30) return Look.Olive;
        if (max >= 150 && r >= b + 12 && g >= b + 8 && r - b < 70) return Look.Sand;
        if (max is >= 40 and <= 110 && b >= r && b - r <= 30) return Look.Dark;
        return Look.None;
    }

    private static Dictionary<Look, (int Count, double X)> Survey(string name)
    {
        var frame = new FramePixels(Path.Combine(RunFolder, "viewer", name + ".png"));
        var seen = new Dictionary<Look, (int Count, double X)>();
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var look = Classify(frame.Pixel(x, y));
                if (look == Look.None) continue;
                var (count, sum) = seen.GetValueOrDefault(look);
                seen[look] = (count + 1, sum + x);
            }
        }

        return seen;
    }

    [SkippableOrRun]
    public void The_viewer_frames_show_olive_sand_dark_grey_and_yellow_with_the_cab_right_of_the_lens()
    {
        foreach (var name in new[] { "viewer-front", "viewer-angle" })
        {
            var seen = Survey(name);
            Assert.True(seen.GetValueOrDefault(Look.Olive).Count >= 8000, $"{name}: {seen.GetValueOrDefault(Look.Olive).Count} olive pixels, at least 8000 (the chassis)");
            Assert.True(seen.GetValueOrDefault(Look.Sand).Count >= 8000, $"{name}: {seen.GetValueOrDefault(Look.Sand).Count} sand pixels, at least 8000 (the cab)");
            Assert.True(seen.GetValueOrDefault(Look.Dark).Count >= 8000, $"{name}: {seen.GetValueOrDefault(Look.Dark).Count} dark grey pixels, at least 8000 (the wheels)");
            Assert.True(seen.GetValueOrDefault(Look.Yellow).Count >= 60, $"{name}: {seen.GetValueOrDefault(Look.Yellow).Count} yellow pixels, at least 60 (the lens)");

            // The cab is at +X and the lamp at -X, and the camera sees +X to its right: a mirrored view puts the cab left of the lens.
            double Centre(Look l) => seen[l].X / seen[l].Count;
            Assert.True(Centre(Look.Sand) - Centre(Look.Yellow) >= 60, $"{name}: the cab's centre {Centre(Look.Sand):F0} is not at least 60 px right of the lens's {Centre(Look.Yellow):F0}");
        }
    }
}

/// <summary>A fact that is skipped when the run folder is not given.</summary>
public sealed class SkippableOrRunAttribute : FactAttribute
{
    public SkippableOrRunAttribute()
    {
        if (Environment.GetEnvironmentVariable("CARVE_PARTS_RUN") is null)
        {
            Skip = "CARVE_PARTS_RUN is unset";
        }
    }
}
