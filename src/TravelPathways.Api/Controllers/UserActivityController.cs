using System.IO.Compression;
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
    private const int MaxBrowserVisitsPerBatch = 50;

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public UserActivityController(
        AppDbContext db,
        TenantContext tenant,
        IWebHostEnvironment env,
        IConfiguration configuration) : base(tenant)
    {
        _db = db;
        _env = env;
        _configuration = configuration;
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
        /// <summary>When true, records a one-time idle system log for this inactivity period.</summary>
        public bool RecordIdleEvent { get; init; }
        public string? IdlePath { get; init; }
        public string? IdleUrl { get; init; }
        public string? IdlePageTitle { get; init; }
    }

    private const string IdlePathMarker = "[idle]";
    private const int IdleLogDedupeMinutes = 10;

    public sealed class PageVisitRequestDto
    {
        public required string Path { get; init; }
        public required string Url { get; init; }
        public string? PageTitle { get; init; }
    }

    public sealed class BrowserVisitItemDto
    {
        public required string Url { get; init; }
        public string? PageTitle { get; init; }
        public int? DurationSeconds { get; init; }
        public DateTime? VisitedAtUtc { get; init; }
    }

    public sealed class BrowserVisitsRequestDto
    {
        public List<BrowserVisitItemDto> Visits { get; init; } = [];
    }

    public sealed class PageVisitDto
    {
        public required string Id { get; init; }
        public required string UserId { get; init; }
        public string? UserName { get; init; }
        public required string Path { get; init; }
        public required string Url { get; init; }
        public string? PageTitle { get; init; }
        public required string Source { get; init; }
        public int? DurationSeconds { get; init; }
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

    private static PageVisitDto ToPageVisitDto(UserActivityPageVisit v)
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
            Source = v.Source,
            DurationSeconds = v.DurationSeconds,
            VisitedAtUtc = v.VisitedAtUtc
        };
    }

    private static string ResolveInAppSource(string path)
    {
        if (path == "[external]")
            return UserActivityVisitSource.ExternalLink;
        return UserActivityVisitSource.InApp;
    }

    private static string? HostFromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;
        return uri.Host;
    }

    private static bool IsTrackableBrowserUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme is not ("http" or "https"))
            return false;
        var host = uri.Host.ToLowerInvariant();
        if (host is "localhost" or "127.0.0.1")
            return false;
        return true;
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

        if (dto.RecordIdleEvent)
            await TryRecordIdleEventAsync(currentUserId.Value, dto, ct);

        await _db.SaveChangesAsync(ct);
        await _db.Entry(summary).Reference(s => s.User).LoadAsync(ct);
        return ApiResponse<HeartbeatResponseDto>.Ok(new HeartbeatResponseDto
        {
            TrackingEnabled = true,
            Summary = ToDto(summary)
        });
    }

    private async Task TryRecordIdleEventAsync(Guid userId, HeartbeatRequestDto dto, CancellationToken ct)
    {
        var dedupeSince = DateTime.UtcNow.AddMinutes(-IdleLogDedupeMinutes);
        var alreadyLogged = await _db.UserActivityPageVisits.AsNoTracking()
            .AnyAsync(
                v => v.TenantId == TenantId
                     && v.UserId == userId
                     && v.Source == UserActivityVisitSource.Idle
                     && v.VisitedAtUtc >= dedupeSince,
                ct);
        if (alreadyLogged)
            return;

        var path = (dto.IdlePath ?? IdlePathMarker).Trim();
        if (path.Length == 0) path = IdlePathMarker;
        path = path.Length > 500 ? path[..500] : path;

        var url = (dto.IdleUrl ?? string.Empty).Trim();
        if (url.Length == 0) url = path;
        url = url.Length > 2000 ? url[..2000] : url;

        var title = dto.IdlePageTitle?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = "Idle for 15 minutes";
        else if (title.Length > 500)
            title = title[..500];

        _db.UserActivityPageVisits.Add(new UserActivityPageVisit
        {
            TenantId = TenantId,
            UserId = userId,
            Path = path,
            Url = url,
            PageTitle = title,
            Source = UserActivityVisitSource.Idle,
            VisitedAtUtc = DateTime.UtcNow
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
            Source = ResolveInAppSource(path),
            VisitedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { recorded = true });
    }

    /// <summary>Browser extension: batch record of websites visited in Chrome/Edge.</summary>
    [HttpPost("browser-visits")]
    public async Task<ActionResult<ApiResponse<object>>> RecordBrowserVisits(
        [FromBody] BrowserVisitsRequestDto dto,
        CancellationToken ct = default)
    {
        if (!HasTenantId)
            return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized(ApiResponse<object>.Fail("User not identified."));

        if (!await IsActivityTrackingEnabledForUserAsync(currentUserId.Value, ct))
            return ApiResponse<object>.Ok(new { recorded = false, count = 0 });

        var items = dto.Visits ?? [];
        if (items.Count == 0)
            return ApiResponse<object>.Ok(new { recorded = true, count = 0 });

        if (items.Count > MaxBrowserVisitsPerBatch)
            return BadRequest(ApiResponse<object>.Fail($"Maximum {MaxBrowserVisitsPerBatch} visits per batch."));

        var now = DateTime.UtcNow;
        var recorded = 0;

        foreach (var item in items)
        {
            var url = (item.Url ?? string.Empty).Trim();
            if (!IsTrackableBrowserUrl(url))
                continue;

            url = url.Length > 2000 ? url[..2000] : url;
            var host = HostFromUrl(url) ?? "unknown";
            var path = host.Length > 500 ? host[..500] : host;
            var title = item.PageTitle?.Trim();
            if (title?.Length > 500) title = title[..500];

            var visitedAt = item.VisitedAtUtc.HasValue
                ? DateTime.SpecifyKind(item.VisitedAtUtc.Value, DateTimeKind.Utc)
                : now;

            int? duration = item.DurationSeconds.HasValue
                ? Math.Clamp(item.DurationSeconds.Value, 0, 86400)
                : null;

            _db.UserActivityPageVisits.Add(new UserActivityPageVisit
            {
                TenantId = TenantId,
                UserId = currentUserId.Value,
                Path = path,
                Url = url,
                PageTitle = title,
                Source = UserActivityVisitSource.Browser,
                DurationSeconds = duration,
                VisitedAtUtc = visitedAt
            });
            recorded++;
        }

        if (recorded > 0)
            await _db.SaveChangesAsync(ct);

        return ApiResponse<object>.Ok(new { recorded = recorded > 0, count = recorded });
    }

    /// <summary>Tenant admin: page visits for a date range.</summary>
    [HttpGet("page-visits")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<List<PageVisitDto>>>> GetPageVisits(
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? source = null,
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

        if (!string.IsNullOrWhiteSpace(source))
        {
            var src = source.Trim();
            query = query.Where(v => v.Source == src);
        }

        var list = await query
            .OrderByDescending(v => v.VisitedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

        return ApiResponse<List<PageVisitDto>>.Ok(list.Select(ToPageVisitDto).ToList());
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
        // Defensive bound: an admin passing a huge/unbounded range could otherwise pull every
        // daily-summary row ever recorded for the tenant in one response.
        if ((toDate - fromDate).TotalDays > 366)
            fromDate = toDate.AddDays(-366);

        var query = _db.UserActivityDailySummaries.AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.TenantId == TenantId && s.ActivityDate >= fromDate && s.ActivityDate <= toDate);

        if (userId.HasValue)
            query = query.Where(s => s.UserId == userId.Value);

        var list = await query
            .OrderByDescending(s => s.ActivityDate)
            .ThenBy(s => s.User!.FirstName)
            .ThenBy(s => s.User!.LastName)
            .Take(5000)
            .ToListAsync(ct);

        return ApiResponse<List<UserActivitySummaryDto>>.Ok(list.Select(ToDto).ToList());
    }

    /// <summary>Download the Travel Pathways Activity browser extension as a ZIP (for Load unpacked install).</summary>
    [HttpGet("extension-download")]
    public ActionResult DownloadExtensionPackage()
    {
        var extensionDir = ResolveExtensionDirectory();
        if (extensionDir is null)
            return NotFound(ApiResponse<object>.Fail("Extension package is not available on this server."));

        var includeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "manifest.json", "background.js", "popup.html", "popup.js", "popup.css"
        };

        var files = Directory.GetFiles(extensionDir, "*", SearchOption.TopDirectoryOnly)
            .Where(f => includeNames.Contains(Path.GetFileName(f)))
            .ToList();

        if (files.Count == 0)
            return NotFound(ApiResponse<object>.Fail("Extension files were not found."));

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var filePath in files)
            {
                var entryName = Path.GetFileName(filePath);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var fileStream = System.IO.File.OpenRead(filePath);
                fileStream.CopyTo(entryStream);
            }
        }

        ms.Position = 0;
        return File(ms.ToArray(), "application/zip", "TravelPathways-Activity-Extension.zip");
    }

    private string? ResolveExtensionDirectory()
    {
        var configured = _configuration["BrowserExtension:SourcePath"]?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            var configuredPath = Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(_env.ContentRootPath, configured));
            if (Directory.Exists(configuredPath))
                return configuredPath;
        }

        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(_env.ContentRootPath, "BrowserExtension")),
            Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "..", "..", "browser-extension"))
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }
}
