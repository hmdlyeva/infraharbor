using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using InfraHarbor.Api;
using InfraHarbor.Application;
using InfraHarbor.Application.Branding;
using InfraHarbor.Application.Security;
using InfraHarbor.Domain.Branding;
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

public sealed class BrandingEndpointTests
{
    private const string SigningKey = "ih-branding-integration-signing-key-32-bytes";
    private const string Password = "Ih-branding-Strong!12345";

    [Fact]
    public async Task CleanInstallReturnsSafePublicInfraHarborDefaults()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();

        using var response = await client.GetAsync("/api/branding/public", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=60", response.Headers.CacheControl?.ToString());
        var branding = await response.Content.ReadFromJsonAsync<BrandingView>(cancellationToken: cancellationToken);
        Assert.NotNull(branding);
        Assert.Equal("InfraHarbor", branding.ProductName);
        Assert.Equal("IH", branding.ShortName);
        Assert.Equal("#17324D", branding.PrimaryColor);
        Assert.Equal("InfraHarbor", branding.FooterText);
        Assert.Null(branding.LogoUrl);
        Assert.Null(branding.FaviconUrl);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.DoesNotContain("signingKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("updatedAt", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OwnerCanPersistValidOverride_AndPublicReadReturnsPersistedPresentationValues()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();
        await AuthorizeAsAsync(client, harness.App, harness.Owner.Id);

        using var update = await client.PutAsJsonAsync(
            "/api/admin/branding",
            new
            {
                productName = "Harbor Operations",
                shortName = "HO",
                logoUrl = "https://assets.example.test/logo.svg",
                faviconUrl = "https://assets.example.test/favicon.ico",
                primaryColor = "#336699",
                supportUrl = "https://support.example.test/help",
                documentationUrl = "https://docs.example.test/infraharbor",
                footerText = "Harbor Operations",
                loginHeadline = "Operate infrastructure with confidence"
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<BrandingView>(cancellationToken: cancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("Harbor Operations", updated.ProductName);
        Assert.Equal("#336699", updated.PrimaryColor);

        using var adminRead = await client.GetAsync("/api/admin/branding", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, adminRead.StatusCode);
        var adminBranding = await adminRead.Content.ReadFromJsonAsync<BrandingView>(cancellationToken: cancellationToken);
        Assert.Equal(updated, adminBranding);

        client.DefaultRequestHeaders.Authorization = null;
        using var publicRead = await client.GetAsync("/api/branding/public", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, publicRead.StatusCode);
        var publicBranding = await publicRead.Content.ReadFromJsonAsync<BrandingView>(cancellationToken: cancellationToken);
        Assert.Equal(updated, publicBranding);

        await using var scope = harness.App.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBrandingRepository>();
        var persisted = await repository.GetAsync(cancellationToken);
        Assert.NotNull(persisted);
        Assert.Equal("Harbor Operations", persisted.ProductName);
        Assert.Equal("https://assets.example.test/logo.svg", persisted.LogoUrl);
    }

    [Fact]
    public async Task InvalidBrandingIsRejectedWithoutReplacingSafePublicDefaults()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();
        await AuthorizeAsAsync(client, harness.App, harness.Owner.Id);

        using var invalid = await client.PutAsJsonAsync(
            "/api/admin/branding",
            new
            {
                productName = "InfraHarbor",
                shortName = "IH",
                logoUrl = "javascript:alert(1)",
                faviconUrl = "data:text/html,bad",
                primaryColor = "red",
                supportUrl = "ftp://example.test/help",
                documentationUrl = "https://docs.example.test",
                footerText = "<script>alert(1)</script>",
                loginHeadline = "<b>unsafe</b>"
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var errorBody = await invalid.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("branding_validation_failed", errorBody, StringComparison.Ordinal);

        client.DefaultRequestHeaders.Authorization = null;
        using var publicRead = await client.GetAsync("/api/branding/public", cancellationToken);
        var branding = await publicRead.Content.ReadFromJsonAsync<BrandingView>(cancellationToken: cancellationToken);
        Assert.NotNull(branding);
        Assert.Equal(BrandingDefaults.Upstream, branding);
    }

    [Fact]
    public async Task CorruptStoredBrandingFallsBackPerFieldInsteadOfBreakingPublicConfiguration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);

        await using (var scope = harness.App.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IBrandingRepository>();
            await repository.UpsertAsync(
                new BrandingSettings
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000019"),
                    ProductName = "<bad>",
                    ShortName = "HO",
                    LogoUrl = "javascript:alert(1)",
                    FaviconUrl = "https://assets.example.test/favicon.ico",
                    PrimaryColor = "not-a-color",
                    SupportUrl = "file:///etc/passwd",
                    DocumentationUrl = "https://docs.example.test",
                    FooterText = "<iframe>",
                    LoginHeadline = "<script>",
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                cancellationToken);
        }

        using var client = harness.App.GetTestClient();
        using var response = await client.GetAsync("/api/branding/public", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var branding = await response.Content.ReadFromJsonAsync<BrandingView>(cancellationToken: cancellationToken);
        Assert.NotNull(branding);

        Assert.Equal("InfraHarbor", branding.ProductName);
        Assert.Equal("HO", branding.ShortName);
        Assert.Null(branding.LogoUrl);
        Assert.Equal("https://assets.example.test/favicon.ico", branding.FaviconUrl);
        Assert.Equal("#17324D", branding.PrimaryColor);
        Assert.Null(branding.SupportUrl);
        Assert.Equal("https://docs.example.test", branding.DocumentationUrl);
        Assert.Equal("InfraHarbor", branding.FooterText);
        Assert.Null(branding.LoginHeadline);
    }

    [Fact]
    public async Task NonOwnerCannotReadOrMutateAdminBranding_WhilePublicEndpointRemainsAnonymous()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var harness = await BuildHarnessAsync(cancellationToken);
        using var client = harness.App.GetTestClient();
        await AuthorizeAsAsync(client, harness.App, harness.Admin.Id);

        using var adminRead = await client.GetAsync("/api/admin/branding", cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, adminRead.StatusCode);

        using var adminUpdate = await client.PutAsJsonAsync(
            "/api/admin/branding",
            new
            {
                productName = "Forbidden",
                shortName = "NO",
                primaryColor = "#112233",
                footerText = "Forbidden"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, adminUpdate.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        using var publicRead = await client.GetAsync("/api/branding/public", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, publicRead.StatusCode);
    }

    private static async Task<TestHarness> BuildHarnessAsync(CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "ConnectionStrings__Database is required for integration tests.");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{AuthOptions.SectionName}:Issuer"] = "InfraHarbor.BrandingTests",
            [$"{AuthOptions.SectionName}:Audience"] = "InfraHarbor.BrandingTests",
            [$"{AuthOptions.SectionName}:SigningKey"] = SigningKey,
            [$"{AuthOptions.SectionName}:AccessTokenLifetimeSeconds"] = "900",
            [$"{AuthOptions.SectionName}:ClockSkewSeconds"] = "0"
        });

        builder.Services.AddOptions<AuthOptions>()
            .Bind(builder.Configuration.GetSection(AuthOptions.SectionName));
        builder.Services.AddInfraHarborPersistence(connectionString!);
        builder.Services.AddInfraHarborIdentity();
        builder.Services.AddInfraHarborBranding();
        builder.Services.AddInfraHarborAuthentication(builder.Configuration);
        builder.Services.AddInfraHarborAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapBrandingEndpoints();

        ApplicationUser owner;
        ApplicationUser admin;
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
            owner = await CreateUserAsync(userManager, "owner-branding@infraharbor.test", "Branding Owner", RoleNames.Owner);
            admin = await CreateUserAsync(userManager, "admin-branding@infraharbor.test", "Branding Admin", RoleNames.Admin);
        }

        await app.StartAsync(cancellationToken);
        return new TestHarness(app, owner, admin);
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
        ApplicationUser Owner,
        ApplicationUser Admin) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => App.DisposeAsync();
    }
}
