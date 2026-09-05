namespace InfraHarbor.Application;

public sealed class RuntimeOptions
{
    public const string SectionName = "Runtime";
    public required string DeploymentName { get; init; }
    public required string PublicUrl { get; init; }
}

public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "Database";
    public required string ConnectionString { get; init; }
}
