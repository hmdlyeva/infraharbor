using InfraHarbor.Application.Branding;
using InfraHarbor.Application.Projects;
using InfraHarbor.Application.Security;
using InfraHarbor.Infrastructure.Branding;
using InfraHarbor.Infrastructure.Identity;
using InfraHarbor.Infrastructure.Persistence;
using InfraHarbor.Infrastructure.Projects;
using InfraHarbor.Infrastructure.Security;
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
        services.TryAddSingleton<ISecurityAuditSink, LoggingSecurityAuditSink>();
        services.AddScoped<IOwnerBootstrapService, OwnerBootstrapService>();
        services.AddScoped<IAuthSessionService, AuthSessionService>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<IUserAccessValidator, UserAccessValidator>();
        return services;
    }

    public static IServiceCollection AddInfraHarborProjects(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectEnvironmentRepository, ProjectEnvironmentRepository>();
        services.AddScoped<IProjectEnvironmentService, ProjectEnvironmentService>();
        services.AddScoped<IProjectService, ProjectService>();
        return services;
    }

    public static IServiceCollection AddInfraHarborBranding(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IBrandingRepository, BrandingRepository>();
        services.AddScoped<IBrandingService, BrandingService>();
        return services;
    }
}
