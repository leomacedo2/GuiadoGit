using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Portfolio.Api.DTOs;
using Portfolio.Api.Services;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class VisitorWithoutDatabaseTests
{
    [Fact]
    public async Task VisitorStillAnalyzesWhileAccountsExplainMissingDatabase()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing").UseSetting("ConnectionStrings:Portfolio", "");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGitHubService>();
                services.AddSingleton<IGitHubService>(new FakePersistentGitHub());
            });
        });
        using var client = factory.CreateClient();
        var result = await client.GetFromJsonAsync<AnalysisDto>("/api/analyses/no-database");
        Assert.NotNull(result);
        Assert.Null(result.Id);
        Assert.Equal("github", result.Source);
        Assert.Contains("Banco não configurado", result.PersistenceWarning);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/auth/register",
            new { Nome = "Teste", Email = "test@example.test", Password = "Only-Test123!", ConfirmPassword = "Only-Test123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/auth/login",
            new { Email = "test@example.test", Password = "Only-Test123!" })).StatusCode);
    }
}
