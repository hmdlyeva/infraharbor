using InfraHarbor.Application.Projects;
using InfraHarbor.Domain.Projects;
using InfraHarbor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraHarbor.Infrastructure.Projects;

public sealed class ProjectRepository(InfraHarborDbContext db) : IProjectRepository
{
    public async Task<IReadOnlyList<Project>> ListAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var query = db.Projects.AsNoTracking();
        if (!includeArchived)
        {
            query = query.Where(project => !project.IsArchived);
        }

        return await query
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Slug)
            .ToListAsync(cancellationToken);
    }

    public Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Projects.FirstOrDefaultAsync(project => project.Id == id, cancellationToken);

    public Task<Project?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
        db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(project => project.Slug == slug, cancellationToken);

    public async Task AddAsync(Project project, CancellationToken cancellationToken)
    {
        await db.Projects.AddAsync(project, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
    }
}
