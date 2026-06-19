using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;

namespace TravelPathways.Api.Controllers;

/// <summary>Tenant admin KPI dashboard: sales + team performance in one response.</summary>
[ApiController]
[Authorize(Policy = "TenantAdminOnly")]
[Route("api/tenant/kpi-dashboard")]
public sealed class KpiDashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;
    private readonly IMemoryCache _cache;

    public KpiDashboardController(AppDbContext db, TenantContext tenant, IMemoryCache cache)
    {
        _db = db;
        _tenant = tenant;
        _cache = cache;
    }

    public sealed class KpiSalesOverviewDto
    {
        public int TotalLeads { get; init; }
        public int LeadsInPeriod { get; init; }
        public int TodayLeads { get; init; }
        public int ActivePackages { get; init; }
        public int ConfirmedPackages { get; init; }
        public int ConfirmedInPeriod { get; init; }
        public decimal ConfirmedRevenueInPeriod { get; init; }
        public int TotalCallsInPeriod { get; init; }
        public int BrowserVisitsInPeriod { get; init; }
    }

    public sealed class KpiEmployeeRowDto
    {
        public required string UserId { get; init; }
        public required string UserName { get; init; }
        public int LeadCount { get; init; }
        public int OutgoingCalls { get; init; }
        public int IncomingCalls { get; init; }
        public int MissedCalls { get; init; }
        public int TotalCalls { get; init; }
        public int ConfirmedPackages { get; init; }
        public decimal ConfirmedRevenue { get; init; }
        public int ActiveSeconds { get; init; }
        public int IdleSeconds { get; init; }
        public int BrowserVisitCount { get; init; }
    }

    public sealed class KpiDashboardDto
    {
        public required string From { get; init; }
        public required string To { get; init; }
        public required KpiSalesOverviewDto Sales { get; init; }
        public required List<KpiEmployeeRowDto> Employees { get; init; }
    }

    private static DateTime ParseQueryDateUtc(string? value, DateTime fallbackUtcDate)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallbackUtcDate;
        if (DateTime.TryParse(value, out var parsed))
            return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        return fallbackUtcDate;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<KpiDashboardDto>>> GetDashboard(
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        CancellationToken ct = default)
    {
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<KpiDashboardDto>.Fail("Tenant context is missing."));

        var tenantId = _tenant.TenantId.Value;
        var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var fromDate = ParseQueryDateUtc(from, todayUtc.AddDays(-6));
        var toDate = ParseQueryDateUtc(to, todayUtc);
        if (toDate < fromDate)
            (fromDate, toDate) = (toDate, fromDate);

        var cacheKey = $"kpi:{tenantId:D}:{fromDate:yyyy-MM-dd}:{toDate:yyyy-MM-dd}";
        if (_cache.TryGetValue(cacheKey, out KpiDashboardDto? cached) && cached is not null)
            return ApiResponse<KpiDashboardDto>.Ok(cached);

        var fromUtc = fromDate;
        var toUtcExclusive = toDate.AddDays(1);
        var todayEndExclusive = todayUtc.AddDays(1);

        var totalLeads = await _db.Leads.AsNoTracking()
            .CountAsync(l => l.TenantId == tenantId, ct);

        var leadsInPeriod = await _db.Leads.AsNoTracking()
            .CountAsync(l => l.TenantId == tenantId && l.CreatedAt >= fromUtc && l.CreatedAt < toUtcExclusive, ct);

        var todayLeads = await _db.Leads.AsNoTracking()
            .CountAsync(l => l.TenantId == tenantId && l.CreatedAt >= todayUtc && l.CreatedAt < todayEndExclusive, ct);

        var activePackages = await _db.Packages.AsNoTracking()
            .CountAsync(p => p.TenantId == tenantId && p.Status == PackageStatus.Followup, ct);

        var confirmedPackages = await _db.Packages.AsNoTracking()
            .CountAsync(p => p.TenantId == tenantId && p.Status == PackageStatus.Confirmed, ct);

        var confirmedInPeriodList = await _db.Packages.AsNoTracking()
            .Where(p => p.TenantId == tenantId
                        && p.Status == PackageStatus.Confirmed
                        && p.CreatedAt >= fromUtc
                        && p.CreatedAt < toUtcExclusive)
            .Select(p => new { p.CreatedBy, Amount = p.TotalAmount - p.Discount })
            .ToListAsync(ct);

        var confirmedInPeriod = confirmedInPeriodList.Count;
        var confirmedRevenueInPeriod = confirmedInPeriodList.Sum(p => p.Amount);

        var totalCallsInPeriod = await _db.CallLogs.AsNoTracking()
            .CountAsync(l => l.TenantId == tenantId && l.CreatedAt >= fromUtc && l.CreatedAt < toUtcExclusive, ct);

        var browserVisitsInPeriod = await _db.UserActivityPageVisits.AsNoTracking()
            .CountAsync(v => v.TenantId == tenantId
                             && v.Source == UserActivityVisitSource.Browser
                             && v.VisitedAtUtc >= fromUtc
                             && v.VisitedAtUtc < toUtcExclusive, ct);

        var activeUsers = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive && u.Role != UserRole.SuperAdmin)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync(ct);

        var leadCounts = await _db.Leads.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.AssignedToUserId != null)
            .GroupBy(l => l.AssignedToUserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var leadCountByUser = leadCounts.ToDictionary(x => x.UserId, x => x.Count);

        var callCounts = await _db.CallLogs.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.UserId != null && l.CreatedAt >= fromUtc && l.CreatedAt < toUtcExclusive)
            .GroupBy(l => new { l.UserId, l.Direction })
            .Select(g => new { g.Key.UserId, g.Key.Direction, Count = g.Count() })
            .ToListAsync(ct);
        var callByUser = callCounts
            .GroupBy(x => x.UserId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(x => x.Direction ?? string.Empty, x => x.Count, StringComparer.OrdinalIgnoreCase));

        var confirmedByEmail = confirmedInPeriodList
            .GroupBy(p => (p.CreatedBy ?? string.Empty).Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => new { Count = g.Count(), Revenue = g.Sum(x => x.Amount) });

        var activitySummaries = await _db.UserActivityDailySummaries.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.ActivityDate >= fromDate && s.ActivityDate <= toDate)
            .GroupBy(s => s.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                ActiveSeconds = g.Sum(x => x.ActiveSeconds),
                IdleSeconds = g.Sum(x => x.IdleSeconds)
            })
            .ToListAsync(ct);
        var activityByUser = activitySummaries.ToDictionary(x => x.UserId);

        var browserVisitCounts = await _db.UserActivityPageVisits.AsNoTracking()
            .Where(v => v.TenantId == tenantId
                        && v.Source == UserActivityVisitSource.Browser
                        && v.VisitedAtUtc >= fromUtc
                        && v.VisitedAtUtc < toUtcExclusive
                        && v.UserId != Guid.Empty)
            .GroupBy(v => v.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var browserByUser = browserVisitCounts.ToDictionary(x => x.UserId, x => x.Count);

        var employees = activeUsers.Select(u =>
        {
            leadCountByUser.TryGetValue(u.Id, out var leadCount);
            callByUser.TryGetValue(u.Id, out var byDir);
            var outgoing = byDir?.GetValueOrDefault("outgoing") ?? 0;
            var incoming = byDir?.GetValueOrDefault("incoming") ?? 0;
            var missed = byDir?.GetValueOrDefault("missed") ?? 0;

            var emailKey = u.Email.Trim().ToLowerInvariant();
            confirmedByEmail.TryGetValue(emailKey, out var confirmedStats);

            activityByUser.TryGetValue(u.Id, out var activity);
            browserByUser.TryGetValue(u.Id, out var browserCount);

            var name = $"{u.FirstName} {u.LastName}".Trim();
            return new KpiEmployeeRowDto
            {
                UserId = u.Id.ToString("D"),
                UserName = string.IsNullOrWhiteSpace(name) ? u.Email : name,
                LeadCount = leadCount,
                OutgoingCalls = outgoing,
                IncomingCalls = incoming,
                MissedCalls = missed,
                TotalCalls = outgoing + incoming + missed,
                ConfirmedPackages = confirmedStats?.Count ?? 0,
                ConfirmedRevenue = confirmedStats?.Revenue ?? 0,
                ActiveSeconds = activity?.ActiveSeconds ?? 0,
                IdleSeconds = activity?.IdleSeconds ?? 0,
                BrowserVisitCount = browserCount
            };
        }).OrderByDescending(e => e.ConfirmedRevenue)
          .ThenByDescending(e => e.LeadCount)
          .ToList();

        return ApiResponse<KpiDashboardDto>.Ok(_cache.Set(cacheKey, new KpiDashboardDto
        {
            From = fromDate.ToString("yyyy-MM-dd"),
            To = toDate.ToString("yyyy-MM-dd"),
            Sales = new KpiSalesOverviewDto
            {
                TotalLeads = totalLeads,
                LeadsInPeriod = leadsInPeriod,
                TodayLeads = todayLeads,
                ActivePackages = activePackages,
                ConfirmedPackages = confirmedPackages,
                ConfirmedInPeriod = confirmedInPeriod,
                ConfirmedRevenueInPeriod = confirmedRevenueInPeriod,
                TotalCallsInPeriod = totalCallsInPeriod,
                BrowserVisitsInPeriod = browserVisitsInPeriod
            },
            Employees = employees
        }, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
        }));
    }
}
