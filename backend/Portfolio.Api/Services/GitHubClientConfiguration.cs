using System.Net.Http.Headers;
using System.Text;

namespace Portfolio.Api.Services;

public static class GitHubClientConfiguration
{
    public static void Validate(IConfiguration configuration)
    {
        var id = configuration["GitHub:ClientId"];
        var secret = configuration["GitHub:ClientSecret"];
        var hasId = !string.IsNullOrWhiteSpace(id);
        var hasSecret = !string.IsNullOrWhiteSpace(secret);
        if (hasId != hasSecret)
            throw new InvalidOperationException("Configure GitHub:ClientId e GitHub:ClientSecret juntos usando configuração segura.");
        if (hasId && !string.IsNullOrWhiteSpace(configuration["GitHub:Token"]))
            throw new InvalidOperationException("Configure apenas OAuth App ou GitHub:Token. Remova o token anterior para usar ClientId e ClientSecret.");
        if ((id?.Contains(':') ?? false) || (id?.Any(char.IsControl) ?? false) || (secret?.Any(char.IsControl) ?? false))
            throw new InvalidOperationException("Credenciais GitHub possuem formato inválido. Confira a configuração segura.");
    }

    public static void Configure(HttpClient client, IConfiguration configuration)
    {
        Validate(configuration);
        client.BaseAddress = new Uri("https://api.github.com/");
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(configuration["GitHub:UserAgent"] ?? "PortfolioCourseProject/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", configuration["GitHub:ApiVersion"] ?? "2022-11-28");
        var id = configuration["GitHub:ClientId"];
        var secret = configuration["GitHub:ClientSecret"];
        client.DefaultRequestHeaders.Authorization = !string.IsNullOrWhiteSpace(id)
            ? new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{secret}")))
            : !string.IsNullOrWhiteSpace(configuration["GitHub:Token"])
                ? new AuthenticationHeaderValue("Bearer", configuration["GitHub:Token"])
                : null;
    }
}
