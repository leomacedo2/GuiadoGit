using Microsoft.AspNetCore.DataProtection;

namespace Portfolio.Api.OAuth;

public sealed class GitHubTokenProtection(IDataProtectionProvider provider)
{
    private IDataProtector Protector(string userId, string kind) => provider.CreateProtector("GitHubConnection", "v1", userId, kind);
    public string Protect(string userId, string value, string kind = "access-token") => Protector(userId, kind).Protect(value);
    public string Unprotect(string userId, string value, string kind = "access-token") => Protector(userId, kind).Unprotect(value);
}
