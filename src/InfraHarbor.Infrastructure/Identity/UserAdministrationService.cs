using System.ComponentModel.DataAnnotations;
using InfraHarbor.Application.Security;
using InfraHarbor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InfraHarbor.Infrastructure.Identity;

public sealed class UserAdministrationService(
    InfraHarborDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IAuthSessionService authSessionService,
    ISecurityAuditSink auditSink,
    TimeProvider timeProvider) : IUserAdministrationService
{
    public async Task<IReadOnlyList<UserAdministrationUser>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .ToListAsync(cancellationToken);

        var result = new List<UserAdministrationUser>(users.Count);
        foreach (var user in users)
        {
            result.Add(await ToDtoAsync(user));
        }

        return result;
    }

    public async Task<UserAdministrationResult> CreateAsync(
        Guid actorUserId,
        CreateManagedUserCommand command,
        CancellationToken cancellationToken)
    {
        if (!await CanManageUsersAsync(actorUserId))
        {
            return await RejectedAsync(actorUserId, null, "create", "actor_not_authorized", cancellationToken);
        }

        var errors = ValidateCreate(command);
        var roles = NormalizeManagedRoles(command.Roles, errors);
        if (errors.Count > 0)
        {
            return new UserAdministrationResult(UserAdministrationOutcome.ValidationFailed, Errors: errors);
        }

        var email = command.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return new UserAdministrationResult(
                UserAdministrationOutcome.Conflict,
                Errors: ["A user with this email already exists."]);
        }

        await EnsureRolesAsync(roles);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
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

        var roleResult = await userManager.AddToRolesAsync(user, roles);
        if (!roleResult.Succeeded)
        {
            return ValidationFailure(roleResult);
        }

        await transaction.CommitAsync(cancellationToken);

        await auditSink.WriteAsync(
            new SecurityAuditEvent(
                SecurityAuditActions.UserCreated,
                actorUserId,
                user.Id,
                "success",
                new Dictionary<string, string> { ["roles"] = string.Join(',', roles) }),
            cancellationToken);

        return new UserAdministrationResult(UserAdministrationOutcome.Created, await ToDtoAsync(user));
    }

    public async Task<UserAdministrationResult> UpdateAsync(
        Guid actorUserId,
        Guid targetUserId,
        UpdateManagedUserCommand command,
        CancellationToken cancellationToken)
    {
        if (!await CanManageUsersAsync(actorUserId))
        {
            return await RejectedAsync(actorUserId, targetUserId, "update", "actor_not_authorized", cancellationToken);
        }

        var displayName = command.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length is < 1 or > 120)
        {
            return new UserAdministrationResult(
                UserAdministrationOutcome.ValidationFailed,
                Errors: ["Display name must be between 1 and 120 characters."]);
        }

        var target = await userManager.FindByIdAsync(targetUserId.ToString());
        if (target is null)
        {
            return new UserAdministrationResult(UserAdministrationOutcome.NotFound);
        }

        target.DisplayName = displayName;
        target.UpdatedAt = timeProvider.GetUtcNow();
        var update = await userManager.UpdateAsync(target);
        if (!update.Succeeded)
        {
            return ValidationFailure(update);
        }

        await auditSink.WriteAsync(
            new SecurityAuditEvent(SecurityAuditActions.UserUpdated, actorUserId, targetUserId, "success"),
            cancellationToken);

        return new UserAdministrationResult(UserAdministrationOutcome.Success, await ToDtoAsync(target));
    }

    public async Task<UserAdministrationResult> SetRolesAsync(
        Guid actorUserId,
        Guid targetUserId,
        SetManagedUserRolesCommand command,
        CancellationToken cancellationToken)
    {
        if (!await CanManageUsersAsync(actorUserId))
        {
            return await RejectedAsync(actorUserId, targetUserId, "roles", "actor_not_authorized", cancellationToken);
        }

        var target = await userManager.FindByIdAsync(targetUserId.ToString());
        if (target is null)
        {
            return new UserAdministrationResult(UserAdministrationOutcome.NotFound);
        }

        var currentRoles = await userManager.GetRolesAsync(target);
        if (currentRoles.Contains(RoleNames.Owner, StringComparer.Ordinal))
        {
            return await RejectedAsync(actorUserId, targetUserId, "roles", "owner_role_is_immutable", cancellationToken);
        }

        var errors = new List<string>();
        var roles = NormalizeManagedRoles(command.Roles, errors);
        if (errors.Count > 0)
        {
            return new UserAdministrationResult(UserAdministrationOutcome.ValidationFailed, Errors: errors);
        }

        await EnsureRolesAsync(roles);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (currentRoles.Count > 0)
        {
            var remove = await userManager.RemoveFromRolesAsync(target, currentRoles);
            if (!remove.Succeeded)
            {
                return ValidationFailure(remove);
            }
        }

        var add = await userManager.AddToRolesAsync(target, roles);
        if (!add.Succeeded)
        {
            return ValidationFailure(add);
        }

        target.UpdatedAt = timeProvider.GetUtcNow();
        var stamp = await userManager.UpdateSecurityStampAsync(target);
        if (!stamp.Succeeded)
        {
            return ValidationFailure(stamp);
        }

        await transaction.CommitAsync(cancellationToken);
        await authSessionService.RevokeAllForUserAsync(targetUserId, cancellationToken);

        await auditSink.WriteAsync(
            new SecurityAuditEvent(
                SecurityAuditActions.UserRolesChanged,
                actorUserId,
                targetUserId,
                "success",
                new Dictionary<string, string> { ["roles"] = string.Join(',', roles) }),
            cancellationToken);

        return new UserAdministrationResult(UserAdministrationOutcome.Success, await ToDtoAsync(target));
    }

    public async Task<UserAdministrationResult> DisableAsync(
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        if (!await CanManageUsersAsync(actorUserId))
        {
            return await RejectedAsync(actorUserId, targetUserId, "disable", "actor_not_authorized", cancellationToken);
        }

        if (actorUserId == targetUserId)
        {
            return await RejectedAsync(actorUserId, targetUserId, "disable", "self_disable_not_allowed", cancellationToken);
        }

        var target = await userManager.FindByIdAsync(targetUserId.ToString());
        if (target is null)
        {
            return new UserAdministrationResult(UserAdministrationOutcome.NotFound);
        }

        var roles = await userManager.GetRolesAsync(target);
        if (roles.Contains(RoleNames.Owner, StringComparer.Ordinal))
        {
            return await RejectedAsync(actorUserId, targetUserId, "disable", "owner_disable_not_allowed", cancellationToken);
        }

        if (target.Status == UserStatus.Disabled)
        {
            return new UserAdministrationResult(UserAdministrationOutcome.Success, await ToDtoAsync(target));
        }

        target.Status = UserStatus.Disabled;
        target.UpdatedAt = timeProvider.GetUtcNow();
        var stamp = await userManager.UpdateSecurityStampAsync(target);
        if (!stamp.Succeeded)
        {
            return ValidationFailure(stamp);
        }

        await authSessionService.RevokeAllForUserAsync(targetUserId, cancellationToken);

        await auditSink.WriteAsync(
            new SecurityAuditEvent(SecurityAuditActions.UserDisabled, actorUserId, targetUserId, "success"),
            cancellationToken);

        return new UserAdministrationResult(UserAdministrationOutcome.Success, await ToDtoAsync(target));
    }

    private async Task<bool> CanManageUsersAsync(Guid actorUserId)
    {
        var actor = await userManager.FindByIdAsync(actorUserId.ToString());
        if (actor is null || actor.Status != UserStatus.Active)
        {
            return false;
        }

        var roles = await userManager.GetRolesAsync(actor);
        return roles.Contains(RoleNames.Owner, StringComparer.Ordinal) ||
               roles.Contains(RoleNames.Admin, StringComparer.Ordinal);
    }

    private async Task<UserAdministrationResult> RejectedAsync(
        Guid actorUserId,
        Guid? targetUserId,
        string mutation,
        string reason,
        CancellationToken cancellationToken)
    {
        await auditSink.WriteAsync(
            new SecurityAuditEvent(
                SecurityAuditActions.UserMutationRejected,
                actorUserId,
                targetUserId,
                "rejected",
                new Dictionary<string, string>
                {
                    ["mutation"] = mutation,
                    ["reason"] = reason
                }),
            cancellationToken);

        return new UserAdministrationResult(UserAdministrationOutcome.Forbidden);
    }

    private static List<string> ValidateCreate(CreateManagedUserCommand command)
    {
        var errors = new List<string>();
        var email = command.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
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

    private static string[] NormalizeManagedRoles(IReadOnlyList<string>? requestedRoles, List<string> errors)
    {
        var roles = (requestedRoles ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (roles.Length == 0)
        {
            errors.Add("At least one role is required.");
            return roles;
        }

        if (roles.Any(role => !RoleNames.All.Contains(role)))
        {
            errors.Add("One or more roles are not recognized.");
        }

        if (roles.Contains(RoleNames.Owner, StringComparer.Ordinal))
        {
            errors.Add("The Owner role cannot be assigned through user administration.");
        }

        return roles;
    }

    private async Task EnsureRolesAsync(IEnumerable<string> roles)
    {
        foreach (var role in roles)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var create = await roleManager.CreateAsync(new ApplicationRole(role));
            if (!create.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to initialize role '{role}': {string.Join("; ", create.Errors.Select(error => error.Description))}");
            }
        }
    }

    private async Task<UserAdministrationUser> ToDtoAsync(ApplicationUser user)
    {
        var roles = (await userManager.GetRolesAsync(user))
            .Where(role => RoleNames.All.Contains(role))
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();

        return new UserAdministrationUser(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.Status.ToString(),
            roles,
            user.CreatedAt,
            user.UpdatedAt);
    }

    private static UserAdministrationResult ValidationFailure(IdentityResult result) =>
        new(
            UserAdministrationOutcome.ValidationFailed,
            Errors: result.Errors.Select(error => error.Description).ToArray());
}
