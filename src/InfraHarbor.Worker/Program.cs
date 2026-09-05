using InfraHarbor.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfraHarborConfiguration(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
