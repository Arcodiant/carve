using System.Numerics;
using Carve.Debug;
using Carve.Scenario.Crate;
using static Carve.Scenario.Shed.Checks;

namespace Carve.Scenario.Shed;

/// <summary>The shed in each look, size and sheet, before it is moved.</summary>
internal static class ShedLooks
{
    private static readonly Vector3 ShedFront = new(1.6f, 1.2f, 2.2f);

    private static readonly Vector3 Min = Vector3.Zero;
    private static readonly Vector3 Max = new(4, 3.9f, 3);
    private static readonly float Radius = (Max - Min).Length() / 2;
    private const string Counts = "25 vertices, 18 faces";

    public static async Task RunAsync(ScenarioDriver driver, Shape shed, Camera camera, ScenarioFrame shaded, int[][] openEdges)
    {
        var spikes = shed.Faces.Select((_, f) =>
        {
            var c = shed.Centre(f);
            return (camera.Project(c), camera.Project(c + shed.Normal(f) * 0.1f * Radius));
        }).ToArray();

        // The ruling of 2026-11-07: the flipped -X wall, face 2, has its normal toward +X.
        if (Vector3.Distance(shed.Normal(2), Vector3.UnitX) > 1e-3)
        {
            Note($"face 2's normal is {shed.Normal(2)}, expected +X: the flipped -X wall faces into the shed");
        }

        // ---- labels faces, 512 ----
        var labelled = await driver.CaptureAsync("shed-front-labels-faces", new { view = "front", labels = "faces", size = 512 });
        Caption(labelled, $"512x512, view front, labels faces, {Counts}");
        Corners(labelled);
        Labels(labelled, shaded, camera, shed, faces: true, vertices: false);

        // ---- wireframe ----
        var wire = await driver.CaptureAsync("shed-front-wireframe", new { view = "front", mode = "wireframe" });
        Caption(wire, $"512x512, view front, wireframe, {Counts}");
        Corners(wire);
        // A line is drawn over the fill, so samples within 4 pixels of any edge are left out.
        var lines = shed.Faces.SelectMany(loop => loop.Select((v, k) => (camera.Project(shed.Vertices[v]), camera.Project(shed.Vertices[loop[(k + 1) % loop.Length]])))).ToArray();
        var wireTested = Samples(wire, camera, shed, (0, 0), 512, _ => Fill, background: true, "wireframe", lines);
        if (wireTested < 20)
        {
            Note($"{wire.Name}: only {wireTested} samples were surely a face or the background");
        }

        // The wireframe's faces are culled as the shaded ones are, so through the remaining -Z slope, seen from inside, the sky shows.
        var wireBehindSlope = BehindSlope(wire, camera, shed, lines, "the culled -Z roof slope, in the wireframe, so what is behind it");
        if (wireBehindSlope == 0)
        {
            Note($"{wire.Name}: no sample of the culled slope showed what is behind it");
        }

        // The ridge, along the far edge of the gap, and the far wall's top edge seen through the gap.
        Line(wire, camera.Project(new Vector3(0.5f, 3.5f, 1.5f)), camera.Project(new Vector3(3.5f, 3.5f, 1.5f)), Edge, "the ridge");
        LineStandsOff(wire, camera, new Vector3(0, 3.5f, 1.5f), new Vector3(4, 3.5f, 1.5f), "the ridge");
        var (v4, v7) = (shed.Vertices[4], shed.Vertices[7]);
        Line(wire, camera.Project(Vector3.Lerp(v4, v7, 1 / 6f)), camera.Project(Vector3.Lerp(v4, v7, 2 / 3f)), Edge, "the flipped -X wall's top edge 4-7, through the gap");

        // ---- normals ----
        var normals = await driver.CaptureAsync("shed-front-normals", new { view = "front", mode = "normals" });
        Caption(normals, $"512x512, view front, normals, {Counts}");
        Corners(normals);
        var seenFaces = new HashSet<int>();
        var tested = Samples(normals, camera, shed, (0, 0), 512, f => { seenFaces.Add(f); return NormalColour(shed.Normal(f)); }, background: true, "normals", spikes);
        if (tested < 20)
        {
            Note($"{normals.Name}: only {tested} samples were surely a face or the background");
        }

        foreach (var wanted in new[] { 2, 3 })
        {
            if (!seenFaces.Contains(wanted))
            {
                Note($"{normals.Name}: no sample showed face {wanted}");
            }
        }

        if (!seenFaces.Overlaps([4, 5, 6]))
        {
            Note($"{normals.Name}: no sample showed the +Z wall");
        }

        var behindSlope = BehindSlope(normals, camera, shed, spikes, "the culled -Z roof slope, so what is behind it");
        if (behindSlope == 0)
        {
            Note($"{normals.Name}: no sample of the culled slope showed what is behind it");
        }

        // ---- the open edges, highlighted ----
        var picked = await driver.CaptureAsync("shed-open-edges", new { view = "front", highlight = new { edges = openEdges } });
        Caption(picked, $"512x512, view front, highlight 8 edges, {Counts}");
        Corners(picked);
        Line(picked, camera.Project(new Vector3(1, 2.5f, 3)), camera.Project(new Vector3(3, 2.5f, 3)), Orange, "the eave edge 6-7");

        // Closed edges facing the eye, not asked for, are not orange: the +Z wall's corner 2-6 (kept clear of 6, where
        // open edges meet) and the base edge 1-2 under the +X wall.
        var (v1, v2, v6) = (shed.Vertices[1], shed.Vertices[2], shed.Vertices[6]);
        NotLine(picked, camera.Project(Vector3.Lerp(v2, v6, 0.1f)), camera.Project(Vector3.Lerp(v2, v6, 0.75f)), Orange, "the closed corner edge 2-6");
        NotLine(picked, camera.Project(Vector3.Lerp(v1, v2, 0.2f)), camera.Project(Vector3.Lerp(v1, v2, 0.8f)), Orange, "the closed base edge 1-2");
        foreach (var face in new[] { 3, 4 })
        foreach (var point in shed.Samples(face))
        {
            var (x, y) = camera.Pixel(point);
            if (camera.Seen(shed, x, y) == face)
            {
                // Not highlighted: R - B within 6 of the grey face's own under the sky, from the light model at the face's normal (the
                // person's ruling of 2026-11-17, "Accept all", M-19 row 5; the sky makes it about -24 here).
                var p = picked.Pixel(x, y);
                var grey = ColourRule.GreyRedLessBlue(shed.Normal(face), ShedFront);
                if (Math.Abs(p.R - p.B - grey) > 6)
                {
                    Note($"{picked.Name}: the closed wall's face {face} at {point} is {p}: it is not highlighted, so R - B should be within 6 of the grey face's {grey}");
                }
            }
        }

        // ---- the six axis views ----
        await SheetAsync(driver, shed);

        // ---- 1024, labels both ----
        var camera1024 = new Camera(Min, Max, new Vector3(1.6f, 1.2f, 2.2f), 1024);
        var plain1024 = await driver.CaptureAsync("shed-front-1024", new { view = "front", size = 1024 });
        Caption(plain1024, $"1024x1024, view front, {Counts}");
        ShedScenario.ShadedFrame(plain1024, camera1024, shed);
        var both = await driver.CaptureAsync("shed-1024-labels-both", new { view = "front", labels = "both", size = 1024 });
        Caption(both, $"1024x1024, view front, labels both, {Counts}");
        Corners(both);
        Labels(both, plain1024, camera1024, shed, faces: true, vertices: true);
    }

    /// <summary>
    /// Which labels the frame must have: the faces that face the eye (a label ignores what hides them), and the vertices on such a face.
    /// Each is white in its box and, off the letters, the plain frame at 0.4 of its linear value; the others leave the plain frame as it was.
    /// </summary>
    private static void Labels(ScenarioFrame labelled, ScenarioFrame plain, Camera camera, Shape shed, bool faces, bool vertices)
    {
        var facing = Enumerable.Range(0, shed.Faces.Length)
            .Where(f => Vector3.Dot(shed.Normal(f), camera.Eye - shed.Centre(f)) > 0).ToArray();
        var rects = new List<(string Name, (int X, int Y, int Width, int Height) Box, bool Dot, Vector2 Dotted)>();
        if (faces)
        {
            rects.AddRange(facing.Select(f => ($"face {f}", IdBox(camera.Project(shed.Centre(f)), f, true), false, Vector2.Zero)));
        }

        var facingVertices = facing.SelectMany(f => shed.Faces[f]).Distinct().Order().ToArray();
        if (vertices)
        {
            rects.AddRange(facingVertices.Select(v => ($"vertex {v}", IdBox(camera.Project(shed.Vertices[v]) + new Vector2(5, -18), v, false), true, camera.Project(shed.Vertices[v]))));
        }

        static (int X, int Y, int W, int H) Backing((int X, int Y, int Width, int Height) b) => (b.X - 2, b.Y - 2, b.Width + 4, b.Height + 4);
        bool Covered(int x, int y, string except) =>
            rects.Any(r => r.Name != except && Backing(r.Box) is var b && x >= b.X && y >= b.Y && x < b.X + b.W && y < b.Y + b.H
                || r.Name != except && r.Dot && Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), r.Dotted) < 8);

        foreach (var (name, box, dot, dotted) in rects)
        {
            if (box.X < 2 || box.Y < 2 || box.X + box.Width > labelled.Width - 2 || box.Y + box.Height > labelled.Height - 2)
            {
                continue; // runs off the image
            }

            if (!WhiteIn(labelled, box))
            {
                Note($"{labelled.Name}: {name}'s label: no white pixel in the box at ({box.X}, {box.Y}), {box.Width} x {box.Height}");
            }

            // The backing just outside the text, in its padding: the plain pixel at 0.4 of its linear value.
            var (bx, by) = (box.X - 1, box.Y - 1);
            if (!Covered(bx, by, name))
            {
                var (was, now) = (plain.Pixel(bx, by), labelled.Pixel(bx, by));
                if (Math.Abs(now.G - Backed(was.G)) > 10)
                {
                    Note($"{labelled.Name}: {name}'s backing at ({bx}, {by}) is {now} over {was}: expected green {Backed(was.G)} within 10");
                }
            }

            if (dot)
            {
                var (dx, dy) = ((int)MathF.Floor(dotted.X), (int)MathF.Floor(dotted.Y));
                if (!Covered(dx, dy, name) && !IsWhite(labelled.Pixel(dx, dy)))
                {
                    Note($"{labelled.Name}: {name}'s dot at ({dx}, {dy}) is {labelled.Pixel(dx, dy)}, expected white");
                }
            }
        }

        // The faces that do not face the eye, and the stray vertex, are unmarked.
        var unmarked = Enumerable.Range(0, shed.Faces.Length).Except(faces ? facing : []).Select(f => ($"face {f}", camera.Pixel(shed.Centre(f))));
        if (vertices)
        {
            unmarked = unmarked.Append(("the stray vertex 24, on no face", camera.Pixel(shed.Vertices[24])));
        }

        foreach (var (name, (x, y)) in unmarked)
        {
            if (x < 4 || y < 4 || x >= labelled.Width - 4 || y >= labelled.Height - 4 || Covered(x, y, ""))
            {
                if (name.StartsWith("the stray vertex", StringComparison.Ordinal))
                {
                    // No frame of this run holds (6, 0, 0): render frames the faces' bounds, 0 to 4, so the vertex is off
                    // every image, and so would its label be. Said in the run's log, not counted as a pass or a failure.
                    Console.Error.WriteLine($"shed: not covered: {labelled.Name}: {name} at ({x}, {y}) is off the image or under a label, so whether it is labelled is not checked");
                }

                continue;
            }

            if (labelled.Pixel(x, y) != plain.Pixel(x, y))
            {
                Note($"{labelled.Name}: {name} at ({x}, {y}) is {labelled.Pixel(x, y)}, without labels {plain.Pixel(x, y)}: there should be no label there");
            }
        }
    }

    /// <summary>The remaining -Z slope, face 11, faces away from the eye: where nothing else is in front of its samples, the background, clear of <paramref name="lines"/>.</summary>
    private static int BehindSlope(ScenarioFrame frame, Camera camera, Shape shed, (Vector2 From, Vector2 To)[] lines, string what)
    {
        var count = 0;
        foreach (var point in shed.Samples(11))
        {
            var (x, y) = camera.Pixel(point);
            if (camera.Seen(shed, x, y) == -1 && !lines.Any(s => DistanceToSegment(new Vector2(x + 0.5f, y + 0.5f), s.From, s.To) < 4))
            {
                count++;
                Background(frame, (x, y), what);
            }
        }

        return count;
    }

    private static async Task SheetAsync(ScenarioDriver driver, Shape shed)
    {
        var sheet = await driver.CaptureAsync("shed-axes-sheet", new { views = new[] { "+x", "-x", "+y", "-y", "+z", "-z" }, projection = "orthographic", size = 256, labels = "none" });
        Caption(sheet, $"3x2 tiles of 256x256, orthographic, views +x, -x, +y, -y, +z, -z, {Counts}");
        Size(sheet, 772, 514);
        foreach (var at in new[] { (256, 100), (257, 100), (514, 100), (515, 100), (100, 256), (100, 257), (600, 256) })
        {
            Background(sheet, at, "a gutter");
        }

        // +z is the fifth tile (column 1, row 1), -x the second (column 1, row 0).
        var plusZ = new Camera(Min, Max, Vector3.UnitZ, 256, orthographic: true);
        var minusX = new Camera(Min, Max, -Vector3.UnitX, 256, orthographic: true);
        var (zLeft, zTop) = (258, 258);
        var (xLeft, xTop) = (258, 0);

        foreach (var (camera, left, top, name) in new[] { (plusZ, zLeft, zTop, "+z"), (minusX, xLeft, xTop, "-x") })
        {
            var tested = Samples(sheet, camera, shed, (left, top), 256, _ => null, background: true, $"tile {name}");
            if (tested < 8)
            {
                Note($"{sheet.Name}: tile {name}: only {tested} samples were surely a face or the background");
            }
        }

        (int X, int Y) At(Camera c, int left, int top, float x, float y, float z) => (left + c.Pixel(new Vector3(x, y, z)).X, top + c.Pixel(new Vector3(x, y, z)).Y);

        // +z: the wall at the door's place has the wall's shade (the door is coplanar with it); the chimney stands above the
        // ridge on the left, not on the right; between the eave (2.5) and the ridge (3.5) the sky shows through the missing slope.
        var (wall, door) = (At(plusZ, zLeft, zTop, 1, 1, 3), At(plusZ, zLeft, zTop, 3, 1, 3));
        NotBackground(sheet, wall, "the +Z wall");
        NotBackground(sheet, door, "the door");
        Equal("the door's shade is the wall's", sheet.Pixel(wall.X, wall.Y).G, sheet.Pixel(door.X, door.Y).G);
        NotBackground(sheet, At(plusZ, zLeft, zTop, 1.2f, 3.75f, 1.5f), "the chimney, above the ridge on the left");
        Background(sheet, At(plusZ, zLeft, zTop, 2.8f, 3.75f, 1.5f), "above the ridge on the right, where a mirrored chimney would be");
        foreach (var x in new[] { 0.5f, 2.0f, 3.5f })
        {
            Background(sheet, At(plusZ, zLeft, zTop, x, 3.0f, 3), $"the gap between the eave and the ridge at x = {x}");
        }

        // -x: the flipped wall faces into the shed, away from this eye, so it is culled and its place shows the sky;
        // the -X gable above it faces out and is drawn.
        NotBackground(sheet, At(minusX, xLeft, xTop, 0, 3.0f, 1.5f), "the -X gable");
        Background(sheet, At(minusX, xLeft, xTop, 0, 1.2f, 1.5f), "the flipped -X wall's centre, culled");
        Background(sheet, At(minusX, xLeft, xTop, 0, 1.2f, 0.5f), "the flipped -X wall near its -Z end, culled");
        Background(sheet, At(minusX, xLeft, xTop, 0, 1.2f, 2.5f), "the flipped -X wall near its +Z end, culled");
    }
}
