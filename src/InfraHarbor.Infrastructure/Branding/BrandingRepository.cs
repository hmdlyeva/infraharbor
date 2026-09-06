using System.Data;
using System.Data.Common;
using InfraHarbor.Application.Branding;
using InfraHarbor.Domain.Branding;
using InfraHarbor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfraHarbor.Infrastructure.Branding;

public sealed class BrandingRepository(InfraHarborDbContext db) : IBrandingRepository
{
    private static readonly Guid InstallationBrandingId = Guid.Parse("00000000-0000-0000-0000-000000000019");

    public async Task<BrandingSettings?> GetAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "Id", "ProductName", "ShortName", "LogoUrl", "FaviconUrl", "PrimaryColor",
                       "SupportUrl", "DocumentationUrl", "FooterText", "LoginHeadline"
                FROM "BrandingSettings"
                WHERE "Id" = @id
                LIMIT 1;
                """;
            AddParameter(command, "@id", InstallationBrandingId, DbType.Guid);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new BrandingSettings
            {
                Id = reader.GetGuid(0),
                ProductName = reader.GetString(1),
                ShortName = reader.GetString(2),
                LogoUrl = GetNullableString(reader, 3),
                FaviconUrl = GetNullableString(reader, 4),
                PrimaryColor = reader.GetString(5),
                SupportUrl = GetNullableString(reader, 6),
                DocumentationUrl = GetNullableString(reader, 7),
                FooterText = reader.GetString(8),
                LoginHeadline = GetNullableString(reader, 9)
            };
        }
        finally
        {
            if (shouldClose)
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }

    public async Task UpsertAsync(BrandingSettings settings, CancellationToken cancellationToken)
    {
        if (settings.Id != InstallationBrandingId)
        {
            throw new InvalidOperationException("Branding settings must use the installation branding identifier.");
        }

        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO "BrandingSettings"
                    ("Id", "ProductName", "ShortName", "LogoUrl", "FaviconUrl", "PrimaryColor",
                     "SupportUrl", "DocumentationUrl", "FooterText", "LoginHeadline", "UpdatedAt")
                VALUES
                    (@id, @productName, @shortName, @logoUrl, @faviconUrl, @primaryColor,
                     @supportUrl, @documentationUrl, @footerText, @loginHeadline, @updatedAt)
                ON CONFLICT ("Id") DO UPDATE SET
                    "ProductName" = EXCLUDED."ProductName",
                    "ShortName" = EXCLUDED."ShortName",
                    "LogoUrl" = EXCLUDED."LogoUrl",
                    "FaviconUrl" = EXCLUDED."FaviconUrl",
                    "PrimaryColor" = EXCLUDED."PrimaryColor",
                    "SupportUrl" = EXCLUDED."SupportUrl",
                    "DocumentationUrl" = EXCLUDED."DocumentationUrl",
                    "FooterText" = EXCLUDED."FooterText",
                    "LoginHeadline" = EXCLUDED."LoginHeadline",
                    "UpdatedAt" = EXCLUDED."UpdatedAt";
                """;

            AddParameter(command, "@id", settings.Id, DbType.Guid);
            AddParameter(command, "@productName", settings.ProductName, DbType.String);
            AddParameter(command, "@shortName", settings.ShortName, DbType.String);
            AddParameter(command, "@logoUrl", settings.LogoUrl, DbType.String);
            AddParameter(command, "@faviconUrl", settings.FaviconUrl, DbType.String);
            AddParameter(command, "@primaryColor", settings.PrimaryColor, DbType.String);
            AddParameter(command, "@supportUrl", settings.SupportUrl, DbType.String);
            AddParameter(command, "@documentationUrl", settings.DocumentationUrl, DbType.String);
            AddParameter(command, "@footerText", settings.FooterText, DbType.String);
            AddParameter(command, "@loginHeadline", settings.LoginHeadline, DbType.String);
            AddParameter(command, "@updatedAt", settings.UpdatedAt.UtcDateTime, DbType.DateTime);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }

    private static string? GetNullableString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
