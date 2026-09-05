namespace InfraHarbor.Application.Security;

public static class RoleNames
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyList<string> All = [Owner, Admin, Operator, Viewer];
}
