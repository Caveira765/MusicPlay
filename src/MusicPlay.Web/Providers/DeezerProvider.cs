using System.Text.Json;
using System.Text.Json.Serialization;
using MusicPlay.Web.Models;

namespace MusicPlay.Web.Providers;

public class DeezerProvider : IMusicProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeezerProvider> _logger;

    public string Id => "deezer";
    public string Name => "Deezer";
    public string Icon => "disc";
    public string AccentColor => "#A238FF"; // Cor roxa / magenta oficial do Deezer
    public string Description => "Pesquisa e leitura ao vivo de músicas e playlists via API aberta do Deezer";
    public bool CanImport => true;
    public bool CanExport => true;
    public bool RequiresAuth => false; // Leitura e pesquisa pública não exigem autenticação

    public DeezerProvider(HttpClient httpClient, ILogger<DeezerProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("https://api.deezer.com/");
        }
    }

    public async Task<List<PlaylistModel>> GetUserPlaylistsAsync(string? authToken = null, CancellationToken ct = default)
    {
        // Playlists populares em destaque para exploração
        var featuredIds = new[] { "3155776842", "11161893042", "1313621735" }; // Top Hits, Charts
        var list = new List<PlaylistModel>();

        foreach (var pid in featuredIds)
        {
            var p = await GetPlaylistAsync(pid, authToken, ct);
            if (p != null) list.Add(p);
        }

        return list;
    }

    public async Task<PlaylistModel?> GetPlaylistAsync(string playlistIdOrUrl, string? authToken = null, CancellationToken ct = default)
    {
        try
        {
            string playlistId = ExtractPlaylistId(playlistIdOrUrl);
            var response = await _httpClient.GetAsync($"playlist/{playlistId}", ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out _)) return null;

            var playlist = new PlaylistModel
            {
                Id = root.GetProperty("id").GetRawText(),
                Name = root.GetProperty("title").GetString() ?? "Playlist Deezer",
                Description = root.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                Owner = root.TryGetProperty("creator", out var creator) && creator.TryGetProperty("name", out var cName) ? cName.GetString() : "Deezer",
                CoverImageUrl = root.TryGetProperty("picture_medium", out var pic) ? pic.GetString() : null,
                ProviderId = Id,
                ExternalUrl = root.TryGetProperty("link", out var link) ? link.GetString() : null
            };

            if (root.TryGetProperty("tracks", out var tracksElem) && tracksElem.TryGetProperty("data", out var tracksData))
            {
                foreach (var t in tracksData.EnumerateArray())
                {
                    playlist.Tracks.Add(ParseDeezerTrack(t));
                }
            }

            return playlist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar playlist no Deezer: {PlaylistId}", playlistIdOrUrl);
            return null;
        }
    }

    public async Task<List<TrackModel>> SearchTracksAsync(string query, int limit = 5, string? authToken = null, CancellationToken ct = default)
    {
        try
        {
            string url = $"search?q={Uri.EscapeDataString(query)}&limit={limit}";
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return new List<TrackModel>();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var results = new List<TrackModel>();
            if (root.TryGetProperty("data", out var data))
            {
                foreach (var item in data.EnumerateArray())
                {
                    results.Add(ParseDeezerTrack(item));
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao pesquisar faixa no Deezer: {Query}", query);
            return new List<TrackModel>();
        }
    }

    public Task<PlaylistModel> CreatePlaylistAsync(string name, string? description, List<TrackModel> tracks, string? authToken = null, CancellationToken ct = default)
    {
        // Criação de playlist Deezer com links para reprodução
        var firstTrack = tracks.FirstOrDefault();
        string destinationUrl = firstTrack != null && !string.IsNullOrEmpty(firstTrack.Id)
            ? $"https://www.deezer.com/track/{firstTrack.Id}"
            : $"https://www.deezer.com/search/{Uri.EscapeDataString(name)}";

        var created = new PlaylistModel
        {
            Id = $"dz-{Guid.NewGuid():N}",
            Name = name,
            Description = description ?? "Criado via MusicPlay",
            ProviderId = Id,
            CoverImageUrl = firstTrack?.ArtworkUrl ?? "https://e-cdns-images.dzcdn.net/images/cover/d41d8cd98f00b204e9800998ecf8427e/250x250-000000-80-0-0.jpg",
            ExternalUrl = destinationUrl,
            Tracks = tracks.ToList()
        };

        return Task.FromResult(created);
    }

    private static TrackModel ParseDeezerTrack(JsonElement item)
    {
        string id = item.GetProperty("id").GetRawText();
        string title = item.GetProperty("title").GetString() ?? "Sem título";
        string artist = item.TryGetProperty("artist", out var art) && art.TryGetProperty("name", out var aName) ? aName.GetString() ?? "Desconhecido" : "Desconhecido";
        string? album = item.TryGetProperty("album", out var alb) && alb.TryGetProperty("title", out var albTitle) ? albTitle.GetString() : null;
        int durationSec = item.TryGetProperty("duration", out var dur) ? dur.GetInt32() : 0;
        string? artwork = item.TryGetProperty("album", out var alb2) && alb2.TryGetProperty("cover_medium", out var cov) ? cov.GetString() : null;
        string? isrc = item.TryGetProperty("isrc", out var isrcElem) ? isrcElem.GetString() : null;
        string? link = item.TryGetProperty("link", out var linkElem) ? linkElem.GetString() : null;

        return new TrackModel
        {
            Id = id,
            Title = title,
            Artist = artist,
            Album = album,
            DurationMs = durationSec * 1000,
            ArtworkUrl = artwork,
            Isrc = isrc,
            ExternalUrl = link,
            Uri = $"deezer:track:{id}"
        };
    }

    private static string ExtractPlaylistId(string input)
    {
        if (long.TryParse(input, out _)) return input;

        // Trata URLs como https://www.deezer.com/playlist/3155776842 ou /br/playlist/3155776842
        var uri = input.Split('?')[0].TrimEnd('/');
        var parts = uri.Split('/');
        return parts.LastOrDefault() ?? input;
    }
}
