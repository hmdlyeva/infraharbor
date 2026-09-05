using System.Security.Cryptography;
using System.Text;
using InfraHarbor.Application.Security;
using Microsoft.AspNetCore.Identity;

namespace InfraHarbor.Infrastructure.Identity;

public sealed class UserAccessValidator(UserManager<ApplicationUser> userManager) : IUserAccessValidator
{
    public async Task<bool> IsAccessTokenValidAsync(
        Guid userId,
        string securityStamp,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.Status != UserStatus.Active)
        {
            return false;
        }

        var currentStamp = await userManager.GetSecurityStampAsync(user);
        var left = Encoding.UTF8.GetBytes(currentStamp ?? string.Empty);
        var right = Encoding.UTF8.GetBytes(securityStamp ?? string.Empty);

        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
