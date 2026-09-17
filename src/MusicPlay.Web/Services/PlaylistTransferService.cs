using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using MusicPlay.Web.Hubs;
using MusicPlay.Web.Models;
using MusicPlay.Web.Providers;

namespace MusicPlay.Web.Services;

public interface IPlaylistTransferService
{
    Task<string> StartTransferAsync(TransferRequest request);
    void CancelTransfer(string sessionId);
    TransferProgressUpdate? GetSessionStatus(string sessionId);
    PlaylistModel? GetCompletedPlaylist(string sessionId);
    List<IMusicProvider> GetAvailableProviders();
}

public class PlaylistTransferService : IPlaylistTransferService
{
    private readonly IEnumerable<IMusicProvider> _providers;
    private readonly IMatchingEngine _matchingEngine;
    private readonly IHubContext<TransferHub, ITransferClient> _hubContext;
    private readonly ILogger<PlaylistTransferService> _logger;

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningTransfers = new();
    private readonly ConcurrentDictionary<string, TransferProgressUpdate> _sessionStates = new();
    private readonly ConcurrentDictionary<string, PlaylistModel> _createdPlaylists = new();

    public PlaylistTransferService(
        IEnumerable<IMusicProvider> providers,
        IMatchingEngine matchingEngine,
        IHubContext<TransferHub, ITransferClient> hubContext,
        ILogger<PlaylistTransferService> logger)
    {
        _providers = providers;
        _matchingEngine = matchingEngine;
        _hubContext = hubContext;
        _logger = logger;
    }

    public List<IMusicProvider> GetAvailableProviders() => _providers.ToList();

    public TransferProgressUpdate? GetSessionStatus(string sessionId)
    {
        _sessionStates.TryGetValue(sessionId, out var status);
        return status;
    }

    public PlaylistModel? GetCompletedPlaylist(string sessionId)
    {
        _createdPlaylists.TryGetValue(sessionId, out var playlist);
        return playlist;
    }

    public void CancelTransfer(string sessionId)
    {
        if (_runningTransfers.TryGetValue(sessionId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Cancelamento solicitado para a sessão {SessionId}", sessionId);
        }
    }

    public Task<string> StartTransferAsync(TransferRequest request)
    {
        var sessionId = request.SessionId;
        var cts = new CancellationTokenSource();
        _runningTransfers[sessionId] = cts;

        var initialProgress = new TransferProgressUpdate
        {
            SessionId = sessionId,
            TotalTracks = request.Tracks.Count,
            ProcessedTracks = 0,
            MatchedTracks = 0,
            FailedTracks = 0,
            Status = TransferStatus.Processing,
            LogMessage = $"Iniciando transferência de {request.Tracks.Count} músicas..."
        };

        _sessionStates[sessionId] = initialProgress;

        // Dispara o processamento assíncrono em segundo plano
        _ = Task.Run(() => ProcessTransferAsync(request, cts.Token));

        return Task.FromResult(sessionId);
    }

    private async Task ProcessTransferAsync(TransferRequest request, CancellationToken ct)
    {
        var sessionId = request.SessionId;
        var srcProvider = _providers.FirstOrDefault(p => p.Id == request.SourceProviderId)
                          ?? _providers.First(p => p.Id == "demo");
        var dstProvider = _providers.FirstOrDefault(p => p.Id == request.DestinationProviderId)
                          ?? _providers.First(p => p.Id == "demo");

        var progress = _sessionStates[sessionId];
        await _hubContext.Clients.Group(sessionId).TransferStarted(progress);

        var matchedTracks = new List<TrackModel>();
        var results = new List<TrackMatchResult>();

        try
        {
            for (int i = 0; i < request.Tracks.Count; i++)
            {
                if (ct.IsCancellationRequested)
                {
                    progress.Status = TransferStatus.Cancelled;
                    progress.LogMessage = "Transferência cancelada pelo usuário.";
                    await _hubContext.Clients.Group(sessionId).TransferCompleted(progress);
                    return;
                }

                var track = request.Tracks[i];
                progress.CurrentTrack = track;

                // Caso especial: Destino é Arquivo (M3U, CSV, JSON) - exportação direta sem necessidade de matching em catálogo externo
                if (dstProvider.Id == "file")
                {
                    var directMatch = new TrackMatchResult
                    {
                        SourceTrack = track,
                        MatchedTrack = track,
                        ConfidenceScore = 1.0,
                        MatchType = TrackMatchType.ExactIsrc,
                        Notes = "Pronto para exportação em arquivo universal"
                    };

                    results.Add(directMatch);
                    progress.ProcessedTracks++;
                    progress.MatchedTracks++;
                    matchedTracks.Add(track);
                    progress.LogMessage = $"✔ Incluída no arquivo: \"{track.Title}\" de {track.Artist}";
                    progress.LastMatchResult = directMatch;

                    await _hubContext.Clients.Group(sessionId).TrackProcessed(directMatch, progress);
                    await Task.Delay(100, ct);
                    continue;
                }

                // 1. Busca candidatos na plataforma de destino
                var candidates = new List<TrackModel>();

                // Tentar buscar por ISRC primeiro se disponível
                if (!string.IsNullOrWhiteSpace(track.Isrc))
                {
                    candidates.AddRange(await dstProvider.SearchTracksAsync($"isrc:{track.Isrc}", 3, request.DestinationAuthToken, ct));
                }

                // Se não achou por ISRC, busca por Título + Artista limpos
                if (candidates.Count == 0)
                {
                    var cleanTitle = _matchingEngine.CleanText(track.Title);
                    var cleanArtist = _matchingEngine.CleanText(track.Artist);
                    var searchQuery = $"{cleanTitle} {cleanArtist}".Trim();

                    if (!string.IsNullOrWhiteSpace(searchQuery))
                    {
                        candidates.AddRange(await dstProvider.SearchTracksAsync(searchQuery, 5, request.DestinationAuthToken, ct));
                    }

                    // Se ainda não achou, tenta busca pelo título e artista brutos
                    if (candidates.Count == 0)
                    {
                        var rawQuery = $"{track.Title} {track.Artist}".Trim();
                        if (!string.Equals(rawQuery, searchQuery, StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.AddRange(await dstProvider.SearchTracksAsync(rawQuery, 5, request.DestinationAuthToken, ct));
                        }
                    }

                    // Se ainda não achou, tenta apenas pelo título limpo
                    if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(cleanTitle))
                    {
                        candidates.AddRange(await dstProvider.SearchTracksAsync(cleanTitle, 5, request.DestinationAuthToken, ct));
                    }
                }

                // 2. Avalia a melhor correspondência com o MatchingEngine
                var matchResult = _matchingEngine.FindBestMatch(track, candidates);
                results.Add(matchResult);

                progress.ProcessedTracks++;
                if (matchResult.IsSuccess && matchResult.MatchedTrack != null)
                {
                    progress.MatchedTracks++;
                    matchedTracks.Add(matchResult.MatchedTrack);
                    progress.LogMessage = $"✔ Encontrada: \"{track.Title}\" ➔ \"{matchResult.MatchedTrack.Title}\" ({matchResult.ConfidenceScore * 100:F0}% confiança)";
                }
                else
                {
                    progress.FailedTracks++;
                    progress.LogMessage = $"✖ Não encontrada no destino: \"{track.Title}\" de {track.Artist}";
                }

                progress.LastMatchResult = matchResult;

                // 3. Notifica clientes via SignalR
                await _hubContext.Clients.Group(sessionId).TrackProcessed(matchResult, progress);

                // Pausa para cadência visual suave e respeitar limites de taxa (rate limits)
                await Task.Delay(250, ct);
            }

            // 4. Cria a playlist no destino
            progress.LogMessage = $"Criando playlist no {dstProvider.Name}...";
            var createdPlaylist = await dstProvider.CreatePlaylistAsync(
                request.DestinationPlaylistName,
                request.DestinationPlaylistDescription,
                matchedTracks,
                request.DestinationAuthToken,
                ct);

            _createdPlaylists[sessionId] = createdPlaylist;

            progress.Status = TransferStatus.Completed;
            progress.CreatedDestinationPlaylistUrl = createdPlaylist.ExternalUrl;
            progress.ExportDownloadUrl = $"/api/export/m3u/{sessionId}";
            progress.LogMessage = $"Transferência finalizada com sucesso! {progress.MatchedTracks} de {progress.TotalTracks} faixas adicionadas.";

            await _hubContext.Clients.Group(sessionId).TransferCompleted(progress);
        }
        catch (OperationCanceledException)
        {
            progress.Status = TransferStatus.Cancelled;
            progress.LogMessage = "Transferência cancelada.";
            await _hubContext.Clients.Group(sessionId).TransferCompleted(progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro fatal durante transferência na sessão {SessionId}", sessionId);
            progress.Status = TransferStatus.Failed;
            progress.LogMessage = $"Erro: {ex.Message}";
            await _hubContext.Clients.Group(sessionId).TransferFailed(ex.Message, progress);
        }
        finally
        {
            _runningTransfers.TryRemove(sessionId, out _);
        }
    }
}
