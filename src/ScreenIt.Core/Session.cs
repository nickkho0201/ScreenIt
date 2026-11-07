namespace ScreenIt.Core;

public static class ScreenshotLetters
{
    public static string FromOrdinal(int ordinal)
    {
        if (ordinal < 1) throw new ArgumentOutOfRangeException(nameof(ordinal));
        string result = "";
        while (ordinal > 0) { ordinal--; result = (char)('A' + ordinal % 26) + result; ordinal /= 26; }
        return result;
    }
}

public sealed class CommittedScreenshot
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Letter { get; }
    public int Width { get; }
    public int Height { get; }
    public DateTimeOffset Created { get; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<Annotation> Annotations { get; }
    internal CommittedScreenshot(ScreenshotDraft draft)
    {
        Letter = draft.Letter; Width = draft.Width; Height = draft.Height;
        Annotations = Array.AsReadOnly(draft.Items.ToArray());
    }
}

public sealed class Session
{
    private readonly List<CommittedScreenshot> screenshots = [];
    public IReadOnlyList<CommittedScreenshot> Screenshots => screenshots.AsReadOnly();
    public string NextLetter => ScreenshotLetters.FromOrdinal(checked(screenshots.Count + 1));
    public ScreenshotDraft CreateDraft(int width, int height) => new(width, height, NextLetter);
    public CommittedScreenshot Commit(ScreenshotDraft draft)
    {
        if (draft.IsCommitted || draft.Letter != NextLetter) throw new InvalidOperationException("Draft is not the next screenshot.");
        var committed = new CommittedScreenshot(draft);
        screenshots.Add(committed); draft.Seal(); return committed;
    }
    public void Clear() => screenshots.Clear();
}

public static class CommentFormatter
{
    public static string Format(Session session) => Format(session.Screenshots);
    public static string Format(IEnumerable<CommittedScreenshot> screenshots) => string.Join(Environment.NewLine + Environment.NewLine,
        screenshots.Select(s => Format(s.Letter, s.Annotations)).Where(text => text.Length > 0));
    public static string Format(string letter, IEnumerable<Annotation> annotations)
    {
        var markers = annotations.OfType<Marker>().OrderBy(m => m.Number).ToArray();
        if (markers.Length == 0) return "";
        var lines = new List<string> { "Screenshot " + letter };
        foreach (var marker in markers)
        {
            var body = marker.Comment.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            lines.Add(marker.Label + " — " + body[0]);
            lines.AddRange(body.Skip(1).Select(line => "     " + line));
        }
        return string.Join(Environment.NewLine, lines);
    }
}
