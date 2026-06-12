using Microsoft.EntityFrameworkCore;
using Npgsql;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;

namespace TravelPathways.Api.Common;

internal static class TenantUserEmailHelper
{
    public static async Task<AppUser?> FindByEmailIncludingDeletedAsync(
        AppDbContext db,
        string email,
        CancellationToken ct)
    {
        var normalized = email.Trim();
        return await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    /// <summary>
    /// Returns an error message when the email cannot be used for a new user, or null when create may proceed
    /// (including restore of a soft-deleted user in the same tenant).
    /// </summary>
    public static string? GetCreateEmailConflictMessage(AppUser existing, Guid tenantId)
    {
        if (!existing.IsDeleted)
        {
            if (existing.TenantId == tenantId)
                return "A user with this email already exists.";
            return "This email is already registered to another agency.";
        }

        if (existing.TenantId != tenantId)
            return "This email belongs to a deleted account in another agency and cannot be reused.";

        return null;
    }

    public static async Task<bool> IsEmailTakenForUpdateAsync(
        AppDbContext db,
        string newEmail,
        Guid userId,
        CancellationToken ct)
    {
        var normalized = newEmail.Trim();
        return await db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Email == normalized && u.Id != userId, ct);
    }

    public static string? TryGetUserFriendlyDbError(Exception ex)
    {
        for (var cur = ex; cur != null; cur = cur.InnerException)
        {
            if (cur is PostgresException pg
                && pg.SqlState == PostgresErrorCodes.UniqueViolation
                && (pg.ConstraintName?.Contains("Email", StringComparison.OrdinalIgnoreCase) == true
                    || pg.Message.Contains("IX_Users_Email", StringComparison.OrdinalIgnoreCase)))
            {
                return "A user with this email already exists.";
            }
        }

        return null;
    }
}
