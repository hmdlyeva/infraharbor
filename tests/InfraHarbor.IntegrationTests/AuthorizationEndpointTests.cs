using System.Net;
using System.Net.Http.Headers;
using InfraHarbor.Api;
using InfraHarbor.Application;
using InfraHarbor.Application.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfraHarbor.IntegrationTests;

public sealed class AuthorizationEndpointTests
{
    private const string SigningKey = "ih-authorization-integration-test-signing-key-32-bytes";

    [Fact]
    public async Task RoleHierarchy_IsEnforcedByEndpointMiddleware()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await BuildApplicationAsync(cancellationToken);
        using var client = app.GetTestClient();
        var issuer = app.Services.GetRequiredService<JwtAccessTokenIssuer>();

        var endpoints = new[]
        {
            (Path: "/probe/viewer", MinimumLevel: 0),
            (Path: "/probe/operator", MinimumLevel: 1),
            (Path: "/probe/admin", MinimumLevel: 2),
            (Path: "/probe/owner", MinimumLevel: 3)
        };

        var roleLevels = new[]
        {
            (Role: RoleNames.Viewer, Level: 0),
            (Role: RoleNames.Operator, Level: 1),
            (Role: RoleNames.Admin, Level: 2),
            (Role: RoleNames.Owner, Level: 3)
        };

        foreach (var (role, level) in roleLevels)
        {
            var access = issuer.Issue(
                Guid.NewGuid(),
                $"{role.ToLowerInvariant()}@infraharbor.test",
                $"{role} User",
                [role],
                "test-security-stamp");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", access.Token);

            foreach (var (path, minimumLevel) in endpoints)
            {
                using var response = await client.GetAsync(path, cancellationToken);
                var expected = level >= minimumLevel ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden;
                Assert.Equal(expected, response.StatusCode);
            }
        }
    }

    [Fact]
    public async Task ProtectedEndpoints_RejectUnauthenticatedRequests()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await BuildApplicationAsync(cancellationToken);
        using var client = app.GetTestClient();

        foreach (var path in new[] { "/probe/viewer", "/probe/operator", "/probe/admin", "/probe/owner" })
        {
            using var response = await client.GetAsync(path, cancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private static async Task<WebApplication> BuildApplicationAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{AuthOptions.SectionName}:Issuer"] = "InfraHarbor.AuthorizationTests",
            [$"{AuthOptions.SectionName}:Audience"] = "InfraHarbor.AuthorizationTests",
            [$"{AuthOptions.SectionName}:SigningKey"] = SigningKey,
            [$"{AuthOptions.SectionName}:AccessTokenLifetimeSeconds"] = "900",
            [$"{AuthOptions.SectionName}:ClockSkewSeconds"] = "0"
        });

        builder.Services.AddOptions<AuthOptions>()
            .Bind(builder.Configuration.GetSection(AuthOptions.SectionName));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddInfraHarborAuthentication(builder.Configuration);
        builder.Services.AddInfraHarborAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/probe/viewer", () => Results.NoContent())
            .RequireAuthorization(AuthorizationPolicyNames.ViewerAccess);
        app.MapGet("/probe/operator", () => Results.NoContent())
            .RequireAuthorization(AuthorizationPolicyNames.OperatorAccess);
        app.MapGet("/probe/admin", () => Results.NoContent())
            .RequireAuthorization(AuthorizationPolicyNames.AdminAccess);
        app.MapGet("/probe/owner", () => Results.NoContent())
            .RequireAuthorization(AuthorizationPolicyNames.OwnerOnly);

        await app.StartAsync(cancellationToken);
        return app;
    }
}
