using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Portfolio.Api.Data;
using Portfolio.Api.OAuth;
using Portfolio.Api.Services;

namespace Portfolio.Api.Controllers;

[ApiController, Route("api/github"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GitHubConnectionController(PortfolioDbContext db, GitHubOAuthConfiguration oauth,
    IConfiguration config, GitHubTokenProtection protection, GitHubOAuthClient client,
    UserGitHubQuotas quotas, AnalysisLocks locks, ILogger<GitHubConnectionController> logger) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static string RandomValue() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string CookieName(Guid id) => "github-correlation-" + id.ToString("N");
    private CookieOptions CookieOptions() => new() { HttpOnly = true, Secure = oauth.SecureCookie, SameSite = SameSiteMode.Lax,
        Path = "/api/github/callback", MaxAge = TimeSpan.FromMinutes(10), IsEssential = true };

    [Authorize, HttpPost("connect"), EnableRateLimiting("auth"), RequestSizeLimit(16384)]
    public async Task<IActionResult> Connect(CancellationToken ct)
    {
        if (!oauth.Enabled) return Problem(statusCode: 503, detail: "Conexão GitHub ainda não habilitada pelo administrador.");
        // HTML form navigates the popup to the backend: correlation cookie is first-party,
        // unlike Set-Cookie on a cross-site AJAX request from Vercel.
        var gate = locks.For("oauth:" + UserId);
        await gate.WaitAsync(ct);
        try
        {
            if (await db.GitHubConnections.AnyAsync(c => c.ApplicationUserId == UserId && c.InvalidatedAt == null &&
                (c.ExpiresAt == null || c.ExpiresAt > DateTimeOffset.UtcNow), ct))
                return Completion(false, "Já existe um GitHub conectado. Desconecte antes de trocar de conta.");
            var state = RandomValue();
            var correlation = RandomValue();
            var verifier = RandomValue();
            await db.GitHubOAuthAttempts.Where(a => a.ApplicationUserId == UserId || a.ExpiresAt < DateTimeOffset.UtcNow).ExecuteDeleteAsync(ct);
            var attempt = new GitHubOAuthAttempt { ApplicationUserId = UserId, StateHash = Hash(state),
                CorrelationHash = Hash(correlation), VerifierEncrypted = protection.Protect(UserId, verifier, "pkce"),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) };
            db.GitHubOAuthAttempts.Add(attempt);
            await db.SaveChangesAsync(ct);
            Response.Cookies.Append(CookieName(attempt.Id), correlation, CookieOptions());
            return Redirect(QueryHelpers.AddQueryString("https://github.com/login/oauth/authorize", new Dictionary<string, string?>
            {
                ["client_id"] = config["GitHub:ClientId"], ["redirect_uri"] = oauth.CallbackUrl, ["state"] = state,
                ["scope"] = "", ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
                ["code_challenge_method"] = "S256", ["allow_signup"] = "false"
            }));
        }
        finally { gate.Release(); }
    }

    [HttpGet("callback"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Callback([FromQuery] string? state, [FromQuery] string? code, CancellationToken ct)
    {
        if (!oauth.Enabled || state?.Length != 43) return Completion(false, "Tentativa inválida ou expirada. Inicie novamente pela plataforma.");
        var hash = Hash(state);
        var attempt = await db.GitHubOAuthAttempts.AsNoTracking().SingleOrDefaultAsync(a => a.StateHash == hash, ct);
        if (attempt is null || attempt.ExpiresAt <= DateTimeOffset.UtcNow ||
            !Request.Cookies.TryGetValue(CookieName(attempt.Id), out var cookie) || cookie.Length != 43 ||
            !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(attempt.CorrelationHash), Convert.FromHexString(Hash(cookie))))
            return Completion(false, "Tentativa inválida ou expirada. Inicie novamente pela plataforma.");

        var gate = locks.For("oauth:" + attempt.ApplicationUserId);
        await gate.WaitAsync(ct);
        try
        {
            // Atomically consume before external calls: callbacks cannot be replayed.
            var consumed = await db.GitHubOAuthAttempts.Where(a => a.Id == attempt.Id && a.ExpiresAt > DateTimeOffset.UtcNow).ExecuteDeleteAsync(ct);
            Response.Cookies.Delete(CookieName(attempt.Id), CookieOptions());
            if (consumed != 1 || string.IsNullOrWhiteSpace(code) || code.Length > 1024)
                return Completion(false, "Autorização cancelada, inválida ou já utilizada. Você pode conectar novamente.");
            var verifier = protection.Unprotect(attempt.ApplicationUserId, attempt.VerifierEncrypted, "pkce");
            var grant = await client.ExchangeAsync(code, verifier, ct);
            if (await db.GitHubConnections.AnyAsync(c => c.GitHubUserId == grant.Identity.Id && c.ApplicationUserId != attempt.ApplicationUserId, ct))
                return Completion(false, "Este GitHub já está conectado a outra conta da plataforma.");
            var existing = await db.GitHubConnections.SingleOrDefaultAsync(c => c.ApplicationUserId == attempt.ApplicationUserId, ct);
            var connection = existing ?? new GitHubConnection { ApplicationUserId = attempt.ApplicationUserId, ConnectedAt = DateTimeOffset.UtcNow };
            connection.GitHubUserId = grant.Identity.Id;
            connection.GitHubUsername = grant.Identity.Login;
            connection.AvatarUrl = grant.Identity.AvatarUrl;
            connection.AccessTokenEncrypted = protection.Protect(attempt.ApplicationUserId, grant.AccessToken);
            connection.ExpiresAt = grant.ExpiresAt;
            connection.InvalidatedAt = null;
            connection.UpdatedAt = DateTimeOffset.UtcNow;
            if (existing is null) db.GitHubConnections.Add(connection);
            await db.SaveChangesAsync(ct);
            return Completion(true, "GitHub conectado. Volte à aba da plataforma.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Completion(false, "Este GitHub já está associado a uma conta. Atualize o estado na plataforma."); }
        catch (GitHubOAuthException ex) { return Completion(false, ex.Message); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or CryptographicException or TaskCanceledException)
        {
            logger.LogWarning("Conexão OAuth não concluída. Tipo: {Type}", ex.GetType().Name);
            return Completion(false, "Não foi possível concluir a conexão. Volte à plataforma e tente novamente.");
        }
        finally { gate.Release(); }
    }

    [Authorize, HttpGet("connection")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        if (!oauth.Enabled) return Ok(new { enabled = false, connected = false });
        var connection = await db.GitHubConnections.AsNoTracking().SingleOrDefaultAsync(c => c.ApplicationUserId == UserId, ct);
        if (connection is null) return Ok(new { enabled = true, connected = false });
        return Ok(new { enabled = true, connected = true, username = connection.GitHubUsername, connection.AvatarUrl,
            connection.ConnectedAt, connection.ExpiresAt,
            requiresReconnect = connection.InvalidatedAt is not null || connection.ExpiresAt <= DateTimeOffset.UtcNow,
            rateLimit = quotas.Observed(connection.GitHubUserId) });
    }

    [Authorize, HttpDelete("connection")]
    public async Task<IActionResult> Disconnect(CancellationToken ct)
    {
        if (!oauth.Enabled) return NoContent();
        var gate = locks.For("oauth:" + UserId);
        await gate.WaitAsync(ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.GitHubOAuthAttempts.Where(a => a.ApplicationUserId == UserId).ExecuteDeleteAsync(ct);
            await db.GitHubConnections.Where(c => c.ApplicationUserId == UserId).ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
            return NoContent();
        }
        finally { gate.Release(); }
    }

    private ContentResult Completion(bool success, string message)
    {
        Response.StatusCode = success ? 200 : 400;
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var nonce = RandomValue();
        Response.Headers["Content-Security-Policy"] = $"default-src 'none'; script-src 'nonce-{nonce}'; base-uri 'none'; frame-ancestors 'none'";
        var payload = JsonSerializer.Serialize(new { type = "github-connection-complete", success, message });
        var origin = JsonSerializer.Serialize(oauth.FrontendOrigin);
        return Content($"<!doctype html><html lang=\"pt-BR\"><meta charset=\"utf-8\"><title>Conexão GitHub</title><p>{HtmlEncoder.Default.Encode(message)}</p><p>Você pode fechar esta janela e atualizar o estado na aba original.</p><script nonce=\"{nonce}\">history.replaceState(null,'','/api/github/callback');if(window.opener)window.opener.postMessage({payload},{origin});</script></html>", "text/html; charset=utf-8");
    }
}
