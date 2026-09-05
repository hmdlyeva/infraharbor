using InfraHarbor.Infrastructure.Identity;
using InfraHarbor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfraHarbor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfraHarborPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<InfraHarborDbContext>(options => options.UseNpgsql(connectionString));
        return services;
    }

    public static IServiceCollection AddInfraHarborIdentity(this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<InfraHarborDbContext>();

        return services;
    }
}
