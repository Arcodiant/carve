using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Debug;
using ModelContextProtocol.Protocol;
using StbImageSharp;

namespace Carve.Scenario.Patina;

/// <summary>
/// RV-76's stage (T-176): a sheet of more than 15 outputs, which threw past the label atlas's 32 rows, now draws, the atlas
/// grown a page. A graph of one dark colour with base_colour and 17 named outputs (18 columns, 38 labels) is drawn as a
/// sheet, and the labels past the first page (the last columns' and the sphere's) are looked for as white text on their
/// backing. No frame is captured: every sheet of 15 outputs or fewer is drawn as before, and the stages' frames hold that.
/// </summary>
internal sealed class WideSheetStage : IPatinaStage
{
    private const int Named = 17;

    // The sheet's layout, as patina's SheetLayout and LabelAtlas give it (the scenario does not reference the server).
    private const int Cell = 256, Gutter = 2, Title = 24, Padding = 2, SphereCell = 512;

    public async Task RunAsync(ScenarioDriver driver, PatinaChecks checks)
    {
        var nodes = new JsonObject
        {
            ["c"] = new JsonObject { ["type"] = "colour", ["params"] = new JsonObject { ["colour"] = "#202020" } },
            ["out"] = new JsonObject { ["type"] = "output", ["inputs"] = new JsonObject { ["in"] = "c" } },
        };
        var names = Enumerable.Range(0, Named).Select(i => "m" + (char)('a' + i)).ToList();
        foreach (var name in names)
            nodes["o_" + name] = new JsonObject { ["type"] = "output", ["params"] = new JsonObject { ["name"] = name }, ["inputs"] = new JsonObject { ["in"] = "c" } };
        var graph = new JsonObject { ["version"] = 1, ["size"] = 64, ["seed"] = 0, ["nodes"] = nodes };
        await checks.CallAsync("define_graph", JsonSerializer.Serialize(new { graph }));

        var result = await driver.CallAsync("swatch", new { view = "sheet" });
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));
        var image = result.Content.OfType<ImageContentBlock>().FirstOrDefault();
        checks.Check(result.IsError is not true && image is not null, "wide sheet: a sheet of 18 outputs was not drawn: " + text);
        if (image is null) return;
        var columns = 1 + Named;
        var decoded = ImageResult.FromMemory(image.DecodedData.ToArray(), ColorComponents.RedGreenBlueAlpha);
        var width = columns * (Cell + Gutter) + SphereCell;
        checks.Check(decoded.Width == width && text.Contains($"; {width} x {Title + 2 * Cell + Gutter} px", StringComparison.Ordinal),
            $"wide sheet: the sheet is {decoded.Width} px wide, not {width}: " + text);
        checks.Check(text.Contains("columns base_colour, " + string.Join(", ", names) + ",", StringComparison.Ordinal), "wide sheet: the caption does not name every column: " + text);

        // Whether a label's band, from (left, top), holds white text: the cells are #202020, so only text is bright.
        bool Lettered(int left, int top)
        {
            for (var y = top; y < top + 18; y++)
                for (var x = left; x < left + 40; x++)
                    if (decoded.Data[(y * decoded.Width + x) * 4] > 200) return true;
            return false;
        }

        // Labels 0 to 31 are the first page; the last columns' (rows 31 to 36) and the sphere's (row 37) are on the second.
        for (var column = 15; column < columns; column++)
        {
            var left = column * (Cell + Gutter) + Padding;
            checks.Check(Lettered(left, Title + Padding), $"wide sheet: column {column}'s flat label is not drawn");
            checks.Check(Lettered(left, Title + Cell + Gutter + Padding), $"wide sheet: column {column}'s tiled label is not drawn");
        }
        checks.Check(Lettered(columns * (Cell + Gutter) + Padding, Title + 1 + Padding), "wide sheet: the sphere's label is not drawn");
    }
}
