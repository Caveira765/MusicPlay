using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MusicPlay.Web.Models;
using MusicPlay.Web.Providers;
using MusicPlay.Web.Services;

namespace MusicPlay.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly IPlaylistTransferService _transferService;

    public ExportController(IPlaylistTransferService transferService)
    {
        _transferService = transferService;
    }

    [HttpGet("m3u/{sessionId}")]
    public IActionResult DownloadM3u(string sessionId)
    {
        var playlist = _transferService.GetCompletedPlaylist(sessionId);
        if (playlist == null) return NotFound("Playlist finalizada não encontrada para esta sessão");

        var m3u = FileProvider.ExportToM3u(playlist);
        var bytes = Encoding.UTF8.GetBytes(m3u);
        return File(bytes, "audio/x-mpegurl", $"{SanitizeFileName(playlist.Name)}.m3u8");
    }

    [HttpGet("csv/{sessionId}")]
    public IActionResult DownloadCsv(string sessionId)
    {
        var playlist = _transferService.GetCompletedPlaylist(sessionId);
        if (playlist == null) return NotFound("Playlist finalizada não encontrada para esta sessão");

        var csv = FileProvider.ExportToCsv(playlist);
        var bytes = Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv; charset=utf-8", $"{SanitizeFileName(playlist.Name)}.csv");
    }

    [HttpGet("txt/{sessionId}")]
    public IActionResult DownloadTxt(string sessionId)
    {
        var playlist = _transferService.GetCompletedPlaylist(sessionId);
        if (playlist == null) return NotFound("Playlist finalizada não encontrada para esta sessão");

        var sb = new StringBuilder();
        foreach (var t in playlist.Tracks)
        {
            sb.AppendLine($"{t.Artist} - {t.Title}");
        }
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/plain; charset=utf-8", $"{SanitizeFileName(playlist.Name)}.txt");
    }

    [HttpGet("json/{sessionId}")]
    public IActionResult DownloadJson(string sessionId)
    {
        var playlist = _transferService.GetCompletedPlaylist(sessionId);
        if (playlist == null) return NotFound("Playlist finalizada não encontrada para esta sessão");

        var json = FileProvider.ExportToJson(playlist);
        var bytes = Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", $"{SanitizeFileName(playlist.Name)}.json");
    }

    [HttpPost("upload-file")]
    public async Task<IActionResult> UploadPlaylistFile([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Nenhum arquivo enviado.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        var playlistName = Path.GetFileNameWithoutExtension(file.FileName);

        PlaylistModel result;
        if (ext == ".m3u" || ext == ".m3u8")
        {
            result = FileProvider.ParseM3u(content, playlistName);
        }
        else if (ext == ".json")
        {
            try
            {
                result = JsonSerializer.Deserialize<PlaylistModel>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                         ?? new PlaylistModel { Name = playlistName };
            }
            catch
            {
                return BadRequest("Formato JSON de playlist inválido.");
            }
        }
        else // Assume CSV
        {
            result = FileProvider.ParseCsv(content, playlistName);
        }

        return Ok(result);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
