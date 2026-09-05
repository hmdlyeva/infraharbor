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

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public bool Enabled { get; set; }

    public string? Token { get; set; }
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string Issuer { get; set; } = "InfraHarbor";

    public string Audience { get; set; } = "InfraHarbor.Api";

    public string? SigningKey { get; set; }

    public int AccessTokenLifetimeSeconds { get; set; } = 900;

    public int RefreshTokenLifetimeDays { get; set; } = 30;

    public int ClockSkewSeconds { get; set; } = 30;

    public string RefreshCookieName { get; set; } = "infraharbor_refresh";

    public bool SecureCookies { get; set; } = true;
}
