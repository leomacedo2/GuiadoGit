using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Portfolio.Api.Data;

namespace Portfolio.Api.Auth;

// Refresh secrets never leave this service except as an HttpOnly Set-Cookie header.
public sealed class RefreshSessionService(PortfolioDbContext db, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, IOptionsMonitor<BearerTokenOptions> bearer, TimeProvider clock,
    IWebHostEnvironment environment)
{
    public const string CookieName = "guidogit-refresh";
    public const string SessionClaim = "guidogit:session";
    public const int LifetimeDays = 30;
    private static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    private static string? Hash(string? token) => token is { Length: 43 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
        ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) : null;

    public async Task<PlatformAccessToken> LoginAsync(ApplicationUser user, HttpContext context, CancellationToken ct)
    {
        // A new login in this browser replaces its previous refresh session, not other devices.
        await RevokeCookieAsync(context.Request.Cookies[CookieName], ct);
        var token = NewToken();
        var session = new RefreshSession
        {
            ApplicationUserId = user.Id, TokenHash = Hash(token)!, SecurityStamp = await users.GetSecurityStampAsync(user),
            CreatedAt = clock.GetUtcNow(), ExpiresAt = clock.GetUtcNow().AddDays(LifetimeDays)
        };
        db.Add(session);
        await db.SaveChangesAsync(ct);
        var access = await IssueAccessAsync(user, session.Id);
        SetCookie(context, token, session.ExpiresAt);
        return access;
    }

    public async Task<PlatformAccessToken?> RefreshAsync(HttpContext context, CancellationToken ct)
    {
        var hash = Hash(context.Request.Cookies[CookieName]);
        if (hash is null) return null;
        var now = clock.GetUtcNow();
        var session = await db.RefreshSessions.AsNoTracking().Include(s => s.ApplicationUser)
            .SingleOrDefaultAsync(s => s.TokenHash == hash && s.RevokedAt == null && s.ExpiresAt > now, ct);
        if (session is null || context.User.Identity?.IsAuthenticated == true && users.GetUserId(context.User) != session.ApplicationUserId)
            return null;
        var user = session.ApplicationUser;
        if (session.SecurityStamp != await users.GetSecurityStampAsync(user) || !await signIn.CanSignInAsync(user) || await users.IsLockedOutAsync(user))
            return null;
        var access = await IssueAccessAsync(user, session.Id);
        var token = NewToken();
        var nextHash = Hash(token)!;
        // Compare-and-swap in PostgreSQL: an old token can win only once, even across server instances.
        var changed = await db.RefreshSessions.Where(s => s.Id == session.Id && s.TokenHash == hash && s.RevokedAt == null && s.ExpiresAt > now)
            .ExecuteUpdateAsync(update => update.SetProperty(s => s.TokenHash, nextHash), ct);
        if (changed != 1) return null;
        SetCookie(context, token, session.ExpiresAt); // Absolute expiry: rotation does not prolong the 30 days.
        return access;
    }

    public async Task<bool> LogoutAsync(HttpContext context, CancellationToken ct)
    {
        var actor = context.User.Identity?.IsAuthenticated == true ? users.GetUserId(context.User) : null;
        var hash = Hash(context.Request.Cookies[CookieName]);
        if (hash is not null)
        {
            var owner = await db.RefreshSessions.Where(s => s.TokenHash == hash).Select(s => s.ApplicationUserId).SingleOrDefaultAsync(ct);
            if (owner is not null && actor is not null && owner != actor) return false;
            await RevokeCookieAsync(context.Request.Cookies[CookieName], ct);
        }
        // Also revoke the bearer-bound session if a concurrent refresh already rotated the cookie.
        if (actor is not null && Guid.TryParse(context.User.FindFirstValue(SessionClaim), out var id))
            await db.RefreshSessions.Where(s => s.Id == id && s.ApplicationUserId == actor && s.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(s => s.RevokedAt, clock.GetUtcNow()), ct);
        context.Response.Cookies.Delete(CookieName, CookieOptions());
        return true;
    }

    private async Task RevokeCookieAsync(string? token, CancellationToken ct)
    {
        var hash = Hash(token);
        if (hash is null) return;
        await db.RefreshSessions.Where(s => s.TokenHash == hash && s.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(s => s.RevokedAt, clock.GetUtcNow()), ct);
    }

    private async Task<PlatformAccessToken> IssueAccessAsync(ApplicationUser user, Guid sessionId)
    {
        var principal = await signIn.CreateUserPrincipalAsync(user);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(SessionClaim, sessionId.ToString()));
        var options = bearer.Get(IdentityConstants.BearerScheme);
        var properties = new AuthenticationProperties { ExpiresUtc = clock.GetUtcNow() + options.BearerTokenExpiration };
        // Same official protector and ticket format as BearerTokenHandler; no JWT or secondary refresh token in JSON.
        var ticket = new AuthenticationTicket(principal, properties, $"{IdentityConstants.BearerScheme}:AccessToken");
        return new("Bearer", options.BearerTokenProtector.Protect(ticket), (long)options.BearerTokenExpiration.TotalSeconds);
    }

    private CookieOptions CookieOptions() => new()
    {
        HttpOnly = true, Secure = !environment.IsDevelopment() && !environment.IsEnvironment("Testing"),
        SameSite = environment.IsDevelopment() || environment.IsEnvironment("Testing") ? SameSiteMode.Lax : SameSiteMode.None,
        Path = "/api/auth", IsEssential = true
    };
    private void SetCookie(HttpContext context, string token, DateTimeOffset expires)
    {
        var options = CookieOptions();
        options.Expires = expires;
        context.Response.Cookies.Append(CookieName, token, options);
    }
}

public sealed record PlatformAccessToken(string TokenType, string AccessToken, long ExpiresIn);
