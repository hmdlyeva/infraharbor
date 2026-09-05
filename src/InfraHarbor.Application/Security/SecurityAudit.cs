namespace InfraHarbor.Application.Security;

public static class SecurityAuditActions
{
    public const string AuthorizationDenied = "authorization.denied";
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserRolesChanged = "user.roles.changed";
    public const string UserDisabled = "user.disabled";
    public const string UserMutationRejected = "user.mutation.rejected";
}

public sealed record SecurityAuditEvent(
    string Action,
    Guid? ActorUserId,
    Guid? TargetUserId,
    string Outcome,
    IReadOnlyDictionary<string, string>? Context = null);

public interface ISecurityAuditSink
{
    ValueTask WriteAsync(SecurityAuditEvent auditEvent, CancellationToken cancellationToken);
}
