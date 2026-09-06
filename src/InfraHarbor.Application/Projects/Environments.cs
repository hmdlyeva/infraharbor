using InfraHarbor.Domain.Projects;

namespace InfraHarbor.Application.Projects;

public sealed record ProjectEnvironmentView(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Key,
    int SortOrder,
    bool IsProduction,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateProjectEnvironmentCommand(
    string? Name,
    string? Key,
    int SortOrder,
    bool IsProduction);

public sealed record UpdateProjectEnvironmentCommand(
    string? Name,
    string? Key,
    int? SortOrder,
    bool? IsProduction);

public enum ProjectEnvironmentOperationOutcome
{
    Success,
    Created,
    NotFound,
    ProjectNotFound,
    ProjectArchived,
    ValidationFailed,
    Conflict
}

public sealed record ProjectEnvironmentOperationResult(
    ProjectEnvironmentOperationOutcome Outcome,
    ProjectEnvironmentView? Environment = null,
    IReadOnlyList<ProjectEnvironmentView>? Environments = null,
    IReadOnlyList<string>? Errors = null);

public interface IProjectEnvironmentRepository
{
    Task<IReadOnlyList<ProjectEnvironment>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ProjectEnvironment?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ProjectEnvironment?> FindByKeyAsync(Guid projectId, string key, CancellationToken cancellationToken);
    Task AddAsync(ProjectEnvironment environment, CancellationToken cancellationToken);
    Task AddRangeAsync(IReadOnlyCollection<ProjectEnvironment> environments, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IProjectEnvironmentService
{
    Task<ProjectEnvironmentOperationResult> ListAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ProjectEnvironmentOperationResult> CreateAsync(Guid projectId, CreateProjectEnvironmentCommand command, CancellationToken cancellationToken);
    Task<ProjectEnvironmentOperationResult> UpdateAsync(Guid id, UpdateProjectEnvironmentCommand command, CancellationToken cancellationToken);
}
