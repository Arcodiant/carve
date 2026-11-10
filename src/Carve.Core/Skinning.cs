using System.Numerics;

namespace Carve.Core;

/// <summary>
/// Linear blend skinning on the CPU (D-6, Two samplers): a vertex goes to the weighted sum of what each influencing bone
/// does to it, where a bone does (pose world transform) x (inverse rest world transform). The rest pose is the bind pose,
/// so the rest pose gives rest positions exactly. All arithmetic is in double and narrowed once. No scale.
/// </summary>
public static class Skinning
{
    private readonly record struct Rigid(double Qx, double Qy, double Qz, double Qw, double Tx, double Ty, double Tz)
    {
        public (double X, double Y, double Z) Rotate(double x, double y, double z)
        {
            // v' = v + 2w(q x v) + 2 q x (q x v)
            double cx = Qy * z - Qz * y, cy = Qz * x - Qx * z, cz = Qx * y - Qy * x;
            double dx = Qy * cz - Qz * cy, dy = Qz * cx - Qx * cz, dz = Qx * cy - Qy * cx;
            return (x + 2 * (Qw * cx + dx), y + 2 * (Qw * cy + dy), z + 2 * (Qw * cz + dz));
        }

        public (double X, double Y, double Z) Apply(double x, double y, double z)
        {
            var (rx, ry, rz) = Rotate(x, y, z);
            return (rx + Tx, ry + Ty, rz + Tz);
        }

        /// <summary>This applied after <paramref name="child"/>: this * child.</summary>
        public Rigid Then(Rigid child)
        {
            var (tx, ty, tz) = Apply(child.Tx, child.Ty, child.Tz);
            var (aw, ax, ay, az) = (Qw, Qx, Qy, Qz);
            var (bw, bx, by, bz) = (child.Qw, child.Qx, child.Qy, child.Qz);
            var w = aw * bw - ax * bx - ay * by - az * bz;
            var x = aw * bx + ax * bw + ay * bz - az * by;
            var y = aw * by - ax * bz + ay * bw + az * bx;
            var z = aw * bz + ax * by - ay * bx + az * bw;
            var n = Math.Sqrt(w * w + x * x + y * y + z * z);
            return new Rigid(x / n, y / n, z / n, w / n, tx, ty, tz);
        }

        public Rigid Inverse()
        {
            var inv = new Rigid(-Qx, -Qy, -Qz, Qw, 0, 0, 0);
            var (tx, ty, tz) = inv.Rotate(Tx, Ty, Tz);
            return inv with { Tx = -tx, Ty = -ty, Tz = -tz };
        }
    }

    private static Rigid Local(Quaternion q, Vector3 t)
    {
        double x = q.X, y = q.Y, z = q.Z, w = q.W;
        var n = Math.Sqrt(x * x + y * y + z * z + w * w);
        return new Rigid(x / n, y / n, z / n, w / n, t.X, t.Y, t.Z);
    }

    /// <summary>What each bone does to a point in model space: pose world x inverse rest world.</summary>
    private static Rigid[] SkinTransforms(Skeleton skeleton, Pose pose)
    {
        var count = skeleton.Bones.Count;
        var rest = new Rigid[count];
        var posed = new Rigid[count];
        var skin = new Rigid[count];
        for (var i = 0; i < count; i++)
        {
            var b = skeleton.Bones[i];
            var restLocal = Local(b.Rotation, b.Position);
            var poseLocal = Local(pose.RotationOf(b.Name) ?? b.Rotation, pose.TranslationOf(b.Name) ?? b.Position);
            rest[i] = b.Parent < 0 ? restLocal : rest[b.Parent].Then(restLocal);
            posed[i] = b.Parent < 0 ? poseLocal : posed[b.Parent].Then(poseLocal);
            skin[i] = posed[i].Then(rest[i].Inverse());
        }
        return skin;
    }

    /// <summary>The posed positions of <paramref name="mesh"/>'s vertices; an unweighted vertex stays where it is.</summary>
    public static Vector3[] Positions(PolygonMesh mesh, Skeleton skeleton, Pose pose)
    {
        var skin = SkinTransforms(skeleton, pose);
        var result = new Vector3[mesh.Vertices.Count];
        for (var v = 0; v < result.Length; v++)
        {
            var p = mesh.Vertices[v];
            if (mesh.Weights is not { } weights || !weights.Has(v)) { result[v] = p; continue; }
            double x = 0, y = 0, z = 0;
            foreach (var (bone, w) in weights.Of(v))
            {
                var (px, py, pz) = skin[bone].Apply(p.X, p.Y, p.Z);
                x += w * px; y += w * py; z += w * pz;
            }
            result[v] = new Vector3((float)x, (float)y, (float)z);
        }
        return result;
    }

    /// <summary>
    /// The posed normals for per-vertex <paramref name="normals"/> (one per vertex of <paramref name="mesh"/>): the blend of
    /// each bone's rotation of the normal, renormalised. An unweighted vertex keeps its normal.
    /// </summary>
    public static Vector3[] Normals(PolygonMesh mesh, IReadOnlyList<Vector3> normals, Skeleton skeleton, Pose pose)
    {
        var skin = SkinTransforms(skeleton, pose);
        var result = new Vector3[normals.Count];
        for (var v = 0; v < result.Length; v++)
        {
            var n = normals[v];
            if (mesh.Weights is not { } weights || !weights.Has(v)) { result[v] = n; continue; }
            double x = 0, y = 0, z = 0;
            foreach (var (bone, w) in weights.Of(v))
            {
                var (px, py, pz) = skin[bone].Rotate(n.X, n.Y, n.Z);
                x += w * px; y += w * py; z += w * pz;
            }
            var len = Math.Sqrt(x * x + y * y + z * z);
            result[v] = len > 0 ? new Vector3((float)(x / len), (float)(y / len), (float)(z / len)) : n;
        }
        return result;
    }

    /// <summary>
    /// The posed corner normals of <paramref name="mesh"/> (face, then position in its loop): each rest corner normal
    /// (<see cref="Shading.Corners"/>) rotated and blended by its vertex's weights, renormalised; an unweighted vertex keeps its own.
    /// </summary>
    public static Vector3[][] CornerNormals(PolygonMesh mesh, Skeleton skeleton, Pose pose)
    {
        var rest = Shading.Corners(mesh);
        var skin = SkinTransforms(skeleton, pose);
        var result = new Vector3[rest.Length][];
        for (var f = 0; f < rest.Length; f++)
        {
            var loop = mesh.Faces[f];
            result[f] = new Vector3[loop.Count];
            for (var k = 0; k < loop.Count; k++)
            {
                var n = rest[f][k];
                if (mesh.Weights is not { } weights || !weights.Has(loop[k])) { result[f][k] = n; continue; }
                double x = 0, y = 0, z = 0;
                foreach (var (bone, w) in weights.Of(loop[k]))
                {
                    var (px, py, pz) = skin[bone].Rotate(n.X, n.Y, n.Z);
                    x += w * px; y += w * py; z += w * pz;
                }
                var len = Math.Sqrt(x * x + y * y + z * z);
                result[f][k] = len > 0 ? new Vector3((float)(x / len), (float)(y / len), (float)(z / len)) : n;
            }
        }
        return result;
    }

    /// <summary>
    /// The model with every weighted mesh at its posed vertex positions, colours and groups kept, weights dropped (the
    /// result is a plain mesh to draw). A model with no skeleton comes back unchanged.
    /// </summary>
    public static Model Posed(Model model, Pose pose)
    {
        if (model.Skeleton is not { } skeleton) return model;
        var result = Model.Empty;
        foreach (var (name, mesh) in model.Meshes)
        {
            result = result.With(name, mesh.Weights is null ? mesh
                : mesh.WithVertices(Positions(mesh, skeleton, pose), mesh.ShadingMode == ShadingMode.Smooth ? CornerNormals(mesh, skeleton, pose) : null));
        }
        return result;
    }

    /// <summary>The bone names of <paramref name="pose"/> that <paramref name="skeleton"/> does not have.</summary>
    public static IEnumerable<string> Unknown(Skeleton skeleton, Pose pose) =>
        pose.Names.Where(n => !skeleton.Names.Contains(n));
}
