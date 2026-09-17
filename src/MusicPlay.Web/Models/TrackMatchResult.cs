namespace MusicPlay.Web.Models;

public class TrackMatchResult
{
    public required TrackModel SourceTrack { get; set; }
    public TrackModel? MatchedTrack { get; set; }
    public double ConfidenceScore { get; set; }
    public TrackMatchType MatchType { get; set; }
    public string? Notes { get; set; }
    public bool IsSuccess => MatchedTrack != null && MatchType != TrackMatchType.NotFound && MatchType != TrackMatchType.LowConfidence;
}
