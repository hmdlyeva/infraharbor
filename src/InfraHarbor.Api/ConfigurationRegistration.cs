using System.Text;
using System.Threading.RateLimiting;
using InfraHarbor.Application;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace InfraHarbor.Api;

internal static class AuthRateLimitPolicies
{
    public const string Login = "auth-login";
    public const string Refresh = "auth-refresh";
}

internal static class ConfigurationRegistration
{
    public static IServiceCollection AddInfraHarborConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RuntimeOptions>()
            .Bind(configuration.GetSection(RuntimeOptions.SectionName))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.DeploymentName), "Runtime:DeploymentName is required")
            .Validate(static options => Uri.TryCreate(options.PublicUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps), "Runtime:PublicUrl must be an absolute HTTP(S) URL")
            .ValidateOnStart();

        services.AddOptions<BootstrapOptions>()
            .Bind(configuration.GetSection(BootstrapOptions.SectionName))
            .Validate(
                static options => !options.Enabled || (!string.IsNullOrWhiteSpace(options.Token) && options.Token.Length >= 32),
                "Bootstrap:Token must contain at least 32 characters when bootstrap is enabled")
            .ValidateOnStart();

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Issuer), "Auth:Issuer is required")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Audience), "Auth:Audience is required")
            .Validate(static options => IsSigningKeyValid(options.SigningKey), "Auth:SigningKey must contain at least 32 UTF-8 bytes")
            .Validate(static options => options.AccessTokenLifetimeSeconds is >= 60 and <= 3600, "Auth:AccessTokenLifetimeSeconds must be between 60 and 3600")
            .Validate(static options => options.RefreshTokenLifetimeDays is >= 1 and <= 365, "Auth:RefreshTokenLifetimeDays must be between 1 and 365")
            .Validate(static options => options.ClockSkewSeconds is >= 0 and <= 300, "Auth:ClockSkewSeconds must be between 0 and 300")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.RefreshCookieName) && options.RefreshCookieName.Length <= 128, "Auth:RefreshCookieName is required and must be at most 128 characters")
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Database is required. Set ConnectionStrings__Database in the environment.");
        }

        services.AddSingleton(new DatabaseOptions { ConnectionString = connectionString });
        return services;
    }

    public static IServiceCollection AddInfraHarborAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var auth = new AuthOptions();
        configuration.GetSection(AuthOptions.SectionName).Bind(auth);
        if (!IsSigningKeyValid(auth.SigningKey))
        {
            throw new InvalidOperationException("Auth:SigningKey is required and must contain at least 32 UTF-8 bytes.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey!));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = auth.Issuer,
                    ValidateAudience = true,
                    ValidAudience = auth.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(auth.ClockSkewSeconds)
                };
            });

        services.AddAuthorization();
        services.AddSingleton<JwtAccessTokenIssuer>();
        return services;
    }

    public static IServiceCollection AddInfraHarborRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AuthRateLimitPolicies.Login, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    static _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(AuthRateLimitPolicies.Refresh, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    static _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }

    private static bool IsSigningKeyValid(string? signingKey) =>
        !string.IsNullOrWhiteSpace(signingKey) && Encoding.UTF8.GetByteCount(signingKey) >= 32;
}
