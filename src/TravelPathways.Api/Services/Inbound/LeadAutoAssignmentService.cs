using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;

namespace TravelPathways.Api.Services.Inbound;

public interface ILeadAutoAssignmentService
{
    Task<Guid?> PickAssigneeAsync(Guid tenantId, LeadSource leadSource, CancellationToken ct);
}

public sealed class LeadAutoAssignmentService : ILeadAutoAssignmentService
{
    private readonly AppDbContext _db;

    public LeadAutoAssignmentService(AppDbContext db) => _db = db;

    public async Task<Guid?> PickAssigneeAsync(Guid tenantId, LeadSource leadSource, CancellationToken ct)
    {
        var integration = await _db.TenantLeadIntegrations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && !i.IsDeleted, ct);

        if (integration is null || !integration.AutoAssignEnabled)
            return null;

        var startOfDayUtc = DateTime.UtcNow.Date;

        var salesUsers = await _db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u =>
                u.TenantId == tenantId &&
                !u.IsDeleted &&
                u.IsActive &&
                u.ParticipateInInboundAutoAssign &&
                u.Department == UserDepartment.Sales &&
                u.InboundDailyLeadQuota > 0)
            .ToListAsync(ct);

        if (salesUsers.Count == 0)
            return null;

        var eligible = salesUsers.Where(u => IsSourceAllowed(u, leadSource)).ToList();
        if (eligible.Count == 0)
            return null;

        var userIds = eligible.Select(u => u.Id).ToList();

        var assignedToday = await _db.Leads
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(l =>
                l.TenantId == tenantId &&
                !l.IsDeleted &&
                l.AssignedToUserId.HasValue &&
                userIds.Contains(l.AssignedToUserId.Value) &&
                l.InboundExternalId != null &&
                l.CreatedAt >= startOfDayUtc)
            .GroupBy(l => l.AssignedToUserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var countByUser = assignedToday.ToDictionary(x => x.UserId, x => x.Count);

        var withCapacity = eligible
            .Select(u => new
            {
                User = u,
                Count = countByUser.GetValueOrDefault(u.Id, 0)
            })
            .Where(x => x.Count < x.User.InboundDailyLeadQuota)
            .OrderBy(x => x.Count)
            .ThenBy(x => x.User.CreatedAt)
            .ToList();

        return withCapacity.FirstOrDefault()?.User.Id;
    }

    private static bool IsSourceAllowed(AppUser user, LeadSource leadSource)
    {
        var allowed = user.InboundAllowedLeadSources ?? [];
        return allowed.Count == 0 || allowed.Contains(leadSource);
    }
}
