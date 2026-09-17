using MusicPlay.Web.Models;

namespace MusicPlay.Web.Providers;

public class DemoProvider : IMusicProvider
{
    public string Id => "demo";
    public string Name => "Demo Library";
    public string Icon => "sparkles";
    public string AccentColor => "#8B5CF6"; // Roxo vibrante
    public string Description => "Catálogo demonstrativo para testar transferência imediata sem chaves de API";
    public bool CanImport => true;
    public bool CanExport => true;
    public bool RequiresAuth => false;

    private readonly List<PlaylistModel> _demoPlaylists = new()
    {
        new PlaylistModel
        {
            Id = "demo-top-hits",
            Name = "Top Hits Globais 2026",
            Description = "As músicas mais tocadas no momento ao redor do mundo",
            Owner = "MusicPlay Curadoria",
            ProviderId = "demo",
            CoverImageUrl = "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=400&q=80",
            Tracks = new List<TrackModel>
            {
                new() { Id = "dh1", Title = "Blinding Lights", Artist = "The Weeknd", Album = "After Hours", DurationMs = 200000, Isrc = "USUM72000787", ArtworkUrl = "https://images.unsplash.com/photo-1614613535308-eb5fbd3d2c17?w=200&q=80" },
                new() { Id = "dh2", Title = "As It Was", Artist = "Harry Styles", Album = "Harry's House", DurationMs = 167000, Isrc = "USSM12200612", ArtworkUrl = "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?w=200&q=80" },
                new() { Id = "dh3", Title = "Levitating", Artist = "Dua Lipa", Album = "Future Nostalgia", DurationMs = 203000, Isrc = "GBAHT2000150", ArtworkUrl = "https://images.unsplash.com/photo-1492684223066-81342ee5ff30?w=200&q=80" },
                new() { Id = "dh4", Title = "Stay", Artist = "The Kid LAROI, Justin Bieber", Album = "F*CK LOVE 3", DurationMs = 141000, Isrc = "USSM12103947", ArtworkUrl = "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=200&q=80" },
                new() { Id = "dh5", Title = "Flowers", Artist = "Miley Cyrus", Album = "Endless Summer Vacation", DurationMs = 200000, Isrc = "USSM12209777", ArtworkUrl = "https://images.unsplash.com/photo-1487180144351-b8472da7d491?w=200&q=80" },
                new() { Id = "dh6", Title = "Espresso", Artist = "Sabrina Carpenter", Album = "Short n' Sweet", DurationMs = 175000, Isrc = "USUM72403305", ArtworkUrl = "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=200&q=80" },
                new() { Id = "dh7", Title = "Cruel Summer", Artist = "Taylor Swift", Album = "Lover", DurationMs = 178000, Isrc = "USUG11901472", ArtworkUrl = "https://images.unsplash.com/photo-1459749411175-04bf5292ceea?w=200&q=80" }
            }
        },
        new PlaylistModel
        {
            Id = "demo-rock-legends",
            Name = "Clássicos do Rock",
            Description = "Hinos atemporais que moldaram gerações do rock",
            Owner = "MusicPlay Rock Studio",
            ProviderId = "demo",
            CoverImageUrl = "https://images.unsplash.com/photo-1498038432885-c6f3f1b912ee?w=400&q=80",
            Tracks = new List<TrackModel>
            {
                new() { Id = "rk1", Title = "Bohemian Rhapsody", Artist = "Queen", Album = "A Night at the Opera", DurationMs = 354000, Isrc = "GBUM71029604", ArtworkUrl = "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=200&q=80" },
                new() { Id = "rk2", Title = "Hotel California", Artist = "Eagles", Album = "Hotel California", DurationMs = 391000, Isrc = "USEE10170068", ArtworkUrl = "https://images.unsplash.com/photo-1487180144351-b8472da7d491?w=200&q=80" },
                new() { Id = "rk3", Title = "Sweet Child O' Mine", Artist = "Guns N' Roses", Album = "Appetite for Destruction", DurationMs = 356000, Isrc = "USGF19942509", ArtworkUrl = "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?w=200&q=80" },
                new() { Id = "rk4", Title = "Smells Like Teen Spirit", Artist = "Nirvana", Album = "Nevermind", DurationMs = 301000, Isrc = "USGF19942501", ArtworkUrl = "https://images.unsplash.com/photo-1459749411175-04bf5292ceea?w=200&q=80" },
                new() { Id = "rk5", Title = "Back In Black", Artist = "AC/DC", Album = "Back In Black", DurationMs = 255000, Isrc = "AUAP08000043", ArtworkUrl = "https://images.unsplash.com/photo-1614613535308-eb5fbd3d2c17?w=200&q=80" },
                new() { Id = "rk6", Title = "Comfortably Numb", Artist = "Pink Floyd", Album = "The Wall", DurationMs = 382000, Isrc = "GBARL7900047", ArtworkUrl = "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=200&q=80" }
            }
        },
        new PlaylistModel
        {
            Id = "demo-lofi-beats",
            Name = "Lo-Fi Beats & Foco",
            Description = "Batidas relaxantes e instrumentais para programar e estudar",
            Owner = "MusicPlay Chill",
            ProviderId = "demo",
            CoverImageUrl = "https://images.unsplash.com/photo-1518609878373-06d740f60d8b?w=400&q=80",
            Tracks = new List<TrackModel>
            {
                new() { Id = "lf1", Title = "Morning Coffee", Artist = "Chillhop Music", Album = "Lo-Fi Essentials", DurationMs = 135000, ArtworkUrl = "https://images.unsplash.com/photo-1518609878373-06d740f60d8b?w=200&q=80" },
                new() { Id = "lf2", Title = "Midnight Coding", Artist = "Synthesized Calm", Album = "Deep Focus Vol. 1", DurationMs = 154000, ArtworkUrl = "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=200&q=80" },
                new() { Id = "lf3", Title = "Tokyo Rainy Nights", Artist = "Komorebi Vibes", Album = "Late Hours", DurationMs = 162000, ArtworkUrl = "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?w=200&q=80" },
                new() { Id = "lf4", Title = "Campfire Memories", Artist = "Acoustic Horizon", Album = "Autumn Chill", DurationMs = 145000, ArtworkUrl = "https://images.unsplash.com/photo-1487180144351-b8472da7d491?w=200&q=80" }
            }
        }
    };

    public Task<List<PlaylistModel>> GetUserPlaylistsAsync(string? authToken = null, CancellationToken ct = default)
    {
        return Task.FromResult(_demoPlaylists.ToList());
    }

    public Task<PlaylistModel?> GetPlaylistAsync(string playlistIdOrUrl, string? authToken = null, CancellationToken ct = default)
    {
        var playlist = _demoPlaylists.FirstOrDefault(p =>
            p.Id.Equals(playlistIdOrUrl, StringComparison.OrdinalIgnoreCase) ||
            p.Name.Equals(playlistIdOrUrl, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(playlist ?? _demoPlaylists.FirstOrDefault());
    }

    public Task<List<TrackModel>> SearchTracksAsync(string query, int limit = 5, string? authToken = null, CancellationToken ct = default)
    {
        var allTracks = _demoPlaylists.SelectMany(p => p.Tracks).ToList();
        var q = query.ToLowerInvariant();

        var matches = allTracks
            .Where(t => t.Title.ToLowerInvariant().Contains(q) || t.Artist.ToLowerInvariant().Contains(q) || (t.Isrc != null && t.Isrc.ToLowerInvariant().Contains(q)))
            .Take(limit)
            .ToList();

        // Se não houver correspondência direta, simula um candidato plausível
        if (matches.Count == 0)
        {
            matches.Add(new TrackModel
            {
                Id = $"demo-match-{Guid.NewGuid():N}",
                Title = query,
                Artist = "Artista Demo",
                DurationMs = 180000,
                ArtworkUrl = "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=200&q=80"
            });
        }

        return Task.FromResult(matches);
    }

    public Task<PlaylistModel> CreatePlaylistAsync(string name, string? description, List<TrackModel> tracks, string? authToken = null, CancellationToken ct = default)
    {
        var newPlaylist = new PlaylistModel
        {
            Id = $"demo-created-{Guid.NewGuid():N}",
            Name = name,
            Description = description ?? "Playlist criada via MusicPlay Demo",
            Owner = "Você",
            ProviderId = "demo",
            CoverImageUrl = "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=400&q=80",
            ExternalUrl = "#",
            Tracks = tracks.ToList()
        };

        _demoPlaylists.Add(newPlaylist);
        return Task.FromResult(newPlaylist);
    }
}
