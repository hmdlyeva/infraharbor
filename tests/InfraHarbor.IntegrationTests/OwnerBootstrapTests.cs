using InfraHarbor.Application;
using InfraHarbor.Application.Security;
using InfraHarbor.Infrastructure;
using InfraHarbor.Infrastructure.Identity;
using InfraHarbor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfraHarbor.IntegrationTests;

public sealed class OwnerBootstrapTests
{
    private const string BootstrapToken = "ih-ci-bootstrap-token-32-characters-minimum";

    [Fact]
    public async Task FreshInstallation_CreatesOwnerOnce_ThenRejectsSecondAttempt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider(enabled: true, BootstrapToken);
        await ResetDatabaseAsync(provider, cancellationToken);

        var first = await BootstrapAsync(
            provider,
            "first.owner@infraharbor.test",
            BootstrapToken,
            cancellationToken);

        Assert.Equal(OwnerBootstrapOutcome.Created, first.Outcome);
        Assert.NotNull(first.UserId);

        await using (var scope = provider.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var owner = await userManager.FindByIdAsync(first.UserId!.Value.ToString());
            Assert.NotNull(owner);
            Assert.True(await userManager.IsInRoleAsync(owner!, RoleNames.Owner));
        }

        var second = await BootstrapAsync(
            provider,
            "second.owner@infraharbor.test",
            BootstrapToken,
            cancellationToken);

        Assert.Equal(OwnerBootstrapOutcome.AlreadyInitialized, second.Outcome);
    }

    [Fact]
    public async Task DisabledBootstrap_IsRejectedBeforeDatabaseMutation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider(enabled: false, token: null);

        var result = await BootstrapAsync(
            provider,
            "owner@infraharbor.test",
            BootstrapToken,
            cancellationToken);

        Assert.Equal(OwnerBootstrapOutcome.Disabled, result.Outcome);
    }

    [Fact]
    public async Task InvalidBootstrapToken_IsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider(enabled: true, BootstrapToken);

        var result = await BootstrapAsync(
            provider,
            "owner@infraharbor.test",
            "wrong-bootstrap-token-that-is-not-valid",
            cancellationToken);

        Assert.Equal(OwnerBootstrapOutcome.InvalidToken, result.Outcome);
    }

    private static ServiceProvider BuildProvider(bool enabled, string? token)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "ConnectionStrings__Database is required for integration tests.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<BootstrapOptions>(options =>
        {
            options.Enabled = enabled;
            options.Token = token;
        });
        services.AddInfraHarborPersistence(connectionString!);
        services.AddInfraHarborIdentity();
        return services.BuildServiceProvider();
    }

    private static async Task ResetDatabaseAsync(ServiceProvider provider, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InfraHarborDbContext>();
        await db.Database.EnsureDeletedAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }

    private static async Task<OwnerBootstrapResult> BootstrapAsync(
        ServiceProvider provider,
        string email,
        string token,
        CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IOwnerBootstrapService>();
        return await service.BootstrapAsync(
            new OwnerBootstrapCommand(
                email,
                "InfraHarbor Owner",
                "Ih-bootstrap-Strong!12345",
                token),
            cancellationToken);
    }
}
