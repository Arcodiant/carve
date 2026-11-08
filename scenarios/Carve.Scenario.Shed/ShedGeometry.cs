using System.Numerics;
using System.Text.Json;
using Carve.Debug;

namespace Carve.Scenario.Shed;

/// <summary>The mesh as get_mesh gives it, with the face rules the renderer follows: Newell normals, centre as the mean of the corners.</summary>
internal sealed record Shape(Vector3[] Vertices, int[][] Faces)
{
    public Vector3 Centre(int face) => Faces[face].Aggregate(Vector3.Zero, (sum, v) => sum + Vertices[v]) / Faces[face].Length;

    public Vector3 Normal(int face)
    {
        var loop = Faces[face];
        var n = Vector3.Zero;
        for (var k = 0; k < loop.Length; k++)
        {
            var (a, b) = (Vertices[loop[k]], Vertices[loop[(k + 1) % loop.Length]]);
            n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
        }

        return Vector3.Normalize(n);
    }

    /// <summary>Near each corner of a face and part way to its centre, and the middle: clear of the edges.</summary>
    public IEnumerable<Vector3> Samples(int face) =>
        Faces[face].SelectMany(v => new[] { Vector3.Lerp(Vertices[v], Centre(face), 0.3f), Vector3.Lerp(Vertices[v], Centre(face), 0.6f) })
            .Append(Centre(face));

    /// <summary>The face a ray meets first among those whose front is toward the ray, or -1.</summary>
    public int FirstHit(Vector3 origin, Vector3 direction)
    {
        var (best, nearest) = (-1, float.MaxValue);
        for (var f = 0; f < Faces.Length; f++)
        {
            if (Vector3.Dot(Normal(f), direction) >= 0)
            {
                continue;
            }

            var loop = Faces[f];
            for (var k = 1; k + 1 < loop.Length; k++)
            {
                if (Triangle(origin, direction, Vertices[loop[0]], Vertices[loop[k]], Vertices[loop[k + 1]]) is { } t && t < nearest - 1e-4f)
                {
                    (best, nearest) = (f, t);
                }
            }
        }

        return best;
    }

    private static float? Triangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c)
    {
        var (e1, e2) = (b - a, c - a);
        var p = Vector3.Cross(d, e2);
        var det = Vector3.Dot(e1, p);
        if (Math.Abs(det) < 1e-9f)
        {
            return null;
        }

        var s = o - a;
        var u = Vector3.Dot(s, p) / det;
        var q = Vector3.Cross(s, e1);
        var v = Vector3.Dot(d, q) / det;
        var t = Vector3.Dot(e2, q) / det;
        return u >= 0 && v >= 0 && u + v <= 1 && t > 0 ? t : null;
    }

    /// <summary>A <c>.carve</c> file as the scenario reads it, apart from the product: <c>vertices</c> as <c>[x, y, z]</c>, <c>faces</c> as id loops.</summary>
    public static Shape Parse(string text)
    {
        using var file = JsonDocument.Parse(text);
        var vertices = file.RootElement.GetProperty("vertices").EnumerateArray()
            .Select(v => new Vector3(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle())).ToArray();
        var faces = file.RootElement.GetProperty("faces").EnumerateArray()
            .Select(f => f.EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray();
        return new Shape(vertices, faces);
    }

    public static async Task<Shape> ReadAsync(ScenarioDriver driver)
    {
        var mesh = JsonDocument.Parse(await Checks.CallTextAsync(driver, "get_mesh", new { })).RootElement;
        var vertices = mesh.GetProperty("vertices").EnumerateArray().Select(v =>
        {
            var at = v.GetProperty("at");
            return new Vector3(at[0].GetSingle(), at[1].GetSingle(), at[2].GetSingle());
        }).ToArray();
        var faces = mesh.GetProperty("faces").EnumerateArray()
            .Select(f => f.GetProperty("vertices").EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray();
        return new Shape(vertices, faces);
    }
}

/// <summary>
/// Worked out here, not asked of the product: where a point lands in a frame, and which way the ray through a pixel goes,
/// under <c>render</c>'s framing rule for the box <c>min</c> to <c>max</c>. A pixel covers x to x + 1.
/// </summary>
internal sealed class Camera
{
    private const float FieldOfViewY = 0.6f;
    private const float Margin = 0.08f;

    private readonly Vector3 _centre, _unit;
    private readonly float _radius, _distance, _height;
    private readonly bool _orthographic;
    private readonly int _size;
    private readonly Matrix4x4 _rotation, _camera;

    public Camera(Vector3 min, Vector3 max, Vector3 direction, int size, bool orthographic = false)
    {
        (_size, _orthographic) = (size, orthographic);
        _centre = (min + max) / 2;
        _radius = (max - min).Length() / 2;
        _unit = Vector3.Normalize(direction);
        _rotation = Matrix4x4.CreateLookAt(_unit, Vector3.Zero, Vector3.UnitY) with { M41 = 0, M42 = 0, M43 = 0 };
        var tangent = MathF.Tan(FieldOfViewY / 2) * (1 - 2 * Margin);
        var (distance, reach) = (1.2f, 0f);
        foreach (var x in new[] { min.X, max.X })
        foreach (var y in new[] { min.Y, max.Y })
        foreach (var z in new[] { min.Z, max.Z })
        {
            var seen = Vector3.Transform(Drawn(new Vector3(x, y, z)), _rotation);
            distance = Math.Max(distance, Math.Max(Math.Abs(seen.X), Math.Abs(seen.Y)) / tangent + seen.Z);
            reach = Math.Max(reach, Math.Max(Math.Abs(seen.X), Math.Abs(seen.Y)));
        }

        _distance = distance;
        _height = reach / (1 - 2 * Margin);
        var view = Matrix4x4.CreateLookAt(_unit * distance, Vector3.Zero, Vector3.UnitY);
        _camera = view * Matrix4x4.CreatePerspectiveFieldOfView(FieldOfViewY, 1, 0.01f, 100f);
    }

    private Vector3 Drawn(Vector3 p) => (p - _centre) / _radius;

    public Vector3 Eye => _centre + _unit * _distance * _radius;

    public Vector2 Project(Vector3 p)
    {
        if (_orthographic)
        {
            var seen = Vector3.Transform(Drawn(p), _rotation);
            return new Vector2((seen.X / _height + 1) / 2 * _size, (1 - seen.Y / _height) / 2 * _size);
        }

        var device = Vector4.Transform(new Vector4(Drawn(p), 1), _camera);
        return new Vector2((device.X / device.W + 1) / 2 * _size, (1 - device.Y / device.W) / 2 * _size);
    }

    public (int X, int Y) Pixel(Vector3 p)
    {
        var v = Project(p);
        return ((int)MathF.Floor(v.X), (int)MathF.Floor(v.Y));
    }

    /// <summary>The ray through the middle of a pixel, in world space.</summary>
    public (Vector3 Origin, Vector3 Direction) Ray(int x, int y)
    {
        var (nx, ny) = ((x + 0.5f) / _size * 2 - 1, 1 - (y + 0.5f) / _size * 2);
        Matrix4x4.Invert(_rotation, out var back);
        if (_orthographic)
        {
            var onPlane = Vector3.Transform(new Vector3(nx * _height, ny * _height, 0), back) * _radius + _centre;
            return (onPlane + _unit * 100, -_unit);
        }

        var local = new Vector3(nx * MathF.Tan(FieldOfViewY / 2), ny * MathF.Tan(FieldOfViewY / 2), -1);
        return (Eye, Vector3.Normalize(Vector3.Transform(local, back)));
    }

    /// <summary>
    /// What a pixel shows: the face it holds, or -1 for background, and -2 when it is not certain: the pixels two to
    /// three across do not agree, so it sits on an edge or a silhouette.
    /// </summary>
    public int Seen(Shape shape, int x, int y)
    {
        if (x < 4 || y < 4 || x >= _size - 4 || y >= _size - 4)
        {
            return -2;
        }

        var first = Cast(shape, x, y);
        foreach (var (dx, dy) in new[] { (3, 0), (-3, 0), (0, 3), (0, -3), (2, 2), (-2, -2), (2, -2), (-2, 2) })
        {
            if (Cast(shape, x + dx, y + dy) != first)
            {
                return -2;
            }
        }

        return first;
    }

    private int Cast(Shape shape, int x, int y)
    {
        var (o, d) = Ray(x, y);
        return shape.FirstHit(o, d);
    }
}
