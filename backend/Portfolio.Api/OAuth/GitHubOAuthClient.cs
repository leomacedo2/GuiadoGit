using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Portfolio.Api.OAuth;

public sealed record GitHubAuthorizedIdentity(long Id, string Login, [property: JsonPropertyName("avatar_url")] string AvatarUrl);
public sealed record GitHubOAuthGrant(string AccessToken, DateTimeOffset? ExpiresAt, GitHubAuthorizedIdentity Identity);
public sealed class GitHubOAuthException(string message) : Exception(message);

public sealed class GitHubOAuthClient(HttpClient client, IConfiguration configuration, GitHubOAuthConfiguration oauth, UserGitHubQuotas quotas)
{
    public async Task<GitHubOAuthGrant> ExchangeAsync(string code, string verifier, CancellationToken ct)
    {
        // Separate from the public API client: no application Basic header sent to the token endpoint.
        using var exchange = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = configuration["GitHub:ClientId"]!, ["client_secret"] = configuration["GitHub:ClientSecret"]!,
                ["redirect_uri"] = oauth.CallbackUrl, ["code"] = code, ["code_verifier"] = verifier
            })
        };
        using var response = await client.SendAsync(exchange, ct);
        if (!response.IsSuccessStatusCode) throw new GitHubOAuthException("Não foi possível concluir a autorização GitHub. Tente conectar novamente.");
        using var body = await ReadJson(response, ct);
        var json = body.RootElement;
        if (!json.TryGetProperty("access_token", out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString())) throw new GitHubOAuthException("Autorização GitHub recusada ou expirada. Inicie uma nova conexão.");
        if (!json.TryGetProperty("scope", out var scope) || !string.IsNullOrWhiteSpace(scope.GetString()))
            throw new GitHubOAuthException("O GitHub retornou permissões adicionais. Revogue a autorização antiga deste OAuth App no GitHub e conecte novamente sem permissões extras.");
        if (!json.TryGetProperty("token_type", out var type) || !string.Equals(type.GetString(), "bearer", StringComparison.OrdinalIgnoreCase))
            throw new GitHubOAuthException("Tipo de autorização GitHub não suportado.");
        var token = value.GetString()!;
        DateTimeOffset? expires = null;
        if (json.TryGetProperty("expires_in", out var lifetime))
        {
            if (!lifetime.TryGetInt32(out var seconds) || seconds <= 0) throw new GitHubOAuthException("Autorização GitHub expirada.");
            expires = DateTimeOffset.UtcNow.AddSeconds(seconds);
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-GitHub-Api-Version", configuration["GitHub:ApiVersion"] ?? "2022-11-28");
        using var identityResponse = await client.SendAsync(request, ct);
        if (!identityResponse.IsSuccessStatusCode) throw new GitHubOAuthException("Não foi possível confirmar sua identidade no GitHub. Tente novamente mais tarde.");
        using var identityBody = await ReadJson(identityResponse, ct);
        var identity = identityBody.RootElement.Deserialize<GitHubAuthorizedIdentity>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (identity is null || identity.Id <= 0 || string.IsNullOrWhiteSpace(identity.Login))
            throw new GitHubOAuthException("O GitHub retornou uma identidade inválida.");
        quotas.For(identity.Id).Observe(identityResponse);
        return new(token, expires, identity);
    }

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response, CancellationToken ct)
    {
        await response.Content.LoadIntoBufferAsync(128 * 1024);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
}
