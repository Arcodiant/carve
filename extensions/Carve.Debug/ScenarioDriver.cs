using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using StbImageSharp;

namespace Carve.Debug;

/// <summary>
/// What a scenario drives the product with. Every call goes through the SDK's real client, over
/// the swapped transport, to the real server: JSON, schema binding and dispatch. Each is in the
/// run log. Nothing here reaches into the product.
/// </summary>
public sealed partial class ScenarioDriver
{
    private readonly McpClient _client;
    private readonly RunFolder _folder;
    private readonly RunLog _log;
    private readonly CancellationToken _stopping;
    private readonly HashSet<string> _frames = new(StringComparer.OrdinalIgnoreCase);

    internal ScenarioDriver(McpClient client, RunFolder folder, RunLog log, CancellationToken stopping)
    {
        _client = client;
        _folder = folder;
        _log = log;
        _stopping = stopping;
    }

    /// <summary>The run's folder, full path.</summary>
    public string RunFolder => _folder.Path;

    /// <summary>The <c>user-data</c> folder under <see cref="RunFolder"/>, fresh each run.</summary>
    public string UserDataFolder => _folder.UserData;

    /// <summary>The first failure, sticky even if a scenario catches what <see cref="Fail"/> threw.</summary>
    internal string? Failure { get; private set; }

    /// <summary>The server's <c>tools/list</c>.</summary>
    public async Task<IReadOnlyList<Tool>> ListToolsAsync()
    {
        try
        {
            var tools = (await _client.ListToolsAsync(cancellationToken: _stopping)).Select(t => t.ProtocolTool).ToList();
            _log.ToolsListed(tools);
            return tools;
        }
        catch (Exception e)
        {
            _log.ToolsListFailed(e);
            throw;
        }
    }

    /// <summary>
    /// Calls a tool. A tool error is a result, with <see cref="CallToolResult.IsError"/> set, since
    /// a scenario may be proving one; a protocol error, such as an unknown tool, throws.
    /// </summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="arguments">An object whose JSON is the arguments, e.g. <c>new { view = "back" }</c>.</param>
    public async Task<CallToolResult> CallAsync(string tool, object? arguments = null)
    {
        var json = ToJson(arguments);
        var result = await InvokeAsync(tool, json);
        _log.ToolCalled(tool, json, result);
        return result;
    }

    /// <summary>
    /// Calls <c>render</c> and saves the image as <c>frames/&lt;name&gt;.png</c> in the run folder. A
    /// tool error, a result with no image, or a name already used fails the run.
    /// </summary>
    /// <param name="name">Letters, digits, dot, dash and underscore; one frame to a name.</param>
    /// <param name="arguments">The arguments of <c>render</c>.</param>
    public async Task<ScenarioFrame> CaptureAsync(string name, object? arguments = null)
    {
        if (!FrameName().IsMatch(name))
        {
            Fail($"'{name}' is not a frame name: use letters, digits, '.', '-' and '_'.");
        }

        if (!_frames.Add(name))
        {
            Fail($"The frame '{name}' has already been captured in this run.");
        }

        var json = ToJson(arguments);
        var result = await InvokeAsync("render", json);
        var image = result.IsError is true ? null : result.Content.OfType<ImageContentBlock>().FirstOrDefault();
        _log.ToolCalled("render", json, result, image is null ? null : $"frames/{name}.png");

        if (result.IsError is true)
        {
            Fail($"Capturing '{name}': render returned a tool error: {Text(result)}");
        }

        if (image is null)
        {
            Fail($"Capturing '{name}': render returned no image.");
        }

        var png = image.DecodedData.ToArray();
        var path = Path.Combine(_folder.Frames, name + ".png");
        await File.WriteAllBytesAsync(path, png, _stopping);

        var decoded = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
        return new ScenarioFrame(name, path, png, decoded.Width, decoded.Height, decoded.Data, Text(result));
    }

    /// <summary>Fails the run unless the two are the same bytes, saying where they first differ.</summary>
    /// <param name="what">What is being compared, for the failure's message.</param>
    /// <param name="expected">The bytes that should be seen.</param>
    /// <param name="actual">The bytes that were.</param>
    public void AssertBytesEqual(string what, byte[] expected, byte[] actual)
    {
        if (expected.Length != actual.Length)
        {
            Fail($"{what}: {expected.Length} bytes against {actual.Length}.");
        }

        var at = expected.AsSpan().CommonPrefixLength(actual);
        if (at != expected.Length)
        {
            Fail($"{what}: first differ at byte {at}, {expected[at]} against {actual[at]}.");
        }
    }

    /// <summary>Ends the run as failed: exit 1, this message on stderr and in the run log.</summary>
    [DoesNotReturn]
    public void Fail(string message)
    {
        Failure ??= message;
        throw new ScenarioFailedException(message);
    }

    private static JsonElement ToJson(object? arguments) =>
        arguments is null
            ? JsonSerializer.SerializeToElement(new Dictionary<string, object?>())
            : JsonSerializer.SerializeToElement(arguments, arguments.GetType(), JsonSerializerOptions.Web);

    private static string? Text(CallToolResult result) =>
        result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex FrameName();

    private async Task<CallToolResult> InvokeAsync(string tool, JsonElement arguments)
    {
        try
        {
            var dictionary = arguments.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value);
            return await _client.CallToolAsync(tool, dictionary, cancellationToken: _stopping);
        }
        catch (Exception e)
        {
            _log.ToolCallFailed(tool, arguments, e);
            throw;
        }
    }
}
