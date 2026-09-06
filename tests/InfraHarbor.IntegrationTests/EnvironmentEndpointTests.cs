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

public sealed class EnvironmentEndpointTests
{
    private const string SigningKey = "ih-environment-integration-signing-key-32-bytes";
    private const string Password = "Ih-environment-Strong!12345";

    [Fact]
    public async Task ProjectCreationSeedsDocumentedDefaultEnvironments()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();
        await AuthorizeAsAsync(client, harness.App, harness.Admin.Id);

        var project = await CreateProjectAsync(client, "Defaulted Project", "defaulted-project", cancellationToken);

        using var response = await client.GetAsync($"/api/projects/{project.Id}/environments/", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var environments = await response.Content.ReadFromJsonAsync<ProjectEnvironmentView[]>(cancellationToken: cancellationToken);
        Assert.NotNull(environments);
        Assert.Collection(
            environments,
            environment => AssertEnvironment(environment, project.Id, "Development", "development", 10, false),
            environment => AssertEnvironment(environment, project.Id, "Staging", "staging", 20, false),
            environment => AssertEnvironment(environment, project.Id, "Production", "production", 30, true));

        await using var scope = harness.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InfraHarborDbContext>();
        var persisted = await db.Environments
            .AsNoTracking()
            .Where(environment => environment.ProjectId == project.Id)
            .OrderBy(environment => environment.SortOrder)
            .ToArrayAsync(cancellationToken);

        Assert.Equal(3, persisted.Length);
        Assert.All(persisted, environment => Assert.Equal(project.Id, environment.ProjectId));
    }

    [Fact]
    public async Task AdminCanCreateAndUpdateEnvironment_WithProjectScopedUniqueKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();
        await AuthorizeAsAsync(client, harness.App, harness.Admin.Id);

        var firstProject = await CreateProjectAsync(client, "First Project", "first-project", cancellationToken);
        var secondProject = await CreateProjectAsync(client, "Second Project", "second-project", cancellationToken);

        var firstCustom = await CreateEnvironmentAsync(
            client,
            firstProject.Id,
            "Quality Assurance",
            "qa",
            15,
            false,
            cancellationToken);

        var secondCustom = await CreateEnvironmentAsync(
            client,
            secondProject.Id,
            "Quality Assurance",
            "QA",
            15,
            false,
            cancellationToken);

        Assert.Equal("qa", firstCustom.Key);
        Assert.Equal("qa", secondCustom.Key);

        using var duplicateResponse = await client.PostAsJsonAsync(
            $"/api/projects/{firstProject.Id}/environments/",
            new { name = "Duplicate QA", key = "QA", sortOrder = 16, isProduction = false },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using var updateResponse = await client.PatchAsJsonAsync(
            $"/api/environments/{firstCustom.Id}",
            new { name = "Preproduction", key = "preprod", sortOrder = 25, isProduction = true },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProjectEnvironmentView>(cancellationToken: cancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(firstProject.Id, updated.ProjectId);
        Assert.Equal("Preproduction", updated.Name);
        Assert.Equal("preprod", updated.Key);
        Assert.Equal(25, updated.SortOrder);
        Assert.True(updated.IsProduction);

        await using var scope = harness.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InfraHarborDbContext>();
        var persisted = await db.Environments.AsNoTracking().SingleAsync(
            environment => environment.Id == firstCustom.Id,
            cancellationToken);
        Assert.Equal(firstProject.Id, persisted.ProjectId);
    }

    [Fact]
    public async Task ViewerAndOperatorCanReadEnvironments_ButCannotMutateHierarchy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();
        await AuthorizeAsAsync(client, harness.App, harness.Admin.Id);
        var project = await CreateProjectAsync(client, "Authorization Project", "authorization-project", cancellationToken);

        using var adminList = await client.GetAsync($"/api/projects/{project.Id}/environments/", cancellationToken);
        var defaults = await adminList.Content.ReadFromJsonAsync<ProjectEnvironmentView[]>(cancellationToken: cancellationToken);
        Assert.NotNull(defaults);
        var development = Assert.Single(defaults, environment => environment.Key == "development");

        await AuthorizeAsAsync(client, harness.App, harness.Viewer.Id);
        using var viewerRead = await client.GetAsync($"/api/projects/{project.Id}/environments/", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, viewerRead.StatusCode);
        using var viewerCreate = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/environments/",
            new { name = "Viewer Env", key = "viewer-env", sortOrder = 40, isProduction = false },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, viewerCreate.StatusCode);

        await AuthorizeAsAsync(client, harness.App, harness.Operator.Id);
        using var operatorRead = await client.GetAsync($"/api/projects/{project.Id}/environments/", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, operatorRead.StatusCode);
        using var operatorUpdate = await client.PatchAsJsonAsync(
            $"/api/environments/{development.Id}",
            new { name = "Operator Mutation" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, operatorUpdate.StatusCode);
    }

    [Fact]
    public async Task ArchivedProjectPreservesEnvironments_AndRejectsEnvironmentMutations()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();
        await AuthorizeAsAsync(client, harness.App, harness.Admin.Id);
        var project = await CreateProjectAsync(client, "Archived Project", "archived-project", cancellationToken);

        using var beforeArchiveResponse = await client.GetAsync($"/api/projects/{project.Id}/environments/", cancellationToken);
        var beforeArchive = await beforeArchiveResponse.Content.ReadFromJsonAsync<ProjectEnvironmentView[]>(cancellationToken: cancellationToken);
        Assert.NotNull(beforeArchive);
        var development = Assert.Single(beforeArchive, environment => environment.Key == "development");

        using var archiveResponse = await client.PostAsync($"/api/projects/{project.Id}/archive", null, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);

        using var listAfterArchive = await client.GetAsync($"/api/projects/{project.Id}/environments/", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, listAfterArchive.StatusCode);
        var archivedEnvironments = await listAfterArchive.Content.ReadFromJsonAsync<ProjectEnvironmentView[]>(cancellationToken: cancellationToken);
        Assert.NotNull(archivedEnvironments);
        Assert.Equal(3, archivedEnvironments.Length);

        using var createAfterArchive = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/environments/",
            new { name = "Forbidden Env", key = "forbidden-env", sortOrder = 40, isProduction = false },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, createAfterArchive.StatusCode);

        using var updateAfterArchive = await client.PatchAsJsonAsync(
            $"/api/environments/{development.Id}",
            new { name = "Forbidden Update" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, updateAfterArchive.StatusCode);

        await using var scope = harness.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InfraHarborDbContext>();
        Assert.Equal(3, await db.Environments.CountAsync(environment => environment.ProjectId == project.Id, cancellationToken));
    }

    [Fact]
    public async Task EnvironmentValidationRejectsMissingParentMalformedKeyAndNegativeSortOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();
        await AuthorizeAsAsync(client, harness.App, harness.Admin.Id);

        var missingProjectId = Guid.NewGuid();
        using var missingProjectResponse = await client.PostAsJsonAsync(
            $"/api/projects/{missingProjectId}/environments/",
            new { name = "Missing Parent", key = "missing-parent", sortOrder = 1, isProduction = false },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingProjectResponse.StatusCode);

        var project = await CreateProjectAsync(client, "Validation Project", "validation-project", cancellationToken);
        using var invalidKeyResponse = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/environments/",
            new { name = "Invalid", key = "bad key!", sortOrder = 1, isProduction = false },
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidKeyResponse.StatusCode);

        using var invalidSortResponse = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/environments/",
            new { name = "Invalid Sort", key = "invalid-sort", sortOrder = -1, isProduction = false },
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidSortResponse.StatusCode);
    }

    private static async Task<ProjectView> CreateProjectAsync(
        HttpClient client,
        string name,
        string slug,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/projects/",
            new { name, slug, description = "Environment test project" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectView>(cancellationToken: cancellationToken))!;
    }

    private static async Task<ProjectEnvironmentView> CreateEnvironmentAsync(
        HttpClient client,
        Guid projectId,
        string name,
        string key,
        int sortOrder,
        bool isProduction,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/environments/",
            new { name, key, sortOrder, isProduction },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectEnvironmentView>(cancellationToken: cancellationToken))!;
    }

    private static void AssertEnvironment(
        ProjectEnvironmentView environment,
        Guid projectId,
        string name,
        string key,
        int sortOrder,
        bool isProduction)
    {
        Assert.Equal(projectId, environment.ProjectId);
        Assert.Equal(name, environment.Name);
        Assert.Equal(key, environment.Key);
        Assert.Equal(sortOrder, environment.SortOrder);
        Assert.Equal(isProduction, environment.IsProduction);
    }

    private static async Task<TestHarness> BuildHarnessAsync(CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "ConnectionStrings__Database is required for integration tests.");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{AuthOptions.SectionName}:Issuer"] = "InfraHarbor.EnvironmentTests",
            [$"{AuthOptions.SectionName}:Audience"] = "InfraHarbor.EnvironmentTests",
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
        app.MapEnvironmentEndpoints();

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
            admin = await CreateUserAsync(userManager, "admin-environment@infraharbor.test", "Environment Admin", RoleNames.Admin);
            viewer = await CreateUserAsync(userManager, "viewer-environment@infraharbor.test", "Environment Viewer", RoleNames.Viewer);
            operatorUser = await CreateUserAsync(userManager, "operator-environment@infraharbor.test", "Environment Operator", RoleNames.Operator);
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
