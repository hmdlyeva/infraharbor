namespace InfraHarbor.Application.Security;

public sealed record UserAdministrationUser(
    Guid Id,
    string Email,
    string DisplayName,
    string Status,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateManagedUserCommand(
    string Email,
    string DisplayName,
    string Password,
    IReadOnlyList<string> Roles);

public sealed record UpdateManagedUserCommand(string DisplayName);

public sealed record SetManagedUserRolesCommand(IReadOnlyList<string> Roles);

public enum UserAdministrationOutcome
{
    Success,
    Created,
    NotFound,
    ValidationFailed,
    Forbidden,
    Conflict
}

public sealed record UserAdministrationResult(
    UserAdministrationOutcome Outcome,
    UserAdministrationUser? User = null,
    IReadOnlyList<string>? Errors = null);

public interface IUserAdministrationService
{
    Task<IReadOnlyList<UserAdministrationUser>> ListAsync(CancellationToken cancellationToken);

    Task<UserAdministrationResult> CreateAsync(
        Guid actorUserId,
        CreateManagedUserCommand command,
        CancellationToken cancellationToken);

    Task<UserAdministrationResult> UpdateAsync(
        Guid actorUserId,
        Guid targetUserId,
        UpdateManagedUserCommand command,
        CancellationToken cancellationToken);

    Task<UserAdministrationResult> SetRolesAsync(
        Guid actorUserId,
        Guid targetUserId,
        SetManagedUserRolesCommand command,
        CancellationToken cancellationToken);

    Task<UserAdministrationResult> DisableAsync(
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancellationToken);
}

public interface IUserAccessValidator
{
    Task<bool> IsAccessTokenValidAsync(
        Guid userId,
        string securityStamp,
        CancellationToken cancellationToken);
}
