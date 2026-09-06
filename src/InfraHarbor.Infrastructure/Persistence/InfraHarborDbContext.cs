using InfraHarbor.Domain.Projects;
using InfraHarbor.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InfraHarbor.Infrastructure.Persistence;

public sealed class InfraHarborDbContext(DbContextOptions<InfraHarborDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectEnvironment> Environments => Set<ProjectEnvironment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.Email)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(user => user.NormalizedEmail)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(user => user.DisplayName)
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(user => user.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(user => user.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(user => user.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.HasIndex(user => user.NormalizedEmail)
                .IsUnique()
                .HasDatabaseName("EmailIndex");
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.Property(role => role.Name)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(role => role.NormalizedName)
                .HasMaxLength(256)
                .IsRequired();
        });

        builder.Entity<RefreshSession>(entity =>
        {
            entity.ToTable("RefreshSessions");
            entity.HasKey(session => session.Id);

            entity.Property(session => session.TokenHash)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(session => session.UserAgent)
                .HasMaxLength(512);

            entity.Property(session => session.IpAddress)
                .HasMaxLength(64);

            entity.HasIndex(session => session.TokenHash)
                .IsUnique();

            entity.HasIndex(session => session.FamilyId);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Project>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasKey(project => project.Id);

            entity.Property(project => project.Name)
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(project => project.Slug)
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(project => project.Description)
                .HasMaxLength(2000);

            entity.Property(project => project.IsArchived)
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(project => project.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(project => project.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.HasIndex(project => project.Slug)
                .IsUnique();
        });

        builder.Entity<ProjectEnvironment>(entity =>
        {
            entity.ToTable("Environments");
            entity.HasKey(environment => environment.Id);

            entity.Property(environment => environment.Name)
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(environment => environment.Key)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(environment => environment.SortOrder)
                .IsRequired();

            entity.Property(environment => environment.IsProduction)
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(environment => environment.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(environment => environment.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.HasIndex(environment => new { environment.ProjectId, environment.Key })
                .IsUnique();

            entity.HasIndex(environment => new { environment.ProjectId, environment.SortOrder });

            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(environment => environment.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IdentityUserLogin<Guid>>().Property(login => login.LoginProvider).HasMaxLength(128);
        builder.Entity<IdentityUserLogin<Guid>>().Property(login => login.ProviderKey).HasMaxLength(128);
        builder.Entity<IdentityUserToken<Guid>>().Property(token => token.LoginProvider).HasMaxLength(128);
        builder.Entity<IdentityUserToken<Guid>>().Property(token => token.Name).HasMaxLength(128);
    }
}
