namespace MusicPlay.Web.Models;

public class PlaylistModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Owner { get; set; }
    public int TrackCount => Tracks.Count > 0 ? Tracks.Count : DeclaredTrackCount;
    public int DeclaredTrackCount { get; set; }
    public string? CoverImageUrl { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string? ExternalUrl { get; set; }
    public List<TrackModel> Tracks { get; set; } = new();
}
