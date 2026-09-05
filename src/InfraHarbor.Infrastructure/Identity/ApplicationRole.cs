using Microsoft.AspNetCore.Identity;

namespace InfraHarbor.Infrastructure.Identity;

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName)
        : base(roleName)
    {
    }
}
