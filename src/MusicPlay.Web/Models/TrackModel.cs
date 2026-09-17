namespace MusicPlay.Web.Models;

public class TrackModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? Album { get; set; }
    public int DurationMs { get; set; }
    public string? Isrc { get; set; }
    public string? Uri { get; set; }
    public string? ExternalUrl { get; set; }
    public string? ArtworkUrl { get; set; }

    public string FormattedDuration
    {
        get
        {
            var ts = TimeSpan.FromMilliseconds(DurationMs);
            return ts.Hours > 0 ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}" : $"{ts.Minutes}:{ts.Seconds:D2}";
        }
    }
}
