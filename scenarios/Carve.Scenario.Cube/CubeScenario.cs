using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Carve.Scenario.Cube;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class CubeExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, CubeScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// A cube, an L-block, a long box and a cube with a flipped face, seen from both views, using
/// only <c>define_mesh</c> and <c>render</c>. Frames are in <c>frames/</c> of the run folder.
/// </summary>
public sealed class CubeScenario : IScenario
{
    /// <summary>
    /// Faces must differ in the green channel by at least this much, in the expected order. 6 since T-82 (10 before), by the person's
    /// ruling of 2026-11-13: under the engine's PBR material the directional light reaches a face as albedo / pi (Lambert's diffuse term,
    /// the light's intensity unscaled) while the ambient floor is unchanged, so through the sRGB encoding each step the light makes is
    /// about 0.4 to 0.45 of what it was. Worked from the faces' n.l (cube front: top 0.78, +Z 0.52, +X 0.35; the grey 0.72, ambient 0.25,
    /// light 0.9), the closest pair, +Z over +X, now steps by 8 (19 under LitMesh), so 10 would fail a correct frame; 6 keeps 2 of headroom
    /// and still fails two faces drawn in one shade (a step of 0) or in the wrong order.
    /// </summary>
    private const int MinimumStep = 6;

    // Fewest non-background pixels each 512 x 512 frame may show, about half its measured count:
    // L-block back 94,688, long box 28,563, flipped cube 53,660. The L-block front's 88,829 gets 56%,
    // since an L-block that lost its side faces draws 49,732 there, and half would let that through.
    private const int LBlockFrontDrawn = 50000;
    private const int LBlockBackDrawn = 47000;
    private const int LongBoxDrawn = 14000;
    private const int FlippedCubeDrawn = 26000;

    // The vertices and faces of the define_mesh description's worked example.
    private static readonly double[][] CubeVertices =
        [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]];

    private static readonly int[][] CubeFaces =
        [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]];

    // Sampled pixels (x, y from the top left) of the unit cube, from T-4: the projected face centres.
    private static readonly (int X, int Y)[] CubeFront = [(256, 133), (172, 302), (367, 288)]; // top, +Z, +X
    private static readonly (int X, int Y)[] CubeBack = [(172, 209), (367, 223), (256, 378)];  // -Z, -X, underside

    // The L-block's notch corner (1.8, 1.8, 1) in front: its sight line never meets the solid.
    private static readonly (int X, int Y) NotchFront = (321, 185);

    private static readonly (int X, int Y) Centre = (256, 256);

    // The L-block from [0, -1, 0], zoom 3 on its corner (0, 0, 0), so the corner is the image centre.
    // Seen from -Y with -Z up, +X points left and +Z points down, so the block fills only the lower
    // left quarter. Its silhouette from -Y is a plain 2 x 1 rectangle, which looks the same upside
    // down or mirrored, so the quarters are what tell the up vector and the sign of the direction:
    // +Z up would put the block in the upper right, a camera at +Y in the lower right. Each quarter
    // is sampled a quarter of the image from the centre both ways (measured from the first run).
    private static readonly (int X, int Y) CornerBlock = (128, 384);
    private static readonly (int X, int Y) CornerAbove = (128, 128);
    private static readonly (int X, int Y) CornerRight = (384, 384);
    private static readonly (int X, int Y) CornerUpperRight = (384, 128);

    // In the zoomed frame, between the cube's far top edges (measured from the first run's frame).
    private static readonly (int X, int Y) ZoomedBackground = (256, 10);

    /// <summary>The part of the frame left clear on each side, as the product frames.</summary>
    private const float Margin = 0.08f;

    /// <summary>In a 512 frame, the first and last pixels inside the margin.</summary>
    private static readonly int MarginLow = (int)MathF.Round(Margin * 512);
    private static readonly int MarginHigh = 511 - MarginLow;

    private static readonly Vector3 CubeMin = Vector3.Zero;
    private static readonly Vector3 CubeMax = Vector3.One;

    public async Task RunAsync(ScenarioDriver driver)
    {
        // 1. The cube, both views.
        await DefineCleanAsync(driver, "cube", CubeVertices, CubeFaces);
        var cubeFront = await driver.CaptureAsync("cube-front", new { view = "front" });
        var cubeBack = await driver.CaptureAsync("cube-back", new { view = "back" });
        CheckCorners(driver, cubeFront);
        CheckCorners(driver, cubeBack);
        CheckFrontOrder(driver, cubeFront, CubeFront);
        CheckOrder(driver, cubeBack, CubeBack, "-Z", "-X", "underside");

        // 2. A repeat of the same view is the same pixels.
        var again = await driver.CaptureAsync("cube-front-again", new { view = "front" });
        driver.AssertBytesEqual("cube front, rendered again", cubeFront.Pixels, again.Pixels);

        // 2b. T-9: the cube from any direction, closer in, and the errors. Each frame is checked for
        // the number of face shades it shows, a sampled pixel on the cube, and the four corners.
        var tilted = await driver.CaptureAsync("cube-dir-0.4-2-0.8", new { direction = new[] { 0.4, 2, 0.8 } });
        CheckCorners(driver, tilted);
        // Three faces are seen, but the light, turned with this camera, reaches only the top: the +Z and +X
        // sides are both ambient-only and share one shade, so two shades show.
        CheckShades(driver, tilted, 2);
        CheckNotBackground(driver, tilted, Centre, "the centre");

        var fromX = await driver.CaptureAsync("cube-dir-1-0-0", new { direction = new[] { 1, 0, 0 } });
        CheckCorners(driver, fromX);
        CheckShades(driver, fromX, 1);
        CheckNotBackground(driver, fromX, Centre, "the centre");

        var fromBelow = await driver.CaptureAsync("cube-dir-0--1-0", new { direction = new[] { 0, -1, 0 } });
        CheckCorners(driver, fromBelow);
        CheckShades(driver, fromBelow, 1);
        CheckNotBackground(driver, fromBelow, Centre, "the centre");

        var zoomed = await driver.CaptureAsync("cube-zoom3-corner", new { view = "front", zoom = 3, target = new[] { 1, 1, 1 } });
        // Two shades, not three: the person's ruling of 2026-11-17 ("Accept all", M-19 row 2). Under M-7's sky +Z and +X step
        // by 2 from the front (199 against 201) and merge into one shade; the top (216) is the other.
        CheckShades(driver, zoomed, 2);
        CheckNotBackground(driver, zoomed, Centre, "the corner (1, 1, 1) at the centre");
        // Zoomed in, the cube fills the lower corners; only the two at the top are background.
        CheckBackground(driver, zoomed, (0, 0), "the top left corner");
        CheckBackground(driver, zoomed, (zoomed.Width - 1, 0), "the top right corner");
        CheckNotBackground(driver, zoomed, (0, zoomed.Height - 1), "the bottom left corner, filled by the cube");
        CheckNotBackground(driver, zoomed, (zoomed.Width - 1, zoomed.Height - 1), "the bottom right corner, filled by the cube");
        CheckBackground(driver, zoomed, ZoomedBackground, "the sky between the cube's top edges");

        await ExpectErrorAsync(driver, "view with direction", new { view = "front", direction = new[] { 1, 0, 0 } });
        await ExpectErrorAsync(driver, "zoom 9", new { zoom = 9 });

        // 2c. T-24: the sizes, the orthographic frames and their errors.
        await CheckSizedFrontAsync(driver, 256);
        await CheckSizedFrontAsync(driver, 1024);

        // +x orthographic: the face fills the margin square. A cube head-on looks the same mirrored or
        // upside down, so the frame after it is what tells the camera's up and handedness.
        var plusX = await driver.CaptureAsync("cube-plusx-ortho", new { view = "+x", projection = "orthographic" });
        CheckCaption(driver, plusX, "512x512, view +x, orthographic, 8 vertices, 6 faces");
        CheckCorners(driver, plusX);
        CheckShades(driver, plusX, 1);
        CheckMarginSquare(driver, plusX);

        // The same view on the corner (1, 1, 1), zoom 3. From +X with +Y up, -Z is on the right and -Y
        // below, so the face fills only the lower right quarter: a flipped camera puts it in the upper
        // right, a mirrored one in the lower left. Zoom 1 would leave the frame's corner background.
        var plusXCorner = await driver.CaptureAsync("cube-plusx-ortho-corner", new { view = "+x", projection = "orthographic", zoom = 3, target = new[] { 1, 1, 1 } });
        CheckNotBackground(driver, plusXCorner, (384, 384), "the lower right quarter (the face, -Z right and -Y down)");
        CheckNotBackground(driver, plusXCorner, (511, 511), "the bottom right corner, filled at zoom 3");
        CheckBackground(driver, plusXCorner, (128, 384), "the lower left quarter (the face would be there mirrored)");
        CheckBackground(driver, plusXCorner, (384, 128), "the upper right quarter (the face would be there flipped)");
        CheckBackground(driver, plusXCorner, (128, 128), "the upper left quarter");

        // [1, 1, 1] orthographic: a regular hexagon about the corner (1, 1, 1). The silhouette corners
        // come from the orthographic matrix; a perspective frame's are 248 px out at the near three and
        // 205 px at the far three, against 215 px here, so it fails both sides of the check.
        var diagonal = new Vector3(1, 1, 1);
        var hexagon = await driver.CaptureAsync("cube-dir-1-1-1-ortho", new { direction = new[] { 1, 1, 1 }, projection = "orthographic" });
        CheckCaption(driver, hexagon, "512x512, direction (1, 1, 1), orthographic, 8 vertices, 6 faces");
        CheckCorners(driver, hexagon);
        var hexagonPixel = OrthographicPixels(CubeMin, CubeMax, diagonal, Vector3.UnitY, hexagon.Width);
        Vector3[] silhouette = [new(0, 1, 0), new(0, 1, 1), new(0, 0, 1), new(1, 0, 1), new(1, 0, 0), new(1, 1, 0)];
        CheckOutline(driver, hexagon, silhouette.Select(hexagonPixel).ToArray(), 3);
        // Flipped, the bright top face leaves the top of the hexagon. +Z and +X differ by only 3 of 255
        // here, too little to tell a mirror, so the frame after this one does that.
        var hexagonTop = Rounded(hexagonPixel(new Vector3(0.5f, 1, 0.5f)));
        CheckBrighter(driver, hexagon, hexagonTop, Rounded(hexagonPixel(new Vector3(0.5f, 0.5f, 1))), "the top, over +Z");
        CheckBrighter(driver, hexagon, hexagonTop, Rounded(hexagonPixel(new Vector3(1, 0.5f, 0.5f))), "the top, over +X");

        // The same view, zoom 3 on the corner (1, 1, 0), the hexagon's upper right corner, now the image
        // centre. The cube is the 120 degree wedge between the edges to (0, 1, 0), up and left, and to
        // (1, 0, 0), straight down: 150 to 270 degrees, anticlockwise from image right. Mirrored it is 270
        // to 390, flipped 90 to 210. So 100 px out, 240 degrees is the mesh, 330 is background and only a
        // mirrored camera would fill it, 120 is background and only a flipped one would.
        var hexagonCorner = await driver.CaptureAsync("cube-dir-1-1-1-ortho-corner", new { direction = new[] { 1, 1, 1 }, projection = "orthographic", zoom = 3, target = new[] { 1, 1, 0 } });
        var cornerPixel = OrthographicPixels(CubeMin, CubeMax, diagonal, Vector3.UnitY, hexagonCorner.Width, 3, new Vector3(1, 1, 0));
        var wedgeTip = cornerPixel(new Vector3(1, 1, 0));
        CheckNotBackground(driver, hexagonCorner, Rounded(wedgeTip + AtAngle(240, 100)), "inside the wedge of the cube's corner");
        CheckBackground(driver, hexagonCorner, Rounded(wedgeTip + AtAngle(330, 100)), "where a mirrored camera would put the cube");
        CheckBackground(driver, hexagonCorner, Rounded(wedgeTip + AtAngle(120, 100)), "where a flipped camera would put the cube");

        await ExpectErrorAsync(driver, "size 300", new { size = 300 }, "size: 300 is not a size; use 256, 512 or 1024");
        await ExpectErrorAsync(driver, "projection iso", new { projection = "iso" }, "projection: 'iso' is not a projection");
        await ExpectErrorAsync(driver, "view top", new { view = "top" }, "view: 'top' is not a view");

        // 2c. T-42: smooth shading. The same hexagon view three ways. Flat is the frame above: a face is one shade. Smooth gives each corner
        // its own normal, so the top face (a gradient from the corner at the centre of the hexagon to the far one) is not one shade.
        // Smooth with the top's four edges hard keeps the top one shade and still grades the +Z face. Every edge hard is the flat frame,
        // pixel for pixel. Points are worked from the mesh by the orthographic rule, so a mirrored or flipped camera samples background or another face.
        var topNear = Rounded(hexagonPixel(new Vector3(0.85f, 1, 0.85f)));
        var topFar = Rounded(hexagonPixel(new Vector3(0.15f, 1, 0.15f)));
        var flatTopNear = hexagon.Pixel(topNear.X, topNear.Y).G;
        var flatTopFar = hexagon.Pixel(topFar.X, topFar.Y).G;
        if (flatTopNear != flatTopFar) driver.Fail($"{hexagon.Name}: the flat top face should be one shade, got {flatTopNear} and {flatTopFar}");

        Ok(driver, await driver.CallAsync("set_shading", new { shading = "smooth" }));
        var smooth = await driver.CaptureAsync("cube-smooth-dir-1-1-1-ortho", new { direction = new[] { 1, 1, 1 }, projection = "orthographic" });
        CheckOutline(driver, smooth, silhouette.Select(hexagonPixel).ToArray(), 3);
        CheckDiffers(driver, smooth, topNear, topFar, "the smooth top face, near the centre corner and near the far one");
        CheckDiffers(driver, smooth, topNear, Rounded(hexagonPixel(new Vector3(0.15f, 1, 0.85f))), "the smooth top face, along the other diagonal");
        if (smooth.Pixels.AsSpan().SequenceEqual(hexagon.Pixels)) driver.Fail($"{smooth.Name}: smooth shading drew the flat frame");

        // The normals look of the smooth cube shows corner normals: across the top face the red channel (the x of the normal) grows
        // with x and the blue channel with z, since each corner's normal leans out along its diagonal. Flat, both would be constant.
        var smoothNormals = await driver.CaptureAsync("cube-smooth-normals-dir-1-1-1-ortho", new { direction = new[] { 1, 1, 1 }, projection = "orthographic", mode = "normals" });
        var topXHigh = Rounded(hexagonPixel(new Vector3(0.9f, 1, 0.5f)));
        var topXLow = Rounded(hexagonPixel(new Vector3(0.1f, 1, 0.5f)));
        var topZHigh = Rounded(hexagonPixel(new Vector3(0.5f, 1, 0.9f)));
        var topZLow = Rounded(hexagonPixel(new Vector3(0.5f, 1, 0.1f)));
        CheckNotBackground(driver, smoothNormals, topXHigh, "the smooth top face, +X side");
        CheckNotBackground(driver, smoothNormals, topXLow, "the smooth top face, -X side");
        CheckChannelGreater(driver, smoothNormals, topXHigh, topXLow, p => p.R, "red across the smooth top, +X side over -X side (a mirrored camera swaps them)");
        CheckChannelGreater(driver, smoothNormals, topZHigh, topZLow, p => p.B, "blue across the smooth top, +Z side over -Z side (a flipped camera swaps them)");

        Ok(driver, await driver.CallAsync("mark_edges", new { edges = new[] { new[] { 3, 7 }, new[] { 7, 6 }, new[] { 6, 2 }, new[] { 2, 3 } } }));
        var creased = await driver.CaptureAsync("cube-creased-dir-1-1-1-ortho", new { direction = new[] { 1, 1, 1 }, projection = "orthographic" });
        CheckOutline(driver, creased, silhouette.Select(hexagonPixel).ToArray(), 3);
        var creasedNear = creased.Pixel(topNear.X, topNear.Y).G;
        var creasedFar = creased.Pixel(topFar.X, topFar.Y).G;
        if (creasedNear != flatTopNear || creasedFar != flatTopFar)
            driver.Fail($"{creased.Name}: the creased top face should keep its flat shade {flatTopNear}, got {creasedNear} and {creasedFar}");
        CheckDiffers(driver, creased, Rounded(hexagonPixel(new Vector3(0.5f, 0.9f, 1))), Rounded(hexagonPixel(new Vector3(0.5f, 0.1f, 1))), "the +Z face, graded from its top edge to its bottom one");

        // Creased, the top face's corners keep the face normal, so the top is one colour in the normals look, and the +Z face is still blended:
        // its top corners lean toward +Z and the sides only, its bottom ones down as well. The +X face blends in z.
        var creasedNormals = await driver.CaptureAsync("cube-creased-normals-dir-1-1-1-ortho", new { direction = new[] { 1, 1, 1 }, projection = "orthographic", mode = "normals" });
        var creasedTop = (Rounded(hexagonPixel(new Vector3(0.9f, 1, 0.5f))), Rounded(hexagonPixel(new Vector3(0.1f, 1, 0.5f))));
        var sideHigh = Rounded(hexagonPixel(new Vector3(1, 0.5f, 0.9f)));
        var sideLow = Rounded(hexagonPixel(new Vector3(1, 0.5f, 0.1f)));
        var upperFront = Rounded(hexagonPixel(new Vector3(0.5f, 0.9f, 1)));
        var lowerFront = Rounded(hexagonPixel(new Vector3(0.5f, 0.1f, 1)));
        CheckNotBackground(driver, creasedNormals, creasedTop.Item1, "the creased top face, +X side");
        if (creasedNormals.Pixel(creasedTop.Item1.X, creasedTop.Item1.Y) != creasedNormals.Pixel(creasedTop.Item2.X, creasedTop.Item2.Y))
            driver.Fail($"{creasedNormals.Name}: the creased top face should be one colour in the normals look");
        CheckChannelGreater(driver, creasedNormals, sideHigh, sideLow, p => p.B, "blue across the creased +X face, +Z end over -Z end (a mirrored camera swaps them)");
        CheckChannelGreater(driver, creasedNormals, upperFront, lowerFront, p => p.G, "green down the creased +Z face, top over bottom (a flipped camera swaps them)");

        var every = new[] { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 0 }, new[] { 4, 5 }, new[] { 5, 6 }, new[] { 6, 7 }, new[] { 7, 4 }, new[] { 0, 4 }, new[] { 1, 5 }, new[] { 2, 6 }, new[] { 3, 7 } };
        Ok(driver, await driver.CallAsync("mark_edges", new { edges = every }));
        var allHard = await driver.CaptureAsync("cube-smooth-all-hard-dir-1-1-1-ortho", new { direction = new[] { 1, 1, 1 }, projection = "orthographic" });
        if (!allHard.Pixels.AsSpan().SequenceEqual(hexagon.Pixels)) driver.Fail($"{allHard.Name}: smooth with every edge hard should draw the flat frame pixel for pixel");

        // 2d. T-42: the cylinder, a sixteen-sided prism (axis Y, radius 1, y -1 to 1) defined through define_mesh, shown flat and then smooth with
        // both cap rims marked hard: a soft side all round and a crease where each cap meets it. The view (2, 2, 1) is not symmetric about
        // x = z, so a mirrored camera draws another frame (the cylinder is otherwise its own mirror image). Points are worked from the mesh.
        const int Sides = 16;
        Vector3 Ring(int i, float y) { var a = 2 * MathF.PI * (i % Sides) / Sides; return new Vector3(MathF.Cos(a), y, MathF.Sin(a)); }
        Vector3 Facet(int k, float t, float y) => Vector3.Lerp(Ring(k, y), Ring(k + 1, y), t);
        var cylVertices = Enumerable.Range(0, Sides).Select(i => Ring(i, -1)).Concat(Enumerable.Range(0, Sides).Select(i => Ring(i, 1)))
            .Select(v => new[] { (double)v.X, v.Y, v.Z }).ToArray();
        var cylFaces = new List<int[]> { Enumerable.Range(0, Sides).ToArray(), Enumerable.Range(0, Sides).Reverse().Select(i => Sides + i).ToArray() };
        for (var i = 0; i < Sides; i++) cylFaces.Add([i, Sides + i, Sides + (i + 1) % Sides, (i + 1) % Sides]);
        await DefineCleanAsync(driver, "cylinder", cylVertices, cylFaces.ToArray());
        var cylView = new { direction = new[] { 2, 2, 1 }, projection = "orthographic" };
        var cylNormalsView = new { direction = new[] { 2, 2, 1 }, projection = "orthographic", mode = "normals" };
        var cylPixel = OrthographicPixels(new Vector3(-1, -1, -1), Vector3.One, new Vector3(2, 2, 1), Vector3.UnitY, 512);
        (int X, int Y) CylAt(Vector3 p) => Rounded(cylPixel(p));
        var edgeAt = CylAt(Facet(1, 1, 0));                      // the side edge at 45 degrees, mid height, a vertical line in the image
        var edgeLeft = (edgeAt.X - 3, edgeAt.Y);
        var edgeRight = (edgeAt.X + 3, edgeAt.Y);
        var capPoint = CylAt(Facet(1, 0.5f, 1) * new Vector3(0.85f, 1, 0.85f));
        var capOther = CylAt(new Vector3(-0.3f, 1, -0.2f));
        var belowRim = CylAt(Facet(1, 0.5f, 0.85f));
        // The side's two ends across the image, far enough apart in n.l that MinimumStep holds under the PBR material (T-82, the person's
        // ruling of 2026-11-13: re-pick, else lower). The light turned with this camera gives facet 13 an n.l of 0.03 and facet 3 one of
        // 0.23, so 119 against 131 (grey 0.72, ambient 0.25, light 0.9 / pi, sRGB); facets 14 and 3 were 127 against 131, 4 apart.
        var sideMirror = CylAt(Facet(13, 0.5f, 0));              // facing (+x, -z), at the image's right edge: the one side of the image
        // Under M-7's sky the side's whole range is 7 (facets 4 to 13 measure 190 to 197), so the other end is facet 1, not 3, and the pair
        // is held to SideEnds, not MinimumStep: the person's ruling of 2026-11-17 ("Accept all", M-19 row 3a). Measured 191 against 197.
        var sideOther = CylAt(Facet(1, 0.5f, 0));                // facing (+x, +z) at 22.5 to 45 degrees: the other side
        const int SideEnds = 4;
        // Facet 14's two ends, a plateau when flat. The smooth side's grade across it is no longer checked in the shaded frame: under M-7's
        // sky it is 1 to 2 (193 against 194), and the person ruled on 2026-11-17 ("Accept all", M-19 row 3b) to drop it; the normals look's
        // red across facet 2 shows the smooth side grading within a facet.
        var gradeLow = CylAt(Facet(14, 0.2f, 0));
        var gradeHigh = CylAt(Facet(14, 0.8f, 0));

        var cylFlat = await driver.CaptureAsync("cylinder-flat-dir-2-2-1-ortho", cylView);
        CheckNotBackground(driver, cylFlat, capPoint, "the flat cylinder's top cap");
        CheckNotBackground(driver, cylFlat, belowRim, "the flat cylinder's side, below the rim");
        CheckNotBackground(driver, cylFlat, sideMirror, "the flat cylinder's side, on one side of the image");
        CheckNotBackground(driver, cylFlat, sideOther, "the flat cylinder's side, on the other side of the image");
        CheckCapOneShade(driver, cylFlat, capPoint, capOther, "the flat cap is one shade");
        CheckSame(driver, cylFlat, CylAt(Facet(2, 0.2f, 0)), CylAt(Facet(2, 0.8f, 0)), "the flat side facet is one shade, a plateau");
        CheckDiffers(driver, cylFlat, capPoint, belowRim, "the flat cap against the side below the rim");
        CheckDiffers(driver, cylFlat, sideMirror, sideOther, "the flat side, at its two ends across the image (a mirrored camera draws other facets)", SideEnds);
        CheckBrightestOf(driver, cylFlat, capPoint, "the cap is the brightest part, so a flipped camera (cap below) fails", sideMirror, sideOther, belowRim);

        var cylFlatNormals = await driver.CaptureAsync("cylinder-flat-normals-dir-2-2-1-ortho", cylNormalsView);
        CheckSame(driver, cylFlatNormals, CylAt(Facet(2, 0.2f, 0)), CylAt(Facet(2, 0.8f, 0)), "the flat side facet is one colour in the normals look");

        Ok(driver, await driver.CallAsync("set_shading", new { shading = "smooth" }));
        var rim = new List<int[]>();
        for (var i = 0; i < Sides; i++) { rim.Add([i, (i + 1) % Sides]); rim.Add([Sides + i, Sides + (i + 1) % Sides]); }
        Ok(driver, await driver.CallAsync("mark_edges", new { edges = rim }));

        var cylSmooth = await driver.CaptureAsync("cylinder-smooth-dir-2-2-1-ortho", cylView);
        CheckCapOneShade(driver, cylSmooth, capPoint, capOther, "the cap keeps one shade, its rim being hard");
        if (cylSmooth.Pixel(capPoint.X, capPoint.Y) != cylFlat.Pixel(capPoint.X, capPoint.Y)) driver.Fail($"{cylSmooth.Name}: the hard-rimmed cap should keep its flat shade");
        CheckClose(driver, cylSmooth, edgeLeft, edgeRight, "the smooth side runs on across a facet edge", 2);
        CheckSame(driver, cylFlat, gradeLow, gradeHigh, "the flat side facet 14 is one shade, a plateau");
        CheckDiffers(driver, cylSmooth, capPoint, belowRim, "the crease where the cap meets the side");
        CheckDiffers(driver, cylSmooth, sideMirror, sideOther, "the smooth side, at its two ends across the image (a mirrored camera draws other facets)", SideEnds);
        CheckBrightestOf(driver, cylSmooth, capPoint, "the cap is the brightest part, so a flipped camera (cap below) fails", sideMirror, sideOther, belowRim);

        var cylSmoothNormals = await driver.CaptureAsync("cylinder-smooth-normals-dir-2-2-1-ortho", cylNormalsView);
        CheckChannelGreater(driver, cylSmoothNormals, CylAt(Facet(2, 0.2f, 0)), CylAt(Facet(2, 0.8f, 0)), p => p.R, "red across one smooth facet, its 45 degree end over its 67 degree end");
        CheckClose(driver, cylSmoothNormals, edgeLeft, edgeRight, "the smooth side's colour runs on across a facet edge");
        CheckChannelGreater(driver, cylSmoothNormals, CylAt(Facet(14, 0.5f, 0)), CylAt(Facet(3, 0.5f, 0)), p => p.R, "red on the +x side facet over the +z one (a mirrored camera swaps them)");
        CheckChannelGreater(driver, cylSmoothNormals, CylAt(Facet(3, 0.5f, 0)), CylAt(Facet(14, 0.5f, 0)), p => p.B, "blue on the +z side facet over the -z one (a mirrored camera swaps them)");
        CheckChannelGreater(driver, cylSmoothNormals, capPoint, belowRim, p => p.G, "green on the cap (up) over the side just below the rim (a flipped camera swaps them)");
        CheckDiffers(driver, cylSmoothNormals, capPoint, belowRim, "the crease at the rim in the normals look");
        CheckSame(driver, cylSmoothNormals, capPoint, capOther, "the hard-rimmed cap is one colour in the normals look");

        // 2e. T-43: the same cylinder subdivided twice, rims still hard: 64 facets round, the caps flat, a crease at each rim. Then a fresh
        // cylinder, smooth with no hard edge, subdivided twice: its rims round off into a pillow. Points are the cylinder's own, as above.
        var farRim = CylAt(Ring(9, 0.97f) * new Vector3(0.9f, 1, 0.9f));   // just inside the far top rim, near the top of the image
        var capCentre = CylAt(new Vector3(0, 1, 0));
        // With no hard rim the twice-subdivided cylinder rounds in, so facet 13's point at mid height is background there. Its far end is taken
        // lower on the side and inside the silhouette instead: the cage's vertex 14 (315 degrees, between facets 13 and 14) at y -0.3, pulled in
        // to 0.9 of the radius. The pillow's normal there has turned down and past the silhouette's, so it faces away from the light (n.l at or
        // below 0) and is lit by the ambient alone: grey 0.72 x ambient 0.25 = 0.18, sRGB 118, against facet 3's 131, 13 apart under
        // MinimumStep 6 (T-82, the person's ruling of 2026-11-13: re-pick, else lower).
        var softSideMirror = CylAt(Ring(14, -0.3f) * new Vector3(0.9f, 1, 0.9f));
        // Its other end is facet 1 at mid height (pulled in to 0.9 of the radius, inside the pillow's silhouette), not facet 3: under M-7's
        // sky facet 3 is 192 against 188, and facet 1 measures 197 to 198. The person's ruling of 2026-11-17 ("Accept all", M-19 row 3c);
        // MinimumStep 6 kept.
        var softSideOther = CylAt(Facet(1, 0.5f, 0) * new Vector3(0.9f, 1, 0.9f));
        Ok(driver, await driver.CallAsync("subdivide", new { levels = 2 }));
        var subSmooth = await driver.CaptureAsync("cylinder-subdivided-smooth-dir-2-2-1-ortho", cylView);
        CheckNotBackground(driver, subSmooth, farRim, "the subdivided cylinder just inside its far top rim, the rim being hard");
        CheckCapOneShade(driver, subSmooth, capPoint, capOther, "the subdivided cap keeps one shade, its rim being hard");
        if (subSmooth.Pixel(capPoint.X, capPoint.Y) != cylSmooth.Pixel(capPoint.X, capPoint.Y)) driver.Fail($"{subSmooth.Name}: the hard-rimmed cap should keep the shade it had before subdividing");
        CheckDiffers(driver, subSmooth, capPoint, belowRim, "the crease where the subdivided cap meets the side");
        CheckClose(driver, subSmooth, edgeLeft, edgeRight, "the subdivided side runs on across the old facet edge", 2);
        CheckDiffers(driver, subSmooth, sideMirror, sideOther, "the subdivided side, at its two ends across the image (a mirrored camera draws other facets)", SideEnds);
        CheckBrightestOf(driver, subSmooth, capPoint, "the subdivided cap is the brightest part, so a flipped camera (cap below) fails", sideMirror, sideOther, belowRim);

        Ok(driver, await driver.CallAsync("set_shading", new { shading = "flat" }));
        var subFlatNormals = await driver.CaptureAsync("cylinder-subdivided-flat-normals-dir-2-2-1-ortho", cylNormalsView);
        CheckChannelGreater(driver, subFlatNormals, CylAt(Facet(2, 0.2f, 0)), CylAt(Facet(2, 0.8f, 0)), p => p.R, "red across one old facet, now four facets, its 49 degree end over its 63 degree end (one colour before subdividing)");
        CheckChannelGreater(driver, subFlatNormals, CylAt(Facet(14, 0.5f, 0)), CylAt(Facet(3, 0.5f, 0)), p => p.R, "red on the +x side over the +z one (a mirrored camera swaps them)");
        CheckChannelGreater(driver, subFlatNormals, capPoint, belowRim, p => p.G, "green on the subdivided cap (up) over the side below the rim (a flipped camera swaps them)");
        CheckSame(driver, subFlatNormals, capPoint, capOther, "the subdivided cap is one colour in the normals look");

        await DefineCleanAsync(driver, "cylinder", cylVertices, cylFaces.ToArray());
        Ok(driver, await driver.CallAsync("set_shading", new { shading = "smooth" }));
        Ok(driver, await driver.CallAsync("subdivide", new { levels = 2 }));
        var subSoft = await driver.CaptureAsync("cylinder-subdivided-soft-rims-dir-2-2-1-ortho", cylView);
        CheckBackground(driver, subSoft, farRim, "just inside the far top rim, rounded away with no hard rim");
        CheckNotBackground(driver, subSoft, capCentre, "the soft cylinder's top");
        CheckNotBackground(driver, subSoft, softSideMirror, "the soft cylinder's side, on one side of the image");
        CheckNotBackground(driver, subSoft, softSideOther, "the soft cylinder's side, on the other side of the image");
        CheckDiffers(driver, subSoft, softSideMirror, softSideOther, "the soft side, at its two ends across the image (a mirrored camera draws other parts)");
        CheckBrightestOf(driver, subSoft, capCentre, "the soft top is the brightest part, so a flipped camera (top below) fails", softSideMirror, softSideOther);

        // 3. The L-block: two concave hexagon caps and six quad sides.
        var (lVertices, lFaces) = LBlock();
        await DefineCleanAsync(driver, "L-block", lVertices, lFaces);
        var lFront = await driver.CaptureAsync("lblock-front", new { view = "front" });
        var lBack = await driver.CaptureAsync("lblock-back", new { view = "back" });
        CheckCorners(driver, lFront);
        CheckCorners(driver, lBack);
        CheckDrawn(driver, lFront, LBlockFrontDrawn);
        CheckDrawn(driver, lBack, LBlockBackDrawn);
        CheckBackground(driver, lFront, NotchFront, "the L-block's notch");

        // 3b. T-9: up is -Z. Zoomed on the block's corner, only the lower left quarter is the mesh.
        var lCorner = await driver.CaptureAsync("lblock-dir-0--1-0-corner", new { direction = new[] { 0, -1, 0 }, zoom = 3, target = new[] { 0, 0, 0 } });
        CheckNotBackground(driver, lCorner, CornerBlock, "the lower left quarter (the block, with -Z up and +X left)");
        CheckBackground(driver, lCorner, CornerAbove, "the upper left quarter (above the block's top face)");
        CheckBackground(driver, lCorner, CornerRight, "the lower right quarter (the block would be there from +Y)");
        CheckBackground(driver, lCorner, CornerUpperRight, "the upper right quarter (the block would be there with +Z up)");

        // 3c. T-24: looking up from below, orthographic, -Z up. The L-block above is a plain rectangle
        // from -Y, so this one lies in the XZ plane (the person's decision, 2026-11-07). +X is left and
        // +Z down, so the X arm runs across the top, the Z arm down the right, and the notch is the
        // lower left quarter. A mirrored camera, or one at +Y, puts the notch lower right and fails on
        // the notch pixel, which it fills; a camera with +Z up puts it upper left and fails there too.
        var (fVertices, fFaces) = LBlockLying();
        await DefineCleanAsync(driver, "lying L-block", fVertices, fFaces);
        var lBelow = await driver.CaptureAsync("lblock-lying-minusy-ortho", new { view = "-y", projection = "orthographic" });
        CheckCaption(driver, lBelow, "512x512, view -y, orthographic, 12 vertices, 8 faces");
        CheckCorners(driver, lBelow);
        CheckShades(driver, lBelow, 1);
        var lBelowPixel = OrthographicPixels(Vector3.Zero, new Vector3(2, 1, 2), -Vector3.UnitY, -Vector3.UnitZ, lBelow.Width);
        CheckBackground(driver, lBelow, Rounded(lBelowPixel(new Vector3(1.5f, 0, 1.5f))), "the notch (a mirrored or flipped camera fills it)");
        CheckNotBackground(driver, lBelow, Rounded(lBelowPixel(new Vector3(1.5f, 0, 0.5f))), "the X arm, upper left");
        CheckNotBackground(driver, lBelow, Rounded(lBelowPixel(new Vector3(0.5f, 0, 1.5f))), "the Z arm, lower right");

        // 4. A long box, off the origin: framing has to find it.
        var (bVertices, bFaces) = Box(5, 2, -3, 4, 0.5, 0.5);
        await DefineAsync(driver, "long box", bVertices, bFaces);
        var box = await driver.CaptureAsync("longbox-front", new { view = "front" });
        CheckCorners(driver, box);
        CheckDrawn(driver, box, LongBoxDrawn);
        CheckEdges(driver, box);

        // 5. A cube with one face wound backwards: accepted with a warning, kept as a record of the look.
        var flipped = CubeFaces.Select(f => f.ToArray()).ToArray();
        Array.Reverse(flipped[1]);
        var warned = await DefineAsync(driver, "flipped cube", CubeVertices, flipped);
        if (warned.GetProperty("warnings").GetArrayLength() == 0)
        {
            driver.Fail($"flipped cube: expected a warning, got {warned}");
        }

        var flippedFront = await driver.CaptureAsync("cube-flipped-front", new { view = "front" });
        CheckCorners(driver, flippedFront);
        CheckDrawn(driver, flippedFront, FlippedCubeDrawn);

        // 6. A bad index is a tool error, and the mesh before it still renders.
        var bad = await driver.CallAsync("define_mesh", new { vertices = new[] { new[] { 0, 0, 0 }, new[] { 1, 0, 0 }, new[] { 0, 1, 0 } }, faces = new[] { new[] { 0, 1, 9 } } });
        if (bad.IsError is not true)
        {
            driver.Fail("a bad index was not a tool error");
        }

        var afterError = await driver.CaptureAsync("after-error-front", new { view = "front" });
        driver.AssertBytesEqual("the mesh before a failed define_mesh", flippedFront.Pixels, afterError.Pixels);
        CheckDrawn(driver, afterError, FlippedCubeDrawn);
    }

    private static async Task<JsonElement> DefineAsync(ScenarioDriver driver, string what, double[][] vertices, int[][] faces)
    {
        var result = await driver.CallAsync("define_mesh", new { vertices, faces });
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
        if (result.IsError is true)
        {
            driver.Fail($"define_mesh {what}: {text}");
        }

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task DefineCleanAsync(ScenarioDriver driver, string what, double[][] vertices, int[][] faces)
    {
        var report = await DefineAsync(driver, what, vertices, faces);
        if (!report.GetProperty("closed").GetBoolean() || report.GetProperty("warnings").GetArrayLength() != 0)
        {
            driver.Fail($"{what}: expected closed and no warnings, got {report}");
        }
    }

    /// <summary>The L of six corners in the XY plane, one thick in Z.</summary>
    private static (double[][] Vertices, int[][] Faces) LBlock()
    {
        double[][] outline = [[0, 0], [2, 0], [2, 1], [1, 1], [1, 2], [0, 2]]; // counter-clockwise from +Z
        var vertices = outline.Select(p => new[] { p[0], p[1], 0 }).Concat(outline.Select(p => new[] { p[0], p[1], 1 })).ToArray();
        var faces = new List<int[]> { new[] { 5, 4, 3, 2, 1, 0 }, new[] { 6, 7, 8, 9, 10, 11 } }; // -Z cap, +Z cap
        for (var i = 0; i < 6; i++)
        {
            var j = (i + 1) % 6;
            faces.Add([i, j, j + 6, i + 6]);
        }

        return (vertices, faces.ToArray());
    }

    /// <summary>
    /// The same L turned a quarter about X, so its outline is in the XZ plane and it is one thick in Y.
    /// A rotation, so the faces keep their winding.
    /// </summary>
    private static (double[][] Vertices, int[][] Faces) LBlockLying()
    {
        var (vertices, faces) = LBlock();
        return (vertices.Select(v => new[] { v[0], 1 - v[2], v[1] }).ToArray(), faces);
    }

    private static (double[][] Vertices, int[][] Faces) Box(double x, double y, double z, double dx, double dy, double dz)
    {
        double[][] vertices =
        [
            [x, y, z], [x + dx, y, z], [x + dx, y + dy, z], [x, y + dy, z],
            [x, y, z + dz], [x + dx, y, z + dz], [x + dx, y + dy, z + dz], [x, y + dy, z + dz],
        ];
        return (vertices, CubeFaces);
    }

    private static async Task ExpectErrorAsync(ScenarioDriver driver, string what, object arguments, string? mentions = null)
    {
        var result = await driver.CallAsync("render", arguments);
        if (result.IsError is not true)
        {
            driver.Fail($"render with {what} was not a tool error");
        }

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
        if (mentions is not null && !text.Contains(mentions, StringComparison.Ordinal))
        {
            driver.Fail($"render with {what} said '{text}', expected '{mentions}'");
        }
    }

    /// <summary>The cube's front at another size: the 512 frame's checks, its sample points scaled.</summary>
    private static async Task CheckSizedFrontAsync(ScenarioDriver driver, int size)
    {
        var frame = await driver.CaptureAsync($"cube-front-{size}", new { view = "front", size });
        if (frame.Width != size || frame.Height != size)
        {
            driver.Fail($"{frame.Name}: the PNG is {frame.Width}x{frame.Height}, expected {size}x{size}");
        }

        CheckCaption(driver, frame, $"{size}x{size}, view front, 8 vertices, 6 faces");
        CheckCorners(driver, frame);
        CheckNotBackground(driver, frame, (size / 2, size / 2), "the centre");
        CheckFrontOrder(driver, frame, CubeFront.Select(p => (p.X * size / 512, p.Y * size / 512)).ToArray());
    }

    /// <summary>One pixel at least <see cref="MinimumStep"/> brighter than another, in the green channel.</summary>
    private static void CheckBrighter(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) brighter, (int X, int Y) darker, string what)
    {
        var (high, low) = (frame.Pixel(brighter.X, brighter.Y).G, frame.Pixel(darker.X, darker.Y).G);
        if (high < low + MinimumStep)
        {
            driver.Fail($"{frame.Name}: {what}: {high} should be at least {MinimumStep} above {low}");
        }
    }

    /// <summary>Two pixels at least <see cref="MinimumStep"/> apart in the green channel, either way.</summary>
    private static void CheckDiffers(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) a, (int X, int Y) b, string what, int least = MinimumStep)
    {
        var (first, second) = (frame.Pixel(a.X, a.Y).G, frame.Pixel(b.X, b.Y).G);
        if (Math.Abs(first - second) < least)
        {
            driver.Fail($"{frame.Name}: {what}: {first} and {second} should differ by at least {least}");
        }
    }

    /// <summary>One channel of the first pixel exceeding the second's by at least <see cref="MinimumStep"/>, in that order only.</summary>
    private static void CheckChannelGreater(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) high, (int X, int Y) low, Func<(byte R, byte G, byte B, byte A), int> channel, string what)
    {
        var (first, second) = (channel(frame.Pixel(high.X, high.Y)), channel(frame.Pixel(low.X, low.Y)));
        if (first - second < MinimumStep)
        {
            driver.Fail($"{frame.Name}: {what}: {first} against {second} should differ by at least {MinimumStep}, the first greater");
        }
    }

    /// <summary>Two pixels of exactly one colour.</summary>
    private static void CheckSame(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) a, (int X, int Y) b, string what)
    {
        if (frame.Pixel(a.X, a.Y) != frame.Pixel(b.X, b.Y)) driver.Fail($"{frame.Name}: {what}: {frame.Pixel(a.X, a.Y)} against {frame.Pixel(b.X, b.Y)}");
    }

    /// <summary>
    /// A flat cap is one shade, to 1 in a channel. Under LitMesh it was equal to the bit; the engine's PBR material adds a specular term
    /// that follows the view vector, and the orthographic frame keeps the perspective eye, so the cylinder's cap reads (164, 164, 161)
    /// at one point and (164, 164, 162) at another. Allowed by the person's ruling of 2026-11-13 (T-82): "the cap allows 1". A cap drawn
    /// smooth through a soft rim grades by far more.
    /// </summary>
    private static void CheckCapOneShade(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) a, (int X, int Y) b, string what) =>
        CheckClose(driver, frame, a, b, what, 1);

    /// <summary>Two pixels within <paramref name="most"/> of each other in every channel: a continuous run.</summary>
    private static void CheckClose(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) a, (int X, int Y) b, string what, int most = 4)
    {
        var (p, q) = (frame.Pixel(a.X, a.Y), frame.Pixel(b.X, b.Y));
        if (Math.Abs(p.R - q.R) > most || Math.Abs(p.G - q.G) > most || Math.Abs(p.B - q.B) > most)
            driver.Fail($"{frame.Name}: {what}: {p} against {q} should be within {most} in each channel");
    }

    /// <summary>The first pixel brighter (green) than every other by at least <see cref="MinimumStep"/>.</summary>
    private static void CheckBrightestOf(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) top, string what, params (int X, int Y)[] others)
    {
        var high = frame.Pixel(top.X, top.Y).G;
        foreach (var o in others)
            if (high < frame.Pixel(o.X, o.Y).G + MinimumStep) driver.Fail($"{frame.Name}: {what}: {high} against {frame.Pixel(o.X, o.Y).G} at {o}");
    }

    private static void Ok(ScenarioDriver driver,ModelContextProtocol.Protocol.CallToolResult result)
    {
        if (result.IsError == true) driver.Fail("tool error: " + string.Join(" ", result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(t => t.Text)));
    }

    private static void CheckCaption(ScenarioDriver driver, ScenarioFrame frame, string expected)
    {
        if (frame.Caption != expected)
        {
            driver.Fail($"{frame.Name}: the caption is '{frame.Caption}', expected '{expected}'");
        }
    }

    /// <summary>
    /// Worked out here, not asked of the product: where a point of the mesh lands, in pixels from the
    /// top left, under the orthographic framing rule, for the box <paramref name="min"/> to
    /// <paramref name="max"/>. Zoom and target as <c>render</c> takes them; the target defaults to the
    /// centre. Near and far do not move a pixel, so they are any that hold the box.
    /// </summary>
    private static Func<Vector3, Vector2> OrthographicPixels(
        Vector3 min, Vector3 max, Vector3 direction, Vector3 up, int size, float zoom = 1, Vector3? target = null)
    {
        var centre = (min + max) / 2;
        var radius = (max - min).Length() / 2;
        Vector3 Drawn(Vector3 p) => (p - centre) / radius;

        var look = target is { } t ? Drawn(t) : Vector3.Zero;
        var view = Matrix4x4.CreateLookAt(look + Vector3.Normalize(direction) * 10, look, up);
        var reach = 0f;
        foreach (var x in new[] { min.X, max.X })
        foreach (var y in new[] { min.Y, max.Y })
        foreach (var z in new[] { min.Z, max.Z })
        {
            var seen = Vector3.TransformNormal(Drawn(new Vector3(x, y, z)) - look, view);
            reach = Math.Max(reach, Math.Max(Math.Abs(seen.X), Math.Abs(seen.Y)));
        }

        var height = reach / (1 - 2 * Margin) / zoom;
        var camera = view * Matrix4x4.CreateOrthographic(2 * height, 2 * height, 0.1f, 100f);
        return p =>
        {
            var device = Vector3.Transform(Drawn(p), camera);
            return new Vector2((device.X + 1) / 2 * size, (1 - device.Y) / 2 * size);
        };
    }

    /// <summary>An offset in pixels, by an angle anticlockwise from image right. Image y runs down.</summary>
    private static Vector2 AtAngle(float degrees, float length) =>
        new Vector2(MathF.Cos(degrees * MathF.PI / 180), -MathF.Sin(degrees * MathF.PI / 180)) * length;

    private static (int X, int Y) Rounded(Vector2 pixel) => ((int)MathF.Round(pixel.X), (int)MathF.Round(pixel.Y));

    /// <summary>
    /// Each corner of a convex outline: the mesh <paramref name="by"/> pixels inside it, toward the
    /// outline's centre, and the background as far outside.
    /// </summary>
    private static void CheckOutline(ScenarioDriver driver, ScenarioFrame frame, Vector2[] corners, float by)
    {
        var middle = corners.Aggregate(Vector2.Zero, (sum, c) => sum + c) / corners.Length;
        foreach (var corner in corners)
        {
            var inward = Vector2.Normalize(middle - corner);
            CheckNotBackground(driver, frame, Rounded(corner + inward * by), $"{by} px inside the corner at {corner}");
            CheckBackground(driver, frame, Rounded(corner - inward * by), $"{by} px outside the corner at {corner}");
        }
    }

    /// <summary>
    /// A face filling the margin square of a 512 frame: on three lines each way its edge pixels lie at
    /// <see cref="MarginLow"/> and <see cref="MarginHigh"/> to within 1 px, with background 2 px outside.
    /// </summary>
    private static void CheckMarginSquare(ScenarioDriver driver, ScenarioFrame frame)
    {
        foreach (var line in new[] { 60, 256, 450 })
        {
            CheckNotBackground(driver, frame, (MarginLow + 1, line), "the left edge");
            CheckNotBackground(driver, frame, (MarginHigh - 1, line), "the right edge");
            CheckNotBackground(driver, frame, (line, MarginLow + 1), "the top edge");
            CheckNotBackground(driver, frame, (line, MarginHigh - 1), "the bottom edge");
            CheckBackground(driver, frame, (MarginLow - 2, line), "2 px left of the face");
            CheckBackground(driver, frame, (MarginHigh + 2, line), "2 px right of the face");
            CheckBackground(driver, frame, (line, MarginLow - 2), "2 px above the face");
            CheckBackground(driver, frame, (line, MarginHigh + 2), "2 px below the face");
        }
    }

    private static void CheckNotBackground(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) at, string what)
    {
        var p = frame.Pixel(at.X, at.Y);
        if (IsBackground(p))
        {
            driver.Fail($"{frame.Name}: {what} at ({at.X}, {at.Y}) is the background, expected the mesh");
        }
    }

    /// <summary>
    /// The number of face shades: green values at least <see cref="MinimumStep"/> apart and from the
    /// background, each held by at least 1,500 pixels (a flat face is one value; edges are a few hundred).
    /// </summary>
    private static void CheckShades(ScenarioDriver driver, ScenarioFrame frame, int expected)
    {
        var counts = new int[256];
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                counts[frame.Pixel(x, y).G]++;
            }
        }

        var shades = new List<int>();
        for (var g = 0; g < 256; g++)
        {
            if (counts[g] >= 1500 && !IsBackground((48, (byte)g, 56, 255)) && shades.All(s => Math.Abs(s - g) >= MinimumStep))
            {
                shades.Add(g);
            }
        }

        if (shades.Count != expected)
        {
            driver.Fail($"{frame.Name}: {shades.Count} face shades ({string.Join(", ", shades)}), expected {expected}");
        }
    }

    private static bool IsBackground((byte R, byte G, byte B, byte A) p) =>
        p.R is >= 47 and <= 49 && p.G is >= 51 and <= 53 && p.B is >= 55 and <= 57;

    /// <summary>
    /// A blank frame passes every background check, so a frame must also show the mesh: at least
    /// <paramref name="minimum"/> pixels that are not the background.
    /// </summary>
    private static void CheckDrawn(ScenarioDriver driver, ScenarioFrame frame, int minimum)
    {
        var drawn = 0;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                if (!IsBackground(frame.Pixel(x, y)))
                {
                    drawn++;
                }
            }
        }

        if (drawn < minimum)
        {
            driver.Fail($"{frame.Name}: only {drawn} pixels are not the background, expected at least {minimum}");
        }
    }

    private static void CheckCorners(ScenarioDriver driver, ScenarioFrame frame)
    {
        var (w, h) = (frame.Width - 1, frame.Height - 1);
        foreach (var (x, y) in new[] { (0, 0), (w, 0), (0, h), (w, h) })
        {
            CheckBackground(driver, frame, (x, y), "a corner");
        }
    }

    private static void CheckBackground(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) at, string what)
    {
        var p = frame.Pixel(at.X, at.Y);
        if (!IsBackground(p))
        {
            driver.Fail($"{frame.Name}: {what} at ({at.X}, {at.Y}) is {p}, not the background");
        }
    }

    private static void CheckEdges(ScenarioDriver driver, ScenarioFrame frame)
    {
        for (var i = 0; i < frame.Width; i++)
        {
            CheckBackground(driver, frame, (i, 0), "the top edge");
            CheckBackground(driver, frame, (i, frame.Height - 1), "the bottom edge");
        }

        for (var i = 0; i < frame.Height; i++)
        {
            CheckBackground(driver, frame, (0, i), "the left edge");
            CheckBackground(driver, frame, (frame.Width - 1, i), "the right edge");
        }
    }

    /// <summary>
    /// The front view's three faces (top, +Z, +X): the top at least <see cref="MinimumStep"/> above +Z, and each face at least
    /// that above the background. +Z over +X is not checked: under M-7's sky the two step by 2 (the light model predicts 3), and
    /// the person ruled on 2026-11-17 ("Accept all", M-19 row 1) to drop that pair from front views; the back view keeps the
    /// three-face order (<see cref="CheckOrder"/>).
    /// </summary>
    private static void CheckFrontOrder(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y)[] at)
    {
        string[] faces = ["top", "+Z", "+X"];
        var g = at.Select(a => (int)frame.Pixel(a.X, a.Y).G).ToArray();
        if (g[0] < g[1] + MinimumStep)
        {
            driver.Fail($"{frame.Name}: top ({g[0]}) should be at least {MinimumStep} brighter than +Z ({g[1]})");
        }

        for (var i = 0; i < 3; i++)
        {
            if (g[i] < 53 + MinimumStep)
            {
                driver.Fail($"{frame.Name}: {faces[i]} ({g[i]}) should be at least {MinimumStep} above the background");
            }
        }
    }

    /// <summary>Three faces, brightest first, each at least <see cref="MinimumStep"/> above the next.</summary>
    private static void CheckOrder(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y)[] at, params string[] faces)
    {
        var g = at.Select(a => (int)frame.Pixel(a.X, a.Y).G).ToArray();
        for (var i = 0; i < 2; i++)
        {
            if (g[i] < g[i + 1] + MinimumStep)
            {
                driver.Fail($"{frame.Name}: {faces[i]} ({g[i]}) should be at least {MinimumStep} brighter than {faces[i + 1]} ({g[i + 1]})");
            }
        }

        if (g[2] < 53 + MinimumStep)
        {
            driver.Fail($"{frame.Name}: {faces[2]} ({g[2]}) should be at least {MinimumStep} above the background");
        }
    }
}
