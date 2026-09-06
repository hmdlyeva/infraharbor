using InfraHarbor.Api;
using InfraHarbor.Application;
using InfraHarbor.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfraHarborConfiguration(builder.Configuration);

var databaseOptions = new DatabaseOptions
{
    ConnectionString = builder.Configuration.GetConnectionString(DatabaseOptions.ConnectionStringName)
        ?? throw new InvalidOperationException("ConnectionStrings:Database is required.")
};

builder.Services.AddInfraHarborPersistence(databaseOptions.ConnectionString);
builder.Services.AddInfraHarborIdentity();
builder.Services.AddInfraHarborAuthentication(builder.Configuration);
builder.Services.AddInfraHarborAuthorization();
builder.Services.AddInfraHarborRateLimiting();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapAuthEndpoints();

app.MapGet("/", () => Results.Ok(new
{
    service = "InfraHarbor.Api",
    status = "foundation"
}));

app.Run();

public partial class Program;
