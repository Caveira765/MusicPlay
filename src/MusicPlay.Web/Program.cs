using MusicPlay.Web.Hubs;
using MusicPlay.Web.Providers;
using MusicPlay.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuração dos serviços MVC e SignalR
builder.Services.AddControllers();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// Clientes HTTP para provedores
builder.Services.AddHttpClient<DeezerProvider>(client =>
{
    client.BaseAddress = new Uri("https://api.deezer.com/");
    client.DefaultRequestHeaders.Add("User-Agent", "MusicPlay-TransferEngine/1.0");
});
builder.Services.AddHttpClient<SpotifyProvider>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "MusicPlay-TransferEngine/1.0");
});
builder.Services.AddHttpClient<YouTubeProvider>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "MusicPlay-TransferEngine/1.0");
});

// Injeção de Dependência dos Provedores e Motor de Matching
builder.Services.AddSingleton<IMatchingEngine, MatchingEngine>();
builder.Services.AddSingleton<IMusicProvider, DemoProvider>();
builder.Services.AddSingleton<IMusicProvider, DeezerProvider>();
builder.Services.AddSingleton<IMusicProvider, SpotifyProvider>();
builder.Services.AddSingleton<IMusicProvider, YouTubeProvider>();
builder.Services.AddSingleton<IMusicProvider, FileProvider>();

builder.Services.AddSingleton<IPlaylistTransferService, PlaylistTransferService>();

// Suporte a CORS caso necessário para integrações externas
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapControllers();
app.MapHub<TransferHub>("/hubs/transfer");



app.MapFallbackToFile("index.html");

app.Run();
