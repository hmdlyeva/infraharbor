using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InfraHarbor.Application;
using InfraHarbor.Application.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InfraHarbor.Api;

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);

public sealed class JwtAccessTokenIssuer(
    IOptions<AuthOptions> authOptions,
    TimeProvider timeProvider)
{
    public AccessTokenResult Issue(
        Guid userId,
        string email,
        string displayName,
        IReadOnlyCollection<string> roles)
    {
        var options = authOptions.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddSeconds(options.AccessTokenLifetimeSeconds);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey!));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Name, displayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(
            roles
                .Where(role => RoleNames.All.Contains(role))
                .Distinct(StringComparer.Ordinal)
                .Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
