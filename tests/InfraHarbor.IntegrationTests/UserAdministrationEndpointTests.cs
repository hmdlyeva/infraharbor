using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InfraHarbor.Api;
using InfraHarbor.Application;
using InfraHarbor.Application.Security;
using InfraHarbor.Infrastructure;
using InfraHarbor.Infrastructure.Identity;
using InfraHarbor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfraHarbor.IntegrationTests;

public sealed class UserAdministrationEndpointTests
{
    private const string SigningKey = "ih-user-admin-integration-test-signing-key-32-bytes";
    private const string Password = "Ih-user-admin-Strong!12345";

    [Fact]
    public async Task OwnerCanCreateAssignAndDisableUser_WithAuditAndSessionInvalidation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();
        await AuthorizeAsAsync(client, harness.App, harness.Owner.Id);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/users/",
            new
            {
                email = "operator@infraharbor.test",
                displayName = "Operations User",
                password = Password,
                roles = new[] { RoleNames.Operator }
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<UserAdministrationUser>(cancellationToken: cancellationToken);
        Assert.NotNull(created);
        Assert.Contains(RoleNames.Operator, created.Roles);

        using var rolesResponse = await client.PostAsJsonAsync(
            $"/api/users/{created.Id}/roles",
            new { roles = new[] { RoleNames.Admin } },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, rolesResponse.StatusCode);

        AuthSessionResult login;
        await using (var loginScope = harness.App.Services.CreateAsyncScope())
        {
            var sessions = loginScope.ServiceProvider.GetRequiredService<IAuthSessionService>();
            login = await sessions.LoginAsync(
                new LoginCommand(
                    created.Email,
                    Password,
                    new AuthSessionMetadata("user-admin-test", "127.0.0.1")),
                cancellationToken);
        }

        Assert.Equal(AuthSessionOutcome.Authenticated, login.Outcome);
        var managedUserAccess = harness.App.Services.GetRequiredService<JwtAccessTokenIssuer>().Issue(
            login.UserId!.Value,
            login.Email!,
            login.DisplayName!,
            login.Roles ?? [],
            login.SecurityStamp!);

        using var disableResponse = await client.PostAsync(
            $"/api/users/{created.Id}/disable",
            content: null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", managedUserAccess.Token);
        using var staleAccessResponse = await client.GetAsync("/api/users/", cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, staleAccessResponse.StatusCode);

        await using (var refreshScope = harness.App.Services.CreateAsyncScope())
        {
            var sessions = refreshScope.ServiceProvider.GetRequiredService<IAuthSessionService>();
            var refreshed = await sessions.RefreshAsync(
                new RefreshSessionCommand(
                    login.RefreshToken!,
                    new AuthSessionMetadata("user-admin-test", "127.0.0.1")),
                cancellationToken);
            Assert.Equal(AuthSessionOutcome.Rejected, refreshed.Outcome);
        }

        Assert.Contains(harness.Audit.Events, item => item.Action == SecurityAuditActions.UserCreated && item.TargetUserId == created.Id);
        Assert.Contains(harness.Audit.Events, item => item.Action == SecurityAuditActions.UserRolesChanged && item.TargetUserId == created.Id);
        Assert.Contains(harness.Audit.Events, item => item.Action == SecurityAuditActions.UserDisabled && item.TargetUserId == created.Id);
    }

    [Fact]
    public async Task AdminCannotMutateOwnerRole_OrCreateOwnerRoleUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();
        await AuthorizeAsAsync(client, harness.App, harness.Admin.Id);

        using var ownerMutation = await client.PostAsJsonAsync(
            $"/api/users/{harness.Owner.Id}/roles",
            new { roles = new[] { RoleNames.Admin } },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, ownerMutation.StatusCode);

        using var ownerCreate = await client.PostAsJsonAsync(
            "/api/users/",
            new
            {
                email = "owner-escalation@infraharbor.test",
                displayName = "Owner Escalation",
                password = Password,
                roles = new[] { RoleNames.Owner }
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, ownerCreate.StatusCode);

        Assert.Contains(
            harness.Audit.Events,
            item => item.Action == SecurityAuditActions.UserMutationRejected &&
                    item.TargetUserId == harness.Owner.Id &&
                    item.Context is not null &&
                    item.Context.TryGetValue("reason", out var reason) &&
                    reason == "owner_role_is_immutable");
    }

    [Fact]
    public async Task ViewerCannotAccessUserAdministration_AndDenialIsAudited()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();
        await AuthorizeAsAsync(client, harness.App, harness.Viewer.Id);

        using var response = await client.GetAsync("/api/users/", cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(
            harness.Audit.Events,
            item => item.Action == SecurityAuditActions.AuthorizationDenied &&
                    item.ActorUserId == harness.Viewer.Id &&
                    item.Context is not null &&
                    item.Context.TryGetValue("path", out var path) &&
                    path.StartsWith("/api/users", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RoleChangeInvalidatesExistingAccessTokenImmediately()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();

        var staleAdminToken = await IssueTokenAsync(harness.App, harness.Admin.Id);
        await AuthorizeAsAsync(client, harness.App, harness.Owner.Id);

        using var response = await client.PostAsJsonAsync(
            $"/api/users/{harness.Admin.Id}/roles",
            new { roles = new[] { RoleNames.Viewer } },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staleAdminToken);
        using var staleResponse = await client.GetAsync("/api/users/", cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, staleResponse.StatusCode);

        var freshViewerToken = await IssueTokenAsync(harness.App, harness.Admin.Id);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", freshViewerToken);
        using var freshResponse = await client.GetAsync("/api/users/", cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, freshResponse.StatusCode);
    }

    private static async Task<TestHarness> BuildHarnessAsync(CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "ConnectionStrings__Database is required for integration tests.");

        var audit = new RecordingSecurityAuditSink();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{AuthOptions.SectionName}:Issuer"] = "InfraHarbor.UserAdminTests",
            [$"{AuthOptions.SectionName}:Audience"] = "InfraHarbor.UserAdminTests",
            [$"{AuthOptions.SectionName}:SigningKey"] = SigningKey,
            [$"{AuthOptions.SectionName}:AccessTokenLifetimeSeconds"] = "900",
            [$"{AuthOptions.SectionName}:ClockSkewSeconds"] = "0"
        });

        builder.Services.AddSingleton<ISecurityAuditSink>(audit);
        builder.Services.AddOptions<AuthOptions>()
            .Bind(builder.Configuration.GetSection(AuthOptions.SectionName));
        builder.Services.AddInfraHarborPersistence(connectionString!);
        builder.Services.AddInfraHarborIdentity();
        builder.Services.AddInfraHarborAuthentication(builder.Configuration);
        builder.Services.AddInfraHarborAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapUserAdministrationEndpoints();

        ApplicationUser owner;
        ApplicationUser admin;
        ApplicationUser viewer;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InfraHarborDbContext>();
            await db.Database.EnsureDeletedAsync(cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            foreach (var role in RoleNames.All)
            {
                var created = await roleManager.CreateAsync(new ApplicationRole(role));
                Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            owner = await CreateUserAsync(userManager, "owner@infraharbor.test", "Installation Owner", RoleNames.Owner);
            admin = await CreateUserAsync(userManager, "admin@infraharbor.test", "Admin User", RoleNames.Admin);
            viewer = await CreateUserAsync(userManager, "viewer@infraharbor.test", "Viewer User", RoleNames.Viewer);
        }

        await app.StartAsync(cancellationToken);
        return new TestHarness(app, audit, owner, admin, viewer);
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string displayName,
        string role)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            DisplayName = displayName,
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var created = await userManager.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
        var assigned = await userManager.AddToRoleAsync(user, role);
        Assert.True(assigned.Succeeded, string.Join("; ", assigned.Errors.Select(error => error.Description)));
        return user;
    }

    private static async Task AuthorizeAsAsync(HttpClient client, WebApplication app, Guid userId)
    {
        var token = await IssueTokenAsync(app, userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<string> IssueTokenAsync(WebApplication app, Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        var roles = await userManager.GetRolesAsync(user);
        var securityStamp = await userManager.GetSecurityStampAsync(user);
        var issuer = app.Services.GetRequiredService<JwtAccessTokenIssuer>();
        return issuer.Issue(user.Id, user.Email!, user.DisplayName, roles.ToArray(), securityStamp).Token;
    }

    private sealed record TestHarness(
        WebApplication App,
        RecordingSecurityAuditSink Audit,
        ApplicationUser Owner,
        ApplicationUser Admin,
        ApplicationUser Viewer) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => App.DisposeAsync();
    }

    private sealed class RecordingSecurityAuditSink : ISecurityAuditSink
    {
        public ConcurrentQueue<SecurityAuditEvent> Events { get; } = new();

        public ValueTask WriteAsync(SecurityAuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Events.Enqueue(auditEvent);
            return ValueTask.CompletedTask;
        }
    }
}
