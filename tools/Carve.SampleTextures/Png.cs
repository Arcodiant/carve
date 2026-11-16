using StbImageWriteSharp;

namespace Carve.SampleTextures;

public static class Png
{
    public static byte[] Encode(byte[] rgba, int width, int height)
    {
        using var stream = new MemoryStream();
        new ImageWriter().WritePng(rgba, width, height, ColorComponents.RedGreenBlueAlpha, stream);
        return stream.ToArray();
    }
}

public static class RepoRoot
{
    /// <summary>The folder holding Carve.slnx, found upward from the current folder and then from the program's.</summary>
    public static string Find()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Carve.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("Carve.slnx not found above the current folder");
    }
}
