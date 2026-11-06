using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Carve.ModelViewer;

/// <summary>One view: a file name and the direction from the model's centre toward the eye.</summary>
internal sealed record ViewSpec(string Name, Vector3 Direction);

/// <summary>The viewer's own command line. Anything under <c>--Empyrean:</c> belongs to the engine.</summary>
internal sealed partial record ViewerOptions(string Content, string Model, string Out, IReadOnlyList<ViewSpec> Views)
{
    public const string Usage =
        "usage: Carve.ModelViewer --content <folder> --model <content path> --out <folder> [--view <name>=<x,y,z>]...";

    private static readonly ViewSpec DefaultView = new("front", new Vector3(1.6f, 1.2f, 2.2f));

    /// <summary>Within this cone of vertical, +Y cannot be up (the agreed look says it is).</summary>
    private static readonly float VerticalCosine = MathF.Cos(MathF.PI / 180f);

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex ViewName();

    /// <summary>Splits the arguments into the viewer's options and the engine's.</summary>
    /// <exception cref="ArgumentException">The message is one line for stderr.</exception>
    public static ViewerOptions Parse(string[] args, out string[] engineArgs)
    {
        string? content = null, model = null, output = null;
        var views = new List<ViewSpec>();
        var engine = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--Empyrean:", StringComparison.OrdinalIgnoreCase))
            {
                engine.Add(arg);
                if (!arg.Contains('=') && i + 1 < args.Length)
                {
                    engine.Add(args[++i]);
                }

                continue;
            }

            if (arg is not ("--content" or "--model" or "--out" or "--view"))
            {
                throw new ArgumentException($"unknown argument \"{arg}\"");
            }

            if (i + 1 == args.Length)
            {
                throw new ArgumentException($"{arg} needs a value");
            }

            var value = args[++i];
            switch (arg)
            {
                case "--content": content = value; break;
                case "--model": model = value; break;
                case "--out": output = value; break;
                default: views.Add(ParseView(value, views)); break;
            }
        }

        if (content is null || model is null || output is null)
        {
            throw new ArgumentException("--content, --model and --out are all required");
        }

        engineArgs = engine.ToArray();
        return new ViewerOptions(
            Path.GetFullPath(content), model, Path.GetFullPath(output), views.Count > 0 ? views : [DefaultView]);
    }

    private static ViewSpec ParseView(string text, List<ViewSpec> seen)
    {
        var parts = text.Split('=');
        var numbers = parts.Length == 2 ? parts[1].Split(',') : [];
        var values = new float[3];
        if (numbers.Length != 3 || !ViewName().IsMatch(parts[0]) || !numbers.Select((n, i) =>
                float.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])).All(ok => ok))
        {
            throw new ArgumentException($"--view \"{text}\" is not <name>=<x,y,z>");
        }

        var direction = new Vector3(values[0], values[1], values[2]);
        if (!float.IsFinite(direction.LengthSquared()) || direction == Vector3.Zero)
        {
            throw new ArgumentException($"--view \"{text}\" has no direction");
        }

        if (MathF.Abs(Vector3.Normalize(direction).Y) > VerticalCosine)
        {
            throw new ArgumentException($"--view \"{text}\" looks straight along Y, and +Y is up");
        }

        if (seen.Any(v => v.Name == parts[0]))
        {
            throw new ArgumentException($"--view \"{parts[0]}\" is given twice");
        }

        return new ViewSpec(parts[0], direction);
    }
}
