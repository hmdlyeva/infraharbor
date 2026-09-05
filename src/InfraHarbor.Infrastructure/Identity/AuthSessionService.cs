using System.Data;
using System.Security.Cryptography;
using System.Text;
using InfraHarbor.Application;
using InfraHarbor.Application.Security;
using InfraHarbor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InfraHarbor.Infrastructure.Identity;

public sealed class AuthSessionService(
    InfraHarborDbContext db,
    UserManager<ApplicationUser> userManager,
    IOptions<AuthOptions> authOptions,
    TimeProvider timeProvider) : IAuthSessionService
{
    public async Task<AuthSessionResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
        {
            return Rejected();
        }

        var user = await userManager.FindByEmailAsync(command.Email.Trim());
        if (user is null || user.Status != UserStatus.Active || await userManager.IsLockedOutAsync(user))
        {
            return Rejected();
        }

        if (!await userManager.CheckPasswordAsync(user, command.Password))
        {
            await userManager.AccessFailedAsync(user);
            return Rejected();
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var now = timeProvider.GetUtcNow();
        var (session, rawToken) = CreateSession(user.Id, Guid.NewGuid(), command.Metadata, now);
        db.RefreshSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return await AuthenticatedAsync(user, rawToken, session.ExpiresAt);
    }

    public async Task<AuthSessionResult> RefreshAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Rejected();
        }

        var tokenHash = HashToken(command.RefreshToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var session = await db.RefreshSessions
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

        if (session is null)
        {
            return Rejected();
        }

        await LockSessionAsync(session.Id, cancellationToken);
        await db.Entry(session).ReloadAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        if (session.RevokedAt.HasValue || session.UsedAt.HasValue)
        {
            await RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Rejected();
        }

        if (session.ExpiresAt <= now)
        {
            session.RevokedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Rejected();
        }

        var user = await userManager.FindByIdAsync(session.UserId.ToString());
        if (user is null || user.Status != UserStatus.Active || await userManager.IsLockedOutAsync(user))
        {
            await RevokeFamilyAsync(session.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Rejected();
        }

        var (nextSession, rawToken) = CreateSession(user.Id, session.FamilyId, command.Metadata, now);
        session.UsedAt = now;
        session.ReplacedBySessionId = nextSession.Id;
        db.RefreshSessions.Add(nextSession);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await AuthenticatedAsync(user, rawToken, nextSession.ExpiresAt);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = HashToken(refreshToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var session = await db.RefreshSessions
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

        if (session is null)
        {
            return;
        }

        await LockSessionAsync(session.Id, cancellationToken);
        await db.Entry(session).ReloadAsync(cancellationToken);
        await RevokeFamilyAsync(session.FamilyId, timeProvider.GetUtcNow(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private (RefreshSession Session, string RawToken) CreateSession(
        Guid userId,
        Guid familyId,
        AuthSessionMetadata metadata,
        DateTimeOffset now)
    {
        var rawToken = GenerateToken();
        var session = new RefreshSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = HashToken(rawToken),
            CreatedAt = now,
            ExpiresAt = now.AddDays(authOptions.Value.RefreshTokenLifetimeDays),
            UserAgent = Limit(metadata.UserAgent, 512),
            IpAddress = Limit(metadata.IpAddress, 64)
        };

        return (session, rawToken);
    }

    private async Task LockSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"RefreshSessions\" WHERE \"Id\" = {sessionId} FOR UPDATE",
            cancellationToken);
    }

    private async Task RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        await db.RefreshSessions
            .Where(item => item.FamilyId == familyId && item.RevokedAt == null)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(item => item.RevokedAt, revokedAt),
                cancellationToken);
    }

    private async Task<AuthSessionResult> AuthenticatedAsync(
        ApplicationUser user,
        string refreshToken,
        DateTimeOffset refreshExpiresAt)
    {
        var roles = (await userManager.GetRolesAsync(user))
            .Where(role => RoleNames.All.Contains(role))
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();

        return new AuthSessionResult(
            AuthSessionOutcome.Authenticated,
            user.Id,
            user.Email,
            user.DisplayName,
            refreshToken,
            refreshExpiresAt,
            roles);
    }

    private static AuthSessionResult Rejected() => new(AuthSessionOutcome.Rejected);

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string? Limit(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
