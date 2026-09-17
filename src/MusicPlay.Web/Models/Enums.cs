namespace MusicPlay.Web.Models;

public enum TrackMatchType
{
    ExactIsrc,
    HighFuzzy,
    MediumFuzzy,
    LowConfidence,
    NotFound
}

public enum TransferStatus
{
    Pending,
    Processing,
    Paused,
    Completed,
    Failed,
    Cancelled
}
