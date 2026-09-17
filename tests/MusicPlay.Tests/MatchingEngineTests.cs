using MusicPlay.Web.Models;
using MusicPlay.Web.Services;
using Xunit;

namespace MusicPlay.Tests;

public class MatchingEngineTests
{
    private readonly MatchingEngine _engine = new();

    [Fact]
    public void EvaluateMatch_WithSameIsrc_ReturnsExactMatch()
    {
        var source = new TrackModel
        {
            Title = "Bohemian Rhapsody",
            Artist = "Queen",
            Isrc = "GBUM71029604",
            DurationMs = 354000
        };

        var candidate = new TrackModel
        {
            Title = "Bohemian Rhapsody - Remastered 2011",
            Artist = "Queen (Queen Productions Ltd)",
            Isrc = "GBUM71029604",
            DurationMs = 354000
        };

        var result = _engine.EvaluateMatch(source, candidate);

        Assert.Equal(TrackMatchType.ExactIsrc, result.MatchType);
        Assert.Equal(1.0, result.ConfidenceScore);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EvaluateMatch_RemovesRemasteredAndNoise_HighFuzzyMatch()
    {
        var source = new TrackModel
        {
            Title = "Hotel California",
            Artist = "Eagles",
            DurationMs = 391000
        };

        var candidate = new TrackModel
        {
            Title = "Hotel California (2013 Remaster)",
            Artist = "Eagles",
            DurationMs = 391000
        };

        var result = _engine.EvaluateMatch(source, candidate);

        Assert.True(result.ConfidenceScore >= 0.85, $"Score was {result.ConfidenceScore}");
        Assert.Equal(TrackMatchType.HighFuzzy, result.MatchType);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EvaluateMatch_WithSignificantDurationMismatch_PenalizesScore()
    {
        var source = new TrackModel
        {
            Title = "Comfortably Numb",
            Artist = "Pink Floyd",
            DurationMs = 382000 // ~6m22s (versão de estúdio)
        };

        var candidate = new TrackModel
        {
            Title = "Comfortably Numb",
            Artist = "Pink Floyd",
            DurationMs = 580000 // ~9m40s (versão Pulse / ao vivo)
        };

        var result = _engine.EvaluateMatch(source, candidate);

        Assert.True(result.ConfidenceScore < 0.80, $"Expected penalty for live/extended mismatch, got {result.ConfidenceScore}");
    }

    [Fact]
    public void CleanText_NormalizesAccentsAndNoise()
    {
        var cleaned = _engine.CleanText("Água de Beber (feat. Astrud Gilberto) [Remastered]");
        Assert.Equal("agua de beber", cleaned);
    }

    [Theory]
    [InlineData("Queen - Bohemian Rhapsody (Official Video Remastered)", "Queen Official", "Bohemian Rhapsody", "Queen")]
    [InlineData("Coldplay - Yellow (Official Video)", "Coldplay", "Yellow", "Coldplay")]
    [InlineData("Linkin Park - In The End (Official HD Music Video)", "Warner Records", "In The End", "Linkin Park")]
    [InlineData("Blinding Lights", "The Weeknd - Topic", "Blinding Lights", "The Weeknd")]
    [InlineData("Eminem - Without Me (Official Music Video)", "EminemVEVO", "Without Me", "Eminem")]
    public void ParseYouTubeTitle_ExtractsCleanArtistAndTitle(string rawTitle, string rawChannel, string expectedTitle, string expectedArtist)
    {
        var (title, artist) = MusicPlay.Web.Providers.YouTubeProvider.ParseYouTubeTitle(rawTitle, rawChannel);
        Assert.Equal(expectedTitle, title);
        Assert.Equal(expectedArtist, artist);
    }
}
