namespace InfraHarbor.Application.Security;

public static class AuthorizationPolicyNames
{
    public const string Authenticated = "auth:authenticated";
    public const string ViewerAccess = "auth:viewer-access";
    public const string OperatorAccess = "auth:operator-access";
    public const string AdminAccess = "auth:admin-access";
    public const string OwnerOnly = "auth:owner-only";
}
