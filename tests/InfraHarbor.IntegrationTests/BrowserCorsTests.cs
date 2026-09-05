using InfraHarbor.Api;
using InfraHarbor.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace InfraHarbor.IntegrationTests;

public sealed class BrowserCorsTests
{
    private const string BrowserOrigin = "http://localhost:3000";

    [Fact]
    public async Task ConfiguredBrowserOrigin_AllowsCredentialedCors_AndRejectsOtherOrigins()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await BuildApplicationAsync(cancellationToken);
        using var client = app.GetTestClient();

        using var allowed = BuildPreflight(BrowserOrigin);
        using var allowedResponse = await client.SendAsync(allowed, cancellationToken);

        Assert.Equal(StatusCodes.Status204NoContent, (int)allowedResponse.StatusCode);
        Assert.Equal(
            BrowserOrigin,
            Assert.Single(allowedResponse.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal(
            "true",
            Assert.Single(allowedResponse.Headers.GetValues("Access-Control-Allow-Credentials")));

        using var denied = BuildPreflight("https://untrusted.example");
        using var deniedResponse = await client.SendAsync(denied, cancellationToken);

        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    private static HttpRequestMessage BuildPreflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/cors-probe");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
        return request;
    }

    private static async Task<WebApplication> BuildApplicationAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{RuntimeOptions.SectionName}:PublicUrl"] = $"{BrowserOrigin}/app"
        });
        builder.Services.AddInfraHarborCors(builder.Configuration);

        var app = builder.Build();
        app.UseCors();
        app.MapPost("/cors-probe", () => Results.NoContent());
        await app.StartAsync(cancellationToken);
        return app;
    }
}
