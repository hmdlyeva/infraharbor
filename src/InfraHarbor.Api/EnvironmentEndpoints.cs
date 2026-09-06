using InfraHarbor.Application.Projects;
using InfraHarbor.Application.Security;

namespace InfraHarbor.Api;

internal sealed record CreateProjectEnvironmentRequest(
    string? Name,
    string? Key,
    int SortOrder,
    bool IsProduction);

internal sealed record UpdateProjectEnvironmentRequest(
    string? Name,
    string? Key,
    int? SortOrder,
    bool? IsProduction);

public static class EnvironmentEndpoints
{
    public static IEndpointRouteBuilder MapEnvironmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var projectGroup = endpoints.MapGroup("/api/projects/{projectId:guid}/environments")
            .RequireAuthorization(AuthorizationPolicyNames.ViewerAccess);

        projectGroup.MapGet("/", ListAsync);
        projectGroup.MapPost("/", CreateAsync)
            .RequireAuthorization(AuthorizationPolicyNames.AdminAccess);

        endpoints.MapPatch("/api/environments/{id:guid}", UpdateAsync)
            .RequireAuthorization(AuthorizationPolicyNames.AdminAccess);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid projectId,
        IProjectEnvironmentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(projectId, cancellationToken);
        return ToHttpResult(result);
    }

    private static async Task<IResult> CreateAsync(
        Guid projectId,
        CreateProjectEnvironmentRequest request,
        IProjectEnvironmentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(
            projectId,
            new CreateProjectEnvironmentCommand(
                request.Name,
                request.Key,
                request.SortOrder,
                request.IsProduction),
            cancellationToken);

        return ToHttpResult(result, created: true);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateProjectEnvironmentRequest request,
        IProjectEnvironmentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(
            id,
            new UpdateProjectEnvironmentCommand(
                request.Name,
                request.Key,
                request.SortOrder,
                request.IsProduction),
            cancellationToken);

        return ToHttpResult(result);
    }

    private static IResult ToHttpResult(ProjectEnvironmentOperationResult result, bool created = false) =>
        result.Outcome switch
        {
            ProjectEnvironmentOperationOutcome.Created when created && result.Environment is not null =>
                Results.Json(result.Environment, statusCode: StatusCodes.Status201Created),
            ProjectEnvironmentOperationOutcome.Success when result.Environment is not null =>
                Results.Ok(result.Environment),
            ProjectEnvironmentOperationOutcome.Success when result.Environments is not null =>
                Results.Ok(result.Environments),
            ProjectEnvironmentOperationOutcome.NotFound =>
                Results.NotFound(new { code = "environment_not_found" }),
            ProjectEnvironmentOperationOutcome.ProjectNotFound =>
                Results.NotFound(new { code = "project_not_found" }),
            ProjectEnvironmentOperationOutcome.ProjectArchived =>
                Results.Conflict(new { code = "project_archived" }),
            ProjectEnvironmentOperationOutcome.Conflict =>
                Results.Conflict(new
                {
                    code = "environment_conflict",
                    errors = result.Errors ?? []
                }),
            ProjectEnvironmentOperationOutcome.ValidationFailed =>
                Results.BadRequest(new
                {
                    code = "environment_validation_failed",
                    errors = result.Errors ?? []
                }),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
}
