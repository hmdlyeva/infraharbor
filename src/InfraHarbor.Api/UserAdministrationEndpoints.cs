using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using InfraHarbor.Application.Security;

namespace InfraHarbor.Api;

internal sealed record CreateManagedUserRequest(
    string Email,
    string DisplayName,
    string Password,
    IReadOnlyList<string> Roles);

internal sealed record UpdateManagedUserRequest(string DisplayName);
internal sealed record SetManagedUserRolesRequest(IReadOnlyList<string> Roles);

public static class UserAdministrationEndpoints
{
    public static IEndpointRouteBuilder MapUserAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users")
            .RequireAuthorization(AuthorizationPolicyNames.AdminAccess);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPatch("/{id:guid}", UpdateAsync);
        group.MapPost("/{id:guid}/roles", SetRolesAsync);
        group.MapPost("/{id:guid}/disable", DisableAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        IUserAdministrationService service,
        CancellationToken cancellationToken)
    {
        var users = await service.ListAsync(cancellationToken);
        return Results.Ok(users);
    }

    private static async Task<IResult> CreateAsync(
        CreateManagedUserRequest request,
        ClaimsPrincipal principal,
        IUserAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.CreateAsync(
            actorUserId,
            new CreateManagedUserCommand(request.Email, request.DisplayName, request.Password, request.Roles ?? []),
            cancellationToken);

        return ToHttpResult(result, created: true);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateManagedUserRequest request,
        ClaimsPrincipal principal,
        IUserAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.UpdateAsync(
            actorUserId,
            id,
            new UpdateManagedUserCommand(request.DisplayName),
            cancellationToken);

        return ToHttpResult(result);
    }

    private static async Task<IResult> SetRolesAsync(
        Guid id,
        SetManagedUserRolesRequest request,
        ClaimsPrincipal principal,
        IUserAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.SetRolesAsync(
            actorUserId,
            id,
            new SetManagedUserRolesCommand(request.Roles ?? []),
            cancellationToken);

        return ToMutationHttpResult(result);
    }

    private static async Task<IResult> DisableAsync(
        Guid id,
        ClaimsPrincipal principal,
        IUserAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(principal, out var actorUserId))
        {
            return Results.Unauthorized();
        }

        var result = await service.DisableAsync(actorUserId, id, cancellationToken);
        return ToMutationHttpResult(result);
    }

    private static bool TryGetActor(ClaimsPrincipal principal, out Guid actorUserId)
    {
        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ??
                      principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subject, out actorUserId);
    }

    private static IResult ToMutationHttpResult(UserAdministrationResult result) =>
        result.Outcome switch
        {
            UserAdministrationOutcome.Success => Results.NoContent(),
            UserAdministrationOutcome.NotFound => Results.NotFound(new { code = "user_not_found" }),
            UserAdministrationOutcome.Forbidden => Results.Forbid(),
            UserAdministrationOutcome.Conflict => Results.Conflict(new
            {
                code = "user_conflict",
                errors = result.Errors ?? []
            }),
            UserAdministrationOutcome.ValidationFailed => Results.BadRequest(new
            {
                code = "user_validation_failed",
                errors = result.Errors ?? []
            }),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };

    private static IResult ToHttpResult(UserAdministrationResult result, bool created = false) =>
        result.Outcome switch
        {
            UserAdministrationOutcome.Created when created && result.User is not null =>
                Results.Created($"/api/users/{result.User.Id}", result.User),
            UserAdministrationOutcome.Success when result.User is not null => Results.Ok(result.User),
            UserAdministrationOutcome.NotFound => Results.NotFound(new { code = "user_not_found" }),
            UserAdministrationOutcome.Forbidden => Results.Forbid(),
            UserAdministrationOutcome.Conflict => Results.Conflict(new
            {
                code = "user_conflict",
                errors = result.Errors ?? []
            }),
            UserAdministrationOutcome.ValidationFailed => Results.BadRequest(new
            {
                code = "user_validation_failed",
                errors = result.Errors ?? []
            }),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
}
