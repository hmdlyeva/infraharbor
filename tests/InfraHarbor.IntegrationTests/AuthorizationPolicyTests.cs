using System.Net;
using System.Net.Http.Headers;
using InfraHarbor.Api;
using InfraHarbor.Application;
using InfraHarbor.Application.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfraHarbor.IntegrationTests;

public sealed class AuthorizationPolicyTests
{
    private const string Issuer = "InfraHarbor.PolicyTests";
    private const string Audience = "InfraHarbor.PolicyTests";
    private const string SigningKey = "ih-policy-test-signing-key-at-least-32-utf8-bytes";

    [Fact]
    public async Task PolicyEndpoints_EnforceRoleHierarchy()
    {
        await using var app = await BuildAppAsync();
        var client = app.GetTestClient();
        var issuer = app.Services.GetRequiredService<JwtAccessTokenIssuer>();

        await AssertRoleAsync(
            client,
            issuer,
            RoleNames.Viewer,
            view: HttpStatusCode.OK,
            operate: HttpStatusCode.Forbidden,
            admin: HttpStatusCode.Forbidden,
            owner: HttpStatusCode.Forbidden);

        await AssertRoleAsync(
            client,
            issuer,
            RoleNames.Operator,
            view: HttpStatusCode.OK,
            operate: HttpStatusCode.OK,
            admin: HttpStatusCode.Forbidden,
            owner: HttpStatusCode.Forbidden);

        await AssertRoleAsync(
            client,
            issuer,
            RoleNames.Admin,
            view: HttpStatusCode.OK,
            operate: HttpStatusCode.OK,
            admin: HttpStatusCode.OK,
            owner: HttpStatusCode.Forbidden);

        await AssertRoleAsync(
            client,
            issuer,
            RoleNames.Owner,
            view: HttpStatusCode.OK,
            operate: HttpStatusCode.OK,
            admin: HttpStatusCode.OK,
            owner: HttpStatusCode.OK);
    }

    [Fact]
    public async Task PolicyEndpoint_RejectsUnauthenticatedRequest()
    {
        await using var app = await BuildAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/test/view", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<WebApplication> BuildAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{AuthOptions.SectionName}:Issuer"] = Issuer,
            [$"{AuthOptions.SectionName}:Audience"] = Audience,
            [$"{AuthOptions.SectionName}:SigningKey"] = SigningKey,
            [$"{AuthOptions.SectionName}:AccessTokenLifetimeSeconds"] = "900",
            [$"{AuthOptions.SectionName}:RefreshTokenLifetimeDays"] = "30",
            [$"{AuthOptions.SectionName}:ClockSkewSeconds"] = "0"
        });

        builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddInfraHarborAuthentication(builder.Configuration);
        builder.Services.AddInfraHarborAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/test/view", () => Results.Ok())
            .RequireAuthorization(AuthorizationPolicyNames.ViewerAccess);
        app.MapPost("/test/operate", () => Results.Ok())
            .RequireAuthorization(AuthorizationPolicyNames.OperatorAccess);
        app.MapPost("/test/admin", () => Results.Ok())
            .RequireAuthorization(AuthorizationPolicyNames.AdminAccess);
        app.MapPost("/test/owner", () => Results.Ok())
            .RequireAuthorization(AuthorizationPolicyNames.OwnerOnly);

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task AssertRoleAsync(
        HttpClient client,
        JwtAccessTokenIssuer issuer,
        string role,
        HttpStatusCode view,
        HttpStatusCode operate,
        HttpStatusCode admin,
        HttpStatusCode owner)
    {
        var access = issuer.Issue(
            Guid.NewGuid(),
            $"{role.ToLowerInvariant()}@infraharbor.test",
            $"{role} User",
            [role]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access.Token);

        var cancellationToken = TestContext.Current.CancellationToken;
        var viewResponse = await client.GetAsync("/test/view", cancellationToken);
        var operateResponse = await client.PostAsync("/test/operate", null, cancellationToken);
        var adminResponse = await client.PostAsync("/test/admin", null, cancellationToken);
        var ownerResponse = await client.PostAsync("/test/owner", null, cancellationToken);

        Assert.Equal(view, viewResponse.StatusCode);
        Assert.Equal(operate, operateResponse.StatusCode);
        Assert.Equal(admin, adminResponse.StatusCode);
        Assert.Equal(owner, ownerResponse.StatusCode);
    }
}
