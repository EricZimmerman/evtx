using System.IO;
using System.IO.Compression;
using NUnit.Framework;

namespace evtx.Test;

public static class TestFiles
{
    public static string Dir => Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFiles");

    public static string MapsDir => Path.Combine(TestContext.CurrentContext.TestDirectory, "Maps");

    /// <summary>A gzipped sample from omerbenamram/evtx, decompressed</summary>
    public static byte[] RustSample(string name)
    {
        using var gz = new GZipStream(File.OpenRead(Path.Combine(Dir, "rust-evtx", name + ".gz")), CompressionMode.Decompress);
        using var ms = new MemoryStream();
        gz.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>A sample written to dir (for the CLI)</summary>
    public static string RustSampleFile(string name, string dir)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, RustSample(name));
        return path;
    }
}
