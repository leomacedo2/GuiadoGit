using Portfolio.Api.Services;
using Portfolio.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Api.Deployment;
using Portfolio.Api.OAuth;

var builder = WebApplication.CreateBuilder(args);
var listenUrl = DeploymentConfiguration.ListenUrl(builder.Configuration, builder.Environment.IsDevelopment());
if (listenUrl is not null) builder.WebHost.UseUrls(listenUrl);
builder.Services.AddScoped<DatabaseDiagnostics>();
builder.Services.AddHostedService<StartupDiagnostics>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DatabaseExceptionHandler>();
builder.Services.AddDbContext<PortfolioDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Portfolio") ?? "",
    provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", "portfolio")));
builder.Services.AddSingleton(GitHubOAuthConfiguration.Configure(builder));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<GitHubRequestContext>();
builder.Services.AddScoped<GitHubTokenProtection>();
builder.Services.AddSingleton<UserGitHubQuotas>();
builder.Services.AddHttpClient<GitHubOAuthClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.MaxResponseContentBufferSize = 128 * 1024;
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(builder.Configuration["GitHub:UserAgent"] ?? "PortfolioCourseProject/1.0");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
}).AddEntityFrameworkStores<PortfolioDbContext>().AddSignInManager();
builder.Services.AddAuthentication(IdentityConstants.BearerScheme).AddBearerToken(IdentityConstants.BearerScheme,
    options =>
    {
        options.BearerTokenExpiration = TimeSpan.FromMinutes(30);
        // Only this top-level popup POST accepts the platform bearer in its form body.
        // It is still validated by the official bearer handler, never taken from the URL.
        options.Events.OnMessageReceived = async context =>
        {
            if (context.Request.Path == "/api/github/connect" && HttpMethods.IsPost(context.Request.Method) && context.Request.HasFormContentType)
            {
                if (context.Request.ContentLength is null or > 16384)
                {
                    context.Fail("Formulário de conexão inválido.");
                    return;
                }
                var form = await context.Request.ReadFormAsync(context.HttpContext.RequestAborted);
                context.Token = form["platformToken"];
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("diagnostics", options =>
    {
        options.PermitLimit = 10;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueLimit = 0;
    });
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AnalysisLocks>();
builder.Services.AddScoped<PersistentAnalysisService>();
builder.Services.AddScoped<ClassroomDashboardService>();
var cacheHours = builder.Configuration.GetValue<double>("GitHubAnalysisCacheHours", 6);
if (!double.IsFinite(cacheHours) || cacheHours <= 0 || cacheHours > 168)
    throw new InvalidOperationException("GitHubAnalysisCacheHours deve estar entre 0 (exclusivo) e 168 horas.");
builder.Services.AddScoped<AnalysisService>();
builder.Services.AddOptions<IndividualAnalysisOptions>()
    .Bind(builder.Configuration.GetSection("Analysis:Individual"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<SkillDetector>();
builder.Services.AddSingleton<RecommendationService>();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(DeploymentConfiguration.Origins(builder.Configuration, builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")))
    .WithMethods("GET", "POST", "PUT", "DELETE").AllowAnyHeader()));
GitHubClientConfiguration.Validate(builder.Configuration);
builder.Services.AddSingleton<GitHubRateLimitState>();
builder.Services.AddTransient<GitHubRateLimitHandler>();
builder.Services.AddTransient<GitHubDiagnosticsHandler>();
builder.Services.AddHttpClient<IGitHubService, GitHubService>(client =>
    GitHubClientConfiguration.Configure(client, builder.Configuration))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
    .AddHttpMessageHandler<GitHubDiagnosticsHandler>()
    .AddHttpMessageHandler<GitHubRateLimitHandler>()
    .RedactLoggedHeaders(_ => true);
var app = builder.Build();
app.Use(async (context, next) =>
{
    await next(context);
    if (context.Response.StatusCode >= 500)
        app.Logger.LogWarning("Requisição falhou: HTTP {Status}. TraceId: {TraceId}", context.Response.StatusCode, context.TraceIdentifier);
});
app.UseExceptionHandler();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/database/status", async (HttpContext context, DatabaseDiagnostics diagnostics, CancellationToken ct) =>
{
    context.Response.Headers.CacheControl = "no-store";
    var status = await diagnostics.CheckAsync(ct);
    return Results.Json(status, statusCode: status.Connected && status.MigrationsApplied ? 200 : 503);
}).RequireRateLimiting("diagnostics");
// Development-only diagnostics. This endpoint never calls GitHub.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/github/rate-limit", (GitHubRateLimitState state) => Results.Ok(state.Snapshot));
}
app.Run();
public partial class Program { }
