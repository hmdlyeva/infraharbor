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

public sealed class AuthSessionTests
{
    private const string Email = "session.owner@infraharbor.test";
    private const string Password = "Ih-session-Strong!12345";

    [Fact]
    public async Task ValidCredentials_CreateHashedRefreshSession_WithMetadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider();
        await ResetDatabaseAndCreateUserAsync(provider, cancellationToken);

        var result = await LoginAsync(provider, cancellationToken);

        Assert.Equal(AuthSessionOutcome.Authenticated, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InfraHarborDbContext>();
        var stored = await db.RefreshSessions.SingleAsync(cancellationToken);

        Assert.NotEqual(result.RefreshToken, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.Equal("InfraHarbor integration test", stored.UserAgent);
        Assert.Equal("127.0.0.1", stored.IpAddress);
        Assert.Null(stored.UsedAt);
        Assert.Null(stored.RevokedAt);
    }

    [Fact]
    public async Task ValidRefresh_RotatesWithoutAccessToken_AndOldTokenReuseRevokesFamily()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider();
        await ResetDatabaseAndCreateUserAsync(provider, cancellationToken);

        var login = await LoginAsync(provider, cancellationToken);
        Assert.Equal(AuthSessionOutcome.Authenticated, login.Outcome);

        var rotated = await RefreshAsync(provider, login.RefreshToken!, cancellationToken);
        Assert.Equal(AuthSessionOutcome.Authenticated, rotated.Outcome);
        Assert.NotEqual(login.RefreshToken, rotated.RefreshToken);

        var reuse = await RefreshAsync(provider, login.RefreshToken!, cancellationToken);
        Assert.Equal(AuthSessionOutcome.Rejected, reuse.Outcome);

        var rotatedAfterReuse = await RefreshAsync(provider, rotated.RefreshToken!, cancellationToken);
        Assert.Equal(AuthSessionOutcome.Rejected, rotatedAfterReuse.Outcome);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InfraHarborDbContext>();
        var sessions = await db.RefreshSessions.OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);

        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, item => Assert.NotNull(item.RevokedAt));
        Assert.NotNull(sessions[0].UsedAt);
        Assert.Equal(sessions[1].Id, sessions[0].ReplacedBySessionId);
    }

    [Fact]
    public async Task LogoutRevocation_RejectsRefreshToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider();
        await ResetDatabaseAndCreateUserAsync(provider, cancellationToken);

        var login = await LoginAsync(provider, cancellationToken);
        Assert.Equal(AuthSessionOutcome.Authenticated, login.Outcome);

        await using (var scope = provider.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IAuthSessionService>();
            await service.RevokeAsync(login.RefreshToken!, cancellationToken);
        }

        var refresh = await RefreshAsync(provider, login.RefreshToken!, cancellationToken);
        Assert.Equal(AuthSessionOutcome.Rejected, refresh.Outcome);
    }

    [Fact]
    public async Task InvalidCredentials_DoNotCreateSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = BuildProvider();
        await ResetDatabaseAndCreateUserAsync(provider, cancellationToken);

        AuthSessionResult result;
        await using (var scope = provider.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IAuthSessionService>();
            result = await service.LoginAsync(
                new LoginCommand(
                    Email,
                    "definitely-wrong-password",
                    new AuthSessionMetadata("InfraHarbor integration test", "127.0.0.1")),
                cancellationToken);
        }

        Assert.Equal(AuthSessionOutcome.Rejected, result.Outcome);

        await using var verificationScope = provider.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<InfraHarborDbContext>();
        Assert.False(await db.RefreshSessions.AnyAsync(cancellationToken));
    }

    private static ServiceProvider BuildProvider()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "ConnectionStrings__Database is required for integration tests.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<AuthOptions>(options =>
        {
            options.Issuer = "InfraHarbor.Tests";
            options.Audience = "InfraHarbor.Tests";
            options.SigningKey = "integration-test-signing-key-32-bytes-minimum";
            options.RefreshTokenLifetimeDays = 30;
        });
        services.AddInfraHarborPersistence(connectionString!);
        services.AddInfraHarborIdentity();
        return services.BuildServiceProvider();
    }

    private static async Task ResetDatabaseAndCreateUserAsync(
        ServiceProvider provider,
        CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InfraHarborDbContext>();
        await db.Database.EnsureDeletedAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var now = DateTimeOffset.UtcNow;
        var user = new ApplicationUser
        {
            Email = Email,
            UserName = Email,
            DisplayName = "Session Owner",
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var create = await userManager.CreateAsync(user, Password);
        Assert.True(create.Succeeded, string.Join("; ", create.Errors.Select(error => error.Description)));
    }

    private static async Task<AuthSessionResult> LoginAsync(
        ServiceProvider provider,
        CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuthSessionService>();
        return await service.LoginAsync(
            new LoginCommand(
                Email,
                Password,
                new AuthSessionMetadata("InfraHarbor integration test", "127.0.0.1")),
            cancellationToken);
    }

    private static async Task<AuthSessionResult> RefreshAsync(
        ServiceProvider provider,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuthSessionService>();
        return await service.RefreshAsync(
            new RefreshSessionCommand(
                refreshToken,
                new AuthSessionMetadata("InfraHarbor integration test", "127.0.0.1")),
            cancellationToken);
    }
}
