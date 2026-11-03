namespace SupaGPO.Core.Visual;

/// <summary>
/// Penyimpanan template reel biner sederhana: magic "SGRT" + version + w/h + pixel
/// grayscale. File hidup di %APPDATA%\SupaGPO\ (git-ignored). Corrupt/missing → null.
/// </summary>
public static class ReelTemplateStore
{
    private static readonly byte[] Magic = { (byte)'S', (byte)'G', (byte)'R', (byte)'T' };
    private const byte Version = 1;

    public static bool Save(string path, VisualReelDetector detector)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            fs.Write(Magic, 0, 4);
            fs.WriteByte(Version);
            WriteInt32(fs, detector.Width);
            WriteInt32(fs, detector.Height);
            fs.Write(detector.Template, 0, detector.Template.Length);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static VisualReelDetector? TryLoad(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 13) return null;
            for (int i = 0; i < 4; i++)
                if (bytes[i] != Magic[i]) return null;
            if (bytes[4] != Version) return null;
            int w = BitConverter.ToInt32(bytes, 5);
            int h = BitConverter.ToInt32(bytes, 9);
            if (w <= 0 || h <= 0 || w > 1024 || h > 1024) return null;
            var template = new byte[w * h];
            if (bytes.Length - 13 != template.Length) return null;
            Array.Copy(bytes, 13, template, 0, template.Length);
            return new VisualReelDetector(w, h, template);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteInt32(Stream s, int v)
    {
        s.Write(BitConverter.GetBytes(v), 0, 4);
    }
}
