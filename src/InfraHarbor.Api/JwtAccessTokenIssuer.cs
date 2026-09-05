using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InfraHarbor.Application;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InfraHarbor.Api;

internal sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);

internal sealed class JwtAccessTokenIssuer(
    IOptions<AuthOptions> authOptions,
    TimeProvider timeProvider)
{
    public AccessTokenResult Issue(Guid userId, string email, string displayName)
    {
        var options = authOptions.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddSeconds(options.AccessTokenLifetimeSeconds);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey!));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Name, displayName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
