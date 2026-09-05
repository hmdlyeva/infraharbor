using InfraHarbor.Application.Security;
using InfraHarbor.Infrastructure.Identity;
using InfraHarbor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<InfraHarborDbContext>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IOwnerBootstrapService, OwnerBootstrapService>();
        services.AddScoped<IAuthSessionService, AuthSessionService>();
        return services;
    }
}
