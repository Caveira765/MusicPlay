namespace MusicPlay.Web.Models;

public class TransferRequest
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public string SourceProviderId { get; set; } = string.Empty;
    public string DestinationProviderId { get; set; } = string.Empty;
    public string? SourcePlaylistId { get; set; }
    public string DestinationPlaylistName { get; set; } = "Minha Playlist Migrada";
    public string? DestinationPlaylistDescription { get; set; } = "Migrado via MusicPlay (.NET C#)";
    public List<TrackModel> Tracks { get; set; } = new();
    public string? SourceAuthToken { get; set; }
    public string? DestinationAuthToken { get; set; }
}

public class TransferProgressUpdate
{
    public string SessionId { get; set; } = string.Empty;
    public int TotalTracks { get; set; }
    public int ProcessedTracks { get; set; }
    public int MatchedTracks { get; set; }
    public int FailedTracks { get; set; }
    public int Percentage => TotalTracks > 0 ? (int)Math.Round((double)ProcessedTracks / TotalTracks * 100) : 0;
    public TrackModel? CurrentTrack { get; set; }
    public TrackMatchResult? LastMatchResult { get; set; }
    public TransferStatus Status { get; set; }
    public string LogMessage { get; set; } = string.Empty;
    public string? CreatedDestinationPlaylistUrl { get; set; }
    public string? ExportDownloadUrl { get; set; }
}
