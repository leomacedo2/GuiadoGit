using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Portfolio.Api.OAuth;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class GitHubOAuthConfigurationTests
{
    [Fact]
    public void HttpsCookieIsSecureRegardlessOfSchemeCasing()
        => Assert.True(new GitHubOAuthConfiguration { CallbackUrl = "HTTPS://backend.test/api/github/callback" }.SecureCookie);

    private static WebApplicationBuilder Builder(bool enabled, string callback = "https://backend.test/api/github/callback")
    {
        // Explicit non-Development environment: never loads the developer's User Secrets.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GitHub:OAuth:Enabled"] = enabled.ToString(), ["GitHub:OAuth:CallbackUrl"] = callback,
            ["Frontend:BaseUrl"] = "https://frontend.test", ["GitHub:ClientId"] = "fictional-id",
            ["GitHub:ClientSecret"] = "fictional-secret", ["ConnectionStrings:Portfolio"] = "unused-test-value"
        });
        return builder;
    }
    [Fact]
    public void DisabledFeatureDoesNotRequireCertificateOrChangeExistingAuthentication()
    {
        var builder = Builder(false);
        builder.Configuration["ConnectionStrings:Portfolio"] = "";
        Assert.False(GitHubOAuthConfiguration.Configure(builder).Enabled);
    }
    [Fact]
    public void EnabledFeatureFailsClosedWithoutPrivateCertificateAndDoesNotExposeConfiguration()
    {
        var builder = Builder(true);
        builder.Configuration["DataProtection:CertificateBase64"] = "fictional-invalid-pfx";
        var failure = Assert.Throws<InvalidOperationException>(() => GitHubOAuthConfiguration.Configure(builder));
        Assert.DoesNotContain("fictional", failure.Message);
        Assert.DoesNotContain("unused-test-value", failure.Message);
    }
    [Theory]
    [InlineData("http://backend.test/api/github/callback")]
    [InlineData("https://backend.test/wrong")]
    [InlineData("https://backend.test/api/github/callback?user=1")]
    public void ProductionCallbackMustBeHttpsAndHaveExactPathWithoutParameters(string callback)
    {
        var builder = Builder(true, callback);
        var failure = Assert.Throws<InvalidOperationException>(() => GitHubOAuthConfiguration.Configure(builder));
        Assert.Contains("URLs", failure.Message);
    }
}
