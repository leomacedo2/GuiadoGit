using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace Portfolio.Api.Data;

public sealed class PortfolioDbContext(DbContextOptions<PortfolioDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<GitHubConnection> GitHubConnections => Set<GitHubConnection>();
    public DbSet<GitHubOAuthAttempt> GitHubOAuthAttempts => Set<GitHubOAuthAttempt>();
    public DbSet<GitHubProfile> GitHubProfiles => Set<GitHubProfile>();
    public DbSet<Analysis> Analyses => Set<Analysis>();
    public DbSet<UserSavedProfile> UserSavedProfiles => Set<UserSavedProfile>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        // Not exposed by Supabase's default public-schema Data API.
        model.HasDefaultSchema("portfolio");
        model.Entity<GitHubConnection>().HasIndex(x => x.ApplicationUserId).IsUnique();
        model.Entity<GitHubConnection>().HasIndex(x => x.GitHubUserId).IsUnique();
        model.Entity<GitHubConnection>().HasOne(x => x.ApplicationUser).WithOne().HasForeignKey<GitHubConnection>(x => x.ApplicationUserId);
        model.Entity<GitHubConnection>().Property(x => x.GitHubUsername).HasMaxLength(39);
        model.Entity<GitHubOAuthAttempt>().HasIndex(x => x.StateHash).IsUnique();
        model.Entity<GitHubOAuthAttempt>().HasIndex(x => x.ExpiresAt);
        model.Entity<GitHubOAuthAttempt>().Property(x => x.StateHash).HasMaxLength(64);
        model.Entity<GitHubOAuthAttempt>().Property(x => x.CorrelationHash).HasMaxLength(64);
        model.Entity<GitHubOAuthAttempt>().HasOne(x => x.ApplicationUser).WithMany().HasForeignKey(x => x.ApplicationUserId);
        model.Entity<ApplicationUser>().Property(x => x.Nome).HasMaxLength(120);
        model.Entity<ApplicationUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
        model.Entity<GitHubProfile>().Property(x => x.Username).HasMaxLength(39);
        model.Entity<GitHubProfile>().HasIndex(x => x.Username).IsUnique();
        model.Entity<GitHubProfile>().HasIndex(x => x.GitHubId).IsUnique();
        model.Entity<Analysis>().HasIndex(x => new { x.GitHubProfileId, x.AnalyzedAt });
        model.Entity<Analysis>().Property(x => x.MetadataJson).HasColumnType("jsonb");
        model.Entity<Analysis>().HasMany(x => x.Repositories).WithOne().HasForeignKey(x => x.AnalysisId);
        model.Entity<Analysis>().HasMany(x => x.Skills).WithOne().HasForeignKey(x => x.AnalysisId);
        model.Entity<Analysis>().HasMany(x => x.Recommendations).WithOne().HasForeignKey(x => x.AnalysisId);
        model.Entity<RepositoryAnalysis>().HasKey(x => new { x.AnalysisId, x.RepositoryId });
        model.Entity<SkillAnalysis>().HasMany(x => x.Evidence).WithOne().HasForeignKey(x => x.SkillAnalysisId);
        model.Entity<Recommendation>().Property(x => x.ConsideredSkillsJson).HasColumnType("jsonb");
        model.Entity<UserSavedProfile>().HasKey(x => new { x.UserId, x.GitHubProfileId });
        model.Entity<UserSavedProfile>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        model.Entity<UserSavedProfile>().HasOne(x => x.GitHubProfile).WithMany().HasForeignKey(x => x.GitHubProfileId);
    }
}
