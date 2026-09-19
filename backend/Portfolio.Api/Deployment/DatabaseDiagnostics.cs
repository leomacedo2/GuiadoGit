using Microsoft.EntityFrameworkCore;
using Portfolio.Api.Data;

namespace Portfolio.Api.Deployment;

public sealed record DatabaseStatus(bool Connected, bool MigrationsApplied);

public sealed class DatabaseDiagnostics(PortfolioDbContext db, IConfiguration configuration, ILogger<DatabaseDiagnostics> logger)
{
    public async Task<DatabaseStatus> CheckAsync(CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Portfolio")) ||
                !await db.Database.CanConnectAsync(deadline.Token))
            {
                logger.LogWarning("Banco indisponível ou não configurado. Nenhuma migration executada.");
                return new(false, false);
            }
            var pending = (await db.Database.GetPendingMigrationsAsync(deadline.Token)).Count();
            logger.LogInformation("Banco disponível. Migrations pendentes: {Count}. Aplicação manual.", pending);
            return new(true, pending == 0);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Never log provider exception messages: these may contain connection details.
            logger.LogWarning("Diagnóstico do banco indisponível. Tipo: {Type}", ex.GetType().Name);
            return new(false, false);
        }
    }
}

public sealed class StartupDiagnostics(IServiceScopeFactory scopes, IHostEnvironment environment,
    ILogger<StartupDiagnostics> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // Liveness must not wait for the database.
        logger.LogInformation("API iniciada. Ambiente: {Environment}. Migrations manuais.", environment.EnvironmentName);
        if (environment.IsEnvironment("Testing")) return;
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseDiagnostics>().CheckAsync(stoppingToken);
    }
}
