using System.Text.Json;
using System.Text.RegularExpressions;
using SpotifyAPI.Web;
using MusicPlay.Web.Models;

namespace MusicPlay.Web.Providers;

public partial class SpotifyProvider : IMusicProvider
{
    private readonly IConfiguration _config;
    private readonly ILogger<SpotifyProvider> _logger;
    private readonly HttpClient _httpClient;

    public string Id => "spotify";
    public string Name => "Spotify";
    public string Icon => "music";
    public string AccentColor => "#1DB954"; // Verde oficial Spotify
    public string Description => "Importe qualquer playlist pública colando o link direto ou conectando sua conta";
    public bool CanImport => true;
    public bool CanExport => true;
    public bool RequiresAuth => false; // Agora pode ler qualquer playlist pública sem chaves!

    [GeneratedRegex(@"<script id=""__NEXT_DATA__""[^>]*>(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex NextDataRegex();

    public SpotifyProvider(IConfiguration config, ILogger<SpotifyProvider> logger, HttpClient httpClient)
    {
        _config = config;
        _logger = logger;
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }
    }

    public async Task<List<PlaylistModel>> GetUserPlaylistsAsync(string? authToken = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(authToken))
        {
            try
            {
                var spotify = new SpotifyClient(authToken);
                var userPlaylists = await spotify.Playlists.CurrentUsers(ct);
                var result = new List<PlaylistModel>();

                if (userPlaylists.Items != null)
                {
                    foreach (var p in userPlaylists.Items)
                    {
                        result.Add(new PlaylistModel
                        {
                            Id = p.Id ?? string.Empty,
                            Name = p.Name ?? "Sem nome",
                            Description = p.Description,
                            Owner = p.Owner?.DisplayName ?? "Você",
                            DeclaredTrackCount = p.Tracks?.Total ?? 0,
                            CoverImageUrl = p.Images?.FirstOrDefault()?.Url,
                            ProviderId = Id,
                            ExternalUrl = p.ExternalUrls?.GetValueOrDefault("spotify")
                        });
                    }
                }

                if (result.Count > 0) return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao obter playlists do usuário com token. Usando destaques.");
            }
        }

        return GetPopularPublicPlaylists();
    }

    public async Task<PlaylistModel?> GetPlaylistAsync(string playlistIdOrUrl, string? authToken = null, CancellationToken ct = default)
    {
        var (entityId, entityType) = ExtractEntityId(playlistIdOrUrl);

        // 1. Tenta carregar via extração direta de embed pública (sem precisar de API key nem login)
        var embedPlaylist = await ExtractFromSpotifyEmbedAsync(entityId, entityType, ct);
        if (embedPlaylist != null && embedPlaylist.Tracks.Count > 0)
        {
            return embedPlaylist;
        }

        // 2. Se falhar e houver credenciais OAuth / Developer configuradas ou authToken
        try
        {
            SpotifyClient? client = null;
            if (!string.IsNullOrWhiteSpace(authToken))
            {
                client = new SpotifyClient(authToken);
            }
            else
            {
                var clientId = _config["Spotify:ClientId"];
                var clientSecret = _config["Spotify:ClientSecret"];

                if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
                {
                    var oauth = new OAuthClient();
                    var tokenResponse = await oauth.RequestToken(new ClientCredentialsRequest(clientId, clientSecret), ct);
                    client = new SpotifyClient(tokenResponse.AccessToken);
                }
            }

            if (client != null)
            {
                if (entityType == "album")
                {
                    var album = await client.Albums.Get(entityId, ct);
                    if (album != null)
                    {
                        var pl = new PlaylistModel
                        {
                            Id = album.Id ?? entityId,
                            Name = album.Name ?? "Álbum Spotify",
                            Description = $"Álbum de {string.Join(", ", album.Artists?.Select(a => a.Name) ?? new[] { "" })}",
                            Owner = album.Artists?.FirstOrDefault()?.Name ?? "Spotify",
                            CoverImageUrl = album.Images?.FirstOrDefault()?.Url,
                            ProviderId = Id,
                            ExternalUrl = album.ExternalUrls?.GetValueOrDefault("spotify")
                        };

                        if (album.Tracks?.Items != null)
                        {
                            foreach (var item in album.Tracks.Items)
                            {
                                pl.Tracks.Add(new TrackModel
                                {
                                    Id = item.Id ?? Guid.NewGuid().ToString("N"),
                                    Title = item.Name ?? "Sem título",
                                    Artist = string.Join(", ", item.Artists?.Select(a => a.Name) ?? new[] { "Desconhecido" }),
                                    Album = album.Name,
                                    DurationMs = item.DurationMs,
                                    Uri = item.Uri,
                                    ExternalUrl = item.ExternalUrls?.GetValueOrDefault("spotify"),
                                    ArtworkUrl = album.Images?.FirstOrDefault()?.Url
                                });
                            }
                        }

                        if (pl.Tracks.Count > 0) return pl;
                    }
                }
                else
                {
                    var p = await client.Playlists.Get(entityId, ct);
                    var playlist = new PlaylistModel
                    {
                        Id = p.Id ?? entityId,
                        Name = p.Name ?? "Playlist Spotify",
                        Description = p.Description,
                        Owner = p.Owner?.DisplayName ?? "Spotify User",
                        CoverImageUrl = p.Images?.FirstOrDefault()?.Url,
                        ProviderId = Id,
                        ExternalUrl = p.ExternalUrls?.GetValueOrDefault("spotify")
                    };

#pragma warning disable CS0618
                    if (p.Tracks?.Items != null)
                    {
                        foreach (var item in p.Tracks.Items)
                        {
                            if (item.Track is FullTrack ft)
                            {
                                string? isrc = null;
                                ft.ExternalIds?.TryGetValue("isrc", out isrc);

                                playlist.Tracks.Add(new TrackModel
                                {
                                    Id = ft.Id ?? Guid.NewGuid().ToString("N"),
                                    Title = ft.Name ?? "Sem título",
                                    Artist = string.Join(", ", ft.Artists?.Select(a => a.Name) ?? new[] { "Desconhecido" }),
                                    Album = ft.Album?.Name,
                                    DurationMs = ft.DurationMs,
                                    Isrc = isrc,
                                    Uri = ft.Uri,
                                    ExternalUrl = ft.ExternalUrls?.GetValueOrDefault("spotify"),
                                    ArtworkUrl = ft.Album?.Images?.FirstOrDefault()?.Url
                                });
                            }
                        }
                    }
#pragma warning restore CS0618

                    if (playlist.Tracks.Count > 0) return playlist;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha na leitura via API oficial do Spotify para {Type}/{EntityId}", entityType, entityId);
        }

        return embedPlaylist;
    }

    private async Task<PlaylistModel?> ExtractFromSpotifyEmbedAsync(string entityId, string entityType, CancellationToken ct)
    {
        try
        {
            string url = $"https://open.spotify.com/embed/{entityType}/{entityId}";
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            var html = await response.Content.ReadAsStringAsync(ct);
            var match = NextDataRegex().Match(html);
            if (!match.Success) return null;

            var json = match.Groups[1].Value;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("props", out var props) ||
                !props.TryGetProperty("pageProps", out var pageProps) ||
                !pageProps.TryGetProperty("state", out var state) ||
                !state.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("entity", out var entity))
            {
                return null;
            }

            string title = entity.TryGetProperty("name", out var n) 
                ? n.GetString() ?? (entityType == "album" ? "Álbum Spotify" : "Playlist Spotify") 
                : (entityType == "album" ? "Álbum Spotify" : "Playlist Spotify");
            string? coverUrl = null;

            if (entity.TryGetProperty("visualIdentity", out var vi) && vi.TryGetProperty("image", out var imgArr))
            {
                foreach (var img in imgArr.EnumerateArray())
                {
                    if (img.TryGetProperty("url", out var u))
                    {
                        coverUrl = u.GetString();
                        break;
                    }
                }
            }

            var playlist = new PlaylistModel
            {
                Id = entityId,
                Name = title,
                Owner = entity.TryGetProperty("subtitle", out var sub) ? sub.GetString() ?? "Spotify" : "Spotify",
                CoverImageUrl = coverUrl ?? "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=400&q=80",
                ProviderId = Id,
                ExternalUrl = $"https://open.spotify.com/{entityType}/{entityId}"
            };

            if (entity.TryGetProperty("trackList", out var trackList))
            {
                int idx = 1;
                foreach (var item in trackList.EnumerateArray())
                {
                    string tTitle = item.TryGetProperty("title", out var tt) ? tt.GetString() ?? "Faixa" : "Faixa";
                    string tArtist = item.TryGetProperty("subtitle", out var ts) ? ts.GetString() ?? "Artista" : "Artista";
                    int duration = item.TryGetProperty("duration", out var td) ? td.GetInt32() : 0;
                    string? uri = item.TryGetProperty("uri", out var tu) ? tu.GetString() : null;

                    string trackId = uri?.StartsWith("spotify:track:") == true ? uri.Replace("spotify:track:", "") : $"sp-{idx}";

                    playlist.Tracks.Add(new TrackModel
                    {
                        Id = trackId,
                        Title = tTitle,
                        Artist = tArtist,
                        DurationMs = duration,
                        Uri = uri,
                        ExternalUrl = $"https://open.spotify.com/track/{trackId}",
                        ArtworkUrl = coverUrl
                    });

                    idx++;
                }
            }

            return playlist;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro na extração direta do embed Spotify para {Type}/{EntityId}", entityType, entityId);
            return null;
        }
    }

    public async Task<List<TrackModel>> SearchTracksAsync(string query, int limit = 5, string? authToken = null, CancellationToken ct = default)
    {
        // 1. Tentar via authToken oficial do usuário (se fornecido)
        if (!string.IsNullOrWhiteSpace(authToken))
        {
            try
            {
                var client = new SpotifyClient(authToken);
                var searchReq = new SearchRequest(SearchRequest.Types.Track, query) { Limit = limit };
                var searchResp = await client.Search.Item(searchReq, ct);

                if (searchResp.Tracks?.Items != null && searchResp.Tracks.Items.Count > 0)
                {
                    return searchResp.Tracks.Items.Select(MapFullTrack).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Busca Spotify API com user authToken falhou.");
            }
        }

        // 2. Tentar via credenciais ClientId/ClientSecret do servidor
        try
        {
            var clientId = _config["Spotify:ClientId"];
            var clientSecret = _config["Spotify:ClientSecret"];

            if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
            {
                var oauth = new OAuthClient();
                var tokenResponse = await oauth.RequestToken(new ClientCredentialsRequest(clientId, clientSecret), ct);
                var client = new SpotifyClient(tokenResponse.AccessToken);

                var searchReq = new SearchRequest(SearchRequest.Types.Track, query) { Limit = limit };
                var searchResp = await client.Search.Item(searchReq, ct);

                if (searchResp.Tracks?.Items != null && searchResp.Tracks.Items.Count > 0)
                {
                    return searchResp.Tracks.Items.Select(MapFullTrack).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Busca Spotify API (ClientCredentials) não configurada ou com erro.");
        }

        // 3. Fallback Aberto: Catálogo universal (Deezer + iTunes) para obter os dados canônicos reais da música (ISRC, Artista, Álbum, Duração)
        try
        {
            var resolvedTracks = await SearchUniversalCatalogForSpotifyAsync(query, limit, ct);
            if (resolvedTracks.Count > 0)
            {
                return resolvedTracks;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro no fallback de catálogo aberto para Spotify: {Query}", query);
        }

        return new List<TrackModel>();
    }

    private static TrackModel MapFullTrack(FullTrack ft)
    {
        string? isrc = null;
#pragma warning disable CS0618
        ft.ExternalIds?.TryGetValue("isrc", out isrc);
#pragma warning restore CS0618
        return new TrackModel
        {
            Id = ft.Id,
            Title = ft.Name,
            Artist = string.Join(", ", ft.Artists.Select(a => a.Name)),
            Album = ft.Album.Name,
            DurationMs = ft.DurationMs,
            Isrc = isrc,
            Uri = ft.Uri,
            ArtworkUrl = ft.Album.Images.FirstOrDefault()?.Url,
            ExternalUrl = ft.ExternalUrls.GetValueOrDefault("spotify")
        };
    }

    private async Task<List<TrackModel>> SearchUniversalCatalogForSpotifyAsync(string query, int limit, CancellationToken ct)
    {
        var results = new List<TrackModel>();

        // Tentativa A: Deezer API aberta (retorna metadados oficiais e ISRC)
        try
        {
            string url = $"https://api.deezer.com/search?q={Uri.EscapeDataString(query)}&limit={limit}";
            var response = await _httpClient.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data))
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                        string artist = item.TryGetProperty("artist", out var a) && a.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                        string album = item.TryGetProperty("album", out var al) && al.TryGetProperty("title", out var alt) ? alt.GetString() ?? "" : "";
                        int duration = item.TryGetProperty("duration", out var d) ? d.GetInt32() * 1000 : 0;
                        string? isrc = item.TryGetProperty("isrc", out var isrcProp) ? isrcProp.GetString() : null;
                        string? cover = item.TryGetProperty("album", out var alCover) && alCover.TryGetProperty("cover_medium", out var acm) ? acm.GetString() : null;

                        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artist))
                        {
                            results.Add(new TrackModel
                            {
                                Id = $"sp-{isrc ?? Guid.NewGuid().ToString("N")}",
                                Title = title,
                                Artist = artist,
                                Album = album,
                                DurationMs = duration,
                                Isrc = isrc,
                                ArtworkUrl = cover ?? "https://images.unsplash.com/photo-1614613535308-eb5fbd3d2c17?w=200&q=80",
                                ExternalUrl = $"https://open.spotify.com/search/{Uri.EscapeDataString($"{artist} {title}")}",
                                Uri = $"spotify:search:{Uri.EscapeDataString($"{artist} {title}")}"
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro no catálogo Deezer para Spotify: {Query}", query);
        }

        // Tentativa B: iTunes Search API aberta se Deezer não retornou
        if (results.Count == 0)
        {
            try
            {
                string itunesUrl = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&entity=song&limit={limit}";
                var itunesResp = await _httpClient.GetAsync(itunesUrl, ct);
                if (itunesResp.IsSuccessStatusCode)
                {
                    var json = await itunesResp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("results", out var itunesResults))
                    {
                        foreach (var item in itunesResults.EnumerateArray())
                        {
                            string title = item.TryGetProperty("trackName", out var tn) ? tn.GetString() ?? "" : "";
                            string artist = item.TryGetProperty("artistName", out var an) ? an.GetString() ?? "" : "";
                            string album = item.TryGetProperty("collectionName", out var cn) ? cn.GetString() ?? "" : "";
                            int duration = item.TryGetProperty("trackTimeMillis", out var tm) ? tm.GetInt32() : 0;
                            string? cover = item.TryGetProperty("artworkUrl100", out var art) ? art.GetString() : null;

                            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artist))
                            {
                                results.Add(new TrackModel
                                {
                                    Id = $"sp-{Guid.NewGuid():N}",
                                    Title = title,
                                    Artist = artist,
                                    Album = album,
                                    DurationMs = duration,
                                    ArtworkUrl = cover,
                                    ExternalUrl = $"https://open.spotify.com/search/{Uri.EscapeDataString($"{artist} {title}")}",
                                    Uri = $"spotify:search:{Uri.EscapeDataString($"{artist} {title}")}"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro no catálogo iTunes para Spotify: {Query}", query);
            }
        }

        return results;
    }

    public async Task<PlaylistModel> CreatePlaylistAsync(string name, string? description, List<TrackModel> tracks, string? authToken = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(authToken))
        {
            try
            {
                var client = new SpotifyClient(authToken);
                var me = await client.UserProfile.Current(ct);
#pragma warning disable CS0618
                var created = await client.Playlists.Create(me.Id, new PlaylistCreateRequest(name)
                {
                    Description = description ?? "Migrado via MusicPlay",
                    Public = true
                }, ct);

                var uris = new List<string>();
                foreach (var t in tracks)
                {
                    if (!string.IsNullOrEmpty(t.Uri) && t.Uri.StartsWith("spotify:track:"))
                    {
                        uris.Add(t.Uri);
                    }
                    else if (!string.IsNullOrEmpty(t.Id) && !t.Id.StartsWith("sp-"))
                    {
                        uris.Add($"spotify:track:{t.Id}");
                    }
                    else
                    {
                        try
                        {
                            var searchResp = await client.Search.Item(new SearchRequest(SearchRequest.Types.Track, $"{t.Title} {t.Artist}") { Limit = 1 }, ct);
                            var found = searchResp.Tracks?.Items?.FirstOrDefault();
                            if (found != null && !string.IsNullOrEmpty(found.Uri))
                            {
                                uris.Add(found.Uri);
                            }
                        }
                        catch { }
                    }
                }

                if (uris.Count > 0)
                {
                    foreach (var chunk in uris.Chunk(100))
                    {
                        await client.Playlists.AddItems(created.Id!, new PlaylistAddItemsRequest(chunk.ToList()), ct);
                    }
                }

                return new PlaylistModel
                {
                    Id = created.Id!,
                    Name = created.Name ?? name,
                    Description = created.Description,
                    Owner = me.DisplayName,
                    ProviderId = Id,
                    CoverImageUrl = created.Images?.FirstOrDefault()?.Url ?? tracks.FirstOrDefault()?.ArtworkUrl,
                    ExternalUrl = created.ExternalUrls?.GetValueOrDefault("spotify") ?? $"https://open.spotify.com/playlist/{created.Id}",
                    Tracks = tracks
                };
#pragma warning restore CS0618
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar playlist no Spotify via API");
            }
        }

        // Fallback quando não há token: abre a primeira música ou a pesquisa no Spotify
        var firstTrack = tracks.FirstOrDefault();
        string destinationUrl = firstTrack != null && !string.IsNullOrEmpty(firstTrack.Id) && !firstTrack.Id.StartsWith("sp-")
            ? $"https://open.spotify.com/track/{firstTrack.Id}"
            : $"https://open.spotify.com/search/{Uri.EscapeDataString(name)}";

        return new PlaylistModel
        {
            Id = $"sp-{Guid.NewGuid():N}",
            Name = name,
            Description = description,
            ProviderId = Id,
            Owner = "Você",
            CoverImageUrl = firstTrack?.ArtworkUrl ?? "https://images.unsplash.com/photo-1614613535308-eb5fbd3d2c17?w=400&q=80",
            ExternalUrl = destinationUrl,
            Tracks = tracks
        };
    }

    public static (string Id, string Type) ExtractEntityId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return (string.Empty, "playlist");
        var clean = input.Trim();

        // 1. Captura URLs completas com ou sem locale (/intl-pt/, /intl-es/, etc.) ou URIs (spotify:playlist:ID, spotify:album:ID)
        var match = Regex.Match(clean, @"(playlist|album)[/:]([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var type = match.Groups[1].Value.ToLowerInvariant();
            var id = match.Groups[2].Value;
            return (id, type);
        }

        // 2. Se o usuário passou apenas o ID da playlist/álbum (alfanumérico de 15 a 30 caracteres)
        var rawClean = clean.Split('?')[0].Split('&')[0].Trim();
        if (Regex.IsMatch(rawClean, @"^[a-zA-Z0-9]{15,30}$"))
        {
            return (rawClean, "playlist");
        }

        return (rawClean, "playlist");
    }

    private static List<PlaylistModel> GetPopularPublicPlaylists()
    {
        return new List<PlaylistModel>
        {
            new()
            {
                Id = "37i9dQZF1DXcBWIGoYBM5M",
                Name = "Today's Top Hits",
                Description = "The biggest songs right now.",
                Owner = "Spotify",
                DeclaredTrackCount = 50,
                ProviderId = "spotify",
                CoverImageUrl = "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=400&q=80",
                ExternalUrl = "https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M"
            },
            new()
            {
                Id = "37i9dQZF1DX0XUsuxWHRQd",
                Name = "RapCaviar",
                Description = "Music from Kendrick Lamar, Drake and more.",
                Owner = "Spotify",
                DeclaredTrackCount = 50,
                ProviderId = "spotify",
                CoverImageUrl = "https://images.unsplash.com/photo-1492684223066-81342ee5ff30?w=400&q=80",
                ExternalUrl = "https://open.spotify.com/playlist/37i9dQZF1DX0XUsuxWHRQd"
            }
        };
    }
}
