using System.Text.RegularExpressions;
using InfraHarbor.Application.Projects;
using InfraHarbor.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InfraHarbor.Infrastructure.Projects;

public sealed class ProjectEnvironmentService(
    IProjectRepository projectRepository,
    IProjectEnvironmentRepository environmentRepository,
    TimeProvider timeProvider) : IProjectEnvironmentService
{
    private const int MaxNameLength = 120;
    private const int MaxKeyLength = 64;

    public async Task<ProjectEnvironmentOperationResult> ListAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (await projectRepository.GetAsync(projectId, cancellationToken) is null)
        {
            return new ProjectEnvironmentOperationResult(ProjectEnvironmentOperationOutcome.ProjectNotFound);
        }

        var environments = await environmentRepository.ListByProjectAsync(projectId, cancellationToken);
        return new ProjectEnvironmentOperationResult(
            ProjectEnvironmentOperationOutcome.Success,
            Environments: environments.Select(ToView).ToArray());
    }

    public async Task<ProjectEnvironmentOperationResult> CreateAsync(
        Guid projectId,
        CreateProjectEnvironmentCommand command,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetAsync(projectId, cancellationToken);
        if (project is null)
        {
            return new ProjectEnvironmentOperationResult(ProjectEnvironmentOperationOutcome.ProjectNotFound);
        }

        if (project.IsArchived)
        {
            return new ProjectEnvironmentOperationResult(ProjectEnvironmentOperationOutcome.ProjectArchived);
        }

        var (name, key, errors) = Validate(command.Name, command.Key, command.SortOrder);
        if (errors.Count > 0)
        {
            return ValidationFailure(errors);
        }

        if (await environmentRepository.FindByKeyAsync(projectId, key!, cancellationToken) is not null)
        {
            return KeyConflict();
        }

        var now = timeProvider.GetUtcNow();
        var environment = new ProjectEnvironment
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name!,
            Key = key!,
            SortOrder = command.SortOrder,
            IsProduction = command.IsProduction,
            CreatedAt = now,
            UpdatedAt = now
        };

        await environmentRepository.AddAsync(environment, cancellationToken);
        try
        {
            await environmentRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsKeyConflict(exception))
        {
            return KeyConflict();
        }

        return new ProjectEnvironmentOperationResult(
            ProjectEnvironmentOperationOutcome.Created,
            Environment: ToView(environment));
    }

    public async Task<ProjectEnvironmentOperationResult> UpdateAsync(
        Guid id,
        UpdateProjectEnvironmentCommand command,
        CancellationToken cancellationToken)
    {
        var environment = await environmentRepository.GetAsync(id, cancellationToken);
        if (environment is null)
        {
            return new ProjectEnvironmentOperationResult(ProjectEnvironmentOperationOutcome.NotFound);
        }

        var project = await projectRepository.GetAsync(environment.ProjectId, cancellationToken);
        if (project is null)
        {
            return new ProjectEnvironmentOperationResult(ProjectEnvironmentOperationOutcome.ProjectNotFound);
        }

        if (project.IsArchived)
        {
            return new ProjectEnvironmentOperationResult(ProjectEnvironmentOperationOutcome.ProjectArchived);
        }

        if (command.Name is null && command.Key is null && command.SortOrder is null && command.IsProduction is null)
        {
            return ValidationFailure(["At least one environment field must be supplied."]);
        }

        var requestedName = command.Name ?? environment.Name;
        var requestedKey = command.Key ?? environment.Key;
        var requestedSortOrder = command.SortOrder ?? environment.SortOrder;
        var (name, key, errors) = Validate(requestedName, requestedKey, requestedSortOrder);
        if (errors.Count > 0)
        {
            return ValidationFailure(errors);
        }

        if (!string.Equals(environment.Key, key, StringComparison.Ordinal))
        {
            var existing = await environmentRepository.FindByKeyAsync(environment.ProjectId, key!, cancellationToken);
            if (existing is not null && existing.Id != environment.Id)
            {
                return KeyConflict();
            }
        }

        environment.Name = name!;
        environment.Key = key!;
        environment.SortOrder = requestedSortOrder;
        environment.IsProduction = command.IsProduction ?? environment.IsProduction;
        environment.UpdatedAt = timeProvider.GetUtcNow();

        try
        {
            await environmentRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsKeyConflict(exception))
        {
            return KeyConflict();
        }

        return new ProjectEnvironmentOperationResult(
            ProjectEnvironmentOperationOutcome.Success,
            Environment: ToView(environment));
    }

    private static (string? Name, string? Key, List<string> Errors) Validate(
        string? requestedName,
        string? requestedKey,
        int sortOrder)
    {
        var errors = new List<string>();
        var name = requestedName?.Trim() ?? string.Empty;
        var key = (requestedKey?.Trim() ?? string.Empty).ToLowerInvariant();

        if (name.Length is < 1 or > MaxNameLength)
        {
            errors.Add($"Environment name must be between 1 and {MaxNameLength} characters.");
        }

        if (key.Length is < 1 or > MaxKeyLength ||
            !Regex.IsMatch(key, "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant))
        {
            errors.Add("Environment key must contain only lowercase letters, numbers and single hyphens between segments.");
        }

        if (sortOrder < 0)
        {
            errors.Add("Environment sort order cannot be negative.");
        }

        return (name, key, errors);
    }

    private static ProjectEnvironmentOperationResult ValidationFailure(IReadOnlyList<string> errors) =>
        new(ProjectEnvironmentOperationOutcome.ValidationFailed, Errors: errors);

    private static ProjectEnvironmentOperationResult KeyConflict() =>
        new(ProjectEnvironmentOperationOutcome.Conflict, Errors: ["An environment with this key already exists in the project."]);

    private static bool IsKeyConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Environments_ProjectId_Key"
        };

    private static ProjectEnvironmentView ToView(ProjectEnvironment environment) =>
        new(
            environment.Id,
            environment.ProjectId,
            environment.Name,
            environment.Key,
            environment.SortOrder,
            environment.IsProduction,
            environment.CreatedAt,
            environment.UpdatedAt);
}
