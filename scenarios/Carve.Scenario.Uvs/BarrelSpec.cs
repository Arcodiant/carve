namespace Carve.Scenario.Uvs;

/// <summary>
/// The barrel of T-53 (the one source of its calls: vertices, faces and seams), as <c>TestMeshes.Barrel</c> and <c>BarrelSeams</c> build
/// it: eight sides, five rings at y 0, 0.1, 0.5, 0.9 and 1, radii 0.36, 0.38, 0.42, 0.38 and 0.36; ring i's vertex k is id 8i + k at
/// angle 2 pi k / 8, at (r cos a, y, -r sin a) rounded to 6 decimals.
/// </summary>
internal static class BarrelSpec
{
    public static readonly double[] Heights = [0, 0.1, 0.5, 0.9, 1.0];
    public static readonly double[] Radii = [0.36, 0.38, 0.42, 0.38, 0.36];

    public static double[][] Vertices { get; } = Enumerable.Range(0, 5).SelectMany(i => Enumerable.Range(0, 8).Select(k =>
    {
        var a = 2 * Math.PI * k / 8;
        return new[] { Math.Round(Radii[i] * Math.Cos(a), 6), Heights[i], Math.Round(-Radii[i] * Math.Sin(a), 6) };
    })).ToArray();

    public static int[][] Faces { get; } =
    [
        .. Enumerable.Range(0, 4).SelectMany(i => Enumerable.Range(0, 8).Select(k => new[] { 8 * i + k, 8 * i + (k + 1) % 8, 8 * (i + 1) + (k + 1) % 8, 8 * (i + 1) + k })),
        [7, 6, 5, 4, 3, 2, 1, 0],
        [32, 33, 34, 35, 36, 37, 38, 39],
    ];

    /// <summary>The four vertical edges at -Z and both cap rings.</summary>
    public static int[][] Seams { get; } =
    [
        [2, 10], [10, 18], [18, 26], [26, 34],
        .. Enumerable.Range(0, 8).Select(k => new[] { k, (k + 1) % 8 }),
        .. Enumerable.Range(0, 8).Select(k => new[] { 32 + k, 32 + (k + 1) % 8 }),
    ];

    /// <summary>Ring 2's vertex k moved outward, along its own radius, by <paramref name="by"/> metres.</summary>
    public static double[] Outward(int k, double by)
    {
        var a = 2 * Math.PI * k / 8;
        return [Math.Round(by * Math.Cos(a), 6), 0, Math.Round(-by * Math.Sin(a), 6)];
    }
}
