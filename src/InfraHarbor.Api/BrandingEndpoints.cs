using InfraHarbor.Application.Branding;
using InfraHarbor.Application.Security;

namespace InfraHarbor.Api;

internal sealed record UpdateBrandingRequest(
    string? ProductName,
    string? ShortName,
    string? LogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? SupportUrl,
    string? DocumentationUrl,
    string? FooterText,
    string? LoginHeadline);

public static class BrandingEndpoints
{
    public static IEndpointRouteBuilder MapBrandingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/branding/public", GetPublicAsync)
            .AllowAnonymous();

        endpoints.MapGet("/api/admin/branding", GetAdminAsync)
            .RequireAuthorization(AuthorizationPolicyNames.OwnerOnly);

        endpoints.MapPut("/api/admin/branding", UpdateAsync)
            .RequireAuthorization(AuthorizationPolicyNames.OwnerOnly);

        return endpoints;
    }

    private static async Task<IResult> GetPublicAsync(
        HttpContext httpContext,
        IBrandingService service,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers["Cache-Control"] = "public, max-age=60";
        return Results.Ok(await service.GetEffectiveAsync(cancellationToken));
    }

    private static async Task<IResult> GetAdminAsync(
        IBrandingService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.GetEffectiveAsync(cancellationToken));

    private static async Task<IResult> UpdateAsync(
        UpdateBrandingRequest request,
        IBrandingService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(
            new UpdateBrandingCommand(
                request.ProductName,
                request.ShortName,
                request.LogoUrl,
                request.FaviconUrl,
                request.PrimaryColor,
                request.SupportUrl,
                request.DocumentationUrl,
                request.FooterText,
                request.LoginHeadline),
            cancellationToken);

        return result.Outcome switch
        {
            BrandingUpdateOutcome.Success when result.Branding is not null => Results.Ok(result.Branding),
            BrandingUpdateOutcome.ValidationFailed => Results.BadRequest(new
            {
                code = "branding_validation_failed",
                errors = result.Errors ?? []
            }),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
