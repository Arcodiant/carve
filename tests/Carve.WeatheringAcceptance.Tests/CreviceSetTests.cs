using Carve.Core;
using Xunit;

namespace Carve.WeatheringAcceptance.Tests;

/// <summary>The crevice set's own fact, which needs no round folder (only samples/wrench.carve), so a unit suite (weathering-unit) runs it.</summary>
[Trait("Tier", "unit")]
public class CreviceSetTests
{
    /// <summary>The crevice set holds texels of the drive's four side faces and of no other face (the socket's 99 and 118 and the drive's ends 104 and 105 included), on the wrench.</summary>
    [Fact]
    public void The_crevice_set_holds_no_texel_of_a_face_that_is_not_a_drive_side()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "samples", "wrench.carve"))) dir = dir.Parent!;
        var wrench = CarveFile.Read(File.ReadAllText(Path.Combine(dir.FullName, "samples", "wrench.carve"))).Mesh!;
        Assert.Equal([100, 101, 102, 103], WeatheringAcceptanceTests.DriveSides(wrench).Order());
        var set = WeatheringAcceptanceTests.CreviceTexels(wrench, 256, new byte[256 * 256 * 4]);
        Assert.NotEmpty(set);
        Assert.Equal([100, 101, 102, 103], set.Select(t => t.Face).Distinct().Order());
    }
}
