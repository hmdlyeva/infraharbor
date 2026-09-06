using InfraHarbor.Application.Projects;
using InfraHarbor.Domain.Projects;
using InfraHarbor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraHarbor.Infrastructure.Projects;

public sealed class ProjectEnvironmentRepository(InfraHarborDbContext db) : IProjectEnvironmentRepository
{
    public async Task<IReadOnlyList<ProjectEnvironment>> ListByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken) =>
        await db.Environments
            .AsNoTracking()
            .Where(environment => environment.ProjectId == projectId)
            .OrderBy(environment => environment.SortOrder)
            .ThenBy(environment => environment.Name)
            .ToListAsync(cancellationToken);

    public Task<ProjectEnvironment?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Environments.FirstOrDefaultAsync(environment => environment.Id == id, cancellationToken);

    public Task<ProjectEnvironment?> FindByKeyAsync(
        Guid projectId,
        string key,
        CancellationToken cancellationToken) =>
        db.Environments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                environment => environment.ProjectId == projectId && environment.Key == key,
                cancellationToken);

    public async Task AddAsync(ProjectEnvironment environment, CancellationToken cancellationToken)
    {
        await db.Environments.AddAsync(environment, cancellationToken);
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<ProjectEnvironment> environments,
        CancellationToken cancellationToken)
    {
        await db.Environments.AddRangeAsync(environments, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
    }
}
