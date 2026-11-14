using System.Globalization;

namespace Carve.Core;

/// <summary>
/// The face and <c>into</c> checks that set_uvs, project_uvs and unwrap share, each worded once (RV-43, the person,
/// 2026-11-14), with every number written the same on every machine.
/// </summary>
public static class UvArguments
{
    /// <summary>Why face <paramref name="f"/> cannot take UVs (out of range, negative ids included, or deleted), or null when it can.</summary>
    public static string? FaceProblem(PolygonMesh mesh, int f) =>
        f < 0 || f >= mesh.Faces.Count ? MeshEdits.Invariant($"face {f} is not a face of the mesh, which has {mesh.Faces.Count}")
        : mesh.Faces[f].IsDeleted ? MeshEdits.Invariant($"face {f} is deleted")
        : null;

    /// <summary>An <c>into</c> that is not four numbers, <paramref name="got"/> saying what it was (its length, for an array).</summary>
    public static string IntoShape(string got) => $"expected 4 numbers [u0, v0, u1, v1], got {got}";

    /// <summary>Why <c>into</c> has no area, or null when u1 exceeds u0 and v1 exceeds v0.</summary>
    public static string? IntoArea(double u0, double v0, double u1, double v1) =>
        u1 > u0 && v1 > v0 ? null
        : $"[{Number(u0)}, {Number(v0)}, {Number(u1)}, {Number(v1)}] has no area; u1 must exceed u0 and v1 exceed v0";

    /// <summary>
    /// A number as the agent gave it, with a point on every machine: the shortest text that reads back to it, as a float
    /// where it is exactly one (so a 0.1 read as a float is written 0.1, not 0.10000000149011612).
    /// </summary>
    public static string Number(double x) =>
        (double)(float)x == x ? ((float)x).ToString("R", CultureInfo.InvariantCulture) : x.ToString("R", CultureInfo.InvariantCulture);
}
