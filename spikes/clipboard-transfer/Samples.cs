using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

// Fixed synthetic fixtures, not ScreenIt session/capture code.
internal sealed class Samples
{
    public const string Comments = "Screenshot A\r\nA1 — тестовый комментарий\r\nA2 — русский\r\n     многострочный комментарий\r\n\r\nScreenshot B\r\nB1 — второй комментарий\r\n\r\nScreenshot C\r\nC1 — третий комментарий";
    public static string Root => Path.Combine(Path.GetTempPath(), "ScreenItClipboardSpike");
    public readonly string DirectoryPath;
    public readonly string[] Files;
    public readonly byte[][] Pngs;
    private readonly byte[] dib;
    public static readonly (string Label, int Width, int Height)[] Sizes = [("A", 1024, 640), ("B", 800, 600), ("C", 640, 960)];
    public static readonly string[] Names = [
        "Single PNG", "Single DIBV5", "PNG + DIBV5", "PNG + Unicode text", "PNG + DIBV5 + Unicode text",
        "Multiple files (CF_HDROP)", "Multiple files + Unicode text", "HTML embedded data URI",
        "HTML local file references", "Competing: files first", "Competing: image first", "Unicode text only (control)"
    ];

    public Samples()
    {
        string id = Guid.NewGuid().ToString("N");
        DirectoryPath = Path.Combine(Root, id);
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(Path.Combine(DirectoryPath, ".spike-owner"), "ScreenItClipboardSpike-v1\n" + id);
        Files = Sizes.Select(s => Path.Combine(DirectoryPath, s.Label + ".png")).ToArray();
        Pngs = new byte[3][];
        for (int i = 0; i < Sizes.Length; i++)
        {
            using Bitmap bitmap = Draw(i);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            Pngs[i] = stream.ToArray();
            File.WriteAllBytes(Files[i], Pngs[i]);
            if (i == 0) dib = MakeDib(bitmap);
        }
        if (dib is null) throw new InvalidOperationException("Fixture A missing");
    }

    private static Bitmap Draw(int index)
    {
        var size = Sizes[index];
        var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        bitmap.SetResolution(96, 96);
        using var g = Graphics.FromImage(bitmap);
        g.PageUnit = GraphicsUnit.Pixel;
        g.Clear(Color.White);
        Color accent = new[] { Color.FromArgb(205, 45, 55), Color.FromArgb(30, 145, 70), Color.FromArgb(40, 85, 210) }[index];
        using var color = new SolidBrush(accent);
        g.FillRectangle(color, 0, 0, size.Width, 110);
        using var title = new Font("Segoe UI", 38, FontStyle.Bold, GraphicsUnit.Pixel);
        using var normal = new Font("Segoe UI", 23, FontStyle.Regular, GraphicsUnit.Pixel);
        using var small = new Font("Consolas", 15, FontStyle.Regular, GraphicsUnit.Pixel);
        g.DrawString($"Screenshot {size.Label}  |  order {index + 1}/3", title, Brushes.White, 22, 12);
        g.DrawString($"{size.Width} x {size.Height} px | 96 DPI | synthetic fixture", normal, Brushes.White, 25, 67);
        using var grid = new Pen(Color.FromArgb(215, 215, 215));
        for (int x = 0; x < size.Width; x += 32) g.DrawLine(grid, x, 120, x, size.Height - 1);
        for (int y = 128; y < size.Height; y += 32) g.DrawLine(grid, 0, y, size.Width - 1, y);
        g.DrawString("32 px grid / 1 px checkerboard / corner anchors", small, Brushes.Black, 20, 128);
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
                g.FillRectangle(((x + y) & 1) == 0 ? Brushes.Black : Brushes.White, 24 + x, 330 + y, 1, 1);
        g.DrawString("1 pixel alternating pattern", small, Brushes.Black, 20, 465);
        g.DrawString("ABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789", small, Brushes.Black, 20, 490);
        g.DrawString("Comments are NOT rasterized into this image", small, Brushes.Black, 20, 520);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int n = 1; n <= (index == 0 ? 2 : 1); n++)
        {
            int x = 200 + n * 120, y = 205 + n * 45;
            g.FillEllipse(color, x - 25, y - 25, 60, 60);
            g.DrawString(size.Label + n, normal, Brushes.White, x - 20, y - 16);
        }
        g.SmoothingMode = SmoothingMode.None;
        using var edge = new Pen(accent, 2);
        g.DrawRectangle(edge, 1, 1, size.Width - 3, size.Height - 3);
        foreach (var p in new[] { new Point(0, 0), new Point(size.Width - 8, 0), new Point(0, size.Height - 8), new Point(size.Width - 8, size.Height - 8) })
            g.FillRectangle(Brushes.Black, p.X, p.Y, 8, 8);
        return bitmap;
    }

    private static byte[] MakeDib(Bitmap bitmap)
    {
        int byteCount = checked(bitmap.Width * bitmap.Height * 4);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(124u); writer.Write(bitmap.Width); writer.Write(-bitmap.Height);
        writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(3u); // BI_BITFIELDS
        writer.Write(byteCount); writer.Write(3780); writer.Write(3780); writer.Write(0u); writer.Write(0u);
        writer.Write(0x00ff0000u); writer.Write(0x0000ff00u); writer.Write(0x000000ffu); writer.Write(0xff000000u);
        writer.Write(0x73524742u); // LCS_sRGB
        writer.Write(new byte[36]); writer.Write(0u); writer.Write(0u); writer.Write(0u);
        writer.Write(4u); writer.Write(0u); writer.Write(0u); writer.Write(0u); // intent/images, no profile
        if (stream.Position != 124) throw new InvalidDataException("DIBV5 header length");
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = new byte[bitmap.Width * 4];
            for (int y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                writer.Write(row);
            }
        }
        finally { bitmap.UnlockBits(data); }
        return stream.ToArray();
    }

    public List<NativeClipboard.Part> Build(int variant, bool privacy)
    {
        var png = NativeClipboard.Registered("PNG", Pngs[0]);
        var image = new NativeClipboard.Part(NativeClipboard.DibV5, "CF_DIBV5", dib);
        var text = new NativeClipboard.Part(NativeClipboard.Text, "CF_UNICODETEXT", Encoding.Unicode.GetBytes(Comments + "\0"));
        var files = new NativeClipboard.Part(NativeClipboard.Drop, "CF_HDROP", DropBytes());
        List<NativeClipboard.Part> parts = variant switch
        {
            1 => [png], 2 => [image], 3 => [png, image], 4 => [png, text], 5 => [png, image, text],
            6 => [files], 7 => [files, text], 8 => [NativeClipboard.Registered("HTML Format", Html(false))],
            9 => [NativeClipboard.Registered("HTML Format", Html(true))],
            10 => [files, text, png, image], 11 => [png, image, files, text], 12 => [text],
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        };
        if (privacy)
        {
            parts.Add(NativeClipboard.Registered("CanUploadToCloudClipboard", new byte[4]));
            parts.Add(NativeClipboard.Registered("CanIncludeInClipboardHistory", new byte[4]));
        }
        return parts;
    }

    private byte[] DropBytes()
    {
        byte[] list = Encoding.Unicode.GetBytes(string.Join('\0', Files) + "\0\0");
        byte[] result = new byte[20 + list.Length];
        BitConverter.GetBytes(20u).CopyTo(result, 0); // pFiles, not a pointer
        BitConverter.GetBytes(1).CopyTo(result, 16); // fWide = TRUE
        list.CopyTo(result, 20);
        return result;
    }
    public static string[] DecodeDrop(byte[] bytes) => Encoding.Unicode.GetString(bytes, 20, bytes.Length - 20)
        .Split('\0', StringSplitOptions.RemoveEmptyEntries);

    private byte[] Html(bool localFiles)
    {
        var fragment = new StringBuilder("<div>");
        for (int i = 0; i < 3; i++)
        {
            string src = localFiles ? new Uri(Files[i]).AbsoluteUri : "data:image/png;base64," + Convert.ToBase64String(Pngs[i]);
            var s = Sizes[i];
            fragment.Append($"<h2>Screenshot {s.Label}</h2><img alt=\"Screenshot {s.Label}\" width=\"{s.Width}\" height=\"{s.Height}\" src=\"{WebUtility.HtmlEncode(src)}\">");
        }
        fragment.Append("<pre>").Append(WebUtility.HtmlEncode(Comments)).Append("</pre></div>");
        const string before = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Русский тест — Clipboard</title></head><body><!--StartFragment-->";
        const string after = "<!--EndFragment--></body></html>";
        string body = before + fragment + after;
        static string Header(int startHtml, int endHtml, int startFragment, int endFragment) =>
            FormattableString.Invariant($"Version:1.0\r\nStartHTML:{startHtml:D10}\r\nEndHTML:{endHtml:D10}\r\nStartFragment:{startFragment:D10}\r\nEndFragment:{endFragment:D10}\r\n");
        int start = Encoding.UTF8.GetByteCount(Header(0, 0, 0, 0));
        int startFragment = start + Encoding.UTF8.GetByteCount(before);
        int endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment.ToString());
        int end = start + Encoding.UTF8.GetByteCount(body);
        return Encoding.UTF8.GetBytes(Header(start, end, startFragment, endFragment) + body + "\0");
    }

    public void Validate(List<NativeClipboard.Part> parts, bool privacy)
    {
        for (int i = 0; i < Files.Length; i++)
        {
            if (!File.ReadAllBytes(Files[i]).AsSpan().SequenceEqual(Pngs[i])) throw new InvalidDataException("Fixture file changed");
            using var stream = new MemoryStream(Pngs[i]);
            using var b = new Bitmap(stream);
            if (b.Width != Sizes[i].Width || b.Height != Sizes[i].Height) throw new InvalidDataException("PNG dimensions");
            if (b.GetPixel(0, 0).ToArgb() != Color.Black.ToArgb()) throw new InvalidDataException("Corner pixel");
            if (b.GetPixel(24, 330).ToArgb() != Color.Black.ToArgb() || b.GetPixel(25, 330).ToArgb() != Color.White.ToArgb())
                throw new InvalidDataException("1px checkerboard");
        }
        foreach (var part in parts)
        {
            if (part.Id == NativeClipboard.Drop)
            {
                if (BitConverter.ToUInt32(part.Bytes, 0) != 20 || BitConverter.ToInt32(part.Bytes, 16) != 1 || !part.Bytes.AsSpan()[^4..].SequenceEqual(new byte[4]))
                    throw new InvalidDataException("DROPFILES header/termination");
                if (!DecodeDrop(part.Bytes).SequenceEqual(Files)) throw new InvalidDataException("Unexpected external path/order");
            }
            if (part.Name == "HTML Format") ValidateHtml(part.Bytes);
            if (part.Id == NativeClipboard.DibV5) ValidateDib(part.Bytes);
        }
        if (parts.Count(p => p.Name is "CanUploadToCloudClipboard" or "CanIncludeInClipboardHistory") != (privacy ? 2 : 0))
            throw new InvalidDataException("Privacy flag presence");
    }

    private void ValidateDib(byte[] bytes)
    {
        if (bytes.Length != 124 + 1024 * 640 * 4 || BitConverter.ToUInt32(bytes, 0) != 124 || BitConverter.ToInt32(bytes, 4) != 1024 ||
            BitConverter.ToInt32(bytes, 8) != -640 || BitConverter.ToUInt16(bytes, 12) != 1 || BitConverter.ToUInt16(bytes, 14) != 32 ||
            BitConverter.ToUInt32(bytes, 16) != 3 || BitConverter.ToUInt32(bytes, 20) != 1024 * 640 * 4 ||
            BitConverter.ToUInt32(bytes, 40) != 0x00ff0000 || BitConverter.ToUInt32(bytes, 44) != 0x0000ff00 ||
            BitConverter.ToUInt32(bytes, 48) != 0x000000ff || BitConverter.ToUInt32(bytes, 52) != 0xff000000 ||
            BitConverter.ToUInt32(bytes, 56) != 0x73524742 || BitConverter.ToUInt32(bytes, 108) != 4 ||
            BitConverter.ToUInt32(bytes, 112) != 0 || BitConverter.ToUInt32(bytes, 116) != 0)
            throw new InvalidDataException("DIBV5 dimensions/header");
        using var stream = new MemoryStream(Pngs[0]);
        using var bitmap = new Bitmap(stream);
        for (int y = 0; y < bitmap.Height; y += 37)
            for (int x = 0; x < bitmap.Width; x += 31)
                if (BitConverter.ToInt32(bytes, 124 + (y * bitmap.Width + x) * 4) != bitmap.GetPixel(x, y).ToArgb())
                    throw new InvalidDataException("DIBV5 pixel/orientation mismatch");
    }
    private void ValidateHtml(byte[] bytes)
    {
        string all = Encoding.UTF8.GetString(bytes);
        int Offset(string key)
        {
            string value = Regex.Match(all, $@"{key}:(\d+)").Groups[1].Value;
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int n)
                ? n : throw new InvalidDataException("Invalid HTML offset: " + key);
        }
        int start = Offset("StartHTML"), end = Offset("EndHTML"), fs = Offset("StartFragment"), fe = Offset("EndFragment");
        if (!(0 < start && start < fs && fs < fe && fe < end && end == bytes.Length - 1)) throw new InvalidDataException("HTML offsets");
        string context = Encoding.UTF8.GetString(bytes, start, end - start);
        string fragment = Encoding.UTF8.GetString(bytes, fs, fe - fs);
        if (!context.StartsWith("<!DOCTYPE html>") || !fragment.StartsWith("<div>") || !fragment.EndsWith("</div>")) throw new InvalidDataException("HTML byte boundaries");
        if (!WebUtility.HtmlDecode(fragment).Contains(Comments, StringComparison.Ordinal)) throw new InvalidDataException("HTML comments missing");
        MatchCollection sources = Regex.Matches(fragment, "src=\"([^\"]+)\"");
        if (sources.Count != 3) throw new InvalidDataException("HTML image count");
        for (int i = 0; i < 3; i++)
        {
            string src = WebUtility.HtmlDecode(sources[i].Groups[1].Value);
            if (src.StartsWith("data:image/png;base64,", StringComparison.Ordinal))
            {
                if (!Convert.FromBase64String(src[22..]).AsSpan().SequenceEqual(Pngs[i])) throw new InvalidDataException("HTML PNG mismatch/order");
            }
            else if (src != new Uri(Files[i]).AbsoluteUri) throw new InvalidDataException("HTML unexpected external path/URL");
        }
    }

    public void Describe()
    {
        Console.WriteLine($"Temporary run directory: {DirectoryPath}");
        for (int i = 0; i < 3; i++) Console.WriteLine($"{i + 1}. {Files[i]} — {Sizes[i].Width}x{Sizes[i].Height}, {Pngs[i].Length} PNG bytes");
    }

    public static void Cleanup(string directory)
    {
        // Delete ONLY a validated immediate child, known files, no recursion/reparse points.
        string path = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        string root = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(Path.GetDirectoryName(path), root, StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(Path.GetFileName(path), "N", out _))
            throw new InvalidOperationException("Not a spike-owned run directory");
        foreach (string p in new[] { root, path })
            if ((File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Refusing reparse point");
        string[] allowed = ["A.png", "B.png", "C.png", ".spike-owner"];
        if (File.ReadAllText(Path.Combine(path, ".spike-owner")) != "ScreenItClipboardSpike-v1\n" + Path.GetFileName(path))
            throw new InvalidOperationException("Owner marker mismatch");
        foreach (string p in Directory.EnumerateFileSystemEntries(path))
            if (!allowed.Contains(Path.GetFileName(p), StringComparer.Ordinal) || (File.GetAttributes(p) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw new InvalidOperationException("Unexpected file/directory; cleanup refused");
        foreach (string name in allowed) File.Delete(Path.Combine(path, name));
        Directory.Delete(path, recursive: false);
        Console.WriteLine("Deleted only this run's known synthetic files: " + path);
    }
}
