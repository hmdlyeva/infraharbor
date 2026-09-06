using InfraHarbor.Domain.Projects;

namespace InfraHarbor.Application.Projects;

public sealed record ProjectView(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateProjectCommand(string? Name, string? Slug, string? Description);

public sealed record UpdateProjectCommand(string? Name, string? Slug, string? Description);

public enum ProjectOperationOutcome
{
    Success,
    Created,
    NotFound,
    ValidationFailed,
    Conflict
}

public sealed record ProjectOperationResult(
    ProjectOperationOutcome Outcome,
    ProjectView? Project = null,
    IReadOnlyList<string>? Errors = null);

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> ListAsync(bool includeArchived, CancellationToken cancellationToken);
    Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Project?> FindBySlugAsync(string slug, CancellationToken cancellationToken);
    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IProjectService
{
    Task<IReadOnlyList<ProjectView>> ListAsync(bool includeArchived, CancellationToken cancellationToken);
    Task<ProjectOperationResult> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ProjectOperationResult> CreateAsync(CreateProjectCommand command, CancellationToken cancellationToken);
    Task<ProjectOperationResult> UpdateAsync(Guid id, UpdateProjectCommand command, CancellationToken cancellationToken);
    Task<ProjectOperationResult> ArchiveAsync(Guid id, CancellationToken cancellationToken);
}
