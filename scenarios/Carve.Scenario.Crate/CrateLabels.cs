using System.Numerics;
using Carve.Debug;
using static Carve.Scenario.Crate.CameraPixels;
using static Carve.Scenario.Crate.CrateBuild;
using static Carve.Scenario.Crate.CrateLooks;

namespace Carve.Scenario.Crate;

/// <summary>
/// The labelled frames and the contact sheets of <c>render</c>, on the finished crate. A label is looked for
/// at the point <see cref="CameraPixels"/> projects, worked out apart from the product, and against the same
/// frame without labels, so that what is measured is the overlay. Head-on the crate is the same mirrored and
/// flipped, so the corner sheet is what tells each axis view's camera.
/// </summary>
internal static class CrateLabels
{
    private const int Tile = 256;
    private const int Gutter = 2;

    /// <summary>The backing darkens a face by at least this.</summary>
    private const int Backing = 60;

    /// <summary>An engine text or dot pixel reads this or more on every channel; nothing under a backing does.</summary>
    private const int White = 250;

    private const float Near = 0.001f;

    private static readonly string[] AxisViews = ["+x", "-x", "+y", "-y", "+z", "-z"];

    public static async Task RunAsync(ScenarioDriver driver, Shape crate, ScenarioFrame shadedFront, ScenarioFrame shadedTilted)
    {
        await LabelsFrontAsync(driver, crate, shadedFront);
        await LabelsTiltedAsync(driver, crate, shadedTilted);
        await AxesSheetAsync(driver);
        await CornerSheetAsync(driver);
        await FourTileSheetAsync(driver, crate);

        await ExpectToolErrorAsync(driver, "render", new { views = new[] { "+x", "-x", "+y", "-y", "+z", "-z", "front" } }, "views", "7");
        await ExpectToolErrorAsync(driver, "render", new { views = new[] { "+x", "-x" }, columns = 4 }, "columns", "4");
        await ExpectToolErrorAsync(driver, "render", new { views = new[] { "+x", "-x" }, view = "front" }, "views", "view");

        var after = await driver.CaptureAsync("crate-front-after-labels", new { view = "front" });
        driver.AssertBytesEqual("crate front, after the labels and sheets", shadedFront.Pixels, after.Pixels);
    }

    private static async Task LabelsFrontAsync(ScenarioDriver driver, Shape crate, ScenarioFrame shadedFront)
    {
        const int Size = 1024;
        var none = await driver.CaptureAsync("crate-labels-none-front", new { view = "front", labels = "none" });
        driver.AssertBytesEqual("crate front, labels none", shadedFront.Pixels, none.Pixels);

        var plain = await driver.CaptureAsync("crate-front-1024", new { view = "front", size = Size });
        var labelled = await driver.CaptureAsync("crate-labels-both-front-1024", new { view = "front", size = Size, labels = "both" });
        CheckCaption(driver, labelled, "1024x1024, view front, labels both, 72 vertices, 70 faces");
        CheckCorners(driver, labelled);
        CheckOpaque(driver, labelled);

        var pixel = Perspective(CrateMin, CrateMax, Front, Size);
        CheckFaceLabel(driver, labelled, plain, pixel, crate, TopFace, "the lid panel");

        // The corner nearest the eye: the dot on it, and its id above and to the right.
        var corner = crate.Vertex(p => p.X > 1 - Near && p.X < 1 + Near && p.Y > 0.8f - Near && p.Z > 1 - Near);
        var at = pixel(crate.Vertices[corner]);
        CheckWhite(driver, labelled, Holding(at), "the dot on the corner");
        CheckWhiteIn(driver, labelled, IdBox(at + new Vector2(5, -18), corner, centred: false), $"vertex {corner}'s id");

        // Turned away, so unlabelled; what is seen there is a nearer face, as it was without labels. The panel
        // is four strips round the bar: the -Z one's centre falls under the label of face 49, which faces the eye.
        foreach (var hidden in new[] { MinusXStrip(crate, p => p.Y < 0.35f), MinusXStrip(crate, p => p.Y > 0.45f), MinusXStrip(crate, p => p.Z > 0.82f) })
        {
            CheckUnmarked(driver, labelled, plain, Holding(pixel(crate.Centre(hidden))), $"the centre of the -X panel's face {hidden}");
        }
    }

    private static async Task LabelsTiltedAsync(ScenarioDriver driver, Shape crate, ScenarioFrame plain)
    {
        var labelled = await driver.CaptureAsync("crate-labels-faces-dir-0.4-2-0.8", new { direction = new[] { 0.4, 2, 0.8 }, labels = "faces" });
        CheckCaption(driver, labelled, "512x512, direction (0.4, 2, 0.8), labels faces, 72 vertices, 70 faces");
        CheckCorners(driver, labelled);
        CheckOpaque(driver, labelled);

        var pixel = Perspective(CrateMin, CrateMax, Tilted, 512);
        CheckFaceLabel(driver, labelled, plain, pixel, crate, TopFace, "the lid panel");
        CheckFaceLabel(driver, labelled, plain, pixel, crate, crate.Find(p => p.Z > 1 - Near && p.Y > 0.68f - Near), "the +Z frame", darkenBy: 0);

        // Turned away, so unlabelled. Of the -X panel's strips this one is clear here; the others, and the -Z
        // frame, fall under labels of faces in front of them.
        var hidden = MinusXStrip(crate, p => p.Z > 0.82f);
        CheckUnmarked(driver, labelled, plain, Holding(pixel(crate.Centre(hidden))), $"the centre of the -X panel's face {hidden}");
    }

    private static async Task AxesSheetAsync(ScenarioDriver driver)
    {
        var sheet = await driver.CaptureAsync("crate-sheet-axes", new { views = AxisViews, projection = "orthographic", size = Tile, labels = "none" });
        CheckCaption(driver, sheet, "3x2 tiles of 256x256, orthographic, views +x, -x, +y, -y, +z, -z, 72 vertices, 70 faces");
        CheckSize(driver, sheet, 3 * Tile + 2 * Gutter, 2 * Tile + Gutter);

        // The gutter is exactly the background of a tile, between columns and between rows.
        var background = sheet.Pixel(Tile - 1, Tile - 1);
        foreach (var (x, y) in new[] { (256, 100), (257, 100), (514, 100), (515, 100), (100, 256), (100, 257), (600, 257) })
        {
            if (sheet.Pixel(x, y) != background)
            {
                driver.Fail($"{sheet.Name}: the gutter at ({x}, {y}) is {sheet.Pixel(x, y)}, expected the background {background}");
            }
        }

        for (var tile = 0; tile < AxisViews.Length; tile++)
        {
            var (left, top) = TileAt(tile, 3);
            CheckNotBackground(driver, sheet, (left + Tile / 2, top + Tile / 2), $"the centre of tile {AxisViews[tile]}");
            CheckWhiteIn(driver, sheet, (left + 4, top + 4, 16, 16), $"tile {AxisViews[tile]}'s caption");

            var single = await driver.CaptureAsync($"crate-axis-{Name(AxisViews[tile])}-ortho-256", new { view = AxisViews[tile], projection = "orthographic", size = Tile });
            CheckTile(driver, sheet, left, top, single, AxisViews[tile].Length, AxisViews[tile]);
        }
    }

    private static async Task CornerSheetAsync(ScenarioDriver driver)
    {
        var target = new[] { 1.02, 0.8, 1 };
        var sheet = await driver.CaptureAsync("crate-sheet-axes-corner", new
        {
            views = AxisViews.Select(view => new { view, zoom = 3, target }).ToArray(),
            projection = "orthographic",
            size = Tile,
        });
        var listed = string.Join(", ", AxisViews.Select(view => $"{view}, zoom 3, target (1.02, 0.8, 1)"));
        CheckCaption(driver, sheet, $"3x2 tiles of 256x256, orthographic, views {listed}, 72 vertices, 70 faces");
        CheckSize(driver, sheet, 3 * Tile + 2 * Gutter, 2 * Tile + Gutter);

        // The crate's largest corner on the centre of each tile: the mesh lies in one quarter of it, a
        // different one on a mirrored or flipped camera. (+x has +Z on the left, -y has +X on the left, and so on.)
        (int X, int Y)[] mesh = [(1, 1), (-1, 1), (-1, -1), (1, -1), (-1, 1), (1, 1)];
        const int Reach = 50;
        for (var tile = 0; tile < AxisViews.Length; tile++)
        {
            var (left, top) = TileAt(tile, 3);
            var (x, y) = (left + Tile / 2, top + Tile / 2);
            var (dx, dy) = (mesh[tile].X * Reach, mesh[tile].Y * Reach);
            CheckNotBackground(driver, sheet, (x + dx, y + dy), $"tile {AxisViews[tile]}'s quarter of the mesh");
            foreach (var (ox, oy) in new[] { (-dx, dy), (dx, -dy), (-dx, -dy) })
            {
                if (!IsBackground(sheet.Pixel(x + ox, y + oy)))
                {
                    driver.Fail($"{sheet.Name}: tile {AxisViews[tile]} at ({ox}, {oy}) from its centre is {sheet.Pixel(x + ox, y + oy)}: the mesh should lie only toward ({dx}, {dy})");
                }
            }
        }
    }

    private static async Task FourTileSheetAsync(ScenarioDriver driver, Shape crate)
    {
        var zoomed = new { direction = new[] { 2, 1, 1 }, zoom = 2, target = new[] { 1.02, 0.4, 0.5 } };
        var sheet = await driver.CaptureAsync("crate-sheet-four", new { views = new object[] { "front", "+x", zoomed, "-z" }, size = Tile, columns = 2 });
        CheckCaption(driver, sheet, "2x2 tiles of 256x256, views front, +x, (2, 1, 1), zoom 2, target (1.02, 0.4, 0.5), -z, 72 vertices, 70 faces");
        CheckSize(driver, sheet, 2 * Tile + Gutter, 2 * Tile + Gutter);

        var front = await driver.CaptureAsync("crate-front-256", new { view = "front", size = Tile });
        var plusX = await driver.CaptureAsync("crate-plusx-256", new { view = "+x", size = Tile });
        var close = await driver.CaptureAsync("crate-dir-2-1-1-zoom2-256", new { zoomed.direction, zoomed.zoom, zoomed.target, size = Tile });
        var minusZ = await driver.CaptureAsync("crate-minusz-256", new { view = "-z", size = Tile });
        CheckTile(driver, sheet, 0, 0, front, "front".Length, "front");
        CheckTile(driver, sheet, Tile + Gutter, 0, plusX, "+x".Length, "+x");
        CheckTile(driver, sheet, 0, Tile + Gutter, close, Tile, "(2, 1, 1)");
        CheckTile(driver, sheet, Tile + Gutter, Tile + Gutter, minusZ, "-z".Length, "-z");

        // The front tile: top over +Z over +X at the projected points, which a mirrored or flipped camera reorders.
        var pixel = Perspective(CrateMin, CrateMax, Front, Tile);
        var lidBand = crate.FindAll(p => p.Y > 0.8f - Near).First(f => crate.Faces[f].Any(v => crate.Vertices[v].Z > 1 - Near));
        var plusZBand = crate.Find(p => p.Z > 1 - Near && p.Y > 0.68f - Near);
        var plusXBand = crate.Find(p => Math.Abs(p.X - 1) < Near && p.Y > 0.68f - Near);
        CheckOrder(driver, sheet, [Holding(pixel(crate.Centre(lidBand))), Holding(pixel(crate.Centre(plusZBand))), Holding(pixel(crate.Centre(plusXBand)))], "top", "+Z", "+X");

        // The direction's tile: its caption's first nine letters read (2, 1, 1): each letter is 8 pixels wide, so
        // the two 1s match, as do the two commas and the two spaces, and the brackets differ. A name would not.
        var (left, top) = (4, Tile + Gutter + 4);
        CheckSameLetter(driver, sheet, left, top, 4, 7, "the two 1s");
        CheckSameLetter(driver, sheet, left, top, 2, 5, "the two commas");
        CheckSameLetter(driver, sheet, left, top, 3, 6, "the two spaces");
        if (Letter(sheet, left, top, 0).SequenceEqual(Letter(sheet, left, top, 1)))
        {
            driver.Fail($"{sheet.Name}: the direction tile's caption does not open with a bracket and a 2");
        }

        if (Letter(sheet, left, top, 3).Any(lit => lit))
        {
            driver.Fail($"{sheet.Name}: the direction tile's caption has no space as its fourth letter");
        }
    }

    private static int MinusXStrip(Shape crate, Func<Vector3, bool> also) => crate.Find(p => Math.Abs(p.X - 0.08f) < Near && also(p));

    private static (int Left, int Top) TileAt(int tile, int columns) => (tile % columns * (Tile + Gutter), tile / columns * (Tile + Gutter));

    private static string Name(string view) => (view[0] == '+' ? "plus" : "minus") + view[1];

    /// <summary>Which of a caption letter's pixels are white.</summary>
    private static bool[] Letter(ScenarioFrame frame, int left, int top, int index)
    {
        var lit = new bool[8 * 16];
        for (var y = 0; y < 16; y++)
        for (var x = 0; x < 8; x++)
        {
            lit[y * 8 + x] = IsWhite(frame.Pixel(left + index * 8 + x, top + y));
        }

        return lit;
    }

    private static void CheckSameLetter(ScenarioDriver driver, ScenarioFrame frame, int left, int top, int a, int b, string what)
    {
        if (!Letter(frame, left, top, a).SequenceEqual(Letter(frame, left, top, b)) || !Letter(frame, left, top, a).Any(lit => lit) && a != 3)
        {
            driver.Fail($"{frame.Name}: the direction tile's caption: letters {a} and {b} should be {what}");
        }
    }

    private static bool IsWhite((byte R, byte G, byte B, byte A) p) => p.R >= White && p.G >= White && p.B >= White;

    /// <summary>The cell of an id: centred on a point for a face, from a top left for a vertex, on whole pixels as the text is.</summary>
    private static (int X, int Y, int Width, int Height) IdBox(Vector2 at, int id, bool centred)
    {
        var width = 8 * id.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
        var from = centred ? at - new Vector2(width / 2f, 8) : at;
        return ((int)MathF.Round(from.X), (int)MathF.Round(from.Y), width, 16);
    }

    private static void CheckFaceLabel(ScenarioDriver driver, ScenarioFrame labelled, ScenarioFrame plain, Func<Vector3, Vector2> pixel, Shape shape, int face, string what, int darkenBy = Backing)
    {
        var at = pixel(shape.Centre(face));
        var box = IdBox(at, face, centred: true);
        CheckWhiteIn(driver, labelled, box, $"{what}'s label (face {face})");

        // Off the letters the box is the backing: the face at 0.4 of its linear value, at most 10/255 from that.
        var (x, y) = Holding(at);
        var shade = plain.Pixel(x, y);
        var seen = labelled.Pixel(x, y);
        if (IsWhite(seen))
        {
            driver.Fail($"{labelled.Name}: {what}'s centre at ({x}, {y}) is a letter's pixel, so it says nothing of the backing");
        }

        var expected = Backed(shade.G);
        if (shade.G - seen.G < darkenBy || Math.Abs(seen.G - expected) > 10)
        {
            driver.Fail($"{labelled.Name}: {what}'s centre at ({x}, {y}) is {seen} over {shade}: the backing should give {expected}, darker by {darkenBy} or more");
        }
    }

    /// <summary>A shade under a 0.6 black backing, which blends in linear light.</summary>
    private static int Backed(int shade)
    {
        var c = shade / 255.0;
        var linear = (c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4)) * 0.4;
        return (int)Math.Round(255 * (linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055));
    }

    private static void CheckUnmarked(ScenarioDriver driver, ScenarioFrame labelled, ScenarioFrame plain, (int X, int Y) at, string what)
    {
        if (labelled.Pixel(at.X, at.Y) != plain.Pixel(at.X, at.Y))
        {
            driver.Fail($"{labelled.Name}: {what} at ({at.X}, {at.Y}) is {labelled.Pixel(at.X, at.Y)}, without labels {plain.Pixel(at.X, at.Y)}: there should be no label there");
        }
    }

    private static void CheckWhite(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) at, string what)
    {
        if (!IsWhite(frame.Pixel(at.X, at.Y)))
        {
            driver.Fail($"{frame.Name}: {what} at ({at.X}, {at.Y}) is {frame.Pixel(at.X, at.Y)}, expected white");
        }
    }

    private static void CheckWhiteIn(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y, int Width, int Height) box, string what)
    {
        for (var y = box.Y; y < box.Y + box.Height; y++)
        for (var x = box.X; x < box.X + box.Width; x++)
        {
            if (IsWhite(frame.Pixel(x, y)))
            {
                return;
            }
        }

        driver.Fail($"{frame.Name}: {what}: no white pixel in the box at ({box.X}, {box.Y}), {box.Width} x {box.Height}");
    }

    private static void CheckOpaque(ScenarioDriver driver, ScenarioFrame frame)
    {
        for (var at = 3; at < frame.Pixels.Length; at += 4)
        {
            if (frame.Pixels[at] != 255)
            {
                driver.Fail($"{frame.Name}: a pixel has alpha {frame.Pixels[at]}: blending the overlay must leave the image opaque");
            }
        }
    }

    private static void CheckSize(ScenarioDriver driver, ScenarioFrame frame, int width, int height)
    {
        if (frame.Width != width || frame.Height != height)
        {
            driver.Fail($"{frame.Name}: the PNG is {frame.Width} x {frame.Height}, expected {width} x {height}");
        }
    }

    /// <summary>A tile is the full render of its view: the same pixel for pixel, but for its caption's backing.</summary>
    private static void CheckTile(ScenarioDriver driver, ScenarioFrame sheet, int left, int top, ScenarioFrame single, int letters, string what)
    {
        var (right, bottom) = (6 + 8 * letters, 22);
        for (var y = 0; y < Tile; y++)
        for (var x = 0; x < Tile; x++)
        {
            if ((y >= bottom || x >= right) && sheet.Pixel(left + x, top + y) != single.Pixel(x, y))
            {
                driver.Fail($"{sheet.Name}: tile {what} at ({x}, {y}) is {sheet.Pixel(left + x, top + y)}; {single.Name} there is {single.Pixel(x, y)}");
            }
        }
    }
}
