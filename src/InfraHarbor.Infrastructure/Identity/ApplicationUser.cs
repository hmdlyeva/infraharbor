using Microsoft.AspNetCore.Identity;

namespace InfraHarbor.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
