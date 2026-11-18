using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Carve.ModelViewer;

/// <summary>One view: a file name and the direction from the model's centre toward the eye.</summary>
internal sealed record ViewSpec(string Name, Vector3 Direction);

/// <summary>The viewer's own command line. Anything under <c>--Empyrean:</c> belongs to the engine.</summary>
internal sealed partial record ViewerOptions(string Content, string Model, string Out, IReadOnlyList<ViewSpec> Views, bool Sky = false, IReadOnlyList<KeyValuePair<string, float>>? Targets = null)
{
    public const string Usage =
        "usage: Carve.ModelViewer --content <folder> --model <content path> --out <folder> [--view <name>=<x,y,z>]... [--sky] [--targets <name>=<0..1>,...]";

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
        var sky = false;
        IReadOnlyList<KeyValuePair<string, float>>? targets = null;

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

            if (arg == "--sky")
            {
                // The one switch with no value: Carve's fixed sky, lighting the model as render does.
                sky = true;
                continue;
            }

            if (arg is not ("--content" or "--model" or "--out" or "--view" or "--targets"))
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
                case "--targets": targets = ParseTargets(value); break;
                default: views.Add(ParseView(value, views)); break;
            }
        }

        if (content is null || model is null || output is null)
        {
            throw new ArgumentException("--content, --model and --out are all required");
        }

        engineArgs = engine.ToArray();
        return new ViewerOptions(
            Path.GetFullPath(content), model, Path.GetFullPath(output), views.Count > 0 ? views : [DefaultView], sky, targets);
    }

    /// <summary>The starting morph weights: <c>Bulk=0.5,Lean=0</c>, each from 0 to 1, a name once.</summary>
    private static List<KeyValuePair<string, float>> ParseTargets(string text)
    {
        var weights = new List<KeyValuePair<string, float>>();
        foreach (var part in text.Split(','))
        {
            var pair = part.Split('=');
            if (pair.Length != 2 || pair[0].Length == 0
                || !float.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var weight))
            {
                throw new ArgumentException($"--targets \"{text}\" is not <name>=<weight>,...");
            }

            if (!(weight is >= 0 and <= 1))
            {
                throw new ArgumentException($"--targets: the weight of \"{pair[0]}\" is {pair[1]}, outside 0 to 1");
            }

            if (weights.Any(w => w.Key == pair[0]))
            {
                throw new ArgumentException($"--targets \"{pair[0]}\" is given twice");
            }

            weights.Add(new(pair[0], weight));
        }

        return weights;
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
