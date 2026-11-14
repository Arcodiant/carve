using System.Numerics;

namespace Carve.Core;

/// <summary>
/// One island of an unwrap: its faces (ascending; the first is the lowest), its UV rectangle
/// <c>(u0, v0, u1, v1)</c>, its stretch (the largest ratio of a triangle's two singular values; 1 is none), how many of
/// its triangles came out folded (a positive shoelace sum; normally 0), and the conjugate-gradient iterations it took.
/// </summary>
public sealed record UnwrapIsland(IReadOnlyList<int> Faces, (float U0, float V0, float U1, float V1) Rect, double Stretch, int Flipped, int Iterations)
{
    public int LowestFace => Faces[0];
}

/// <summary>
/// What <see cref="Unwrap.Run"/> made: the mesh with its new UVs, the islands by lowest face, the fill (the islands'
/// UV triangle area over <c>into</c>'s area), the common scale in UV units per metre, and the margin used.
/// </summary>
public sealed record UnwrapResult(PolygonMesh Mesh, IReadOnlyList<UnwrapIsland> Islands, double Fill, double Scale, double Margin);

/// <summary>
/// Unwraps faces into UV space (T-56): cuts them into islands at seams, open edges and the border of the chosen set
/// (<see cref="UnwrapIslands"/>), flattens each by LSCM (<see cref="UnwrapLscm"/>), rescales it to its real 3D area,
/// turns it and shelf-packs all of them at one scale into <c>into</c> (<see cref="UnwrapPack"/>). UVs are in glTF
/// axes, (0, 0) top-left and v down; every face comes out with a negative shoelace sum (unflipped). Faces outside the
/// set keep their UVs, and seams are untouched. Deterministic: two runs are bit-identical.
/// </summary>
public static class Unwrap
{
    /// <summary>CG stops when the residual is at most this times the right-hand side's norm.</summary>
    public const double Tolerance = 1e-10;

    /// <summary>CG's iteration cap; not converging within it refuses the call, naming the island.</summary>
    public const int MaxIterations = 10000;

    /// <summary>The default margin, as a fraction of the shorter side of <c>into</c>: four texels of a 256 checker.</summary>
    public const double MarginFraction = 1.0 / 64;

    public static (double U0, double V0, double U1, double V1) UnitSquare => (0, 0, 1, 1);

    public static (UnwrapResult? Result, IReadOnlyList<MeshError> Errors) Run(PolygonMesh mesh, IReadOnlyList<int>? faces = null,
        (double U0, double V0, double U1, double V1)? into = null, double? margin = null)
    {
        var errors = new List<MeshError>();
        var box = into ?? UnitSquare;
        if (!double.IsFinite(box.U0) || !double.IsFinite(box.V0) || !double.IsFinite(box.U1) || !double.IsFinite(box.V1))
            errors.Add(new MeshError("into", "every value must be a finite number"));
        else if (UvArguments.IntoArea(box.U0, box.V0, box.U1, box.V1) is not null)
            errors.Add(new MeshError("into", UvArguments.IntoArea(box.U0, box.V0, box.U1, box.V1)!));
        var shorter = Math.Min(box.U1 - box.U0, box.V1 - box.V0);
        var m = margin ?? shorter * MarginFraction;
        if (margin is { } given && errors.Count == 0 && (!double.IsFinite(given) || given < 0 || !(given < shorter / 2)))
            errors.Add(new MeshError("margin", $"{UvArguments.Number(given)} is not a margin into can take; it must be at least 0 and less than half of into's shorter side ({UvArguments.Number(shorter / 2)})"));

        var set = Faces(mesh, faces, errors);
        if (errors.Count > 0) return (null, errors);
        var flat = Prepare(mesh, set, errors);
        if (flat is null) return (null, errors);
        var (patches, frames, us, vs, sizes, stretch, iterations) = flat;
        var count = patches.Count;

        // Pack: height descending, then lowest face.
        var order = new int[count];
        for (var i = 0; i < count; i++) order[i] = i;
        Array.Sort(order, (a, b) => sizes[a].H != sizes[b].H ? sizes[b].H.CompareTo(sizes[a].H) : patches[a].LowestFace.CompareTo(patches[b].LowestFace));
        var fit = UnwrapPack.Fit(sizes, order, box, m);
        if (fit is null)
            return (null, [new MeshError("margin", $"{UnwrapIslands.Plural(count, "island")} do not fit into into with a margin of {UvArguments.Number(m)}; give a smaller margin")]);
        var (s, offU, offV) = fit.Value;

        // Place, round once to float, and measure.
        var newUvs = new List<KeyValuePair<int, IReadOnlyList<Vector2>?>>();
        var islands = new List<UnwrapIsland>();
        double filled = 0;
        for (var i = 0; i < count; i++)
        {
            var patch = patches[i];
            var fu = new double[us[i].Length];
            var fv = new double[us[i].Length];
            float minU = float.MaxValue, minV = float.MaxValue, maxU = float.MinValue, maxV = float.MinValue;
            for (var k = 0; k < fu.Length; k++)
            {
                var pu = (float)(offU[i] + s * us[i][k]);
                var pv = (float)(offV[i] + s * vs[i][k]);
                fu[k] = pu;
                fv[k] = pv;
                minU = Math.Min(minU, pu); maxU = Math.Max(maxU, pu);
                minV = Math.Min(minV, pv); maxV = Math.Max(maxV, pv);
            }
            var flipped = 0;
            for (var t = 0; t < patch.Triangles.Count; t++)
            {
                var twice = TwiceArea(patch.Triangles[t], fu, fv);
                filled += Math.Abs(twice) / 2;
                if (!frames[i][t].Degenerate && twice > 0) flipped++;
            }
            for (var f = 0; f < patch.Faces.Length; f++)
            {
                var corners = patch.Corners[f];
                var uv = new Vector2[corners.Length];
                for (var c = 0; c < corners.Length; c++) uv[c] = new Vector2((float)fu[corners[c]], (float)fv[corners[c]]);
                newUvs.Add(KeyValuePair.Create(patch.Faces[f], (IReadOnlyList<Vector2>?)uv));
            }
            islands.Add(new UnwrapIsland(patch.Faces, (minU, minV, maxU, maxV), stretch[i], flipped, iterations[i]));
        }

        var uvs = (mesh.Uvs ?? MeshUvs.None).WithFaces(newUvs);
        var fill = filled / ((box.U1 - box.U0) * (box.V1 - box.V0));
        return (new UnwrapResult(mesh.WithUvs(uvs), islands, fill, s, m), []);
    }

    private sealed record Prepared(List<UnwrapPatch> Patches, UnwrapLscm.Frame[][] Frames, double[][] U, double[][] V,
        (double W, double H)[] Sizes, double[] Stretch, int[] Iterations);

    /// <summary>Cuts, flattens, rescales to real area and orients each island: null with errors when any is refused.</summary>
    private static Prepared? Prepare(PolygonMesh mesh, List<int> set, List<MeshError> errors)
    {
        var patches = UnwrapIslands.Find(mesh, set, errors);
        if (errors.Count > 0) return null;

        // Flatten, rescale to real area, measure, and orient each island.
        var count = patches.Count;
        var us = new double[count][];
        var vs = new double[count][];
        var frames = new UnwrapLscm.Frame[count][];
        var sizes = new (double W, double H)[count];
        var stretch = new double[count];
        var iterations = new int[count];
        for (var i = 0; i < count; i++)
        {
            var patch = patches[i];
            frames[i] = UnwrapLscm.Frames(mesh, patch);
            var (p0, p1, d) = UnwrapLscm.Pins(mesh, patch);
            if (d == 0)
            {
                errors.Add(NoExtent(patch));
                continue;
            }
            var flat = UnwrapLscm.Solve(patch, frames[i], p0, p1, d, errors);
            if (flat is null) continue;
            iterations[i] = flat.Iterations;
            var u = flat.X;
            var v = new double[flat.Y.Length];
            for (var k = 0; k < v.Length; k++) v[k] = -flat.Y[k]; // math axes to screen: CCW stays a negative shoelace

            double area3 = 0, areaUv = 0;
            for (var t = 0; t < frames[i].Length; t++)
            {
                area3 += frames[i][t].TwoA / 2;
                areaUv += Math.Abs(TwiceArea(patch.Triangles[t], u, v)) / 2;
            }
            if (!(areaUv > 0) || !double.IsFinite(areaUv))
            {
                errors.Add(NoExtent(patch));
                continue;
            }
            var scale = Math.Sqrt(area3 / areaUv);
            for (var k = 0; k < u.Length; k++) { u[k] *= scale; v[k] *= scale; }

            stretch[i] = Stretch(patch, frames[i], u, v);
            sizes[i] = UnwrapPack.Orient(u, v);
            us[i] = u;
            vs[i] = v;
        }
        if (errors.Count > 0) return null;
        return new Prepared(patches, frames, us, vs, sizes, stretch, iterations);
    }

    /// <summary>
    /// The islands flattened and turned but not packed: per island its faces and, per face, one (u, v) per corner in
    /// metres, the island's rectangle's min corner at the origin. What <see cref="Run"/> packs; for checking the
    /// flattening on its own.
    /// </summary>
    public static (IReadOnlyList<(IReadOnlyList<int> Faces, IReadOnlyList<(double U, double V)[]> Corners)>? Islands, IReadOnlyList<MeshError> Errors) Flatten(
        PolygonMesh mesh, IReadOnlyList<int>? faces = null)
    {
        var errors = new List<MeshError>();
        var set = Faces(mesh, faces, errors);
        if (errors.Count > 0) return (null, errors);
        var flat = Prepare(mesh, set, errors);
        if (flat is null) return (null, errors);
        var islands = new List<(IReadOnlyList<int>, IReadOnlyList<(double, double)[]>)>();
        for (var i = 0; i < flat.Patches.Count; i++)
        {
            var corners = new List<(double, double)[]>();
            foreach (var face in flat.Patches[i].Corners)
            {
                var uv = new (double, double)[face.Length];
                for (var c = 0; c < face.Length; c++) uv[c] = (flat.U[i][face[c]], flat.V[i][face[c]]);
                corners.Add(uv);
            }
            islands.Add((flat.Patches[i].Faces, corners));
        }
        return (islands, []);
    }

    private static List<int> Faces(PolygonMesh mesh, IReadOnlyList<int>? faces, List<MeshError> errors)
    {
        if (faces is null) return [.. mesh.LiveFaces];
        if (faces.Count == 0)
        {
            errors.Add(new MeshError("faces", "the list is empty; leave faces out to unwrap every face, or list the faces to unwrap"));
            return [];
        }
        var seen = new HashSet<int>();
        foreach (var f in faces)
        {
            if (UvArguments.FaceProblem(mesh, f) is { } why) errors.Add(new MeshError(MeshEdits.Invariant($"faces[{f}]"), why));
            else if (!seen.Add(f)) errors.Add(new MeshError(MeshEdits.Invariant($"faces[{f}]"), MeshEdits.Invariant($"face {f} is listed more than once")));
        }
        var set = new List<int>(seen);
        set.Sort();
        return set;
    }

    private static MeshError NoExtent(UnwrapPatch patch) => new(MeshEdits.Invariant($"faces[{patch.LowestFace}]"),
        MeshEdits.Invariant($"the island of {UnwrapIslands.Plural(patch.Faces.Length, "face")} has no extent: its corners all lie on one point or one line, so it cannot be flattened"));

    /// <summary>Twice the signed area (the shoelace sum) of a triangle in (u, v); negative is unflipped in screen axes.</summary>
    internal static double TwiceArea((int A, int B, int C) t, double[] u, double[] v) =>
        (u[t.B] - u[t.A]) * (v[t.C] - v[t.A]) - (u[t.C] - u[t.A]) * (v[t.B] - v[t.A]);

    /// <summary>The largest ratio of a triangle's singular values, over the island's non-degenerate triangles; a collapsed triangle gives <see cref="double.MaxValue"/>.</summary>
    private static double Stretch(UnwrapPatch patch, UnwrapLscm.Frame[] frames, double[] u, double[] v)
    {
        var worst = 1.0;
        for (var t = 0; t < frames.Length; t++)
        {
            var f = frames[t];
            if (f.Degenerate) continue;
            var (a, b, c) = patch.Triangles[t];
            double u1 = u[b] - u[a], u2 = u[c] - u[a], v1 = v[b] - v[a], v2 = v[c] - v[a];
            // J = [u1 u2; v1 v2] * inverse([L1 X2; 0 Y2])
            var det = f.L1 * f.Y2;
            double j00 = u1 * f.Y2 / det, j01 = (u2 * f.L1 - u1 * f.X2) / det;
            double j10 = v1 * f.Y2 / det, j11 = (v2 * f.L1 - v1 * f.X2) / det;
            // Singular values as |Q| + |R| and ||Q| - |R||, from the similarity and anti-similarity parts of J: no
            // square root of a cancelling difference, so an isometry gives 1 to rounding.
            double e = (j00 + j11) / 2, f2 = (j00 - j11) / 2, g = (j10 + j01) / 2, h = (j10 - j01) / 2;
            double qn = Math.Sqrt(e * e + h * h), rn = Math.Sqrt(f2 * f2 + g * g);
            var small = Math.Abs(qn - rn);
            var ratio = small > 0 ? (qn + rn) / small : double.MaxValue;
            worst = Math.Max(worst, ratio);
        }
        return worst;
    }
}
