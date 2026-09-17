using MusicPlay.Web.Models;

namespace MusicPlay.Web.Providers;

public interface IMusicProvider
{
    string Id { get; }
    string Name { get; }
    string Icon { get; }
    string AccentColor { get; }
    string Description { get; }
    bool CanImport { get; }
    bool CanExport { get; }
    bool RequiresAuth { get; }

    Task<List<PlaylistModel>> GetUserPlaylistsAsync(string? authToken = null, CancellationToken ct = default);
    Task<PlaylistModel?> GetPlaylistAsync(string playlistIdOrUrl, string? authToken = null, CancellationToken ct = default);
    Task<List<TrackModel>> SearchTracksAsync(string query, int limit = 5, string? authToken = null, CancellationToken ct = default);
    Task<PlaylistModel> CreatePlaylistAsync(string name, string? description, List<TrackModel> tracks, string? authToken = null, CancellationToken ct = default);
}
