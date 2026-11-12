using System.Numerics;
using Carve.Scenario.Cuts;
using Carve.Scenario.Shed;

namespace Carve.Scenario.Gatehouse;

/// <summary>One pixel the scenario asks about: a point on a face, and whether that face is narrow on screen.</summary>
internal readonly record struct Probe(int Face, string Name, Vector3 At, bool Narrow);

/// <summary>
/// What the gatehouse scenario looks at, worked apart from the product so that the Core test (GatehouseGeometry) and the
/// scenario share it: the three directions, the box a stage is framed on, and the probes of a stage's mesh. A probe is the
/// middle of a face and a point near each of its corners, so the corners are asked about as well as the middles; whether
/// the ray test is sure of its pixel, and which face it shows, is <see cref="Solid.Seen"/> or, for a face only a few pixels
/// wide, <see cref="Solid.SeenNarrow"/> (T-67's rule).
/// </summary>
internal static class GatehousePlan
{
    public const int Size = 512;

    /// <summary>The directions (from the model toward the eye) of the planned frames.</summary>
    public static readonly Vector3 FromFront = new(0.6f, 0.35f, 1);
    public static readonly Vector3 FromBack = new(-0.6f, 0.35f, -1);
    public static readonly Vector3 Straight = new(0, 0, 1);

    /// <summary>A face whose shortest edge is under this is narrow on screen (the cornice's ends, the window's reveal).</summary>
    private const float NarrowEdge = 0.35f;

    /// <summary>How far a corner probe is moved toward the face's middle.</summary>
    private const float Inset = 0.3f;

    /// <summary>The box <c>render</c> frames: the vertices the live faces use.</summary>
    public static (Vector3 Min, Vector3 Max) Bounds(Solid solid)
    {
        var used = Enumerable.Range(0, solid.Faces.Length).Where(f => !solid.IsDeleted(f))
            .SelectMany(f => solid.Faces[f].Loop).Distinct().Select(v => solid.Vertices[v]).ToArray();
        return (used.Aggregate(Vector3.Min), used.Aggregate(Vector3.Max));
    }

    /// <summary>Points in the window's hole, in the front's plane (z = 0.15): the six places a straight look through it shows the background.</summary>
    public static IEnumerable<Probe> WindowBackground() =>
        new[] { (0.7f, 1.15f), (0.45f, 0.95f), (0.95f, 0.95f), (0.95f, 1.35f), (0.45f, 1.35f), (0.7f, 1.55f) }
            .Select(p => new Probe(-1, $"window ({p.Item1}, {p.Item2})", new Vector3(p.Item1, p.Item2, 0.15f), false));

    /// <summary>Every edge of the live faces, holes included, as a sorted pair of vertex ids; of <paramref name="faces"/> only if given.</summary>
    public static (int Lo, int Hi)[] Edges(Solid solid, IEnumerable<int>? faces = null) =>
        (faces ?? Enumerable.Range(0, solid.Faces.Length)).Where(f => !solid.IsDeleted(f))
            .SelectMany(f => solid.Faces[f].Holes.Prepend(solid.Faces[f].Loop))
            .SelectMany(l => l.Select((v, k) => (A: v, B: l[(k + 1) % l.Length])))
            .Select(e => (Lo: Math.Min(e.A, e.B), Hi: Math.Max(e.A, e.B))).Distinct().OrderBy(e => e).ToArray();

    /// <summary>
    /// The edges a stage added: those of its mesh whose middle is on no edge of the stage before. An edge cut short or in two
    /// (a ring cut through a vertical edge, a plane cut across a face) lies along an old edge and is not new.
    /// </summary>
    public static (int Lo, int Hi)[] NewEdges(Solid before, Solid after)
    {
        var old = Edges(before);
        return Edges(after).Where(e =>
        {
            var middle = (after.Vertices[e.Lo] + after.Vertices[e.Hi]) / 2;
            return !old.Any(o => Distance(middle, before.Vertices[o.Lo], before.Vertices[o.Hi]) < 1e-4f);
        }).ToArray();
    }

    private static float Distance(Vector3 p, Vector3 a, Vector3 b)
    {
        var t = Math.Clamp(Vector3.Dot(p - a, b - a) / Vector3.Dot(b - a, b - a), 0, 1);
        return Vector3.Distance(p, a + (t * (b - a)));
    }

    /// <summary>The edges of the faces turned toward the camera (as T-67's wireframe check takes them).</summary>
    public static (int Lo, int Hi)[] InSight(Solid solid, Vector3 direction)
    {
        var unit = Vector3.Normalize(direction);
        return Edges(solid, Enumerable.Range(0, solid.Faces.Length).Where(f => !solid.IsDeleted(f) && Vector3.Dot(solid.Normal(f), unit) > 0.2f));
    }

    public static IEnumerable<Probe> Probes(Solid solid)
    {
        for (var f = 0; f < solid.Faces.Length; f++)
        {
            if (solid.IsDeleted(f))
            {
                continue;
            }

            var loop = solid.Faces[f].Loop;
            var corners = loop.Select(v => solid.Vertices[v]).ToArray();
            var middle = corners.Aggregate(Vector3.Zero, (a, b) => a + b) / corners.Length;
            var shortest = corners.Select((c, k) => Vector3.Distance(c, corners[(k + 1) % corners.Length])).Where(d => d > 1e-4f).Min();
            var narrow = shortest < NarrowEdge;
            yield return new Probe(f, "middle", middle, narrow);
            for (var k = 0; k < corners.Length; k++)
            {
                // A corner between two collinear neighbours is not a corner of the shape.
                var (a, b) = (corners[(k + corners.Length - 1) % corners.Length], corners[(k + 1) % corners.Length]);
                if (Vector3.Cross(corners[k] - a, b - corners[k]).Length() < 1e-5f)
                {
                    continue;
                }

                yield return new Probe(f, $"corner {loop[k]}", Vector3.Lerp(corners[k], middle, Inset), narrow);
            }
        }
    }

    /// <summary>What the ray test is sure of at a probe's pixel: the face, -1 for the background, -2 when not sure.</summary>
    public static int Seen(Solid solid, Camera camera, Probe probe)
    {
        var (x, y) = camera.Pixel(probe.At);
        return probe.Narrow ? solid.SeenNarrow(camera, Size, x, y) : solid.Seen(camera, Size, x, y);
    }
}
