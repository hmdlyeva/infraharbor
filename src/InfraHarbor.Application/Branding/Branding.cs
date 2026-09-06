using InfraHarbor.Domain.Branding;

namespace InfraHarbor.Application.Branding;

public sealed record BrandingView(
    string ProductName,
    string ShortName,
    string? LogoUrl,
    string? FaviconUrl,
    string PrimaryColor,
    string? SupportUrl,
    string? DocumentationUrl,
    string FooterText,
    string? LoginHeadline);

public sealed record UpdateBrandingCommand(
    string? ProductName,
    string? ShortName,
    string? LogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? SupportUrl,
    string? DocumentationUrl,
    string? FooterText,
    string? LoginHeadline);

public enum BrandingUpdateOutcome
{
    Success,
    ValidationFailed
}

public sealed record BrandingUpdateResult(
    BrandingUpdateOutcome Outcome,
    BrandingView? Branding = null,
    IReadOnlyList<string>? Errors = null);

public static class BrandingDefaults
{
    public static BrandingView Upstream { get; } = new(
        ProductName: "InfraHarbor",
        ShortName: "IH",
        LogoUrl: null,
        FaviconUrl: null,
        PrimaryColor: "#17324D",
        SupportUrl: null,
        DocumentationUrl: null,
        FooterText: "InfraHarbor",
        LoginHeadline: null);
}

public interface IBrandingRepository
{
    Task<BrandingSettings?> GetAsync(CancellationToken cancellationToken);
    Task UpsertAsync(BrandingSettings settings, CancellationToken cancellationToken);
}

public interface IBrandingService
{
    Task<BrandingView> GetEffectiveAsync(CancellationToken cancellationToken);
    Task<BrandingUpdateResult> UpdateAsync(UpdateBrandingCommand command, CancellationToken cancellationToken);
}
