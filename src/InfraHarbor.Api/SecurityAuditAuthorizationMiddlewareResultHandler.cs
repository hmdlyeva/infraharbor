using System.IdentityModel.Tokens.Jwt;
using InfraHarbor.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace InfraHarbor.Api;

public sealed class SecurityAuditAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _fallback = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            var auditSink = context.RequestServices.GetService<ISecurityAuditSink>();
            if (auditSink is not null)
            {
                Guid? actorUserId = null;
                var subject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                if (Guid.TryParse(subject, out var parsed))
                {
                    actorUserId = parsed;
                }

                var policies = context.GetEndpoint()?
                    .Metadata
                    .GetOrderedMetadata<IAuthorizeData>()
                    .Select(item => item.Policy)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray() ?? [];

                await auditSink.WriteAsync(
                    new SecurityAuditEvent(
                        SecurityAuditActions.AuthorizationDenied,
                        actorUserId,
                        null,
                        "forbidden",
                        new Dictionary<string, string>
                        {
                            ["method"] = context.Request.Method,
                            ["path"] = context.Request.Path.Value ?? string.Empty,
                            ["policies"] = string.Join(',', policies)
                        }),
                    context.RequestAborted);
            }
        }

        await _fallback.HandleAsync(next, context, policy, authorizeResult);
    }
}
