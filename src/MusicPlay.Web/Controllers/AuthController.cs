using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace MusicPlay.Web.Controllers;

[ApiController]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthController(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("api/auth/status")]
    public IActionResult GetAuthStatus()
    {
        var spotifyConfigured = !string.IsNullOrWhiteSpace(_config["Spotify:ClientId"]);
        var youtubeConfigured = !string.IsNullOrWhiteSpace(_config["YouTube:ApiKey"]);

        return Ok(new
        {
            Spotify = new
            {
                IsConfigured = spotifyConfigured,
                ClientId = spotifyConfigured ? _config["Spotify:ClientId"]![..Math.Min(6, _config["Spotify:ClientId"]!.Length)] + "..." : null
            },
            YouTube = new
            {
                IsConfigured = youtubeConfigured
            },
            Deezer = new
            {
                IsConfigured = true,
                Message = "API aberta - Pronto para uso imediato"
            },
            Demo = new
            {
                IsConfigured = true,
                Message = "Pronto para testes e demonstrações"
            }
        });
    }

    [HttpGet("api/auth/spotify/login-url")]
    public IActionResult GetSpotifyLoginUrl([FromQuery] string? redirectUri)
    {
        var clientId = _config["Spotify:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return BadRequest(new { message = "Spotify ClientId não configurado no appsettings.json" });
        }
        var redirect = redirectUri ?? _config["Spotify:RedirectUri"] ?? "http://127.0.0.1:5000/callback";
        var scopes = "playlist-read-private playlist-modify-public playlist-modify-private user-library-read";
        var state = Guid.NewGuid().ToString("N");

        var url = $"https://accounts.spotify.com/authorize?response_type=code&client_id={clientId}&scope={Uri.EscapeDataString(scopes)}&redirect_uri={Uri.EscapeDataString(redirect)}&state={state}";

        return Ok(new { url });
    }

    [HttpGet("callback")]
    public async Task<IActionResult> SpotifyCallback([FromQuery] string? code, [FromQuery] string? error, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
        {
            return Redirect("/#error=" + Uri.EscapeDataString(error ?? "Acesso negado pelo usuário"));
        }

        var clientId = _config["Spotify:ClientId"];
        var clientSecret = _config["Spotify:ClientSecret"];
        var redirectUri = _config["Spotify:RedirectUri"] ?? "http://127.0.0.1:5000/callback";

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return Redirect("/#error=" + Uri.EscapeDataString("Spotify ClientId e ClientSecret devem ser configurados no appsettings.json"));
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var postData = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", redirectUri }
            };

            var resp = await client.PostAsync("https://accounts.spotify.com/api/token", new FormUrlEncodedContent(postData), ct);
            if (!resp.IsSuccessStatusCode)
            {
                var errBody = await resp.Content.ReadAsStringAsync(ct);
                return Redirect("/#error=" + Uri.EscapeDataString("Falha ao autenticar no Spotify: " + errBody));
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("access_token", out var tokenProp))
            {
                var accessToken = tokenProp.GetString();
                return Redirect("/#access_token=" + accessToken);
            }

            return Redirect("/#error=Token_invalido");
        }
        catch (Exception ex)
        {
            return Redirect("/#error=" + Uri.EscapeDataString(ex.Message));
        }
    }
}
