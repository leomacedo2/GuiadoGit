namespace Portfolio.Api.Deployment;

public static class DeploymentConfiguration
{
    public static string? ListenUrl(IConfiguration config, bool development)
    {
        if (development || string.IsNullOrWhiteSpace(config["PORT"])) return null;
        if (!int.TryParse(config["PORT"], out var port) || port is < 1024 or > 65535)
            throw new InvalidOperationException("PORT deve ser uma porta entre 1024 e 65535.");
        return $"http://0.0.0.0:{port}";
    }

    public static string[] Origins(IConfiguration config, bool development)
    {
        if (development) return config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var origin = config["Frontend:BaseUrl"]?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(origin)) return []; // Backend can be deployed before Vercel exists.
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.AbsolutePath != "/" || uri.Query != "" || uri.Fragment != "" || uri.UserInfo != "")
            throw new InvalidOperationException("Frontend:BaseUrl deve ser uma origem HTTPS sem caminho, query ou credenciais.");
        return [uri.GetLeftPart(UriPartial.Authority)];
    }
}
