using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>
/// On a queued run (<c>CAIRN_ARTIFACTS</c> set), every frame the real process answers and every <c>tools/list</c> it
/// sends are logged by hash to <c>server-regression-frames.tsv</c> there, so a change that must leave frames alone
/// (T-111's frame guard) can compare a run before it with one after. Unset, it does nothing.
/// </summary>
internal static class FrameLog
{
    private static readonly object Writing = new();

    private static string? File => Environment.GetEnvironmentVariable("CAIRN_ARTIFACTS") is { Length: > 0 } folder
        ? Path.Combine(folder, "server-regression-frames.tsv")
        : null;

    /// <summary>Logs the hash of each image in a <c>tools/call</c> result, keyed by the tool and its arguments.</summary>
    public static void Call(string tool, string arguments, JsonElement result)
    {
        if (File is not { } file || !result.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var item in content.EnumerateArray())
        {
            if (item.TryGetProperty("type", out var type) && type.GetString() == "image" &&
                item.TryGetProperty("data", out var data))
            {
                var png = Convert.FromBase64String(data.GetString()!);
                var hash = Hash(png);
                Append(file, $"{tool}\t{Hash(Encoding.UTF8.GetBytes(arguments))}\t{index}\t{hash}\t{Test}");
                Keep(file, hash, png);
            }

            index++;
        }
    }

    /// <summary>Logs the hash of a <c>tools/list</c> answer's <c>tools</c> array, byte for byte as sent.</summary>
    public static void Tools(JsonElement tools)
    {
        if (File is { } file)
        {
            Append(file, $"tools/list\t-\t-\t{Hash(Encoding.UTF8.GetBytes(tools.GetRawText()))}\t{Test}");
        }
    }

    /// <summary>The test that made the call, so a differing frame can be traced to it.</summary>
    private static string Test => TestContext.Current.Test?.TestDisplayName ?? "-";

    /// <summary>
    /// Each distinct frame once, as <c>server-regression-frames/&lt;hash&gt;.png</c> beside the table. Under the lock:
    /// overlapping calls answer on their own threads, often with the same frame, and two writers of one file collide.
    /// </summary>
    internal static void Keep(string file, string hash, byte[] png)
    {
        lock (Writing)
        {
            var folder = Path.Combine(Path.GetDirectoryName(file)!, "server-regression-frames");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, hash + ".png");
            if (!System.IO.File.Exists(path))
            {
                System.IO.File.WriteAllBytes(path, png);
            }
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void Append(string file, string line)
    {
        lock (Writing)
        {
            System.IO.File.AppendAllText(file, line + "\n");
        }
    }
}
