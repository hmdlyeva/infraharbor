using InfraHarbor.Application.Branding;
using InfraHarbor.Domain.Branding;
using InfraHarbor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraHarbor.Infrastructure.Branding;

public sealed class BrandingRepository(InfraHarborDbContext db) : IBrandingRepository
{
    public Task<BrandingSettings?> GetAsync(CancellationToken cancellationToken) =>
        db.BrandingSettings.SingleOrDefaultAsync(cancellationToken);

    public async Task AddAsync(BrandingSettings settings, CancellationToken cancellationToken)
    {
        await db.BrandingSettings.AddAsync(settings, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
    }
}
