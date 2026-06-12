using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/user-activity")]
public sealed class UserActivityController : TenantControllerBase
{
    private const int MaxSecondsPerHeartbeat = 120;

    private readonly AppDbContext _db;

    public UserActivityController(AppDbContext db, TenantContext tenant) : base(tenant)
    {
        _db = db;
    }

    public sealed class UserActivitySummaryDto
    {
        public required string Id { get; init; }
        public required string UserId { get; init; }
        public string? UserName { get; init; }
        public required DateTime ActivityDate { get; init; }
        public int ActiveSeconds { get; init; }
        public int IdleSeconds { get; init; }
        public bool IsCurrentlyIdle { get; init; }
        public DateTime LastReportedAtUtc { get; init; }
        public bool ActivityTrackingEnabled { get; init; } = true;
    }

    public sealed class HeartbeatResponseDto
    {
        public bool TrackingEnabled { get; init; }
        public UserActivitySummaryDto? Summary { get; init; }
    }

    public sealed class SetActivityTrackingRequestDto
    {
        public bool Enabled { get; init; } = true;
    }

    public sealed class HeartbeatRequestDto
    {
        /// <summary>Local calendar date YYYY-MM-DD.</summary>
        public required string ActivityDate { get; init; }
        public int ActiveSeconds { get; init; }
        public int IdleSeconds { get; init; }
        public bool IsCurrentlyIdle { get; init; }
    }

    public sealed class PageVisitRequestDto
    {
        public required string Path { get; init; }
        public required string Url { get; init; }
        public string? PageTitle { get; init; }
    }

    public sealed class PageVisitDto
    {
        public required string Id { get; init; }
        public required string UserId { get; init; }
        public string? UserName { get; init; }
        public required string Path { get; init; }
        public required string Url { get; init; }
        public string? PageTitle { get; init; }
        public DateTime VisitedAtUtc { get; init; }
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private static DateTime ParseActivityDate(string value)
    {
        if (DateTime.TryParse(value, out var parsed))
            return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        throw new ArgumentException("Invalid activity date.");
    }

    private static DateTime ParseQueryDateUtc(string? value, DateTime fallbackUtcDate)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallbackUtcDate;
        if (DateTime.TryParse(value, out var parsed))
            return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        return fallbackUtcDate;
    }

    private static UserActivitySummaryDto ToDto(UserActivityDailySummary s)
    {
        var userName = s.User == null ? null : $"{s.User.FirstName} {s.User.LastName}".Trim();
        return new UserActivitySummaryDto
        {
            Id = s.Id.ToString("D"),
            UserId = s.UserId.ToString("D"),
            UserName = string.IsNullOrWhiteSpace(userName) ? s.User?.Email : userName,
            ActivityDate = s.ActivityDate,
            ActiveSeconds = s.ActiveSeconds,
            IdleSeconds = s.IdleSeconds,
            IsCurrentlyIdle = s.IsCurrentlyIdle,
            LastReportedAtUtc = s.LastReportedAtUtc,
            ActivityTrackingEnabled = s.User?.ActivityTrackingEnabled ?? true
        };
    }

    private async Task<bool> IsActivityTrackingEnabledForUserAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == TenantId && u.Id == userId)
            .Select(u => u.ActivityTrackingEnabled)
            .FirstOrDefaultAsync(ct);
    }

    [HttpPost("heartbeat")]
    public async Task<ActionResult<ApiResponse<HeartbeatResponseDto>>> ReportHeartbeat(
        [FromBody] HeartbeatRequestDto dto,
        CancellationToken ct = default)
    {
        if (!HasTenantId)
            return BadRequest(ApiResponse<HeartbeatResponseDto>.Fail("Tenant context is missing."));

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<HeartbeatResponseDto>.Fail("User not identified."));

        if (!await IsActivityTrackingEnabledForUserAsync(currentUserId.Value, ct))
        {
            return ApiResponse<HeartbeatResponseDto>.Ok(new HeartbeatResponseDto
            {
                TrackingEnabled = false,
                Summary = null
            });
        }

        DateTime activityDate;
        try
        {
            activityDate = ParseActivityDate(dto.ActivityDate);
        }
        catch
        {
            return BadRequest(ApiResponse<HeartbeatResponseDto>.Fail("Invalid activity date."));
        }

        var activeDelta = Math.Clamp(dto.ActiveSeconds, 0, MaxSecondsPerHeartbeat);
        var idleDelta = Math.Clamp(dto.IdleSeconds, 0, MaxSecondsPerHeartbeat);

        var summary = await _db.UserActivityDailySummaries
            .FirstOrDefaultAsync(
                s => s.TenantId == TenantId && s.UserId == currentUserId.Value && s.ActivityDate == activityDate,
                ct);

        if (summary == null)
        {
            summary = new UserActivityDailySummary
            {
                TenantId = TenantId,
                UserId = currentUserId.Value,
                ActivityDate = activityDate,
                ActiveSeconds = activeDelta,
                IdleSeconds = idleDelta,
                IsCurrentlyIdle = dto.IsCurrentlyIdle,
                LastReportedAtUtc = DateTime.UtcNow
            };
            _db.UserActivityDailySummaries.Add(summary);
        }
        else
        {
            summary.ActiveSeconds += activeDelta;
            summary.IdleSeconds += idleDelta;
            summary.IsCurrentlyIdle = dto.IsCurrentlyIdle;
            summary.LastReportedAtUtc = DateTime.UtcNow;
            summary.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await _db.Entry(summary).Reference(s => s.User).LoadAsync(ct);
        return ApiResponse<HeartbeatResponseDto>.Ok(new HeartbeatResponseDto
        {
            TrackingEnabled = true,
            Summary = ToDto(summary)
        });
    }

    /// <summary>Tenant admin: enable or disable activity tracking for a user.</summary>
    [HttpPut("users/{userId:guid}/tracking")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> SetUserActivityTracking(
        [FromRoute] Guid userId,
        [FromBody] SetActivityTrackingRequestDto request,
        CancellationToken ct = default)
    {
        if (!HasTenantId)
            return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));

        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == TenantId && u.Id == userId, ct);
        if (user is null)
            return NotFound(ApiResponse<object>.Fail("User not found."));

        user.ActivityTrackingEnabled = request.Enabled;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<object>.Ok(new
        {
            userId = user.Id.ToString("D"),
            activityTrackingEnabled = user.ActivityTrackingEnabled
        });
    }

    [HttpPost("page-visit")]
    public async Task<ActionResult<ApiResponse<object>>> RecordPageVisit(
        [FromBody] PageVisitRequestDto dto,
        CancellationToken ct = default)
    {
        if (!HasTenantId)
            return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<object>.Fail("User not identified."));

        if (!await IsActivityTrackingEnabledForUserAsync(currentUserId.Value, ct))
            return ApiResponse<object>.Ok(new { recorded = false });

        var path = (dto.Path ?? string.Empty).Trim();
        var url = (dto.Url ?? string.Empty).Trim();
        if (path.Length == 0 || url.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Path and URL are required."));

        path = path.Length > 500 ? path[..500] : path;
        url = url.Length > 2000 ? url[..2000] : url;
        var title = dto.PageTitle?.Trim();
        if (title?.Length > 500) title = title[..500];

        _db.UserActivityPageVisits.Add(new UserActivityPageVisit
        {
            TenantId = TenantId,
            UserId = currentUserId.Value,
            Path = path,
            Url = url,
            PageTitle = title,
            VisitedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { recorded = true });
    }

    /// <summary>Tenant admin: in-app page visits for a date range.</summary>
    [HttpGet("page-visits")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<List<PageVisitDto>>>> GetPageVisits(
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] int limit = 500,
        CancellationToken ct = default)
    {
        if (!HasTenantId)
            return BadRequest(ApiResponse<List<PageVisitDto>>.Fail("Tenant context is missing."));

        var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var fromUtc = ParseQueryDateUtc(from, todayUtc.AddDays(-6));
        var toUtcExclusive = ParseQueryDateUtc(to, todayUtc).AddDays(1);
        if (toUtcExclusive <= fromUtc)
            toUtcExclusive = fromUtc.AddDays(1);

        limit = Math.Clamp(limit, 1, 1000);

        var query = _db.UserActivityPageVisits.AsNoTracking()
            .Include(v => v.User)
            .Where(v => v.TenantId == TenantId && v.VisitedAtUtc >= fromUtc && v.VisitedAtUtc < toUtcExclusive);

        if (userId.HasValue)
            query = query.Where(v => v.UserId == userId.Value);

        var list = await query
            .OrderByDescending(v => v.VisitedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

        return ApiResponse<List<PageVisitDto>>.Ok(list.Select(v =>
        {
            var userName = v.User == null ? null : $"{v.User.FirstName} {v.User.LastName}".Trim();
            return new PageVisitDto
            {
                Id = v.Id.ToString("D"),
                UserId = v.UserId.ToString("D"),
                UserName = string.IsNullOrWhiteSpace(userName) ? v.User?.Email : userName,
                Path = v.Path,
                Url = v.Url,
                PageTitle = v.PageTitle,
                VisitedAtUtc = v.VisitedAtUtc
            };
        }).ToList());
    }

    /// <summary>Tenant admin: daily active/idle summaries for a date range.</summary>
    [HttpGet]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<List<UserActivitySummaryDto>>>> GetSummaries(
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] Guid? userId = null,
        CancellationToken ct = default)
    {
        if (!HasTenantId)
            return BadRequest(ApiResponse<List<UserActivitySummaryDto>>.Fail("Tenant context is missing."));

        var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var fromDate = ParseQueryDateUtc(from, todayUtc.AddDays(-6));
        var toDate = ParseQueryDateUtc(to, todayUtc);
        if (toDate < fromDate)
            (fromDate, toDate) = (toDate, fromDate);

        var query = _db.UserActivityDailySummaries.AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.TenantId == TenantId && s.ActivityDate >= fromDate && s.ActivityDate <= toDate);

        if (userId.HasValue)
            query = query.Where(s => s.UserId == userId.Value);

        var list = await query
            .OrderByDescending(s => s.ActivityDate)
            .ThenBy(s => s.User!.FirstName)
            .ThenBy(s => s.User!.LastName)
            .ToListAsync(ct);

        return ApiResponse<List<UserActivitySummaryDto>>.Ok(list.Select(ToDto).ToList());
    }
}
