namespace Carve.Server.Tests;

[Trait("Tier", "unit")]
public class FrameLogTests
{
    [Fact]
    public void OneFrameKeptFromManyThreadsAtOnceIsWrittenWithoutACollision()
    {
        var folder = Directory.CreateTempSubdirectory("carve-framelog-").FullName;
        try
        {
            var file = Path.Combine(folder, "server-regression-frames.tsv");
            var png = new byte[4 << 20];
            new Random(1).NextBytes(png);
            const int threads = 8, rounds = 8;
            for (var round = 0; round < rounds; round++)
            {
                var hash = "frame" + round;
                using var start = new Barrier(threads);
                var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
                var workers = Enumerable.Range(0, threads).Select(_ => new Thread(() =>
                {
                    start.SignalAndWait();
                    try { FrameLog.Keep(file, hash, png); }
                    catch (Exception e) { errors.Add(e); }
                })).ToList();
                workers.ForEach(t => t.Start());
                workers.ForEach(t => t.Join());
                Assert.Empty(errors);
            }
            Assert.Equal(rounds, Directory.GetFiles(Path.Combine(folder, "server-regression-frames")).Length);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
