using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Shed;

namespace Carve.Scenario.Cuts;

/// <summary>
/// The mesh as get_mesh gives it, holes and all, with the rules the renderer follows, worked here apart from the
/// product: a face's normal is its outer loop's Newell normal, a face is culled from behind, and a ray meets a face where
/// it crosses the face's plane inside the outer loop and inside none of its holes. A deleted face (null in get_mesh) has
/// an empty loop and is met by no ray; a deleted vertex (null) sits at the origin, used by no live face.
/// </summary>
internal sealed record Solid(Vector3[] Vertices, (int[] Loop, int[][] Holes)[] Faces)
{
    public bool IsDeleted(int face) => Faces[face].Loop.Length == 0;

    public Vector3 Normal(int face)
    {
        var loop = Faces[face].Loop;
        var n = Vector3.Zero;
        for (var k = 0; k < loop.Length; k++)
        {
            var (a, b) = (Vertices[loop[k]], Vertices[loop[(k + 1) % loop.Length]]);
            n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
        }

        return Vector3.Normalize(n);
    }

    /// <summary>The face a ray meets first among those whose front is toward the ray, or -1.</summary>
    public int FirstHit(Vector3 origin, Vector3 direction)
    {
        var (best, nearest) = (-1, float.MaxValue);
        for (var f = 0; f < Faces.Length; f++)
        {
            if (IsDeleted(f))
            {
                continue;
            }

            var n = Normal(f);
            var facing = Vector3.Dot(n, direction);
            if (facing >= 0)
            {
                continue;
            }

            var t = Vector3.Dot(Vertices[Faces[f].Loop[0]] - origin, n) / facing;
            if (t <= 0 || t >= nearest - 1e-4f)
            {
                continue;
            }

            var p = origin + t * direction;
            if (Inside(Faces[f].Loop, p, n) && !Faces[f].Holes.Any(h => Inside(h, p, n)))
            {
                (best, nearest) = (f, t);
            }
        }

        return best;
    }

    /// <summary>Even-odd: is <paramref name="p"/>, on the loop's plane, inside the loop, seen along the normal's largest axis.</summary>
    private bool Inside(int[] loop, Vector3 p, Vector3 n)
    {
        var (ax, ay, az) = (Math.Abs(n.X), Math.Abs(n.Y), Math.Abs(n.Z));
        Vector2 Flat(Vector3 v) => az >= ax && az >= ay ? new(v.X, v.Y) : ay >= ax ? new(v.Z, v.X) : new(v.Y, v.Z);
        var q = Flat(p);
        var inside = false;
        for (var k = 0; k < loop.Length; k++)
        {
            var (a, b) = (Flat(Vertices[loop[k]]), Flat(Vertices[loop[(k + 1) % loop.Length]]));
            if ((a.Y > q.Y) != (b.Y > q.Y) && q.X < a.X + (q.Y - a.Y) / (b.Y - a.Y) * (b.X - a.X))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// What a pixel shows: the face it holds, or -1 for background, and -2 when it is not certain: the pixels two to
    /// three across do not agree, so it sits on an edge or a silhouette (as the shed's camera does it).
    /// </summary>
    public int Seen(Camera camera, int size, int x, int y)
    {
        if (x < 4 || y < 4 || x >= size - 4 || y >= size - 4)
        {
            return -2;
        }

        var first = Cast(camera, x, y);
        foreach (var (dx, dy) in new[] { (3, 0), (-3, 0), (0, 3), (0, -3), (2, 2), (-2, -2), (2, -2), (-2, 2) })
        {
            if (Cast(camera, x + dx, y + dy) != first)
            {
                return -2;
            }
        }

        return first;
    }

    private int Cast(Camera camera, int x, int y)
    {
        var (o, d) = camera.Ray(x, y);
        return FirstHit(o, d);
    }

    /// <summary>
    /// The current mesh from get_mesh: a face's <c>vertices</c> is its loop, or <c>{loop, holes}</c> for a holed face; a
    /// deleted slot is null.
    /// </summary>
    public static async Task<Solid> ReadAsync(ScenarioDriver driver)
    {
        var mesh = JsonDocument.Parse(await Checks.CallTextAsync(driver, "get_mesh", new { })).RootElement;
        var vertices = mesh.GetProperty("vertices").EnumerateArray().Select(v =>
        {
            if (v.ValueKind == JsonValueKind.Null)
            {
                return Vector3.Zero;
            }

            var at = v.GetProperty("at");
            return new Vector3(at[0].GetSingle(), at[1].GetSingle(), at[2].GetSingle());
        }).ToArray();
        static int[] Ids(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var faces = mesh.GetProperty("faces").EnumerateArray().Select(f =>
        {
            if (f.ValueKind == JsonValueKind.Null)
            {
                return (Array.Empty<int>(), Array.Empty<int[]>());
            }

            var v = f.GetProperty("vertices");
            return v.ValueKind == JsonValueKind.Object
                ? (Ids(v.GetProperty("loop")), v.GetProperty("holes").EnumerateArray().Select(Ids).ToArray())
                : (Ids(v), Array.Empty<int[]>());
        }).ToArray();
        return new Solid(vertices, faces);
    }
}
