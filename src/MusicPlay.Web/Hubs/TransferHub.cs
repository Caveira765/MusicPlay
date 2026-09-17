using Microsoft.AspNetCore.SignalR;
using MusicPlay.Web.Models;

namespace MusicPlay.Web.Hubs;

public interface ITransferClient
{
    Task TransferStarted(TransferProgressUpdate update);
    Task TrackProcessed(TrackMatchResult result, TransferProgressUpdate progress);
    Task TransferCompleted(TransferProgressUpdate update);
    Task TransferFailed(string errorMessage, TransferProgressUpdate update);
    Task LogMessage(string message);
}

public class TransferHub : Hub<ITransferClient>
{
    private readonly ILogger<TransferHub> _logger;

    public TransferHub(ILogger<TransferHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        _logger.LogInformation("Cliente {ConnectionId} conectado à sessão de transferência {SessionId}", Context.ConnectionId, sessionId);
        await Clients.Caller.LogMessage($"Conectado com sucesso ao monitor de transferência (Sessão: {sessionId[..Math.Min(8, sessionId.Length)]}...)");
    }

    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
    }
}
