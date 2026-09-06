using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InfraHarbor.Api;
using InfraHarbor.Application;
using InfraHarbor.Application.Projects;
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

public sealed class ProjectEndpointTests
{
    private const string SigningKey = "ih-project-integration-test-signing-key-32-bytes";
    private const string Password = "Ih-project-Strong!12345";

    [Fact]
    public async Task AdminCanCreateReadUpdateAndArchiveProject_WithPostgresPersistence()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();
        await AuthorizeAsAsync(client, harness.App, harness.Admin.Id);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/projects/",
            new
            {
                name = "Core Platform",
                slug = "core-platform",
                description = "Primary infrastructure project"
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ProjectView>(cancellationToken: cancellationToken);
        Assert.NotNull(created);
        Assert.Equal("core-platform", created.Slug);
        Assert.False(created.IsArchived);

        using var getResponse = await client.GetAsync($"/api/projects/{created.Id}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var read = await getResponse.Content.ReadFromJsonAsync<ProjectView>(cancellationToken: cancellationToken);
        Assert.NotNull(read);
        Assert.Equal(created.Id, read.Id);

        using var updateResponse = await client.PatchAsJsonAsync(
            $"/api/projects/{created.Id}",
            new
            {
                name = "Core Control Plane",
                slug = "core-control-plane",
                description = "Updated project description"
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProjectView>(cancellationToken: cancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("Core Control Plane", updated.Name);
        Assert.Equal("core-control-plane", updated.Slug);

        using var archiveResponse = await client.PostAsync(
            $"/api/projects/{created.Id}/archive",
            content: null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);

        using var defaultListResponse = await client.GetAsync("/api/projects/", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, defaultListResponse.StatusCode);
        var defaultList = await defaultListResponse.Content.ReadFromJsonAsync<ProjectView[]>(cancellationToken: cancellationToken);
        Assert.NotNull(defaultList);
        Assert.DoesNotContain(defaultList, project => project.Id == created.Id);

        using var archivedListResponse = await client.GetAsync("/api/projects/?includeArchived=true", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, archivedListResponse.StatusCode);
        var archivedList = await archivedListResponse.Content.ReadFromJsonAsync<ProjectView[]>(cancellationToken: cancellationToken);
        Assert.NotNull(archivedList);
        Assert.Contains(archivedList, project => project.Id == created.Id && project.IsArchived);

        await using var persistenceScope = harness.App.Services.CreateAsyncScope();
        var db = persistenceScope.ServiceProvider.GetRequiredService<InfraHarborDbContext>();
        var persisted = await db.Projects.AsNoTracking().SingleAsync(
            project => project.Id == created.Id,
            cancellationToken);
        Assert.True(persisted.IsArchived);
        Assert.Equal("core-control-plane", persisted.Slug);
    }

    [Fact]
    public async Task ViewerAndOperatorCanReadProjects_ButCannotMutateHierarchy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();

        await AuthorizeAsAsync(client, harness.App, harness.Admin.Id);
        using var createResponse = await client.PostAsJsonAsync(
            "/api/projects/",
            new { name = "Read Only Project", slug = "read-only-project", description = "Authorization fixture" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ProjectView>(cancellationToken: cancellationToken);
        Assert.NotNull(created);

        await AuthorizeAsAsync(client, harness.App, harness.Viewer.Id);
        using var viewerRead = await client.GetAsync("/api/projects/", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, viewerRead.StatusCode);
        using var viewerMutation = await client.PostAsJsonAsync(
            "/api/projects/",
            new { name = "Viewer Mutation", slug = "viewer-mutation", description = "Must fail" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, viewerMutation.StatusCode);

        await AuthorizeAsAsync(client, harness.App, harness.Operator.Id);
        using var operatorRead = await client.GetAsync($"/api/projects/{created.Id}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, operatorRead.StatusCode);
        using var operatorMutation = await client.PatchAsJsonAsync(
            $"/api/projects/{created.Id}",
            new { name = "Operator Mutation" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, operatorMutation.StatusCode);
    }

    [Fact]
    public async Task ProjectValidationRejectsMalformedAndDuplicateCanonicalSlugs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();
        await AuthorizeAsAsync(client, harness.App, harness.Admin.Id);

        using var invalidResponse = await client.PostAsJsonAsync(
            "/api/projects/",
            new { name = "Invalid Project", slug = "bad slug!", description = "Invalid slug" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/projects/",
            new { name = "Infra Core", slug = "infra-core", description = "First" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/projects/",
            new { name = "Infra Core Duplicate", slug = "INFRA-CORE", description = "Duplicate" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        await using var scope = harness.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InfraHarborDbContext>();
        Assert.Equal(1, await db.Projects.CountAsync(project => project.Slug == "infra-core", cancellationToken));
    }

    private static async Task<TestHarness> BuildHarnessAsync(CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "ConnectionStrings__Database is required for integration tests.");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{AuthOptions.SectionName}:Issuer"] = "InfraHarbor.ProjectTests",
            [$"{AuthOptions.SectionName}:Audience"] = "InfraHarbor.ProjectTests",
            [$"{AuthOptions.SectionName}:SigningKey"] = SigningKey,
            [$"{AuthOptions.SectionName}:AccessTokenLifetimeSeconds"] = "900",
            [$"{AuthOptions.SectionName}:ClockSkewSeconds"] = "0"
        });

        builder.Services.AddOptions<AuthOptions>()
            .Bind(builder.Configuration.GetSection(AuthOptions.SectionName));
        builder.Services.AddInfraHarborPersistence(connectionString!);
        builder.Services.AddInfraHarborIdentity();
        builder.Services.AddInfraHarborProjects();
        builder.Services.AddInfraHarborAuthentication(builder.Configuration);
        builder.Services.AddInfraHarborAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapProjectEndpoints();

        ApplicationUser admin;
        ApplicationUser viewer;
        ApplicationUser operatorUser;
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
            admin = await CreateUserAsync(userManager, "admin@infraharbor.test", "Admin User", RoleNames.Admin);
            viewer = await CreateUserAsync(userManager, "viewer@infraharbor.test", "Viewer User", RoleNames.Viewer);
            operatorUser = await CreateUserAsync(userManager, "operator@infraharbor.test", "Operator User", RoleNames.Operator);
        }

        await app.StartAsync(cancellationToken);
        return new TestHarness(app, admin, viewer, operatorUser);
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
        await using var scope = app.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        var roles = await userManager.GetRolesAsync(user);
        var securityStamp = await userManager.GetSecurityStampAsync(user);
        var issuer = app.Services.GetRequiredService<JwtAccessTokenIssuer>();
        var token = issuer.Issue(user.Id, user.Email!, user.DisplayName, roles.ToArray(), securityStamp).Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed record TestHarness(
        WebApplication App,
        ApplicationUser Admin,
        ApplicationUser Viewer,
        ApplicationUser Operator) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => App.DisposeAsync();
    }
}
