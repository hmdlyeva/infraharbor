using InfraHarbor.Application;

namespace InfraHarbor.Api;

internal static class ConfigurationRegistration
{
    public static IServiceCollection AddInfraHarborConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RuntimeOptions>()
            .Bind(configuration.GetSection(RuntimeOptions.SectionName))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.DeploymentName), "Runtime:DeploymentName is required")
            .Validate(static options => Uri.TryCreate(options.PublicUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps), "Runtime:PublicUrl must be an absolute HTTP(S) URL")
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Database is required. Set ConnectionStrings__Database in the environment.");
        }

        services.AddSingleton(new DatabaseOptions { ConnectionString = connectionString });
        return services;
    }
}
