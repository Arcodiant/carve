using System.Numerics;

namespace Carve.Rover;

/// <summary>
/// The rover (M-9's agreed sample), as data: every number of the milestone's sketch, typed here once. Metres, +Y up, +X
/// forward. A chassis box with a cab, a turning mast carrying a lamp, and two empty axles, each with a wheel and an
/// instance of it turned 180° about +Y, so every hub boss faces outward. Two kept levels: level 0 is the blockout (boxes and
/// an 8-sided wheel); level 1 (and live) has a 16-sided wheel with a hub boss, the cab's windscreen recess and the lamp's
/// lens recess. The Mast and the Chassis are kept at level 0 only.
/// <para>
/// This file uses System.Numerics alone, so the T-96 scenario can link it and send the same numbers through the tools:
/// <see cref="Tree"/> to <c>define_part</c>, <see cref="Blockout"/> to <c>define_mesh</c>, <see cref="BlockoutPaint"/> to
/// <c>set_colour</c>, then <c>keep_level 0</c>, then <see cref="RefinedWheel"/> (a <c>define_mesh</c>), <see cref="Cuts"/>
/// (one <c>extrude</c> each, in order) and <see cref="RefinedPaint"/>, then <c>keep_level 1</c> on <see cref="RefinedParts"/>.
/// <c>RoverSpec.Model.cs</c> (in <c>tests/Carve.Core.Tests</c> only) builds the same rover from <c>Model</c> calls.
/// </para>
/// <para>
/// Numbers the sketch does not give, chosen by T-91's second agent and named here so they are typed once: the colours'
/// hex values; the insets and depths of the windscreen (0.1, 0.02), the lens (0.03, 0.02) and the hub boss (inset 0.25,
/// so a boss of circumradius about 0.145); the windscreen keeps the cab's sand and the Mast and the Lamp's body are left
/// uncoloured (the agreed palette names neither); the wheels' vertices start at angle 0 on +X, so a vertex sits on the
/// ground (y 0) and the wheel spans x and y ±0.4 at both levels; the instance edit moves the boss cap 0.1 up.
/// </para>
/// </summary>
internal static partial class RoverSpec
{
    public const string Olive = "#6B7B3A";     // chassis
    public const string Sand = "#D9C59A";      // cab
    public const string DarkGrey = "#3C3C3C";  // wheels
    public const string LightGrey = "#B4B4B4"; // hub boss
    public const string Yellow = "#F2C12E";    // lens

    /// <summary>One part record, as <c>define_part</c> takes it. Rotation is <c>[x, y, z, w]</c>; Envelope is in the part's own space.</summary>
    public sealed record PartSpec(string Name, string? Parent, Vector3 Translation, Quaternion Rotation,
        (Vector3 Min, Vector3 Max)? Envelope, string? InstanceOf, bool Moves);

    /// <summary>A mesh as <c>define_mesh</c> takes it: positions and faces, counter-clockwise seen from outside.</summary>
    public sealed record Shape(Vector3[] Positions, int[][] Faces);

    /// <summary>One <c>set_colour</c>: these faces of the part's live mesh get the colour.</summary>
    public sealed record Paint(string Part, int[] Faces, string Colour);

    /// <summary>One <c>extrude</c> of one face: an inset (distance 0) or a straight extrude or recess (inset 0).</summary>
    public sealed record Cut(string Part, int Face, float Distance, float Inset);

    public static readonly Quaternion Turned = new(0, 1, 0, 0);                    // 180° about +Y
    public static readonly Quaternion MastTurned = new(0, 0.7071068f, 0, 0.7071068f); // 90° about +Y: the lamp from +X to -Z

    private static readonly (Vector3, Vector3) AxleEnvelope = (new(-0.45f, -0.45f, -0.92f), new(0.45f, 0.45f, 0.92f));

    /// <summary>The part tree in definition order (which is its pre-order).</summary>
    public static readonly PartSpec[] Tree =
    [
        new("Chassis", null, Vector3.Zero, Quaternion.Identity, (new(-1.2f, -0.05f, -0.95f), new(1.2f, 1.75f, 0.95f)), null, false),
        new("Cab", "Chassis", new(0.5f, 0.8f, 0), Quaternion.Identity, (new(-0.42f, -0.02f, -0.52f), new(0.42f, 0.62f, 0.52f)), null, false),
        new("Mast", "Chassis", new(-0.6f, 0.8f, 0), Quaternion.Identity, (new(-0.4f, 0, -0.4f), new(0.4f, 0.95f, 0.4f)), null, true),
        new("Lamp", "Mast", new(0, 0.8f, 0), Quaternion.Identity, (new(-0.02f, -0.12f, -0.12f), new(0.32f, 0.12f, 0.12f)), null, false),
        new("Axle_F", "Chassis", new(0.7f, 0.4f, 0), Quaternion.Identity, AxleEnvelope, null, false),
        new("Wheel_FL", "Axle_F", new(0, 0, 0.72f), Quaternion.Identity, (new(-0.42f, -0.42f, -0.12f), new(0.42f, 0.42f, 0.17f)), null, true),
        new("Wheel_FR", "Axle_F", new(0, 0, -0.72f), Turned, null, "Wheel_FL", true),
        new("Axle_R", "Chassis", new(-0.7f, 0.4f, 0), Quaternion.Identity, AxleEnvelope, null, false),
        new("Wheel_RL", "Axle_R", new(0, 0, 0.72f), Quaternion.Identity, null, "Wheel_FL", true),
        new("Wheel_RR", "Axle_R", new(0, 0, -0.72f), Turned, null, "Wheel_FL", true),
    ];

    /// <summary>The parts that own geometry, in tree order; each is kept at level 0.</summary>
    public static readonly string[] GeometryParts = ["Chassis", "Cab", "Mast", "Lamp", "Wheel_FL"];

    /// <summary>The parts refined and kept at level 1. The Mast and the Chassis are not, so they draw as level 0 at level 1.</summary>
    public static readonly string[] RefinedParts = ["Cab", "Lamp", "Wheel_FL"];

    // A box's faces: 0 -z, 1 +z, 2 -y, 3 +y, 4 -x, 5 +x.
    public const int BoxMinusY = 2, BoxPlusY = 3, BoxPlusX = 5;

    // A wheel's faces: 0 the -Z cap (inner), 1 the +Z cap (the hub side), then one side quad per edge.
    public const int WheelInnerCap = 0, WheelHubCap = 1;

    public const float WheelRadius = 0.4f, WheelHalfWidth = 0.1f, BossHeight = 0.05f, BossInset = 0.25f;
    public const float WindscreenInset = 0.1f, WindscreenDepth = 0.02f, LensInset = 0.03f, LensDepth = 0.02f;

    /// <summary>Level 0's geometry, by part.</summary>
    public static readonly IReadOnlyDictionary<string, Shape> Blockout = new Dictionary<string, Shape>
    {
        ["Chassis"] = Box(new(-1, 0.4f, -0.6f), new(1, 0.8f, 0.6f)),
        ["Cab"] = Box(new(-0.4f, 0, -0.5f), new(0.4f, 0.6f, 0.5f)),
        ["Mast"] = Box(new(-0.05f, 0, -0.05f), new(0.05f, 0.8f, 0.05f)),
        ["Lamp"] = Box(new(0, -0.1f, -0.1f), new(0.3f, 0.1f, 0.1f)),
        ["Wheel_FL"] = Wheel(8),
    };

    public static readonly Paint[] BlockoutPaint =
    [
        new("Chassis", All(6), Olive),
        new("Cab", All(6), Sand),
        new("Wheel_FL", All(10), DarkGrey),
    ];

    /// <summary>The 16-sided wheel, defined over the 8-sided one after level 0 is kept.</summary>
    public static readonly Shape RefinedWheel = Wheel(16);

    /// <summary>The refinement's extrudes, in order: each face's inset, then its boss or recess.</summary>
    public static readonly Cut[] Cuts =
    [
        new("Wheel_FL", WheelHubCap, 0, BossInset),
        new("Wheel_FL", WheelHubCap, BossHeight, 0),
        new("Cab", BoxPlusX, 0, WindscreenInset),
        new("Cab", BoxPlusX, -WindscreenDepth, 0),
        new("Lamp", BoxPlusX, 0, LensInset),
        new("Lamp", BoxPlusX, -LensDepth, 0),
    ];

    /// <summary>
    /// The 16-sided wheel's faces: 18 (two caps and 16 sides), then the inset's 16 walls (18..33, the dark ring round the
    /// boss), then the boss's 16 walls (34..49). The boss is its cap and its walls.
    /// </summary>
    public static readonly int[] HubBossFaces = [WheelHubCap, .. Enumerable.Range(34, 16)];

    public static readonly Paint[] RefinedPaint =
    [
        new("Wheel_FL", All(50), DarkGrey),
        new("Wheel_FL", HubBossFaces, LightGrey),
        new("Lamp", [BoxPlusX], Yellow),
    ];

    /// <summary>The instance edit: one move of the boss cap's corners on <c>Wheel_FL</c>, seen in all four wheels.</summary>
    public static readonly (string Part, int Face, Vector3 By) HubMove = ("Wheel_FL", WheelHubCap, new(0, 0.1f, 0));

    /// <summary>The planted envelope fault: <c>Axle_F</c> lowered 0.3 m, so its placed envelope reaches y -0.35, below the Chassis's -0.05.</summary>
    public static readonly Vector3 AxleLoweredTranslation = new(0.7f, 0.1f, 0);

    public static PartSpec Find(string name) => Array.Find(Tree, p => p.Name == name) ?? throw new ArgumentException(name);

    private static int[] All(int faces) => [.. Enumerable.Range(0, faces)];

    /// <summary>An axis-aligned box: vertex i has x max when bit 0 xor bit 1 is set, y max when bit 1 is, z max when bit 2 is.</summary>
    public static Shape Box(Vector3 min, Vector3 max)
    {
        Vector3[] p =
        [
            new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z), new(max.X, max.Y, min.Z), new(min.X, max.Y, min.Z),
            new(min.X, min.Y, max.Z), new(max.X, min.Y, max.Z), new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z),
        ];
        return new(p, [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]]);
    }

    /// <summary>
    /// An n-sided prism about +Z, circumradius 0.4, z -0.1..0.1: vertex k of the -Z ring (id k) and the +Z ring (id n + k) at
    /// angle 2πk/n from +X towards +Y, each coordinate rounded to 6 decimals so it is the same on every machine.
    /// </summary>
    public static Shape Wheel(int n)
    {
        var p = new Vector3[2 * n];
        for (var k = 0; k < n; k++)
        {
            var a = 2 * Math.PI * k / n;
            var x = (float)(Math.Round(WheelRadius * Math.Cos(a), 6) + 0.0);
            var y = (float)(Math.Round(WheelRadius * Math.Sin(a), 6) + 0.0);
            p[k] = new(x, y, -WheelHalfWidth);
            p[n + k] = new(x, y, WheelHalfWidth);
        }
        var faces = new List<int[]>
        {
            Enumerable.Range(0, n).Select(k => (n - k) % n).ToArray(),
            Enumerable.Range(n, n).ToArray(),
        };
        for (var k = 0; k < n; k++)
            faces.Add([k, (k + 1) % n, n + (k + 1) % n, n + k]);
        return new(p, [.. faces]);
    }
}
