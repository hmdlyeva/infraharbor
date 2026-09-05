FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props global.json InfraHarbor.slnx ./
COPY src/InfraHarbor.Domain/InfraHarbor.Domain.csproj src/InfraHarbor.Domain/
COPY src/InfraHarbor.Application/InfraHarbor.Application.csproj src/InfraHarbor.Application/
COPY src/InfraHarbor.Infrastructure/InfraHarbor.Infrastructure.csproj src/InfraHarbor.Infrastructure/
COPY src/InfraHarbor.Api/InfraHarbor.Api.csproj src/InfraHarbor.Api/
RUN dotnet restore src/InfraHarbor.Api/InfraHarbor.Api.csproj
COPY src src
RUN dotnet publish src/InfraHarbor.Api/InfraHarbor.Api.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "InfraHarbor.Api.dll"]
