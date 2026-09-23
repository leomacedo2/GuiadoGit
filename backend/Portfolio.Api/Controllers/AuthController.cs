using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Api.Data;
using Portfolio.Api.Services;
using Portfolio.Api.Auth;
using Portfolio.Api.Deployment;

namespace Portfolio.Api.Controllers;

public sealed record RegisterRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Nome,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 8)] string Password,
    [Required] string ConfirmPassword);
public sealed record LoginRequest([Required, EmailAddress] string Email, [Required, StringLength(128)] string Password);

[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    PersistentAnalysisService persistence, RefreshSessionService sessions,
    IConfiguration configuration, IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (!persistence.IsConfigured) return DatabaseUnavailable();
        if (request.Password != request.ConfirmPassword) return Problem(statusCode: 400, detail: "A confirmação de senha deve ser igual à senha.");
        var user = new ApplicationUser { UserName = request.Email.Trim(), Email = request.Email.Trim(), Nome = request.Nome.Trim() };
        if (user.Nome.Length < 2) return Problem(statusCode: 400, detail: "Informe um nome com pelo menos dois caracteres.");
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return Problem(statusCode: 400, detail: "Não foi possível criar a conta. Confira o e-mail e use uma senha de 8 a 128 caracteres, com maiúscula, minúscula, número e símbolo. Se já tem conta, entre.");
        return StatusCode(201, new { user.Id, user.Nome, user.Email });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (!TrustedOrigin()) return StatusCode(403);
        if (!persistence.IsConfigured) return DatabaseUnavailable();
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !(await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)).Succeeded)
            return Problem(statusCode: 401, detail: "E-mail ou senha inválidos, ou acesso temporariamente bloqueado. Tente novamente mais tarde.");
        return Ok(await sessions.LoginAsync(user, HttpContext, HttpContext.RequestAborted));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!TrustedSessionRequest()) return StatusCode(403);
        if (!persistence.IsConfigured) return DatabaseUnavailable();
        var access = await sessions.RefreshAsync(HttpContext, ct);
        return access is null ? Unauthorized() : Ok(access);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (!TrustedSessionRequest()) return StatusCode(403);
        if (!persistence.IsConfigured) return DatabaseUnavailable();
        return await sessions.LogoutAsync(HttpContext, ct) ? NoContent() : Unauthorized();
    }

    // A non-simple header prevents form/no-cors CSRF. CORS permits its preflight only for configured origins.
    // Non-browser clients can omit Origin, but cannot bypass the custom header requirement.
    private bool TrustedSessionRequest() => Request.Headers["X-Session-Request"] == "1" && TrustedOrigin();
    private bool TrustedOrigin() => !Request.Headers.ContainsKey("Origin") ||
        DeploymentConfiguration.Origins(configuration, environment.IsDevelopment() || environment.IsEnvironment("Testing"))
            .Contains(Request.Headers.Origin.ToString(), StringComparer.Ordinal);

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await users.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(new { user.Id, user.Nome, user.Email });
    }

    private ObjectResult DatabaseUnavailable() => Problem(statusCode: 503, detail: "Configure o PostgreSQL no backend e aplique as migrations para utilizar contas e análises salvas.");
}
