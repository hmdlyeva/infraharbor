using InfraHarbor.Application.Security;
using Microsoft.Extensions.Logging;

namespace InfraHarbor.Infrastructure.Security;

public sealed class LoggingSecurityAuditSink(ILogger<LoggingSecurityAuditSink> logger) : ISecurityAuditSink
{
    public ValueTask WriteAsync(SecurityAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "SecurityAudit Action={Action} ActorUserId={ActorUserId} TargetUserId={TargetUserId} Outcome={Outcome} Context={Context}",
            auditEvent.Action,
            auditEvent.ActorUserId,
            auditEvent.TargetUserId,
            auditEvent.Outcome,
            auditEvent.Context);

        return ValueTask.CompletedTask;
    }
}
