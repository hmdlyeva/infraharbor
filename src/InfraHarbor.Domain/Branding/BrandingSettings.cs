namespace InfraHarbor.Domain.Branding;

public sealed class BrandingSettings
{
    public Guid Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string PrimaryColor { get; set; } = string.Empty;
    public string? SupportUrl { get; set; }
    public string? DocumentationUrl { get; set; }
    public string FooterText { get; set; } = string.Empty;
    public string? LoginHeadline { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
