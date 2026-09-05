using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InfraHarbor.Infrastructure.Persistence;

public sealed class InfraHarborDbContextFactory : IDesignTimeDbContextFactory<InfraHarborDbContext>
{
    public InfraHarborDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Host=localhost;Port=5432;Database=infraharbor_design;Username=infraharbor;Password=design-time-only";

        var options = new DbContextOptionsBuilder<InfraHarborDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new InfraHarborDbContext(options);
    }
}
