using InfraHarbor.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfraHarborConfiguration(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapGet("/", () => Results.Ok(new
{
    service = "InfraHarbor.Api",
    status = "foundation"
}));

app.Run();

public partial class Program;
