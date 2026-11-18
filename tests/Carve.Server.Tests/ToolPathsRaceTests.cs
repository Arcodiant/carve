extern alias carve;

using ToolPaths = carve::Carve.Server.ToolPaths;

namespace Carve.Server.Tests;

/// <summary>RV-24 B1: without overwrite, writes racing to one new path leave exactly one winner, whose bytes are the file's.</summary>
[Trait("Tier", "unit")]
public sealed class ToolPathsRaceTests() : TempFolderTests("carve-toolpaths-race")
{
    [Fact]
    public async Task Racing_writes_without_overwrite_have_one_winner_and_the_rest_are_refused()
    {
        const int Rounds = 40, Writers = 8;
        for (var round = 0; round < Rounds; round++)
        {
            var path = ToolPaths.Resolve("export", $"race{round}.glb", ".glb", out var resolveError, Root);
            Assert.True(path is not null, resolveError);
            using var start = new ManualResetEventSlim();
            var writes = Enumerable.Range(0, Writers).Select(k => Task.Run(() =>
            {
                start.Wait();
                var bytes = Enumerable.Repeat((byte)(k + 1), 4096).ToArray();
                var ok = ToolPaths.Write("export", path!, bytes, out var replaced, out var error, overwrite: false);
                return (Writer: k, Ok: ok, Replaced: replaced, Error: error);
            })).ToArray();
            start.Set();
            var results = await Task.WhenAll(writes);

            var won = results.Where(r => r.Ok).ToArray();
            Assert.True(won.Length == 1, $"round {round}: {won.Length} writers reported success without overwrite");
            Assert.False(won[0].Replaced);
            foreach (var lost in results.Where(r => !r.Ok))
                Assert.Equal($"export: {path!.FullPath} already exists; pass overwrite: true to replace it. Nothing was written.", lost.Error);
            Assert.All(File.ReadAllBytes(path!.FullPath), b => Assert.Equal((byte)(won[0].Writer + 1), b));
        }
        Assert.Equal(Rounds, Directory.GetFiles(Root).Length);
    }
}
