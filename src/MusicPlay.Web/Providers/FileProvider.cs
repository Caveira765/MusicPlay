using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MusicPlay.Web.Models;

namespace MusicPlay.Web.Providers;

public class FileProvider : IMusicProvider
{
    public string Id => "file";
    public string Name => "Arquivo (CSV / M3U / JSON)";
    public string Icon => "file-text";
    public string AccentColor => "#3B82F6"; // Azul moderno
    public string Description => "Importe ou exporte backups completos em formatos padrão CSV, M3U8 e JSON";
    public bool CanImport => true;
    public bool CanExport => true;
    public bool RequiresAuth => false;

    public Task<List<PlaylistModel>> GetUserPlaylistsAsync(string? authToken = null, CancellationToken ct = default)
    {
        return Task.FromResult(new List<PlaylistModel>());
    }

    public Task<PlaylistModel?> GetPlaylistAsync(string playlistIdOrUrl, string? authToken = null, CancellationToken ct = default)
    {
        return Task.FromResult<PlaylistModel?>(null);
    }

    public Task<List<TrackModel>> SearchTracksAsync(string query, int limit = 5, string? authToken = null, CancellationToken ct = default)
    {
        return Task.FromResult(new List<TrackModel>());
    }

    public Task<PlaylistModel> CreatePlaylistAsync(string name, string? description, List<TrackModel> tracks, string? authToken = null, CancellationToken ct = default)
    {
        var playlist = new PlaylistModel
        {
            Id = $"file-{Guid.NewGuid():N}",
            Name = name,
            Description = description ?? "Backup de Playlist gerado pelo MusicPlay",
            Owner = "MusicPlay File Exporter",
            ProviderId = Id,
            CoverImageUrl = "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=400&q=80",
            ExternalUrl = "#",
            Tracks = tracks.ToList()
        };

        return Task.FromResult(playlist);
    }

    public static string ExportToCsv(PlaylistModel playlist)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Title,Artist,Album,DurationSeconds,ISRC,ExternalUrl");

        foreach (var t in playlist.Tracks)
        {
            var title = EscapeCsv(t.Title);
            var artist = EscapeCsv(t.Artist);
            var album = EscapeCsv(t.Album ?? string.Empty);
            var durSec = t.DurationMs / 1000;
            var isrc = EscapeCsv(t.Isrc ?? string.Empty);
            var url = EscapeCsv(t.ExternalUrl ?? string.Empty);

            sb.AppendLine($"{title},{artist},{album},{durSec},{isrc},{url}");
        }

        return sb.ToString();
    }

    public static string ExportToM3u(PlaylistModel playlist)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");
        sb.AppendLine($"#PLAYLIST:{playlist.Name}");

        foreach (var t in playlist.Tracks)
        {
            int durSec = t.DurationMs > 0 ? t.DurationMs / 1000 : -1;
            sb.AppendLine($"#EXTINF:{durSec},{t.Artist} - {t.Title}");
            if (!string.IsNullOrWhiteSpace(t.Isrc))
            {
                sb.AppendLine($"#EXTISRC:{t.Isrc}");
            }
            sb.AppendLine(t.ExternalUrl ?? $"track://{t.Id}");
        }

        return sb.ToString();
    }

    public static string ExportToJson(PlaylistModel playlist)
    {
        return JsonSerializer.Serialize(playlist, new JsonSerializerOptions { WriteIndented = true });
    }

    public static PlaylistModel ParseCsv(string csvContent, string playlistName = "Playlist Importada via CSV")
    {
        var playlist = new PlaylistModel
        {
            Id = $"csv-{Guid.NewGuid():N}",
            Name = playlistName,
            ProviderId = "file",
            Owner = "Importado",
            CoverImageUrl = "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=400&q=80"
        };

        var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1) return playlist;

        // Tentar identificar índice das colunas pelo cabeçalho
        var header = lines[0].Split(',');
        int titleIdx = 0, artistIdx = 1, albumIdx = 2, durIdx = -1, isrcIdx = -1;

        for (int i = 0; i < header.Length; i++)
        {
            var col = header[i].Trim().ToLowerInvariant().Trim('"');
            if (col.Contains("title") || col.Contains("track") || col.Contains("nome") || col.Contains("titulo")) titleIdx = i;
            else if (col.Contains("artist") || col.Contains("artista")) artistIdx = i;
            else if (col.Contains("album") || col.Contains("álbum")) albumIdx = i;
            else if (col.Contains("dur") || col.Contains("tempo")) durIdx = i;
            else if (col.Contains("isrc")) isrcIdx = i;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            var parts = ParseCsvRow(lines[i]);
            if (parts.Count <= Math.Max(titleIdx, artistIdx)) continue;

            var title = parts[titleIdx].Trim();
            var artist = parts[artistIdx].Trim();
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist)) continue;

            var track = new TrackModel
            {
                Id = $"csv-{i}",
                Title = title,
                Artist = artist,
                Album = albumIdx >= 0 && albumIdx < parts.Count ? parts[albumIdx].Trim() : null,
                Isrc = isrcIdx >= 0 && isrcIdx < parts.Count ? parts[isrcIdx].Trim() : null
            };

            if (durIdx >= 0 && durIdx < parts.Count && int.TryParse(parts[durIdx], out int sec))
            {
                track.DurationMs = sec * 1000;
            }

            playlist.Tracks.Add(track);
        }

        return playlist;
    }

    public static PlaylistModel ParseM3u(string m3uContent, string defaultName = "Playlist Importada via M3U")
    {
        var playlist = new PlaylistModel
        {
            Id = $"m3u-{Guid.NewGuid():N}",
            Name = defaultName,
            ProviderId = "file",
            Owner = "Importado",
            CoverImageUrl = "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=400&q=80"
        };

        var lines = m3uContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        TrackModel? currentTrack = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#PLAYLIST:", StringComparison.OrdinalIgnoreCase))
            {
                playlist.Name = trimmed["#PLAYLIST:".Length..].Trim();
            }
            else if (trimmed.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                var content = trimmed["#EXTINF:".Length..].Trim();
                var commaIdx = content.IndexOf(',');
                int durSec = 0;
                string titlePart = content;

                if (commaIdx >= 0)
                {
                    int.TryParse(content[..commaIdx], out durSec);
                    titlePart = content[(commaIdx + 1)..].Trim();
                }

                var hyphenIdx = titlePart.IndexOf(" - ", StringComparison.Ordinal);
                string artist = "Desconhecido";
                string title = titlePart;

                if (hyphenIdx > 0)
                {
                    artist = titlePart[..hyphenIdx].Trim();
                    title = titlePart[(hyphenIdx + 3)..].Trim();
                }

                currentTrack = new TrackModel
                {
                    Id = $"m3u-{playlist.Tracks.Count + 1}",
                    Title = title,
                    Artist = artist,
                    DurationMs = durSec > 0 ? durSec * 1000 : 0
                };
            }
            else if (trimmed.StartsWith("#EXTISRC:", StringComparison.OrdinalIgnoreCase) && currentTrack != null)
            {
                currentTrack.Isrc = trimmed["#EXTISRC:".Length..].Trim();
            }
            else if (!trimmed.StartsWith('#') && currentTrack != null)
            {
                currentTrack.ExternalUrl = trimmed;
                playlist.Tracks.Add(currentTrack);
                currentTrack = null;
            }
        }

        if (currentTrack != null)
        {
            playlist.Tracks.Add(currentTrack);
        }

        return playlist;
    }

    private static string EscapeCsv(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
        {
            return $"\"{s.Replace("\"", "\"\"")}\"";
        }
        return s;
    }

    private static List<string> ParseCsvRow(string row)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < row.Length; i++)
        {
            char c = row[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < row.Length && row[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
