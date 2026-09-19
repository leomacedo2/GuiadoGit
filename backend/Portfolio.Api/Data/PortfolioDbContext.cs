using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Portfolio.Api.Data;

public sealed class PortfolioDbContext(DbContextOptions<PortfolioDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<GitHubProfile> GitHubProfiles => Set<GitHubProfile>();
    public DbSet<Analysis> Analyses => Set<Analysis>();
    public DbSet<UserSavedProfile> UserSavedProfiles => Set<UserSavedProfile>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        // Not exposed by Supabase's default public-schema Data API.
        model.HasDefaultSchema("portfolio");
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
