using System.Text.RegularExpressions;
using InfraHarbor.Application.Projects;
using InfraHarbor.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InfraHarbor.Infrastructure.Projects;

public sealed class ProjectService(
    IProjectRepository repository,
    IProjectEnvironmentRepository environmentRepository,
    TimeProvider timeProvider) : IProjectService
{
    private const int MaxNameLength = 120;
    private const int MaxSlugLength = 80;
    private const int MaxDescriptionLength = 2000;

    public async Task<IReadOnlyList<ProjectView>> ListAsync(
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var projects = await repository.ListAsync(includeArchived, cancellationToken);
        return projects.Select(ToView).ToArray();
    }

    public async Task<ProjectOperationResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await repository.GetAsync(id, cancellationToken);
        return project is null
            ? new ProjectOperationResult(ProjectOperationOutcome.NotFound)
            : new ProjectOperationResult(ProjectOperationOutcome.Success, ToView(project));
    }

    public async Task<ProjectOperationResult> CreateAsync(
        CreateProjectCommand command,
        CancellationToken cancellationToken)
    {
        var (name, slug, description, errors) = Validate(command.Name, command.Slug, command.Description);
        if (errors.Count > 0)
        {
            return ValidationFailure(errors);
        }

        if (await repository.FindBySlugAsync(slug!, cancellationToken) is not null)
        {
            return SlugConflict();
        }

        var now = timeProvider.GetUtcNow();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name!,
            Slug = slug!,
            Description = description,
            IsArchived = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await repository.AddAsync(project, cancellationToken);
        await environmentRepository.AddRangeAsync(ProjectEnvironmentDefaults.Create(project.Id, now), cancellationToken);
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsSlugConflict(exception))
        {
            return SlugConflict();
        }

        return new ProjectOperationResult(ProjectOperationOutcome.Created, ToView(project));
    }

    public async Task<ProjectOperationResult> UpdateAsync(
        Guid id,
        UpdateProjectCommand command,
        CancellationToken cancellationToken)
    {
        var project = await repository.GetAsync(id, cancellationToken);
        if (project is null)
        {
            return new ProjectOperationResult(ProjectOperationOutcome.NotFound);
        }

        if (command.Name is null && command.Slug is null && command.Description is null)
        {
            return ValidationFailure(["At least one project field must be supplied."]);
        }

        var requestedName = command.Name is null ? project.Name : command.Name;
        var requestedSlug = command.Slug is null ? project.Slug : command.Slug;
        var requestedDescription = command.Description is null ? project.Description : command.Description;
        var (name, slug, description, errors) = Validate(requestedName, requestedSlug, requestedDescription);
        if (errors.Count > 0)
        {
            return ValidationFailure(errors);
        }

        if (!string.Equals(project.Slug, slug, StringComparison.Ordinal))
        {
            var existing = await repository.FindBySlugAsync(slug!, cancellationToken);
            if (existing is not null && existing.Id != project.Id)
            {
                return SlugConflict();
            }
        }

        project.Name = name!;
        project.Slug = slug!;
        project.Description = description;
        project.UpdatedAt = timeProvider.GetUtcNow();

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsSlugConflict(exception))
        {
            return SlugConflict();
        }

        return new ProjectOperationResult(ProjectOperationOutcome.Success, ToView(project));
    }

    public async Task<ProjectOperationResult> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await repository.GetAsync(id, cancellationToken);
        if (project is null)
        {
            return new ProjectOperationResult(ProjectOperationOutcome.NotFound);
        }

        if (!project.IsArchived)
        {
            project.IsArchived = true;
            project.UpdatedAt = timeProvider.GetUtcNow();
            await repository.SaveChangesAsync(cancellationToken);
        }

        return new ProjectOperationResult(ProjectOperationOutcome.Success, ToView(project));
    }

    private static (string? Name, string? Slug, string? Description, List<string> Errors) Validate(
        string? requestedName,
        string? requestedSlug,
        string? requestedDescription)
    {
        var errors = new List<string>();
        var name = requestedName?.Trim() ?? string.Empty;
        var slug = (requestedSlug?.Trim() ?? string.Empty).ToLowerInvariant();
        var description = string.IsNullOrWhiteSpace(requestedDescription)
            ? null
            : requestedDescription.Trim();

        if (name.Length is < 1 or > MaxNameLength)
        {
            errors.Add($"Project name must be between 1 and {MaxNameLength} characters.");
        }

        if (slug.Length is < 1 or > MaxSlugLength ||
            !Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant))
        {
            errors.Add("Project slug must contain only lowercase letters, numbers and single hyphens between segments.");
        }

        if (description is not null && description.Length > MaxDescriptionLength)
        {
            errors.Add($"Project description cannot exceed {MaxDescriptionLength} characters.");
        }

        return (name, slug, description, errors);
    }

    private static ProjectOperationResult ValidationFailure(IReadOnlyList<string> errors) =>
        new(ProjectOperationOutcome.ValidationFailed, Errors: errors);

    private static ProjectOperationResult SlugConflict() =>
        new(ProjectOperationOutcome.Conflict, Errors: ["A project with this slug already exists."]);

    private static bool IsSlugConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Projects_Slug"
        };

    private static ProjectView ToView(Project project) =>
        new(
            project.Id,
            project.Name,
            project.Slug,
            project.Description,
            project.IsArchived,
            project.CreatedAt,
            project.UpdatedAt);
}
