using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Api.Data;
using Portfolio.Api.DTOs;
using Xunit;

namespace Portfolio.Api.Tests;

public sealed class ClassroomTests(PersistenceFixture fixture) : IClassFixture<PersistenceFixture>
{
    private static long nextId = 50000;
    private async Task<HttpClient> Account()
    {
        var client = fixture.CreateClient();
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await users.CreateAsync(new ApplicationUser { UserName = email, Email = email, Nome = "Teste" }, "Tests-Only123!")).Succeeded);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Tests-Only123!" });
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        return client;
    }
    private async Task<string> UserId(HttpClient client) => (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetString()!;
    private async Task<(Guid ProfileId, Guid AnalysisId)> Seed(HttpClient client, string name = "Aluno", bool save = true, bool expired = false, int repositories = 3, bool pushed = true)
    {
        var id = Interlocked.Increment(ref nextId);
        var now = DateTimeOffset.UtcNow;
        var repos = Enumerable.Range(0, repositories).Select(i => new RepositoryDto(id * 10 + i, $"repo{i}", null, "JavaScript", "", now)
            { PushedAt = pushed ? now.AddMonths(-i) : null }).ToList();
        var profile = new GitHubProfile { GitHubId = id, Username = $"student-{id}", Nome = name, AvatarUrl = "", UpdatedAt = now };
        var evidence = repos.Select(r => new EvidenceDto(r.Id, r.Name, "", "Manifesto de teste")).ToList();
        var dto = new AnalysisDto(new(profile.Username, name, null, "", "", repos.Count, repos), repos.Count, repos.Count, 0, false, [],
            [new("React", "Frontend", "Boa evidência", repos.Count, evidence), new("JavaScript", "Linguagens", "Boa evidência", repos.Count, evidence), new("TypeScript", "Linguagens", "Pouca evidência", 1, [evidence[0]])], []);
        if (pushed)
        {
            var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-11);
            dto = dto with { CommitActivity = new(Enumerable.Range(0, 12).Select(i => start.AddMonths(i).ToString("yyyy-MM")).ToArray(),
                [new("React", Enumerable.Range(0, 12).Select(i => (int?)(i == 11 ? repositories * 10 : 0)).ToArray())],
                start, start.AddMonths(12), now, false, repositories, repositories, repositories, 1, 100, 25, 10, [], []) };
        }
        var snapshot = AnalysisSnapshot.Create(dto, profile, expired ? now.AddDays(-9) : now, 6);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        db.Analyses.Add(snapshot); await db.SaveChangesAsync();
        if (save) (await client.PutAsync($"/api/me/profiles/{snapshot.Id}", null)).EnsureSuccessStatusCode();
        return (profile.Id, snapshot.Id);
    }
    private static async Task<Guid> Create(HttpClient client, string name, params Guid[] ids)
    {
        var response = await client.PostAsJsonAsync("/api/classes", new { name, profileIds = ids });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private static Task<ClassroomDashboardDto?> Dashboard(HttpClient client, Guid id) => client.GetFromJsonAsync<ClassroomDashboardDto>($"/api/classes/{id}");

    [Fact]
    public async Task AllClassroomEndpointsRequireAuthentication()
    {
        using var client = fixture.CreateClient(); var id = Guid.NewGuid();
        var requests = new[] { new HttpRequestMessage(HttpMethod.Get, "/api/classes"), new(HttpMethod.Get, $"/api/classes/{id}"),
            new(HttpMethod.Post, "/api/classes"), new(HttpMethod.Put, $"/api/classes/{id}"), new(HttpMethod.Delete, $"/api/classes/{id}"),
            new(HttpMethod.Post, $"/api/classes/{id}/members"), new(HttpMethod.Delete, $"/api/classes/{id}/members/{Guid.NewGuid()}") };
        foreach (var request in requests) Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task CreatesMultipleClassesAndTrimsNameWithoutTrustingOwnerInBody()
    {
        using var client = await Account(); var profile = await Seed(client);
        var first = await Create(client, "  Python Manhã  ", profile.ProfileId);
        await Create(client, "React Noite", profile.ProfileId);
        var response = await client.PostAsJsonAsync("/api/classes", new { name = "Turma A", profileIds = new[] { profile.ProfileId }, applicationUserId = "someone-else" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Python Manhã", (await Dashboard(client, first))!.Name);
        Assert.Equal(3, (await client.GetFromJsonAsync<List<ClassroomSummaryDto>>("/api/classes"))!.Count);
    }

    [Fact]
    public async Task OwnershipIsEnforcedForEveryOperation()
    {
        using var owner = await Account(); using var stranger = await Account();
        var profile = await Seed(owner); var id = await Create(owner, "Privada", profile.ProfileId);
        Assert.Empty((await stranger.GetFromJsonAsync<List<ClassroomSummaryDto>>("/api/classes"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/classes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync($"/api/classes/{id}", new { name = "Invadida" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"/api/classes/{id}/members", new { profileIds = new[] { profile.ProfileId } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/classes/{id}/members/{profile.ProfileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/classes/{id}")).StatusCode);
        Assert.Equal("Privada", (await Dashboard(owner, id))!.Name);
    }

    [Theory]
    [InlineData("")][InlineData("a")][InlineData("  ")][InlineData("x\ny")]
    public async Task InvalidNamesAndEmptyMembershipAreRejected(string name)
    {
        using var client = await Account(); var profile = await Seed(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/classes", new { name, profileIds = new[] { profile.ProfileId } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/classes", new { name = "Turma", profileIds = Array.Empty<Guid>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/classes", new { name = new string('a', 101), profileIds = new[] { profile.ProfileId } })).StatusCode);
    }

    [Fact]
    public async Task ArbitraryAndOtherUsersProfilesCannotBeAdded()
    {
        using var client = await Account(); using var other = await Account();
        var owned = await Seed(client); var foreign = await Seed(other); var notSaved = await Seed(client, save: false);
        var id = await Create(client, "Minha turma", owned.ProfileId);
        foreach (var invalid in new[] { Guid.NewGuid(), foreign.ProfileId, notSaved.ProfileId })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/classes", new { name = "Outra", profileIds = new[] { invalid } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/classes/{id}/members", new { profileIds = new[] { invalid } })).StatusCode);
        }
        Assert.Single((await Dashboard(client, id))!.Members);
    }

    [Fact]
    public async Task DuplicateMembersArePreventedButProfilesCanBelongToMultipleOwnersAndClasses()
    {
        using var client = await Account(); using var other = await Account(); var profile = await Seed(client);
        var id = await Create(client, "Primeira", profile.ProfileId, profile.ProfileId);
        Assert.Single((await Dashboard(client, id))!.Members);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/classes/{id}/members", new { profileIds = new[] { profile.ProfileId } })).StatusCode);
        await Create(client, "Segunda", profile.ProfileId);
        (await other.PutAsync($"/api/me/profiles/{profile.AnalysisId}", null)).EnsureSuccessStatusCode();
        var otherId = await Create(other, "De outra conta", profile.ProfileId);
        Assert.Single((await Dashboard(other, otherId))!.Members);
    }

    [Fact]
    public async Task SavingListRemovalNamesOnlyOwnersClassesAndIsUnblockedAfterTheirDeletion()
    {
        using var client = await Account(); using var other = await Account(); var profile = await Seed(client);
        var id = await Create(client, "Minha turma", profile.ProfileId);
        (await other.PutAsync($"/api/me/profiles/{profile.AnalysisId}", null)).EnsureSuccessStatusCode();
        await Create(other, "Turma secreta de outra conta", profile.ProfileId);
        var response = await client.DeleteAsync($"/api/me/profiles/{profile.ProfileId}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var detail = await response.Content.ReadAsStringAsync();
        Assert.Contains("Minha turma", detail); Assert.DoesNotContain("secreta", detail);
        (await client.DeleteAsync($"/api/classes/{id}")).EnsureSuccessStatusCode();
        (await client.DeleteAsync($"/api/me/profiles/{profile.ProfileId}")).EnsureSuccessStatusCode();
        Assert.Single((await other.GetFromJsonAsync<List<ClassroomSummaryDto>>("/api/classes"))!);
    }

    [Fact]
    public async Task RemovingMembershipAndDeletingClassPreserveAllUnderlyingData()
    {
        using var client = await Account(); var first = await Seed(client); var second = await Seed(client);
        var id = await Create(client, "Preservada", first.ProfileId, second.ProfileId);
        (await client.DeleteAsync($"/api/classes/{id}/members/{first.ProfileId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/classes/{id}/members/{second.ProfileId}")).StatusCode);
        (await client.DeleteAsync($"/api/classes/{id}")).EnsureSuccessStatusCode();
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        Assert.False(await db.ClassroomMembers.AnyAsync(m => m.ClassroomId == id));
        Assert.Equal(2, await db.GitHubProfiles.CountAsync(p => p.Id == first.ProfileId || p.Id == second.ProfileId));
        Assert.Equal(2, await db.Analyses.CountAsync(a => a.Id == first.AnalysisId || a.Id == second.AnalysisId));
        Assert.Equal(2, (await client.GetFromJsonAsync<JsonElement>("/api/me/profiles")).GetArrayLength());
    }

    [Fact]
    public async Task DashboardDeduplicatesStudentsAndUsesExpiredPersistedActivityWithoutGitHub()
    {
        using var client = await Account(); var first = await Seed(client, "Zeca", expired: true, repositories: 7); var second = await Seed(client, "Ana", repositories: 1);
        var id = await Create(client, "Tecnologias", first.ProfileId, second.ProfileId);
        var calls = fixture.GitHub.RequestCount;
        var result = (await Dashboard(client, id))!;
        Assert.Equal(2, result.Technologies.Single(t => t.Label == "React").Count);
        Assert.Equal(2, result.Categories.Single(t => t.Label == "Linguagens").Count);
        Assert.Equal("Ana", result.Members[0].Name);
        Assert.True(result.Members.Single(m => m.Name == "Zeca").IsExpired);
        Assert.Equal(80, result.CommitActivity.Series.Single(s => s.Name == "React").Counts.Sum(n => n ?? 0));
        var open = await client.GetFromJsonAsync<AnalysisDto>($"/api/me/profiles/{first.ProfileId}/analysis");
        Assert.Equal(70, open!.CommitActivity!.Series.Single(s => s.Name == "React").Counts.Sum(n => n ?? 0));
        Assert.NotNull(open.Profile.Repositories[0].PushedAt);
        Assert.Equal(calls, fixture.GitHub.RequestCount);
    }

    [Fact]
    public async Task MissingSnapshotAndOldSnapshotsWithoutPushRemainUsable()
    {
        using var client = await Account(); var old = await Seed(client, pushed: false); var missing = await Seed(client);
        var id = await Create(client, "Legado", old.ProfileId, missing.ProfileId);
        using (var scope = fixture.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().Analyses.Where(a => a.Id == missing.AnalysisId).ExecuteDeleteAsync();
        var result = (await Dashboard(client, id))!;
        Assert.Null(result.Members.Single(m => m.GitHubProfileId == missing.ProfileId).AnalyzedAt);
        Assert.Equal(1, result.Technologies.Single(t => t.Label == "React").Count);
        Assert.Equal(2, result.CommitActivity.MissingStudents);
        Assert.Empty(result.CommitActivity.Series);
    }

    [Fact]
    public async Task LatestSnapshotWinsAndClassesAreOrderedByTheirLastEdit()
    {
        using var client = await Account(); var profile = await Seed(client);
        var first = await Create(client, "Mais antiga", profile.ProfileId); var second = await Create(client, "Mais recente", profile.ProfileId);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
            await db.Classrooms.Where(c => c.Id == first).ExecuteUpdateAsync(u => u.SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow.AddDays(-1)));
            var original = await db.Analyses.Include(a => a.GitHubProfile).SingleAsync(a => a.Id == profile.AnalysisId);
            db.Analyses.Add(new Analysis { GitHubProfile = original.GitHubProfile, AnalyzedAt = original.AnalyzedAt.AddHours(1), ExpiresAt = original.ExpiresAt.AddHours(1), IsComplete = false, MetadataJson = original.MetadataJson,
                Skills = [new() { Name = "Python", Category = "Linguagens", RepositoryCount = 1 }] });
            await db.SaveChangesAsync();
        }
        Assert.Equal(second, (await client.GetFromJsonAsync<List<ClassroomSummaryDto>>("/api/classes"))![0].Id);
        Assert.Equal("Python", Assert.Single((await Dashboard(client, first))!.Technologies).Label);
        (await client.PutAsJsonAsync($"/api/classes/{first}", new { name = "Renomeada" })).EnsureSuccessStatusCode();
        Assert.Equal(first, (await client.GetFromJsonAsync<List<ClassroomSummaryDto>>("/api/classes"))![0].Id);
    }

    [Fact]
    public async Task ConcurrentRemovalsCannotLeaveClassEmpty()
    {
        using var client = await Account(); var first = await Seed(client); var second = await Seed(client);
        var id = await Create(client, "Concorrência", first.ProfileId, second.ProfileId);
        var responses = await Task.WhenAll(client.DeleteAsync($"/api/classes/{id}/members/{first.ProfileId}"), client.DeleteAsync($"/api/classes/{id}/members/{second.ProfileId}"));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Single((await Dashboard(client, id))!.Members);
    }

    [Fact]
    public async Task ConcurrentAddAndUnsaveCannotLeaveDanglingMember()
    {
        using var client = await Account(); var first = await Seed(client); var second = await Seed(client);
        var id = await Create(client, "Concorrência", first.ProfileId);
        var responses = await Task.WhenAll(client.PostAsJsonAsync($"/api/classes/{id}/members", new { profileIds = new[] { second.ProfileId } }), client.DeleteAsync($"/api/me/profiles/{second.ProfileId}"));
        Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.Conflict }));
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        var member = await db.ClassroomMembers.AnyAsync(m => m.ClassroomId == id && m.GitHubProfileId == second.ProfileId);
        var user = await UserId(client);
        Assert.True(!member || await db.UserSavedProfiles.AnyAsync(s => s.UserId == user && s.GitHubProfileId == second.ProfileId));
    }

    [Fact]
    public async Task DatabaseConstraintsPreventBypassingSavedProfileAndOwnerValidation()
    {
        using var owner = await Account(); using var other = await Account();
        var owned = await Seed(owner); var foreign = await Seed(other); var id = await Create(owner, "Protegida", owned.ProfileId);
        using var scope = fixture.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        db.ClassroomMembers.Add(new() { ClassroomId = id, ApplicationUserId = await UserId(other), GitHubProfileId = foreign.ProfileId, AddedAt = DateTimeOffset.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.ClassroomMembers.Add(new() { ClassroomId = id, ApplicationUserId = await UserId(owner), GitHubProfileId = foreign.ProfileId, AddedAt = DateTimeOffset.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.UserSavedProfiles.Where(s => s.GitHubProfileId == owned.ProfileId).ExecuteDeleteAsync());
    }
}
