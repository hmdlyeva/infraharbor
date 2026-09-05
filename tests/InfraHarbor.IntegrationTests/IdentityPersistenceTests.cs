using InfraHarbor.Application.Security;
using InfraHarbor.Infrastructure;
using InfraHarbor.Infrastructure.Identity;
using InfraHarbor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfraHarbor.IntegrationTests;

public sealed class IdentityPersistenceTests
{
    [Fact]
    public async Task InitialMigration_AllowsCreatingUserAndRoleOnPostgres()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "ConnectionStrings__Database is required for integration tests.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfraHarborPersistence(connectionString!);
        services.AddInfraHarborIdentity();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<InfraHarborDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var roleResult = await roleManager.CreateAsync(new ApplicationRole(RoleNames.Owner));
        Assert.True(roleResult.Succeeded, FormatErrors(roleResult));

        const string email = "owner.integration@infraharbor.test";
        const string password = "Ih-ci-Strong!12345";
        var now = DateTimeOffset.UtcNow;
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Integration Owner",
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var createResult = await userManager.CreateAsync(user, password);
        Assert.True(createResult.Succeeded, FormatErrors(createResult));
        Assert.NotNull(user.PasswordHash);
        Assert.NotEqual(password, user.PasswordHash);

        var roleAssignment = await userManager.AddToRoleAsync(user, RoleNames.Owner);
        Assert.True(roleAssignment.Succeeded, FormatErrors(roleAssignment));
        Assert.True(await userManager.IsInRoleAsync(user, RoleNames.Owner));

        var persisted = await db.Users.SingleAsync(candidate => candidate.NormalizedEmail == email.ToUpperInvariant());
        Assert.Equal("Integration Owner", persisted.DisplayName);
        Assert.Equal(UserStatus.Active, persisted.Status);
    }

    private static string FormatErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
}
