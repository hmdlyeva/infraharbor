using InfraHarbor.Application.Security;

namespace InfraHarbor.Api;

internal sealed record BootstrapOwnerRequest(string Email, string DisplayName, string Password);

internal static class AuthEndpoints
{
    private const string BootstrapTokenHeader = "X-InfraHarbor-Bootstrap-Token";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/bootstrap-owner", BootstrapOwnerAsync);
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
}
