using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Search;
using MusicPlay.Web.Models;

using System.Text.RegularExpressions;

namespace MusicPlay.Web.Providers;

public partial class YouTubeProvider : IMusicProvider
{
    private readonly IConfiguration _config;
    private readonly ILogger<YouTubeProvider> _logger;
    private readonly YoutubeClient _youtubeClient;

    [GeneratedRegex(@"\([^)]*?\b(official|music\s*video|video|audio|hd|4k|live|ao\s*vivo|remaster(ed)?|feat\.?|ft\.?|clip(e)?|legendado|letra|visualizer|version)\b[^)]*?\)", RegexOptions.IgnoreCase)]
    private static partial Regex ParenthesesNoiseRegex();

    [GeneratedRegex(@"\[[^\]]*?\b(official|music\s*video|video|audio|hd|4k|live|ao\s*vivo|remaster(ed)?|feat\.?|ft\.?|clip(e)?|legendado|letra|visualizer|version)\b[^\]]*?\]", RegexOptions.IgnoreCase)]
    private static partial Regex BracketsNoiseRegex();

    [GeneratedRegex(@"\b(official\s*(music\s*)?video|official\s*audio|clipe\s*oficial|video\s*oficial|audio\s*oficial|ao\s*vivo|remaster(ed)?(\s*\d{4})?|4k|hd)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingNoiseRegex();

    public string Id => "youtube";
    public string Name => "YouTube / YT Music";
    public string Icon => "play-square";
    public string AccentColor => "#FF0000"; // Vermelho oficial YouTube
    public string Description => "Transfira ou crie playlists no YouTube e YouTube Music sem precisar de chaves ou cadastro";
    public bool CanImport => true;
    public bool CanExport => true;
    public bool RequiresAuth => false; // Agora 100% livre com YoutubeExplode

    public YouTubeProvider(IConfiguration config, ILogger<YouTubeProvider> logger, HttpClient httpClient)
    {
        _config = config;
        _logger = logger;
        _youtubeClient = new YoutubeClient(httpClient);
    }

    public Task<List<PlaylistModel>> GetUserPlaylistsAsync(string? authToken = null, CancellationToken ct = default)
    {
        var samplePlaylists = new List<PlaylistModel>
        {
            new()
            {
                Id = "PLMC9KNkIncKtPzgY-5rmhvj7fax8fdxoj",
                Name = "Pop Music Hits - YouTube Music",
                Description = "As melhores músicas pop do YouTube Music",
                Owner = "YouTube",
                DeclaredTrackCount = 50,
                ProviderId = Id,
                CoverImageUrl = "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=400&q=80",
                ExternalUrl = "https://music.youtube.com/playlist?list=PLMC9KNkIncKtPzgY-5rmhvj7fax8fdxoj"
            },
            new()
            {
                Id = "PL4fGSI1pDJn6jXS_PEoNnmdWCn99dgW8v",
                Name = "Rock Classics - YouTube Music",
                Description = "Grandes sucessos do Rock no YouTube",
                Owner = "YouTube",
                DeclaredTrackCount = 60,
                ProviderId = Id,
                CoverImageUrl = "https://images.unsplash.com/photo-1498038432885-c6f3f1b912ee?w=400&q=80",
                ExternalUrl = "https://music.youtube.com/playlist?list=PL4fGSI1pDJn6jXS_PEoNnmdWCn99dgW8v"
            }
        };

        return Task.FromResult(samplePlaylists);
    }

    public async Task<PlaylistModel?> GetPlaylistAsync(string playlistIdOrUrl, string? authToken = null, CancellationToken ct = default)
    {
        string playlistId = ExtractPlaylistId(playlistIdOrUrl);

        try
        {
            var playlistMeta = await _youtubeClient.Playlists.GetAsync(playlistId, ct);
            var result = new PlaylistModel
            {
                Id = playlistMeta.Id.Value,
                Name = playlistMeta.Title,
                Description = playlistMeta.Description,
                Owner = playlistMeta.Author?.ChannelTitle ?? "YouTube",
                CoverImageUrl = GetThumbnailUrl(playlistMeta.Thumbnails),
                ProviderId = Id,
                ExternalUrl = playlistMeta.Url
            };

            await foreach (var video in _youtubeClient.Playlists.GetVideosAsync(playlistId, ct))
            {
                var (cleanTitle, cleanArtist) = ParseYouTubeTitle(video.Title, video.Author.ChannelTitle);

                result.Tracks.Add(new TrackModel
                {
                    Id = video.Id.Value,
                    Title = cleanTitle,
                    Artist = cleanArtist,
                    DurationMs = video.Duration.HasValue ? (int)video.Duration.Value.TotalMilliseconds : 0,
                    ArtworkUrl = GetThumbnailUrl(video.Thumbnails),
                    ExternalUrl = video.Url,
                    Uri = $"youtube:video:{video.Id.Value}"
                });

                if (result.Tracks.Count >= 250) break;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao obter playlist do YouTube via YoutubeExplode: {PlaylistId}", playlistIdOrUrl);
            return null;
        }
    }

    public async Task<List<TrackModel>> SearchTracksAsync(string query, int limit = 5, string? authToken = null, CancellationToken ct = default)
    {
        try
        {
            var results = new List<TrackModel>();
            await foreach (var item in _youtubeClient.Search.GetVideosAsync(query, ct))
            {
                var (cleanTitle, cleanArtist) = ParseYouTubeTitle(item.Title, item.Author.ChannelTitle);

                results.Add(new TrackModel
                {
                    Id = item.Id.Value,
                    Title = cleanTitle,
                    Artist = cleanArtist,
                    DurationMs = item.Duration.HasValue ? (int)item.Duration.Value.TotalMilliseconds : 0,
                    ArtworkUrl = GetThumbnailUrl(item.Thumbnails),
                    ExternalUrl = item.Url,
                    Uri = $"youtube:video:{item.Id.Value}"
                });

                if (results.Count >= limit) break;
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao pesquisar faixa no YouTube via YoutubeExplode: {Query}", query);
            return new List<TrackModel>();
        }
    }

    public static (string Title, string Artist) ParseYouTubeTitle(string rawTitle, string? channelTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
            return ("Sem título", channelTitle ?? "Desconhecido");

        string cleanChannel = CleanChannelName(channelTitle);
        string title = rawTitle;
        string artist = cleanChannel;

        // Separadores comuns de "Artista - Título" em vídeos do YouTube
        string[] separators = { " - ", " – ", " — ", " // ", " / ", " : " };
        foreach (var sep in separators)
        {
            if (rawTitle.Contains(sep))
            {
                var parts = rawTitle.Split(sep, 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    artist = parts[0];
                    title = parts[1];
                    break;
                }
            }
        }

        title = CleanYouTubeNoise(title);
        artist = CleanYouTubeNoise(artist);

        if (string.IsNullOrWhiteSpace(title)) title = rawTitle;
        if (string.IsNullOrWhiteSpace(artist)) artist = cleanChannel;

        return (title, artist);
    }

    private static string CleanChannelName(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return "YouTube";
        var clean = channel.Trim();
        if (clean.EndsWith(" - Topic", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^8].Trim();
        }
        else if (clean.EndsWith("VEVO", StringComparison.OrdinalIgnoreCase) && clean.Length > 4)
        {
            clean = clean[..^4].Trim();
        }
        return clean;
    }

    private static string CleanYouTubeNoise(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var step1 = ParenthesesNoiseRegex().Replace(input, " ");
        var step2 = BracketsNoiseRegex().Replace(step1, " ");
        var step3 = TrailingNoiseRegex().Replace(step2, " ");
        return Regex.Replace(step3, @"\s+", " ").Trim();
    }

    public Task<PlaylistModel> CreatePlaylistAsync(string name, string? description, List<TrackModel> tracks, string? authToken = null, CancellationToken ct = default)
    {
        string destinationUrl;
        var videoIds = tracks.Where(t => !string.IsNullOrEmpty(t.Id)).Select(t => t.Id).Take(50).ToList();

        if (videoIds.Count > 0)
        {
            // Endpoint oficial do YouTube para criar fila e lista sequencial com botão de salvar playlist
            destinationUrl = $"https://www.youtube.com/watch_videos?video_ids={string.Join(",", videoIds)}";
        }
        else
        {
            destinationUrl = $"https://www.youtube.com/results?search_query={Uri.EscapeDataString(name)}";
        }

        var playlist = new PlaylistModel
        {
            Id = $"yt-{Guid.NewGuid():N}",
            Name = name,
            Description = description ?? "Criada pelo MusicPlay",
            Owner = "Você",
            ProviderId = Id,
            CoverImageUrl = tracks.FirstOrDefault()?.ArtworkUrl ?? "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=400&q=80",
            ExternalUrl = destinationUrl,
            Tracks = tracks.ToList()
        };

        return Task.FromResult(playlist);
    }

    private static string? GetThumbnailUrl(IReadOnlyList<Thumbnail> thumbnails)
    {
        return thumbnails.LastOrDefault()?.Url ?? thumbnails.FirstOrDefault()?.Url;
    }

    private static string ExtractPlaylistId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var clean = input.Trim();
        if (clean.Contains("list="))
        {
            var part = clean.Split("list=")[1];
            return part.Split('&')[0];
        }
        return clean;
    }
}
