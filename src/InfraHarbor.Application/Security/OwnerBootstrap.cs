namespace InfraHarbor.Application.Security;

public sealed record OwnerBootstrapCommand(
    string Email,
    string DisplayName,
    string Password,
    string BootstrapToken);

public enum OwnerBootstrapOutcome
{
    Created,
    Disabled,
    InvalidToken,
    AlreadyInitialized,
    ValidationFailed
}

public sealed record OwnerBootstrapResult(
    OwnerBootstrapOutcome Outcome,
    Guid? UserId = null,
    string? Email = null,
    string? DisplayName = null,
    IReadOnlyList<string>? Errors = null);

public interface IOwnerBootstrapService
{
    Task<OwnerBootstrapResult> BootstrapAsync(OwnerBootstrapCommand command, CancellationToken cancellationToken);
}
