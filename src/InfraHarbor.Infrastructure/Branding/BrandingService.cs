using System.Text.RegularExpressions;
using InfraHarbor.Application.Branding;
using InfraHarbor.Domain.Branding;

namespace InfraHarbor.Infrastructure.Branding;

public sealed class BrandingService(
    IBrandingRepository repository,
    TimeProvider timeProvider) : IBrandingService
{
    private static readonly Guid InstallationBrandingId = Guid.Parse("00000000-0000-0000-0000-000000000019");
    private const int ProductNameMaxLength = 120;
    private const int ShortNameMaxLength = 40;
    private const int UrlMaxLength = 2048;
    private const int FooterTextMaxLength = 500;
    private const int LoginHeadlineMaxLength = 200;

    public async Task<BrandingView> GetEffectiveAsync(CancellationToken cancellationToken)
    {
        var stored = await repository.GetAsync(cancellationToken);
        return stored is null ? BrandingDefaults.Upstream : SanitizeStored(stored);
    }

    public async Task<BrandingUpdateResult> UpdateAsync(
        UpdateBrandingCommand command,
        CancellationToken cancellationToken)
    {
        var productName = command.ProductName?.Trim() ?? string.Empty;
        var shortName = command.ShortName?.Trim() ?? string.Empty;
        var logoUrl = NormalizeOptional(command.LogoUrl);
        var faviconUrl = NormalizeOptional(command.FaviconUrl);
        var primaryColor = (command.PrimaryColor?.Trim() ?? string.Empty).ToUpperInvariant();
        var supportUrl = NormalizeOptional(command.SupportUrl);
        var documentationUrl = NormalizeOptional(command.DocumentationUrl);
        var footerText = command.FooterText?.Trim() ?? string.Empty;
        var loginHeadline = NormalizeOptional(command.LoginHeadline);

        var errors = Validate(
            productName,
            shortName,
            logoUrl,
            faviconUrl,
            primaryColor,
            supportUrl,
            documentationUrl,
            footerText,
            loginHeadline);

        if (errors.Count > 0)
        {
            return new BrandingUpdateResult(BrandingUpdateOutcome.ValidationFailed, Errors: errors);
        }

        var settings = new BrandingSettings
        {
            Id = InstallationBrandingId,
            ProductName = productName,
            ShortName = shortName,
            LogoUrl = logoUrl,
            FaviconUrl = faviconUrl,
            PrimaryColor = primaryColor,
            SupportUrl = supportUrl,
            DocumentationUrl = documentationUrl,
            FooterText = footerText,
            LoginHeadline = loginHeadline,
            UpdatedAt = timeProvider.GetUtcNow()
        };

        await repository.UpsertAsync(settings, cancellationToken);
        return new BrandingUpdateResult(BrandingUpdateOutcome.Success, ToView(settings));
    }

    private static BrandingView SanitizeStored(BrandingSettings stored)
    {
        var defaults = BrandingDefaults.Upstream;
        var productName = IsTextValid(stored.ProductName, ProductNameMaxLength)
            ? stored.ProductName.Trim()
            : defaults.ProductName;
        var shortName = IsTextValid(stored.ShortName, ShortNameMaxLength)
            ? stored.ShortName.Trim()
            : defaults.ShortName;
        var primaryColor = IsHexColor(stored.PrimaryColor)
            ? stored.PrimaryColor.Trim().ToUpperInvariant()
            : defaults.PrimaryColor;
        var footerText = IsTextValid(stored.FooterText, FooterTextMaxLength)
            ? stored.FooterText.Trim()
            : defaults.FooterText;

        return new BrandingView(
            productName,
            shortName,
            SafeStoredUrl(stored.LogoUrl, defaults.LogoUrl),
            SafeStoredUrl(stored.FaviconUrl, defaults.FaviconUrl),
            primaryColor,
            SafeStoredUrl(stored.SupportUrl, defaults.SupportUrl),
            SafeStoredUrl(stored.DocumentationUrl, defaults.DocumentationUrl),
            footerText,
            SafeStoredText(stored.LoginHeadline, LoginHeadlineMaxLength, defaults.LoginHeadline));
    }

    private static List<string> Validate(
        string productName,
        string shortName,
        string? logoUrl,
        string? faviconUrl,
        string primaryColor,
        string? supportUrl,
        string? documentationUrl,
        string footerText,
        string? loginHeadline)
    {
        var errors = new List<string>();
        ValidateRequiredText(errors, productName, ProductNameMaxLength, "Product name");
        ValidateRequiredText(errors, shortName, ShortNameMaxLength, "Short name");
        ValidateRequiredText(errors, footerText, FooterTextMaxLength, "Footer text");

        if (!IsHexColor(primaryColor))
        {
            errors.Add("Primary color must be a six-digit hex CSS color such as #17324D.");
        }

        ValidateUrl(errors, logoUrl, "Logo URL");
        ValidateUrl(errors, faviconUrl, "Favicon URL");
        ValidateUrl(errors, supportUrl, "Support URL");
        ValidateUrl(errors, documentationUrl, "Documentation URL");

        if (loginHeadline is not null)
        {
            if (loginHeadline.Length > LoginHeadlineMaxLength)
            {
                errors.Add($"Login headline cannot exceed {LoginHeadlineMaxLength} characters.");
            }
            else if (ContainsUnsafeMarkup(loginHeadline))
            {
                errors.Add("Login headline must be plain text and cannot contain markup.");
            }
        }

        return errors;
    }

    private static void ValidateRequiredText(List<string> errors, string value, int maxLength, string fieldName)
    {
        if (value.Length < 1 || value.Length > maxLength)
        {
            errors.Add($"{fieldName} must be between 1 and {maxLength} characters.");
            return;
        }

        if (ContainsUnsafeMarkup(value))
        {
            errors.Add($"{fieldName} must be plain text and cannot contain markup.");
        }
    }

    private static void ValidateUrl(List<string> errors, string? value, string fieldName)
    {
        if (value is null)
        {
            return;
        }

        if (value.Length > UrlMaxLength || !IsAllowedHttpUrl(value))
        {
            errors.Add($"{fieldName} must be an absolute HTTP or HTTPS URL no longer than {UrlMaxLength} characters.");
        }
    }

    private static bool IsTextValid(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Trim().Length <= maxLength &&
        !ContainsUnsafeMarkup(value);

    private static bool ContainsUnsafeMarkup(string value) =>
        value.Contains('<', StringComparison.Ordinal) || value.Contains('>', StringComparison.Ordinal);

    private static bool IsHexColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        Regex.IsMatch(value.Trim(), "^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant);

    private static bool IsAllowedHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? SafeStoredUrl(string? value, string? fallback) =>
        NormalizeOptional(value) is { } normalized && normalized.Length <= UrlMaxLength && IsAllowedHttpUrl(normalized)
            ? normalized
            : fallback;

    private static string? SafeStoredText(string? value, int maxLength, string? fallback) =>
        NormalizeOptional(value) is { } normalized && normalized.Length <= maxLength && !ContainsUnsafeMarkup(normalized)
            ? normalized
            : fallback;

    private static BrandingView ToView(BrandingSettings settings) =>
        new(
            settings.ProductName,
            settings.ShortName,
            settings.LogoUrl,
            settings.FaviconUrl,
            settings.PrimaryColor,
            settings.SupportUrl,
            settings.DocumentationUrl,
            settings.FooterText,
            settings.LoginHeadline);
}
