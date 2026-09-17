using MusicPlay.Web.Providers;
using Xunit;

namespace MusicPlay.Tests;

public class SpotifyProviderTests
{
    [Theory]
    [InlineData("https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M", "37i9dQZF1DXcBWIGoYBM5M", "playlist")]
    [InlineData("https://open.spotify.com/intl-pt/playlist/37i9dQZF1DXcBWIGoYBM5M?si=e2f5b84920b74da1", "37i9dQZF1DXcBWIGoYBM5M", "playlist")]
    [InlineData("https://open.spotify.com/intl-es/album/4aawyAB9vmqN3uQ7FjRGTy?si=abc", "4aawyAB9vmqN3uQ7FjRGTy", "album")]
    [InlineData("spotify:playlist:37i9dQZF1DXcBWIGoYBM5M", "37i9dQZF1DXcBWIGoYBM5M", "playlist")]
    [InlineData("spotify:album:4aawyAB9vmqN3uQ7FjRGTy", "4aawyAB9vmqN3uQ7FjRGTy", "album")]
    [InlineData("37i9dQZF1DXcBWIGoYBM5M", "37i9dQZF1DXcBWIGoYBM5M", "playlist")]
    public void ExtractEntityId_ShouldHandleVariousFormats(string input, string expectedId, string expectedType)
    {
        var (id, type) = SpotifyProvider.ExtractEntityId(input);
        Assert.Equal(expectedId, id);
        Assert.Equal(expectedType, type);
    }
}
