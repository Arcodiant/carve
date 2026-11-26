using System.Text.Json;
using static Carve.Scenario.Shed.Checks;

namespace Carve.Scenario.Shed;

/// <summary>What T-23's <c>### As built</c> says <c>check</c> reports on the shed, verbatim.</summary>
internal static class ShedFacts
{
    /// <summary>What <c>open</c> reports as warnings: the same 22 as <c>check</c>, as the first run showed.</summary>
    public static string[] OpenWarnings => Warnings;

    public static readonly (string Check, string Element, string Message)[] Problems =
    [
        ("inside-out", "shells[1]", "closed shell of 6 faces (faces 12 to 17) is inside-out: its faces are wound clockwise seen from outside, so a game culls them and sees nothing"),
        ("coincident-vertices", "vertices[10]", "vertex is at the same position as vertices[5]"),
        ("coincident-vertices", "vertices[11]", "vertex is at the same position as vertices[6]"),
        ("duplicate-face", "faces[8]", "face has the same vertices as faces[7]"),
    ];

    public static readonly string[] Warnings =
    [
        "8 edges have two faces crossing them in the same direction: e.g. edges[0-3], edges[0-4], edges[3-7]",
        "8 edges belong to only one face: e.g. edges[4-5], edges[4-10], edges[5-6]; select edges where open names them",
        "4 edges are shared by more than two faces: worst edges[12-13], shared by 3 faces; e.g. edges[12-13], edges[12-15], edges[13-14]; select edges where nonmanifold names them",
        "faces[11]: face is not planar: a vertex is 0.0105 off its plane (limit 0.000439)",
        "vertices[24]: no face uses this vertex",
    ];

    public static readonly string[] Notes = ["size 4 x 3.9 x 3 m", "base at y = 0", "origin inside the bounds", "2 separate shells; a model is usually one"];

    public static void CheckReport(JsonElement check)
    {
        Equal("check: ok", false, check.GetProperty("ok").GetBoolean());
        SameList(
            "check: problems",
            Problems.Select(p => $"{p.Check} {p.Element} {p.Message}"),
            check.GetProperty("problems").EnumerateArray().Select(p => $"{p.GetProperty("check").GetString()} {p.GetProperty("element").GetString()} {p.GetProperty("message").GetString()}"));
        SameList("check: warnings", Warnings, check.GetProperty("warnings").EnumerateArray().Select(w => w.GetString() ?? ""));
        SameList("check: notes", Notes, check.GetProperty("notes").EnumerateArray().Select(w => w.GetString() ?? ""));

        var stats = check.GetProperty("stats");
        foreach (var (name, value) in new[] { ("vertices", 25), ("faces", 18), ("edges", 38), ("triangles", 36), ("shells", 2) })
        {
            Equal($"check: stats.{name}", value, stats.GetProperty(name).GetInt32());
        }

        Equal("check: stats.closed", false, stats.GetProperty("closed").GetBoolean());
        Equal("check: stats.volume", JsonValueKind.Null, stats.GetProperty("volume").ValueKind);
        Equal("check: stats.area", 60.09371484553011, stats.GetProperty("area").GetDouble());
        SameList("check: stats.bounds.min", ["0", "0", "0"], stats.GetProperty("bounds").GetProperty("min").EnumerateArray().Select(v => $"{v.GetDouble()}"));
        SameList("check: stats.bounds.max", ["4", "3.9", "3"], stats.GetProperty("bounds").GetProperty("max").EnumerateArray().Select(v => $"{v.GetDouble()}"));
        SameList("check: stats.size", ["4", "3.9", "3"], stats.GetProperty("size").EnumerateArray().Select(v => $"{v.GetDouble()}"));
    }
}
