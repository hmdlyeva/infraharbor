using InfraHarbor.Application;
using InfraHarbor.Application.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace InfraHarbor.Api;

internal sealed record BootstrapOwnerRequest(string Email, string DisplayName, string Password);
internal sealed record LoginRequest(string Email, string Password);

internal static class AuthEndpoints
{
    private const string BootstrapTokenHeader = "X-InfraHarbor-Bootstrap-Token";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/bootstrap-owner", BootstrapOwnerAsync);
        endpoints.MapPost("/api/auth/login", LoginAsync)
            .RequireRateLimiting(AuthRateLimitPolicies.Login);
        endpoints.MapPost("/api/auth/refresh", RefreshAsync)
            .RequireRateLimiting(AuthRateLimitPolicies.Refresh);
        endpoints.MapPost("/api/auth/logout", LogoutAsync);
        return endpoints;
    }

    private static async Task<IResult> BootstrapOwnerAsync(
        BootstrapOwnerRequest request,
        HttpContext httpContext,
        IOwnerBootstrapService bootstrapService,
        CancellationToken cancellationToken)
    {
        var suppliedToken = httpContext.Request.Headers[BootstrapTokenHeader].ToString();
        var result = await bootstrapService.BootstrapAsync(
            new OwnerBootstrapCommand(request.Email, request.DisplayName, request.Password, suppliedToken),
            cancellationToken);

        return result.Outcome switch
        {
            OwnerBootstrapOutcome.Created => Results.Created(
                "/api/auth/me",
                new
                {
                    userId = result.UserId,
                    email = result.Email,
                    displayName = result.DisplayName,
                    role = RoleNames.Owner
                }),
            OwnerBootstrapOutcome.Disabled => Results.NotFound(new { code = "bootstrap_unavailable" }),
            OwnerBootstrapOutcome.InvalidToken => Results.Unauthorized(),
            OwnerBootstrapOutcome.AlreadyInitialized => Results.Conflict(new { code = "bootstrap_already_completed" }),
            OwnerBootstrapOutcome.ValidationFailed => Results.BadRequest(new
            {
                code = "bootstrap_validation_failed",
                errors = result.Errors ?? []
            }),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        IAuthSessionService sessionService,
        JwtAccessTokenIssuer tokenIssuer,
        IOptions<AuthOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var result = await sessionService.LoginAsync(
            new LoginCommand(request.Email, request.Password, GetMetadata(httpContext)),
            cancellationToken);

        if (result.Outcome != AuthSessionOutcome.Authenticated)
        {
            return Results.Json(
                new { code = "authentication_failed" },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var access = tokenIssuer.Issue(result.UserId!.Value, result.Email!, result.DisplayName!);
        WriteRefreshCookie(httpContext, authOptions.Value, result.RefreshToken!, result.RefreshExpiresAt!.Value);

        return Results.Ok(new
        {
            tokenType = "Bearer",
            accessToken = access.Token,
            accessTokenExpiresAt = access.ExpiresAt,
            user = new
            {
                id = result.UserId,
                email = result.Email,
                displayName = result.DisplayName
            }
        });
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext httpContext,
        IAuthSessionService sessionService,
        JwtAccessTokenIssuer tokenIssuer,
        IOptions<AuthOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var options = authOptions.Value;
        if (!httpContext.Request.Cookies.TryGetValue(options.RefreshCookieName, out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            return InvalidSession();
        }

        var result = await sessionService.RefreshAsync(
            new RefreshSessionCommand(refreshToken, GetMetadata(httpContext)),
            cancellationToken);

        if (result.Outcome != AuthSessionOutcome.Authenticated)
        {
            DeleteRefreshCookie(httpContext, options);
            return InvalidSession();
        }

        var access = tokenIssuer.Issue(result.UserId!.Value, result.Email!, result.DisplayName!);
        WriteRefreshCookie(httpContext, options, result.RefreshToken!, result.RefreshExpiresAt!.Value);

        return Results.Ok(new
        {
            tokenType = "Bearer",
            accessToken = access.Token,
            accessTokenExpiresAt = access.ExpiresAt,
            user = new
            {
                id = result.UserId,
                email = result.Email,
                displayName = result.DisplayName
            }
        });
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        IAuthSessionService sessionService,
        IOptions<AuthOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var options = authOptions.Value;
        if (httpContext.Request.Cookies.TryGetValue(options.RefreshCookieName, out var refreshToken) &&
            !string.IsNullOrWhiteSpace(refreshToken))
        {
            await sessionService.RevokeAsync(refreshToken, cancellationToken);
        }

        DeleteRefreshCookie(httpContext, options);
        return Results.NoContent();
    }

    private static AuthSessionMetadata GetMetadata(HttpContext httpContext) =>
        new(
            httpContext.Request.Headers["User-Agent"].ToString(),
            httpContext.Connection.RemoteIpAddress?.ToString());

    private static void WriteRefreshCookie(
        HttpContext httpContext,
        AuthOptions options,
        string token,
        DateTimeOffset expiresAt)
    {
        httpContext.Response.Cookies.Append(
            options.RefreshCookieName,
            token,
            BuildCookieOptions(options, expiresAt));
    }

    private static void DeleteRefreshCookie(HttpContext httpContext, AuthOptions options)
    {
        httpContext.Response.Cookies.Delete(
            options.RefreshCookieName,
            BuildCookieOptions(options, expiresAt: null));
    }

    private static CookieOptions BuildCookieOptions(AuthOptions options, DateTimeOffset? expiresAt) =>
        new()
        {
            HttpOnly = true,
            Secure = options.SecureCookies,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            IsEssential = true,
            Expires = expiresAt
        };

    private static IResult InvalidSession() =>
        Results.Json(
            new { code = "session_invalid" },
            statusCode: StatusCodes.Status401Unauthorized);
}
