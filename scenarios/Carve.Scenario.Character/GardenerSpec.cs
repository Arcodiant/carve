using System.Numerics;

namespace Carve.Scenario.Character;

/// <summary>
/// The gardener (T-36), as data: an original small round gardener with a straw hat and a watering can in his right hand,
/// 1.5 m tall, the sample that M-17, M-18 and M-19 build on. Metres, +Y up, facing +Z, so the character's LEFT is +X and
/// the can (in the right hand) is at -X: seen head-on from +Z it is on the image's LEFT. A mirrored frame puts it on the right.
/// <para>
/// Two meshes: <c>Body</c> (torso, head, nose, arms, legs, boots, can) and <c>Hat</c> (brim and crown). Seven colours
/// (skin, shirt, trousers, boots, can, straw, band), each its own face group, so a primitive of the export is one named
/// group. 22 bones named by the engine's Synty preset (Hips, Spine_01..03, Head, Clavicle/Shoulder/Elbow/Hand, UpperLeg/
/// LowerLeg/Ankle/Ball, both sides) plus one prop bone, <c>WateringCan</c>, under <c>Hand_R</c>. Every bone rests with no
/// rotation; pose values and clip keys are absolute local transforms per bone, so a key's rotation is the whole local rotation.
/// Round parts are octagonal lathes (a ring of 8 at each height), so the mesh needs no trigonometry. The clip and pose
/// rotations do: <see cref="Rot"/> calls <c>MathF.Sin</c> and <c>MathF.Cos</c>, and rounds each component to 5 decimal places,
/// which is what keeps the numbers the same on every machine. Two clips, <c>Idle</c> (2 s) and <c>Walk</c> (1 s), both looping and in place: the root's
/// x and z are never keyed and the last key of each channel is the first.
/// </para>
/// The scenario alone reads this file; the checks beside the suite (<c>Carve.CharacterLoad.Tests</c>) compare the sample by content.
/// </summary>
internal static class GardenerSpec
{
    public const string Skin = "#E8B896";
    public const string Shirt = "#6AAE48";
    public const string Trousers = "#3C54A0";
    public const string Boots = "#4A2C1A";
    public const string Can = "#1FA8A0";
    public const string Straw = "#E8C86A";
    public const string Band = "#C0392B";

    /// <summary>The colours in the order their groups are made; each group is named for its part.</summary>
    public static readonly (string Group, string Colour)[] Palette =
    [
        ("skin", Skin), ("shirt", Shirt), ("trousers", Trousers), ("boots", Boots), ("can", Can), ("straw", Straw), ("band", Band),
    ];

    // ---- the skeleton ----

    public sealed record Bone(string Name, string? Parent, float X, float Y, float Z);

    /// <summary>In the skeleton's own order, a parent before its children. Positions are local to the parent.</summary>
    public static readonly Bone[] Bones =
    [
        new("Hips", null, 0, 0.60f, 0),
        new("Spine_01", "Hips", 0, 0.08f, 0),
        new("Spine_02", "Spine_01", 0, 0.12f, 0),
        new("Spine_03", "Spine_02", 0, 0.12f, 0),
        new("Head", "Spine_03", 0, 0.18f, 0),
        new("Clavicle_L", "Spine_03", 0.07f, 0.08f, 0),
        new("Shoulder_L", "Clavicle_L", 0.10f, 0, 0),
        new("Elbow_L", "Shoulder_L", 0.13f, -0.20f, 0),
        new("Hand_L", "Elbow_L", 0.07f, -0.18f, 0),
        new("Clavicle_R", "Spine_03", -0.07f, 0.08f, 0),
        new("Shoulder_R", "Clavicle_R", -0.10f, 0, 0),
        new("Elbow_R", "Shoulder_R", -0.13f, -0.20f, 0),
        new("Hand_R", "Elbow_R", -0.07f, -0.18f, 0),
        new("WateringCan", "Hand_R", 0, -0.04f, 0),
        new("UpperLeg_L", "Hips", 0.09f, -0.05f, 0),
        new("LowerLeg_L", "UpperLeg_L", 0, -0.27f, 0),
        new("Ankle_L", "LowerLeg_L", 0, -0.25f, 0),
        new("Ball_L", "Ankle_L", 0, -0.02f, 0.10f),
        new("UpperLeg_R", "Hips", -0.09f, -0.05f, 0),
        new("LowerLeg_R", "UpperLeg_R", 0, -0.27f, 0),
        new("Ankle_R", "LowerLeg_R", 0, -0.25f, 0),
        new("Ball_R", "Ankle_R", 0, -0.02f, 0.10f),
    ];

    /// <summary>The <c>bones</c> argument of <c>define_skeleton</c>.</summary>
    public static object[] SkeletonArgument() =>
        [.. Bones.Select(b => (object)new { name = b.Name, parent = b.Parent, position = new[] { b.X, b.Y, b.Z }, rotation = new[] { 0f, 0, 0, 1 } })];

    // ---- rotations ----

    private static float[] Q(float x, float y, float z, float w) => [R5(x), R5(y), R5(z), R5(w)];

    private static float R5(float v) => MathF.Round(v, 5);

    private static float[] Rot(char axis, float degrees)
    {
        var h = degrees * MathF.PI / 360f;
        var (s, c) = (MathF.Sin(h), MathF.Cos(h));
        return axis switch { 'X' => Q(s, 0, 0, c), 'Y' => Q(0, s, 0, c), _ => Q(0, 0, s, c) };
    }

    // ---- clips ----

    public sealed record Key(float Time, float[] Value);

    public sealed record Channel(string Bone, string Part, Key[] Keys);

    public sealed record ClipSpec(string Name, float Duration, bool Loop, Channel[] Channels)
    {
        /// <summary>The <c>channels</c> argument of <c>define_clip</c>: bone to its rotation and/or translation.</summary>
        public Dictionary<string, object> Argument()
        {
            var result = new Dictionary<string, object>();
            foreach (var bone in Channels.Select(c => c.Bone).Distinct())
            {
                var parts = new Dictionary<string, object>();
                foreach (var c in Channels.Where(c => c.Bone == bone))
                {
                    parts[c.Part] = new { interpolation = "LINEAR", keys = c.Keys.Select(k => new { time = k.Time, value = k.Value }).ToArray() };
                }

                result[bone] = parts;
            }

            return result;
        }

        /// <summary>The pose the keys at <paramref name="time"/> give (a time that is a key): every bone and part the clip animates.</summary>
        public Dictionary<string, object> PoseAt(float time)
        {
            var result = new Dictionary<string, object>();
            foreach (var bone in Channels.Select(c => c.Bone).Distinct())
            {
                var parts = new Dictionary<string, object>();
                foreach (var c in Channels.Where(c => c.Bone == bone))
                {
                    parts[c.Part] = c.Keys.Single(k => k.Time == time).Value;
                }

                result[bone] = parts;
            }

            return result;
        }

        /// <summary>The local rotation and translation of a bone at a time, from the keys alone: lerp and slerp between keys, held outside them.</summary>
        public (Quaternion? Rotation, Vector3? Translation) Sample(string bone, float time)
        {
            Quaternion? rotation = null;
            Vector3? translation = null;
            foreach (var c in Channels.Where(c => c.Bone == bone))
            {
                var next = Array.FindIndex(c.Keys, k => k.Time >= time);
                var (a, b) = next <= 0 ? (c.Keys[Math.Max(next, 0)], c.Keys[Math.Max(next, 0)]) : (c.Keys[next - 1], c.Keys[next]);
                if (next < 0)
                {
                    a = b = c.Keys[^1];
                }

                var f = b.Time == a.Time ? 0 : (time - a.Time) / (b.Time - a.Time);
                if (c.Part == "rotation")
                {
                    rotation = Quaternion.Slerp(new Quaternion(a.Value[0], a.Value[1], a.Value[2], a.Value[3]), new Quaternion(b.Value[0], b.Value[1], b.Value[2], b.Value[3]), f);
                }
                else
                {
                    translation = Vector3.Lerp(new Vector3(a.Value[0], a.Value[1], a.Value[2]), new Vector3(b.Value[0], b.Value[1], b.Value[2]), f);
                }
            }

            return (rotation, translation);
        }
    }

    private static Channel Rotation(string bone, char axis, float[] times, params float[] degrees) =>
        new(bone, "rotation", [.. times.Select((t, i) => new Key(t, Rot(axis, degrees[i])))]);

    private static Channel Hips(float[] times, params float[] y) =>
        new("Hips", "translation", [.. times.Select((t, i) => new Key(t, [0f, y[i], 0f]))]);

    /// <summary>Breathing and a look about, the can swaying out and back. In place; the last key of each channel is its first.</summary>
    public static readonly ClipSpec Idle = new("Idle", 2f, true,
    [
        Hips([0f, 1f, 2f], 0.60f, 0.59f, 0.60f),
        Rotation("Spine_02", 'X', [0f, 1f, 2f], 0, 3, 0),
        Rotation("Head", 'Y', [0f, 0.5f, 1f, 1.5f, 2f], 0, 20, 0, -20, 0),
        Rotation("Shoulder_R", 'Z', [0f, 1f, 2f], 0, -12, 0),
    ]);

    private static readonly float[] Quarters = [0f, 0.25f, 0.5f, 0.75f, 1f];

    /// <summary>
    /// A walk in place, one stride a second: the thighs and the knees, the arms against the legs (the right arm, which carries
    /// the can, swings less), a little twist of the spine and a bob of the hips. At 0 s the left leg is back and the left arm
    /// forward; at 0.5 s the other way round; a positive turn about X swings a hanging limb toward -Z, backward.
    /// </summary>
    public static readonly ClipSpec Walk = new("Walk", 1f, true,
    [
        Hips(Quarters, 0.60f, 0.585f, 0.60f, 0.585f, 0.60f),
        Rotation("Spine_02", 'Y', Quarters, 4, 0, -4, 0, 4),
        Rotation("UpperLeg_L", 'X', Quarters, 30, 0, -30, 0, 30),
        Rotation("UpperLeg_R", 'X', Quarters, -30, 0, 30, 0, -30),
        Rotation("LowerLeg_L", 'X', Quarters, 5, 40, 5, 15, 5),
        Rotation("LowerLeg_R", 'X', Quarters, 5, 15, 5, 40, 5),
        Rotation("Shoulder_L", 'X', Quarters, -25, 0, 25, 0, -25),
        Rotation("Shoulder_R", 'X', Quarters, 12, 0, -12, 0, 12),
    ]);

    /// <summary>The one still pose of the scenario: the can arm swung out and up, the head turned, the left leg forward.</summary>
    public static object Pose => new Dictionary<string, object>
    {
        ["Shoulder_R"] = new { rotation = Rot('Z', -35) },
        ["Head"] = new { rotation = Rot('Y', 30) },
        ["UpperLeg_L"] = new { rotation = Rot('X', -25) },
    };

    /// <summary>The rotation of the pose, as a quaternion, for a bone it poses.</summary>
    public static Quaternion PoseRotation(char axis, float degrees)
    {
        var q = Rot(axis, degrees);
        return new Quaternion(q[0], q[1], q[2], q[3]);
    }

    // ---- forward kinematics of the rest skeleton, to place a point of a bone in a pose ----

    private static Matrix4x4 Local(Bone b, Quaternion? rotation, Vector3? translation) =>
        Matrix4x4.CreateFromQuaternion(rotation ?? Quaternion.Identity) * Matrix4x4.CreateTranslation(translation ?? new Vector3(b.X, b.Y, b.Z));

    private static Matrix4x4 World(string name, Func<Bone, (Quaternion? Rotation, Vector3? Translation)> local)
    {
        var bone = Bones.Single(b => b.Name == name);
        var (r, t) = local(bone);
        var m = Local(bone, r, t);
        return bone.Parent is null ? m : m * World(bone.Parent, local);
    }

    /// <summary>Where a point that rests at <paramref name="rest"/> and moves with <paramref name="bone"/> goes, given each bone's local rotation and translation (rest for a bone left out).</summary>
    public static Vector3 Posed(Vector3 rest, string bone, Func<string, (Quaternion? Rotation, Vector3? Translation)> locals)
    {
        Matrix4x4.Invert(World(bone, b => (null, null)), out var inverse);
        var posed = World(bone, b => locals(b.Name));
        return Vector3.Transform(rest, inverse * posed);
    }

    // ---- the meshes ----

    public sealed class MeshSpec(string name)
    {
        public string Name { get; } = name;

        public List<float[]> Vertices { get; } = [];

        public List<int[]> Faces { get; } = [];

        /// <summary>Face id to its group, which is also its colour's name in <see cref="Palette"/>.</summary>
        public List<string> FaceGroup { get; } = [];

        /// <summary>Weights per vertex group: the same bone weights for these vertices, in call order.</summary>
        public List<(int[] Vertices, Dictionary<string, double> Weights)> Weights { get; } = [];

        public object[] FacesArgument => [.. Faces.Select(f => (object)f)];

        public int[] FacesOf(string group) => [.. Enumerable.Range(0, Faces.Count).Where(f => FaceGroup[f] == group)];

        public Vector3 Centroid(int face) =>
            Faces[face].Select(v => new Vector3(Vertices[v][0], Vertices[v][1], Vertices[v][2])).Aggregate(Vector3.Zero, (a, b) => a + b) / Faces[face].Length;

        /// <summary>The face of <paramref name="group"/> whose centre is nearest <paramref name="near"/>.</summary>
        public int NearestFace(string group, Vector3 near) => FacesOf(group).MinBy(f => Vector3.DistanceSquared(Centroid(f), near));

        public (Vector3 Min, Vector3 Max) Bounds()
        {
            var points = Vertices.Select(v => new Vector3(v[0], v[1], v[2])).ToArray();
            return (points.Aggregate(Vector3.Min), points.Aggregate(Vector3.Max));
        }

        public sealed record Ring(float Y, float Radius, float X, float Z, Dictionary<string, double> Weights);

        private static readonly float C = 0.70710677f;
        private static readonly (float Cos, float Sin)[] Octagon = [(1, 0), (C, C), (0, 1), (-C, C), (-1, 0), (-C, -C), (0, -1), (C, -C)];

        /// <summary>
        /// A stack of octagonal rings, one segment of quads between each pair, outward counter-clockwise. A segment's group is
        /// <paramref name="segments"/>[i]; the end caps are ignored when their group is null. Returns the id of the top cap's face, or -1.
        /// </summary>
        public int Lathe(Ring[] rings, string[] segments, string? bottom, string? top)
        {
            var first = Vertices.Count;
            foreach (var ring in rings)
            {
                var ids = new List<int>();
                foreach (var (cos, sin) in Octagon)
                {
                    ids.Add(Vertices.Count);
                    Vertices.Add([ring.X + ring.Radius * cos, ring.Y, ring.Z + ring.Radius * sin]);
                }

                Weights.Add(([.. ids], ring.Weights));
            }

            for (var i = 0; i < rings.Length - 1; i++)
            {
                for (var j = 0; j < 8; j++)
                {
                    var (a, b) = (first + i * 8, first + (i + 1) * 8);
                    var next = (j + 1) % 8;
                    Add([a + next, a + j, b + j, b + next], segments[i]);
                }
            }

            if (bottom is not null)
            {
                Add([.. Enumerable.Range(0, 8).Select(j => first + j)], bottom);
            }

            var topFace = -1;
            if (top is not null)
            {
                var last = first + (rings.Length - 1) * 8;
                topFace = Faces.Count;
                Add([.. Enumerable.Range(0, 8).Select(j => last + 7 - j)], top);
            }

            return topFace;
        }

        /// <summary>An axis-aligned box, outward counter-clockwise, all of one group and one set of weights.</summary>
        public void Box(Vector3 min, Vector3 max, string group, Dictionary<string, double> weights)
        {
            var first = Vertices.Count;
            foreach (var z in new[] { min.Z, max.Z })
            {
                Vertices.Add([min.X, min.Y, z]);
                Vertices.Add([max.X, min.Y, z]);
                Vertices.Add([max.X, max.Y, z]);
                Vertices.Add([min.X, max.Y, z]);
            }

            foreach (var f in new[] { new[] { 0, 3, 2, 1 }, [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5] })
            {
                Add([.. f.Select(i => first + i)], group);
            }

            Weights.Add(([.. Enumerable.Range(first, 8)], weights));
        }

        private void Add(int[] face, string group)
        {
            Faces.Add(face);
            FaceGroup.Add(group);
        }
    }

    private static Dictionary<string, double> W(params (string Bone, double Weight)[] w) => w.ToDictionary(x => x.Bone, x => x.Weight);

    private static MeshSpec.Ring Ring(float y, float r, Dictionary<string, double> w, float x = 0, float z = 0) => new(y, r, x, z, w);

    /// <summary>
    /// The torso's top segment, from the chest ring (y 1.02) to the neck ring (y 1.12), is the slope of the shoulders, the arms' sleeves
    /// start on it, so it is the shirt's: it was skin (T-84), a patch of flesh at the top of each arm. The cap above it stays skin, a neck.
    /// </summary>
    public const string ShoulderSegment = "shirt";

    /// <summary>The Hat's top cap, the face the unweighted-export step extrudes (its new vertices start with no weight).</summary>
    public static int HatTopFace { get; private set; }

    public static (MeshSpec Body, MeshSpec Hat) Build()
    {
        var body = new MeshSpec("Body");
        var hat = new MeshSpec("Hat");
        var hips = W(("Hips", 1));
        var chest = W(("Spine_03", 1));

        // The torso: trousers to the belt, shirt to the collar, neck. Weighted Hips, blended to Spine_02, then Spine_03.
        body.Lathe(
            [Ring(0.52f, 0.16f, hips), Ring(0.62f, 0.22f, hips), Ring(0.76f, 0.25f, W(("Hips", 0.5), ("Spine_02", 0.5))),
             Ring(0.90f, 0.21f, chest), Ring(1.02f, 0.15f, chest), Ring(1.12f, 0.07f, chest)],
            ["trousers", "shirt", "shirt", "shirt", ShoulderSegment], "trousers", "skin");

        // The head and a nose on its front.
        var head = W(("Head", 1));
        body.Lathe([Ring(1.12f, 0.09f, head), Ring(1.18f, 0.15f, head), Ring(1.26f, 0.17f, head), Ring(1.34f, 0.14f, head), Ring(1.39f, 0.07f, head)],
            ["skin", "skin", "skin", "skin"], "skin", "skin");
        body.Box(new Vector3(-0.025f, 1.22f, 0.16f), new Vector3(0.025f, 1.27f, 0.21f), "skin", head);

        foreach (var (side, sign) in new[] { ("L", 1f), ("R", -1f) })
        {
            // An arm: a sleeve to the elbow, then skin to the hand; the elbow ring is shared between the shoulder and the elbow.
            var shoulder = W(($"Shoulder_{side}", 1));
            var elbow = W(($"Elbow_{side}", 1));
            var hand = W(($"Hand_{side}", 1));
            body.Lathe(
                [Ring(1.02f, 0.06f, shoulder, 0.17f * sign), Ring(0.90f, 0.055f, shoulder, 0.23f * sign),
                 Ring(0.80f, 0.047f, W(($"Shoulder_{side}", 0.5), ($"Elbow_{side}", 0.5)), 0.30f * sign), Ring(0.70f, 0.04f, elbow, 0.34f * sign),
                 Ring(0.62f, 0.045f, hand, 0.37f * sign), Ring(0.56f, 0.035f, hand, 0.37f * sign)],
                ["shirt", "shirt", "skin", "skin", "skin"], "shirt", "skin");

            // A leg: trousers, thigh to knee to shin, the knee ring shared; a boot on the ankle.
            var upper = W(($"UpperLeg_{side}", 1));
            var lower = W(($"LowerLeg_{side}", 1));
            body.Lathe(
                [Ring(0.55f, 0.07f, upper, 0.09f * sign), Ring(0.42f, 0.065f, upper, 0.09f * sign),
                 Ring(0.28f, 0.058f, W(($"UpperLeg_{side}", 0.5), ($"LowerLeg_{side}", 0.5)), 0.09f * sign),
                 Ring(0.16f, 0.052f, lower, 0.09f * sign), Ring(0.09f, 0.052f, lower, 0.09f * sign)],
                ["trousers", "trousers", "trousers", "trousers"], "trousers", "trousers");
            body.Box(new Vector3(0.09f * sign - 0.06f, 0, -0.07f), new Vector3(0.09f * sign + 0.06f, 0.10f, 0.17f), "boots", W(($"Ankle_{side}", 1)));
        }

        // The watering can in the right hand (at -X): a drum, a spout pointing forward and a handle up to the hand.
        var can = W(("WateringCan", 1));
        body.Lathe([Ring(0.30f, 0.09f, can, -0.43f, 0.02f), Ring(0.48f, 0.09f, can, -0.43f, 0.02f)], ["can"], "can", "can");
        body.Box(new Vector3(-0.455f, 0.36f, 0.10f), new Vector3(-0.415f, 0.42f, 0.30f), "can", can);
        body.Box(new Vector3(-0.40f, 0.48f, -0.015f), new Vector3(-0.35f, 0.64f, 0.045f), "can", can);

        // The hat: a brim and a crown with a red band, all on the head.
        hat.Lathe([Ring(1.335f, 0.26f, head), Ring(1.355f, 0.26f, head)], ["straw"], "straw", "straw");
        HatTopFace = hat.Lathe(
            [Ring(1.355f, 0.155f, head), Ring(1.40f, 0.155f, head), Ring(1.46f, 0.135f, head), Ring(1.50f, 0.09f, head)],
            ["straw", "band", "straw"], "straw", "straw");
        return (body, hat);
    }

    /// <summary>The rest bounds over both meshes: what a frame is framed on.</summary>
    public static (Vector3 Min, Vector3 Max) RestBounds()
    {
        var (body, hat) = Build();
        var (bmin, bmax) = body.Bounds();
        var (hmin, hmax) = hat.Bounds();
        return (Vector3.Min(bmin, hmin), Vector3.Max(bmax, hmax));
    }
}
