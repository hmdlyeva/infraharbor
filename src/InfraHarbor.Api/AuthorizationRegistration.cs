using InfraHarbor.Application.Security;

namespace InfraHarbor.Api;

public static class AuthorizationRegistration
{
    public static IServiceCollection AddInfraHarborAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicyNames.Authenticated,
                policy => policy.RequireAuthenticatedUser());

            options.AddPolicy(
                AuthorizationPolicyNames.ViewerAccess,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(RoleNames.Viewer, RoleNames.Operator, RoleNames.Admin, RoleNames.Owner));

            options.AddPolicy(
                AuthorizationPolicyNames.OperatorAccess,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(RoleNames.Operator, RoleNames.Admin, RoleNames.Owner));

            options.AddPolicy(
                AuthorizationPolicyNames.AdminAccess,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(RoleNames.Admin, RoleNames.Owner));

            options.AddPolicy(
                AuthorizationPolicyNames.OwnerOnly,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(RoleNames.Owner));
        });

        return services;
    }
}
