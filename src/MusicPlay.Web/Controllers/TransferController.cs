using Microsoft.AspNetCore.Mvc;
using MusicPlay.Web.Models;
using MusicPlay.Web.Providers;
using MusicPlay.Web.Services;

namespace MusicPlay.Web.Controllers;

public record LoadUrlRequest(string Url, string? Token = null);

[ApiController]
[Route("api/[controller]")]
public class TransferController : ControllerBase
{
    private readonly IPlaylistTransferService _transferService;
    private readonly IEnumerable<IMusicProvider> _providers;
    private readonly ILogger<TransferController> _logger;

    public TransferController(
        IPlaylistTransferService transferService,
        IEnumerable<IMusicProvider> providers,
        ILogger<TransferController> logger)
    {
        _transferService = transferService;
        _providers = providers;
        _logger = logger;
    }

    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        var list = _providers.Select(p => new
        {
            p.Id,
            p.Name,
            p.Icon,
            p.AccentColor,
            p.Description,
            p.CanImport,
            p.CanExport,
            p.RequiresAuth
        });

        return Ok(list);
    }

    [HttpGet("playlists")]
    public async Task<IActionResult> GetPlaylists([FromQuery] string providerId, [FromQuery] string? token = null)
    {
        var provider = _providers.FirstOrDefault(p => p.Id == providerId);
        if (provider == null) return NotFound("Provedor não encontrado");

        var playlists = await provider.GetUserPlaylistsAsync(token, HttpContext.RequestAborted);
        return Ok(playlists);
    }

    [HttpGet("playlist-details")]
    public async Task<IActionResult> GetPlaylistDetails(
        [FromQuery] string providerId,
        [FromQuery] string playlistId,
        [FromQuery] string? token = null)
    {
        var provider = _providers.FirstOrDefault(p => p.Id == providerId);
        if (provider == null) return NotFound("Provedor não encontrado");

        var playlist = await provider.GetPlaylistAsync(playlistId, token, HttpContext.RequestAborted);
        if (playlist == null) return NotFound("Playlist não encontrada");

        return Ok(playlist);
    }

    [HttpPost("load-url")]
    public async Task<IActionResult> LoadPlaylistFromUrl([FromBody] LoadUrlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest("URL da playlist não informada.");
        }

        var url = request.Url.Trim();
        IMusicProvider? matchedProvider = null;

        if (url.Contains("spotify.com", StringComparison.OrdinalIgnoreCase))
        {
            matchedProvider = _providers.FirstOrDefault(p => p.Id == "spotify");
        }
        else if (url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
                 url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            matchedProvider = _providers.FirstOrDefault(p => p.Id == "youtube");
        }
        else if (url.Contains("deezer.com", StringComparison.OrdinalIgnoreCase))
        {
            matchedProvider = _providers.FirstOrDefault(p => p.Id == "deezer");
        }

        if (matchedProvider == null)
        {
            matchedProvider = _providers.FirstOrDefault(p => p.Id == "spotify");
        }

        if (matchedProvider == null)
        {
            return BadRequest("Nenhum provedor suportado identificado para esta URL.");
        }

        var playlist = await matchedProvider.GetPlaylistAsync(url, request.Token, HttpContext.RequestAborted);
        if (playlist == null || playlist.Tracks.Count == 0)
        {
            return NotFound("Não foi possível carregar as faixas desta playlist. Verifique se o link é público.");
        }

        return Ok(new
        {
            providerId = matchedProvider.Id,
            providerName = matchedProvider.Name,
            playlist
        });
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartTransfer([FromBody] TransferRequest request)
    {
        if (request.Tracks == null || request.Tracks.Count == 0)
        {
            return BadRequest("Nenhuma música foi selecionada para transferência.");
        }

        var sessionId = await _transferService.StartTransferAsync(request);
        return Ok(new { sessionId, message = "Transferência iniciada com sucesso" });
    }

    [HttpGet("status/{sessionId}")]
    public IActionResult GetStatus(string sessionId)
    {
        var status = _transferService.GetSessionStatus(sessionId);
        if (status == null) return NotFound("Sessão de transferência não encontrada");
        return Ok(status);
    }

    [HttpPost("cancel/{sessionId}")]
    public IActionResult CancelTransfer(string sessionId)
    {
        _transferService.CancelTransfer(sessionId);
        return Ok(new { message = "Cancelamento solicitado" });
    }
}
