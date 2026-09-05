using System.ComponentModel.DataAnnotations;
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

public sealed class OwnerBootstrapService(
    InfraHarborDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IOptions<BootstrapOptions> bootstrapOptions) : IOwnerBootstrapService
{
    private const long BootstrapAdvisoryLockId = 746318927401;

    public async Task<OwnerBootstrapResult> BootstrapAsync(
        OwnerBootstrapCommand command,
        CancellationToken cancellationToken)
    {
        var options = bootstrapOptions.Value;
        if (!options.Enabled)
        {
            return new OwnerBootstrapResult(OwnerBootstrapOutcome.Disabled);
        }

        if (string.IsNullOrWhiteSpace(options.Token) ||
            string.IsNullOrWhiteSpace(command.BootstrapToken) ||
            !TokenMatches(options.Token, command.BootstrapToken))
        {
            return new OwnerBootstrapResult(OwnerBootstrapOutcome.InvalidToken);
        }

        var validationErrors = ValidateCommand(command);
        if (validationErrors.Count > 0)
        {
            return new OwnerBootstrapResult(OwnerBootstrapOutcome.ValidationFailed, Errors: validationErrors);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({BootstrapAdvisoryLockId});",
            cancellationToken);

        if (await db.Users.AnyAsync(cancellationToken))
        {
            return new OwnerBootstrapResult(OwnerBootstrapOutcome.AlreadyInitialized);
        }

        var ownerRole = await roleManager.FindByNameAsync(RoleNames.Owner);
        if (ownerRole is null)
        {
            ownerRole = new ApplicationRole(RoleNames.Owner);
            var roleResult = await roleManager.CreateAsync(ownerRole);
            if (!roleResult.Succeeded)
            {
                return ValidationFailure(roleResult);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var email = command.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = command.DisplayName.Trim(),
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var createResult = await userManager.CreateAsync(user, command.Password);
        if (!createResult.Succeeded)
        {
            return ValidationFailure(createResult);
        }

        var roleAssignment = await userManager.AddToRoleAsync(user, RoleNames.Owner);
        if (!roleAssignment.Succeeded)
        {
            return ValidationFailure(roleAssignment);
        }

        await transaction.CommitAsync(cancellationToken);

        return new OwnerBootstrapResult(
            OwnerBootstrapOutcome.Created,
            user.Id,
            user.Email,
            user.DisplayName);
    }

    private static List<string> ValidateCommand(OwnerBootstrapCommand command)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(command.Email) || !new EmailAddressAttribute().IsValid(command.Email.Trim()))
        {
            errors.Add("A valid email address is required.");
        }

        var displayName = command.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length is < 1 or > 120)
        {
            errors.Add("Display name must be between 1 and 120 characters.");
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            errors.Add("Password is required.");
        }

        return errors;
    }

    private static OwnerBootstrapResult ValidationFailure(IdentityResult result) =>
        new(
            OwnerBootstrapOutcome.ValidationFailed,
            Errors: result.Errors.Select(error => error.Description).ToArray());

    private static bool TokenMatches(string configuredToken, string suppliedToken)
    {
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredToken));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedToken));
        return CryptographicOperations.FixedTimeEquals(configuredHash, suppliedHash);
    }
}
