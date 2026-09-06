using InfraHarbor.Application.Projects;
using InfraHarbor.Application.Security;

namespace InfraHarbor.Api;

internal sealed record CreateProjectRequest(string? Name, string? Slug, string? Description);
internal sealed record UpdateProjectRequest(string? Name, string? Slug, string? Description);

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects")
            .RequireAuthorization(AuthorizationPolicyNames.ViewerAccess);

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync)
            .RequireAuthorization(AuthorizationPolicyNames.AdminAccess);
        group.MapPatch("/{id:guid}", UpdateAsync)
            .RequireAuthorization(AuthorizationPolicyNames.AdminAccess);
        group.MapPost("/{id:guid}/archive", ArchiveAsync)
            .RequireAuthorization(AuthorizationPolicyNames.AdminAccess);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        bool includeArchived,
        IProjectService service,
        CancellationToken cancellationToken)
    {
        var projects = await service.ListAsync(includeArchived, cancellationToken);
        return Results.Ok(projects);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IProjectService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return ToHttpResult(result);
    }

    private static async Task<IResult> CreateAsync(
        CreateProjectRequest request,
        IProjectService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(
            new CreateProjectCommand(request.Name, request.Slug, request.Description),
            cancellationToken);
        return ToHttpResult(result, created: true);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateProjectRequest request,
        IProjectService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(
            id,
            new UpdateProjectCommand(request.Name, request.Slug, request.Description),
            cancellationToken);
        return ToHttpResult(result);
    }

    private static async Task<IResult> ArchiveAsync(
        Guid id,
        IProjectService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ArchiveAsync(id, cancellationToken);
        return ToHttpResult(result);
    }

    private static IResult ToHttpResult(ProjectOperationResult result, bool created = false) =>
        result.Outcome switch
        {
            ProjectOperationOutcome.Created when created && result.Project is not null =>
                Results.Created($"/api/projects/{result.Project.Id}", result.Project),
            ProjectOperationOutcome.Success when result.Project is not null => Results.Ok(result.Project),
            ProjectOperationOutcome.NotFound => Results.NotFound(new { code = "project_not_found" }),
            ProjectOperationOutcome.Conflict => Results.Conflict(new
            {
                code = "project_conflict",
                errors = result.Errors ?? []
            }),
            ProjectOperationOutcome.ValidationFailed => Results.BadRequest(new
            {
                code = "project_validation_failed",
                errors = result.Errors ?? []
            }),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
}
