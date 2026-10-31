using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Carve.Debug;

/// <summary>
/// The run's JSON log, rewritten whole after each entry so that a run that dies leaves everything
/// up to its last call. No timestamps, so the same calls give the same log. An image's data
/// becomes its size and hash; the frames are the images.
/// </summary>
internal sealed class RunLog
{
    private static readonly JsonSerializerOptions Written = new() { WriteIndented = true };

    private readonly string _path;
    private readonly JsonObject _root;
    private readonly JsonArray _calls = [];
    private readonly Lock _gate = new();

    public RunLog(string path, string scenario, int seed)
    {
        _path = path;
        _root = new JsonObject
        {
            ["scenario"] = scenario,
            ["seed"] = seed,
            ["audio"] = "none",
            ["outcome"] = "running",
            ["calls"] = _calls,
        };
        Save();
    }

    public void ToolsListed(IEnumerable<Tool> tools) => Add(new JsonObject
    {
        ["method"] = "tools/list",
        ["result"] = new JsonArray(tools.Select(t => JsonSerializer.SerializeToNode(t, McpJsonUtilities.DefaultOptions)).ToArray()),
    });

    public void ToolsListFailed(Exception error) => Add(new JsonObject
    {
        ["method"] = "tools/list",
        ["error"] = error.Message,
    });

    public void ToolCalled(string tool, JsonElement arguments, CallToolResult result, string? frame = null)
    {
        var entry = Call(tool, arguments);
        entry["result"] = Record(result);
        if (frame is not null)
        {
            entry["frame"] = frame;
        }

        Add(entry);
    }

    public void ToolCallFailed(string tool, JsonElement arguments, Exception error)
    {
        var entry = Call(tool, arguments);
        entry["error"] = error.Message;
        Add(entry);
    }

    public void Finish(string? failure)
    {
        lock (_gate)
        {
            _root["outcome"] = failure is null ? "passed" : "failed";
            if (failure is not null)
            {
                _root["failure"] = failure;
            }

            Save();
        }
    }

    private static JsonObject Call(string tool, JsonElement arguments) => new()
    {
        ["method"] = "tools/call",
        ["tool"] = tool,
        ["arguments"] = JsonSerializer.SerializeToNode(arguments),
    };

    private static JsonNode? Record(CallToolResult result)
    {
        var node = JsonSerializer.SerializeToNode(result, McpJsonUtilities.DefaultOptions);
        if (node?["content"] is JsonArray blocks)
        {
            foreach (var (written, block) in blocks.Zip(result.Content))
            {
                if (written is JsonObject image && block is ImageContentBlock { DecodedData: var bytes })
                {
                    image.Remove("data");
                    image["bytes"] = bytes.Length;
                    image["sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes.Span));
                }
            }
        }

        return node;
    }

    private void Add(JsonObject entry)
    {
        lock (_gate)
        {
            _calls.Add(entry);
            Save();
        }
    }

    private void Save() => File.WriteAllText(_path, _root.ToJsonString(Written));
}
