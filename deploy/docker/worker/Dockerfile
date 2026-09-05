FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props global.json InfraHarbor.slnx ./
COPY src/InfraHarbor.Domain/InfraHarbor.Domain.csproj src/InfraHarbor.Domain/
COPY src/InfraHarbor.Application/InfraHarbor.Application.csproj src/InfraHarbor.Application/
COPY src/InfraHarbor.Infrastructure/InfraHarbor.Infrastructure.csproj src/InfraHarbor.Infrastructure/
COPY src/InfraHarbor.Worker/InfraHarbor.Worker.csproj src/InfraHarbor.Worker/
RUN dotnet restore src/InfraHarbor.Worker/InfraHarbor.Worker.csproj
COPY src src
RUN dotnet publish src/InfraHarbor.Worker/InfraHarbor.Worker.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InfraHarbor.Worker.dll"]
