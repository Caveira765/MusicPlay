using MusicPlay.Web.Models;
using MusicPlay.Web.Providers;
using Xunit;

namespace MusicPlay.Tests;

public class FileProviderTests
{
    [Fact]
    public void ExportAndParseCsv_PreservesTracksAndMetadata()
    {
        var original = new PlaylistModel
        {
            Name = "Minha Playlist de Teste",
            Tracks = new List<TrackModel>
            {
                new() { Title = "Song One, With Comma", Artist = "Artist \"Quoted\"", Album = "Album A", DurationMs = 210000, Isrc = "US1234567890" },
                new() { Title = "Song Two", Artist = "Artist B", Album = "Album B", DurationMs = 180000, Isrc = "GB9876543210" }
            }
        };

        var csv = FileProvider.ExportToCsv(original);
        var parsed = FileProvider.ParseCsv(csv, "Importada");

        Assert.Equal(2, parsed.Tracks.Count);
        Assert.Equal("Song One, With Comma", parsed.Tracks[0].Title);
        Assert.Equal("Artist \"Quoted\"", parsed.Tracks[0].Artist);
        Assert.Equal("US1234567890", parsed.Tracks[0].Isrc);
        Assert.Equal(210000, parsed.Tracks[0].DurationMs);
    }

    [Fact]
    public void ExportAndParseM3u_PreservesTrackInfo()
    {
        var original = new PlaylistModel
        {
            Name = "Rock Classics",
            Tracks = new List<TrackModel>
            {
                new() { Title = "Bohemian Rhapsody", Artist = "Queen", DurationMs = 354000, Isrc = "GBUM71029604" }
            }
        };

        var m3u = FileProvider.ExportToM3u(original);
        var parsed = FileProvider.ParseM3u(m3u);

        Assert.Single(parsed.Tracks);
        Assert.Equal("Bohemian Rhapsody", parsed.Tracks[0].Title);
        Assert.Equal("Queen", parsed.Tracks[0].Artist);
        Assert.Equal("GBUM71029604", parsed.Tracks[0].Isrc);
        Assert.Equal(354000, parsed.Tracks[0].DurationMs);
    }
}
