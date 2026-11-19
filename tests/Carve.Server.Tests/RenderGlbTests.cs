using System.Numerics;
using System.Text.Json;
using Carve.Core;
using StbImageSharp;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>
/// render_glb through the real server: the gardener exported and drawn back through the engine's loader and GPU skinning at a
/// clip and time, with each pixel worked from the gardener's geometry and the framing from its rest bounds; and the errors.
/// </summary>
[Trait("Tier", "regression")]
public class RenderGlbTests
{
    private const int Size = 512;

    private static readonly string Root = Directory.CreateTempSubdirectory("carve-render-glb-").FullName;

    /// <summary>Kept, for a person to look at: <c>artifacts/render-glb-tests</c>.</summary>
    private static readonly string Frames =
        Directory.CreateDirectory(Path.Combine(Paths.RepoRoot, "artifacts", "render-glb-tests")).FullName;

    private static readonly string Sample = Path.Combine(Paths.RepoRoot, "samples", "gardener.carve");

    /// <summary>The gardener's colours (GardenerSpec), and the background, by direction in linear light.</summary>
    private static readonly (string Group, Vector3 Linear)[] Palette =
    [
        .. new[] { ("skin", "#E8B896"), ("shirt", "#6AAE48"), ("trousers", "#3C54A0"), ("boots", "#4A2C1A"), ("can", "#1FA8A0"), ("straw", "#E8C86A"), ("band", "#C0392B") }
            .Select(p => (p.Item1, Vector3.Normalize(Linear(p.Item2)))),
        ("background", Vector3.Normalize(new Vector3(Srgb(48), Srgb(52), Srgb(56)))),
    ];

    private static float Srgb(int v)
    {
        var c = v / 255f;
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }

    private static Vector3 Linear(string hex) =>
        new(Srgb(Convert.ToInt32(hex[1..3], 16)), Srgb(Convert.ToInt32(hex[3..5], 16)), Srgb(Convert.ToInt32(hex[5..7], 16)));

    private static string Classify(ImageResult image, (int X, int Y) at)
    {
        if (at.X < 0 || at.Y < 0 || at.X >= image.Width || at.Y >= image.Height) return "outside the image";
        var i = (at.Y * image.Width + at.X) * 4;
        var v = new Vector3(Srgb(image.Data[i]), Srgb(image.Data[i + 1]), Srgb(image.Data[i + 2]));
        if (v.LengthSquared() < 1e-12f) return "black";
        var n = Vector3.Normalize(v);
        return Palette.MaxBy(c => Vector3.Dot(c.Linear, n)).Group;
    }

    private static bool IsBackground(ImageResult image, int x, int y)
    {
        var i = (y * image.Width + x) * 4;
        return Math.Abs(image.Data[i] - 48) <= 1 && Math.Abs(image.Data[i + 1] - 52) <= 1 && Math.Abs(image.Data[i + 2] - 56) <= 1;
    }

    /// <summary>The smallest box holding every pixel that is not the background.</summary>
    private static (int Left, int Top, int Right, int Bottom) Silhouette(ImageResult image)
    {
        var (left, top, right, bottom) = (int.MaxValue, int.MaxValue, -1, -1);
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            if (IsBackground(image, x, y)) continue;
            (left, top, right, bottom) = (Math.Min(left, x), Math.Min(top, y), Math.Max(right, x), Math.Max(bottom, y));
        }

        return (left, top, right, bottom);
    }

    private static ImageResult Image(JsonElement result, string keepAs)
    {
        Assert.True(result.TryGetProperty("content", out _) && !IsError(result), result.ToString());
        var png = Convert.FromBase64String(result.GetProperty("content")[0].GetProperty("data").GetString()!);
        File.WriteAllBytes(Path.Combine(Frames, keepAs + ".png"), png);
        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
        Assert.Equal(Size, image.Width);
        return image;
    }

    private static string ErrorText(JsonElement result)
    {
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private static async Task<CarveProcess> StartAsync()
    {
        var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        return carve;
    }

    /// <summary>The gardener as the sample holds it: its rest bounds frame every view, as render frames a posed model (T-32).</summary>
    private sealed class Gardener
    {
        public Gardener()
        {
            Model = CarveFile.Read(File.ReadAllText(Sample)).Model!;
            var rest = Model.Combined!.Vertices;
            var min = rest.Aggregate(Vector3.Min);
            var max = rest.Aggregate(Vector3.Max);
            Space = DrawnSpace.Of(min, max);
            Half = Space.Apply(max);
        }

        public Carve.Core.Model Model { get; }

        public DrawnSpace Space { get; }

        public Vector3 Half { get; }

        /// <summary>Where a point of the gardener's own coordinates lands, seen along a named view.</summary>
        public (int X, int Y) PixelOf(Vector3 point, string view)
        {
            var frame = ViewFraming.Frame(Half, new CameraSpec(ViewFraming.NamedViews[view], 1, null));
            var clip = Vector4.Transform(new Vector4(Space.Apply(point), 1), frame.View * frame.Projection);
            return ((int)((clip.X / clip.W + 1) / 2 * Size), (int)((1 - clip.Y / clip.W) / 2 * Size));
        }

        /// <summary>
        /// The mean of the vertices wholly on <paramref name="bone"/>, posed by the clip at the time. Worked with Carve's own
        /// sampler: the test's oracle, which the product's render_glb does not share (D-6, two samplers).
        /// </summary>
        public Vector3 Posed(string bone, string clip, float time)
        {
            var index = Model.Skeleton!.Names.ToList().IndexOf(bone);
            var pose = Model.FindClip(clip)!.Sample(time);
            var sum = Vector3.Zero;
            var count = 0;
            foreach (var named in Model.Meshes)
            {
                var mesh = named.Mesh;
                if (mesh.Weights is not { } weights) continue;
                var posed = Skinning.Positions(mesh, Model.Skeleton, pose);
                foreach (var v in weights.Vertices)
                {
                    if (weights.Of(v) is [{ Bone: var b, Weight: >= 0.999 }] && b == index)
                    {
                        sum += posed[v];
                        count++;
                    }
                }
            }

            Assert.True(count > 0, $"no vertex is wholly on {bone}");
            return sum / count;
        }
    }

    [Fact]
    public async Task The_exported_gardener_draws_skinned_at_a_clip_and_time_framed_as_render_frames_it()
    {
        using var carve = await StartAsync();
        Assert.False(IsError(await carve.CallToolAsync("open", JsonSerializer.Serialize(new { path = Sample }))));
        Assert.False(IsError(await carve.CallToolAsync("export", """{"path":"gardener.glb"}""")));
        var gardener = new Gardener();

        // Walk from the side (+X: forward, +Z, is the image's left): the near (left) hand is forward at 0 s and back at 0.5 s.
        foreach (var t in new[] { 0f, 0.5f })
        {
            var result = await carve.CallToolAsync("render_glb", JsonSerializer.Serialize(new { path = "gardener.glb", clip = "Walk", time = t, view = "+x" }));
            var loaded = Image(result, $"gardener-walk-{t:0.0}-side-glb");
            Assert.Contains($"clip 'Walk' at {t} s", TextOf(result, 1));

            var hand = gardener.PixelOf(gardener.Posed("Hand_L", "Walk", t), "+x");
            Assert.True(Classify(loaded, hand) == "skin", $"Walk at {t} s: the near hand at {hand} is {Classify(loaded, hand)}, expected skin");

            // A camera mirrored in the image's X (or a clip played backward, or at rest) puts the hand at its mirror.
            var mirror = (Size - 1 - hand.X, hand.Y);
            Assert.True(Classify(loaded, mirror) != "skin", $"Walk at {t} s: the hand's mirror at {mirror} is skin");

            // The frame is render's: the same scene, light, camera, size and rest-bounds framing, so the silhouettes agree.
            var preview = Image(await carve.CallToolAsync("render", JsonSerializer.Serialize(new { clip = "Walk", time = t, view = "+x" })), $"gardener-walk-{t:0.0}-side-render");
            var (a, b) = (Silhouette(loaded), Silhouette(preview));
            Assert.True(
                Math.Abs(a.Left - b.Left) <= 2 && Math.Abs(a.Top - b.Top) <= 2 && Math.Abs(a.Right - b.Right) <= 2 && Math.Abs(a.Bottom - b.Bottom) <= 2,
                $"Walk at {t} s: render_glb's silhouette {a} is not render's {b}");
        }

        // With no clip, the file skinned at its bind pose, framed on the same bounds.
        var front = Image(await carve.CallToolAsync("render_glb", """{"path":"gardener.glb","view":"+z"}"""), "gardener-rest-front-glb");
        var head = gardener.PixelOf(gardener.Posed("Head", "Idle", 0), "+z");
        Assert.True(Classify(front, head) == "skin", $"rest: the head at {head} is {Classify(front, head)}, expected skin");
    }

    /// <summary>
    /// RV-52 finding 3, held by RV-53: with no clip, render_glb skins a skinned file at its bind pose, as a game does with no clip
    /// playing, so a file whose skin is wrong (the thighs' inverse binds swapped, Orcward's <c>wrong-inverse-binds</c>) draws
    /// wrong at rest too. Drawn as loaded, unskinned, the two rest frames would be the same.
    /// </summary>
    [Fact]
    public async Task With_no_clip_a_file_with_a_wrong_skin_draws_wrong_at_rest()
    {
        using var carve = await StartAsync();
        Assert.False(IsError(await carve.CallToolAsync("open", JsonSerializer.Serialize(new { path = Sample }))));
        Assert.False(IsError(await carve.CallToolAsync("export", """{"path":"bind-gardener.glb"}""")));
        var broken = Carve.Scenario.Orcward.GlbEdit.Read(File.ReadAllBytes(Path.Combine(Root, "bind-gardener.glb")));
        broken.SwapInverseBinds("UpperLeg_L", "UpperLeg_R");
        File.WriteAllBytes(Path.Combine(Root, "bind-gardener-wrong-binds.glb"), broken.Write());

        var goodResult = await carve.CallToolAsync("render_glb", """{"path":"bind-gardener.glb","view":"+z"}""");
        Assert.Contains("no clip: skinned at its bind pose", TextOf(goodResult, 1));
        var good = Image(goodResult, "gardener-bind-rest-front-glb");
        var wrong = Image(await carve.CallToolAsync("render_glb", """{"path":"bind-gardener-wrong-binds.glb","view":"+z"}"""), "gardener-wrong-binds-rest-front-glb");

        var differing = 0;
        for (var i = 0; i < Size * Size; i++)
        {
            var d = 0;
            for (var c = 0; c < 3; c++) d = Math.Max(d, Math.Abs(good.Data[i * 4 + c] - wrong.Data[i * 4 + c]));
            differing += d > 8 ? 1 : 0;
        }

        // D-6's frame rule fails a frame past 0.5 % of its pixels; Orcward measured this break at rest at 3.952 %.
        Assert.True(differing * 200 > Size * Size, $"the wrong skin's rest frame differs from the good one by {differing} px, no more than 0.5 %: it was drawn unskinned");
    }

    /// <summary>
    /// T-82: render draws with the PBR material the loader makes from the exported file, so a render and a render_glb of the
    /// gardener at the same pose agree in shading under D-6's rule (a pixel differs past 8 of 255 in R, G or B; a frame fails
    /// past 0.5 % of its pixels). T-48 owns the full comparison; this keeps the shading gap T-47 measured (0.73 of the
    /// brightness, about 12 % of the pixels) from coming back.
    /// </summary>
    [Fact]
    public async Task Render_and_render_glb_of_the_gardener_agree_in_shading()
    {
        using var carve = await StartAsync();
        Assert.False(IsError(await carve.CallToolAsync("open", JsonSerializer.Serialize(new { path = Sample }))));
        Assert.False(IsError(await carve.CallToolAsync("export", """{"path":"agree-gardener.glb"}""")));
        var poses = new (string Name, string Glb, string Render)[]
        {
            ("rest-front", """{"path":"agree-gardener.glb","view":"+z"}""", """{"view":"+z"}"""),
            ("walk-0.5-side", """{"path":"agree-gardener.glb","clip":"Walk","time":0.5,"view":"+x"}""", """{"clip":"Walk","time":0.5,"view":"+x"}"""),
        };
        foreach (var (name, glbArgs, renderArgs) in poses)
        {
            var loaded = Image(await carve.CallToolAsync("render_glb", glbArgs), $"agree-{name}-glb");
            var preview = Image(await carve.CallToolAsync("render", renderArgs), $"agree-{name}-render");
            var (differing, ratio) = Agreement(loaded, preview);
            var share = 100.0 * differing / (Size * Size);
            Assert.True(share <= 0.5, $"{name}: {differing} pixels ({share:0.00} %) differ past 8/255; brightness on the model {ratio:0.000} of render's");
        }
    }

    /// <summary>
    /// RV-71: a weighted mesh on a placed part is skinned in model space, where it is drawn, by render as by the skinned
    /// export's bake, so a pose of it agrees with the exported file drawn back. Skinning the part's own coordinates and then
    /// placing them moves the legs' and arms' vertices off what the game draws.
    /// </summary>
    [Fact]
    public async Task Render_and_render_glb_agree_on_a_posed_placed_rigged_part()
    {
        using var carve = await StartAsync();
        Assert.False(IsError(await carve.CallToolAsync("open", JsonSerializer.Serialize(new { path = Sample }))));
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.3f);
        Assert.False(IsError(await carve.CallToolAsync("define_part", JsonSerializer.Serialize(new
        {
            part = "Body", translation = new[] { 0f, 0.25f, 0f }, rotation = new[] { turn.X, turn.Y, turn.Z, turn.W },
        }))));
        Assert.False(IsError(await carve.CallToolAsync("export", """{"path":"agree-placed.glb"}""")));
        var loaded = Image(await carve.CallToolAsync("render_glb", """{"path":"agree-placed.glb","clip":"Walk","time":0.5,"view":"+x"}"""), "agree-placed-walk-glb");
        var preview = Image(await carve.CallToolAsync("render", """{"clip":"Walk","time":0.5,"view":"+x"}"""), "agree-placed-walk-render");
        var (differing, ratio) = Agreement(loaded, preview);
        var share = 100.0 * differing / (Size * Size);
        Assert.True(share <= 0.5, $"{differing} pixels ({share:0.00} %) differ past 8/255; brightness on the model {ratio:0.000} of render's");
    }

    /// <summary>The pixels that differ past 8/255 in R, G or B, and the glb's brightness on the pixels both draw the model on, over render's.</summary>
    private static (int Differing, double Ratio) Agreement(ImageResult glb, ImageResult render)
    {
        var (differing, glbSum, renderSum) = (0, 0L, 0L);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            var i = (y * Size + x) * 4;
            if (Enumerable.Range(0, 3).Any(c => Math.Abs(glb.Data[i + c] - render.Data[i + c]) > 8)) differing++;
            if (IsBackground(glb, x, y) || IsBackground(render, x, y)) continue;
            glbSum += glb.Data[i] + glb.Data[i + 1] + glb.Data[i + 2];
            renderSum += render.Data[i] + render.Data[i + 1] + render.Data[i + 2];
        }

        return (differing, (double)glbSum / renderSum);
    }

    [Fact]
    public async Task Errors_name_the_argument_and_list_the_clips()
    {
        using var carve = await StartAsync();
        Assert.False(IsError(await carve.CallToolAsync("define_mesh",
            """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""")));
        Assert.False(IsError(await carve.CallToolAsync("export", """{"path":"errors-cube.glb"}""")));
        Assert.False(IsError(await carve.CallToolAsync("open", JsonSerializer.Serialize(new { path = Sample }))));
        Assert.False(IsError(await carve.CallToolAsync("export", """{"path":"errors-gardener.glb"}""")));
        var before = Image(await carve.CallToolAsync("render", "{}"), "gardener-render-before-glb");

        Assert.Contains("no file at", ErrorText(await carve.CallToolAsync("render_glb", """{"path":"nowhere.glb"}""")));
        Assert.Contains("clip: the file has no skin", ErrorText(await carve.CallToolAsync("render_glb", """{"path":"errors-cube.glb","clip":"Walk","time":0}""")));
        var unknown = ErrorText(await carve.CallToolAsync("render_glb", """{"path":"errors-gardener.glb","clip":"Run","time":0}"""));
        Assert.Contains("there is no clip named 'Run'", unknown);
        Assert.Contains("'Idle'", unknown);
        Assert.Contains("'Walk'", unknown);
        Assert.Contains("time: only with clip", ErrorText(await carve.CallToolAsync("render_glb", """{"path":"errors-gardener.glb","time":0.5}""")));
        Assert.Contains("time: required with clip", ErrorText(await carve.CallToolAsync("render_glb", """{"path":"errors-gardener.glb","clip":"Walk"}""")));
        Assert.Contains("is outside the clip 'Walk'", ErrorText(await carve.CallToolAsync("render_glb", """{"path":"errors-gardener.glb","clip":"Walk","time":3}""")));

        // A file with no skin draws as it is, and the model being edited is untouched by any of it.
        Image(await carve.CallToolAsync("render_glb", """{"path":"errors-cube.glb"}"""), "cube-glb");
        var after = Image(await carve.CallToolAsync("render", "{}"), "gardener-render-after-glb");
        Assert.Equal(before.Data, after.Data);
    }
}
