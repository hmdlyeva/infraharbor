namespace InfraHarbor.Application.Security;

public sealed record AuthSessionMetadata(string? UserAgent, string? IpAddress);

public sealed record LoginCommand(
    string Email,
    string Password,
    AuthSessionMetadata Metadata);

public sealed record RefreshSessionCommand(
    string RefreshToken,
    AuthSessionMetadata Metadata);

public enum AuthSessionOutcome
{
    Authenticated,
    Rejected
}

public sealed record AuthSessionResult(
    AuthSessionOutcome Outcome,
    Guid? UserId = null,
    string? Email = null,
    string? DisplayName = null,
    string? RefreshToken = null,
    DateTimeOffset? RefreshExpiresAt = null,
    IReadOnlyList<string>? Roles = null);

public interface IAuthSessionService
{
    Task<AuthSessionResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken);

    Task<AuthSessionResult> RefreshAsync(RefreshSessionCommand command, CancellationToken cancellationToken);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}
