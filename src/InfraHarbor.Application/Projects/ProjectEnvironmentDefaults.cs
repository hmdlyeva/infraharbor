using InfraHarbor.Domain.Projects;

namespace InfraHarbor.Application.Projects;

public static class ProjectEnvironmentDefaults
{
    public static IReadOnlyList<ProjectEnvironment> Create(Guid projectId, DateTimeOffset now) =>
    [
        new ProjectEnvironment
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "Development",
            Key = "development",
            SortOrder = 10,
            IsProduction = false,
            CreatedAt = now,
            UpdatedAt = now
        },
        new ProjectEnvironment
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "Staging",
            Key = "staging",
            SortOrder = 20,
            IsProduction = false,
            CreatedAt = now,
            UpdatedAt = now
        },
        new ProjectEnvironment
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "Production",
            Key = "production",
            SortOrder = 30,
            IsProduction = true,
            CreatedAt = now,
            UpdatedAt = now
        }
    ];
}
