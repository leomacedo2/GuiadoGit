using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Portfolio.Api.Data;

namespace Portfolio.Api.OAuth;

public sealed class GitHubOAuthConfiguration
{
    public bool Enabled { get; init; }
    public string CallbackUrl { get; init; } = "";
    public string FrontendOrigin { get; init; } = "";
    public bool SecureCookie => CallbackUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public static GitHubOAuthConfiguration Configure(WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        if (!config.GetValue<bool>("GitHub:OAuth:Enabled")) return new();
        var development = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
        var callback = config["GitHub:OAuth:CallbackUrl"];
        var frontend = config["Frontend:BaseUrl"] ?? (development ? "http://localhost:5173" : null);
        static bool ValidUrl(string? value, bool local, bool callback)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "https" || local && uri.Scheme == "http" && uri.IsLoopback) &&
                uri.UserInfo == "" && uri.Query == "" && uri.Fragment == "" &&
                uri.AbsolutePath == (callback ? "/api/github/callback" : "/");
        }
        if (!ValidUrl(callback, development, true) || !ValidUrl(frontend, development, false))
            throw new InvalidOperationException("Configure as URLs válidas de callback GitHub e frontend; produção exige HTTPS.");
        if (string.IsNullOrWhiteSpace(config["GitHub:ClientId"]) || string.IsNullOrWhiteSpace(config["GitHub:ClientSecret"]) ||
            string.IsNullOrWhiteSpace(config.GetConnectionString("Portfolio")))
            throw new InvalidOperationException("Conexão GitHub exige o par OAuth App e PostgreSQL configurados no backend.");
        X509Certificate2 certificate;
        try
        {
            certificate = new X509Certificate2(Convert.FromBase64String(config["DataProtection:CertificateBase64"] ?? ""),
                config["DataProtection:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet);
            using var rsa = certificate.GetRSAPrivateKey();
            if (rsa is null || rsa.KeySize < 2048) throw new CryptographicException();
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            throw new InvalidOperationException("Configure um certificado PFX privado RSA válido para proteger as chaves persistidas. Nenhum valor foi registrado.");
        }
        builder.Services.AddDataProtection().SetApplicationName("GuiaDoGit")
            .PersistKeysToDbContext<PortfolioDbContext>().ProtectKeysWithCertificate(certificate);
        return new() { Enabled = true, CallbackUrl = callback!, FrontendOrigin = frontend!.TrimEnd('/') };
    }
}
