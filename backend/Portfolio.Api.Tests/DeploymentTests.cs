using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Portfolio.Api.Deployment;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class DeploymentTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] entries) =>
        new ConfigurationBuilder().AddInMemoryCollection(entries.ToDictionary(e => e.Key, e => e.Value)).Build();

    [Fact]
    public void RenderPortIsUsedOnlyOutsideDevelopment()
    {
        var config = Config(("PORT", "10000"));
        Assert.Equal("http://0.0.0.0:10000", DeploymentConfiguration.ListenUrl(config, false));
        Assert.Null(DeploymentConfiguration.ListenUrl(config, true));
        Assert.Null(DeploymentConfiguration.ListenUrl(Config(), false));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    public void InvalidPortFailsWithoutEchoingConfiguration(string port) =>
        Assert.Throws<InvalidOperationException>(() => DeploymentConfiguration.ListenUrl(Config(("PORT", port)), false));

    [Fact]
    public void ProductionDoesNotInheritLocalCors()
    {
        var config = Config(("Cors:AllowedOrigins:0", "http://localhost:5173"), ("Frontend:BaseUrl", "https://portfolio-example.vercel.app/"));
        Assert.Equal(["http://localhost:5173"], DeploymentConfiguration.Origins(config, true));
        Assert.Equal(["https://portfolio-example.vercel.app"], DeploymentConfiguration.Origins(config, false));
        Assert.Empty(DeploymentConfiguration.Origins(Config(), false));
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/path")]
    [InlineData("https://user:password@example.com")]
    [InlineData("*")]
    public void RejectsUnsafeProductionOrigins(string origin) =>
        Assert.Throws<InvalidOperationException>(() => DeploymentConfiguration.Origins(Config(("Frontend:BaseUrl", origin)), false));

    [Fact]
    public async Task ProductionHealthAndCorsWorkWithoutDatabaseOrGitHub()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production").UseSetting("ConnectionStrings:Portfolio", "")
                .UseSetting("Frontend:BaseUrl", "https://portfolio-example.vercel.app"));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        var status = await client.GetAsync("/api/database/status");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status.StatusCode);
        Assert.Equal(new DatabaseStatus(false, false), await status.Content.ReadFromJsonAsync<DatabaseStatus>());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/github/rate-limit")).StatusCode);
        foreach (var origin in new[] { "https://portfolio-example.vercel.app", "https://other.vercel.app", "http://localhost:5173" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api/me/profiles");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "PUT");
            request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
            var response = await client.SendAsync(request);
            Assert.Equal(origin == "https://portfolio-example.vercel.app", response.Headers.Contains("Access-Control-Allow-Origin"));
            Assert.Equal(origin == "https://portfolio-example.vercel.app", response.Headers.Contains("Access-Control-Allow-Credentials"));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me/profiles")).StatusCode);
    }
}
