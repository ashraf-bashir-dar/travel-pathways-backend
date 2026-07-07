using System.Security.Claims;
using System.Text.Json;
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
[Route("api/calls")]
public sealed class CallsController : ControllerBase
{
    public sealed class SalesTeamCallSummaryDto
    {
        public required string UserId { get; init; }
        public required string DisplayName { get; init; }
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public required int OutgoingCount { get; init; }
        public required int IncomingCount { get; init; }
        public required int MissedCount { get; init; }
        public required int TotalCount { get; init; }
    }

    public sealed class MobileCallSyncItemDto
    {
        public string ProviderCallId { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string? FromNumber { get; set; }
        public string? ToNumber { get; set; }
        public string? Status { get; set; }
        public int? DurationSeconds { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? EndedAtUtc { get; set; }
    }

    public sealed class MobileCallSyncRequestDto
    {
        public List<MobileCallSyncItemDto> Calls { get; set; } = [];
    }

    public sealed class MobileCallSyncResponseDto
    {
        public required int Received { get; init; }
        public required int Created { get; init; }
        public required int Updated { get; init; }
        public required int Skipped { get; init; }
    }

    public sealed class CallLogDto
    {
        public required string Id { get; init; }
        public string? UserId { get; init; }
        public string? UserDisplayName { get; init; }
        public required string Direction { get; init; }
        public string? Status { get; init; }

        public string? Provider { get; init; }
        public string? ProviderCallId { get; init; }

        public string? FromNumber { get; init; }
        public string? ToNumber { get; init; }

        public DateTime? StartedAtUtc { get; init; }
        public DateTime? EndedAtUtc { get; init; }
        public int? DurationSeconds { get; init; }

        public required DateTime CreatedAt { get; init; }
    }

    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public CallsController(AppDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private const string MobileProvider = "android";

    /// <summary>
    /// Mobile app: upload call log entries from the employee's office phone (JWT auth).
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult<ApiResponse<MobileCallSyncResponseDto>>> SyncMobileCalls(
        [FromBody] MobileCallSyncRequestDto request,
        CancellationToken ct)
    {
        if (!TryGetCallerId(out var callerId))
            return Unauthorized(ApiResponse<MobileCallSyncResponseDto>.Fail("User not found."));

        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<MobileCallSyncResponseDto>.Fail("Tenant context is missing."));

        var tenantId = _tenant.TenantId.Value;
        var items = request.Calls ?? [];
        if (items.Count == 0)
        {
            return ApiResponse<MobileCallSyncResponseDto>.Ok(new MobileCallSyncResponseDto
            {
                Received = 0,
                Created = 0,
                Updated = 0,
                Skipped = 0
            });
        }

        if (items.Count > 500)
            return BadRequest(ApiResponse<MobileCallSyncResponseDto>.Fail("Maximum 500 calls per sync request."));

        var created = 0;
        var updated = 0;
        var skipped = 0;

        var providerCallIds = items
            .Select(i => (i.ProviderCallId ?? string.Empty).Trim())
            .Where(id => id.Length > 0)
            .Distinct()
            .ToList();

        var existingByProviderCallId = await _db.CallLogs
            .Where(l =>
                l.TenantId == tenantId &&
                l.Provider == MobileProvider &&
                !l.IsDeleted &&
                l.ProviderCallId != null &&
                providerCallIds.Contains(l.ProviderCallId))
            .ToDictionaryAsync(l => l.ProviderCallId!, ct);

        foreach (var item in items)
        {
            var providerCallId = (item.ProviderCallId ?? string.Empty).Trim();
            if (providerCallId.Length == 0)
            {
                skipped++;
                continue;
            }

            var direction = NormalizeMobileDirection(item.Direction);
            if (direction is null)
            {
                skipped++;
                continue;
            }

            existingByProviderCallId.TryGetValue(providerCallId, out var existing);

            var rawPayload = JsonSerializer.Serialize(item);

            if (existing is not null)
            {
                existing.Direction = direction;
                existing.Status = item.Status ?? existing.Status;
                existing.FromNumber = item.FromNumber ?? existing.FromNumber;
                existing.ToNumber = item.ToNumber ?? existing.ToNumber;
                existing.StartedAtUtc = item.StartedAtUtc ?? existing.StartedAtUtc;
                existing.EndedAtUtc = item.EndedAtUtc ?? existing.EndedAtUtc;
                existing.DurationSeconds = item.DurationSeconds ?? existing.DurationSeconds;
                existing.UserId = callerId;
                existing.RawPayload = rawPayload;
                updated++;
            }
            else
            {
                _db.CallLogs.Add(new CallLog
                {
                    TenantId = tenantId,
                    IsActive = true,
                    UserId = callerId,
                    Direction = direction,
                    Status = item.Status,
                    Provider = MobileProvider,
                    ProviderCallId = providerCallId,
                    FromNumber = item.FromNumber,
                    ToNumber = item.ToNumber,
                    StartedAtUtc = item.StartedAtUtc,
                    EndedAtUtc = item.EndedAtUtc,
                    DurationSeconds = item.DurationSeconds,
                    RawPayload = rawPayload
                });
                created++;
            }
        }

        await _db.SaveChangesAsync(ct);

        return ApiResponse<MobileCallSyncResponseDto>.Ok(new MobileCallSyncResponseDto
        {
            Received = items.Count,
            Created = created,
            Updated = updated,
            Skipped = skipped
        });
    }

    /// <summary>
    /// Fetch call logs for the currently logged-in user.
    /// </summary>
    [HttpGet("me/logs")]
    public async Task<ActionResult<ApiResponse<List<CallLogDto>>>> GetMyCallLogs(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        if (!TryGetCallerId(out var callerId))
            return Unauthorized(ApiResponse<List<CallLogDto>>.Fail("User not found."));

        var logs = await QueryCallLogsAsync(callerId, dateFrom, dateTo, ct);
        return ApiResponse<List<CallLogDto>>.Ok(logs);
    }

    /// <summary>
    /// Tenant Admin: view call history for any user in the tenant. Other users only see their own logs.
    /// </summary>
    [HttpGet("logs")]
    public async Task<ActionResult<ApiResponse<List<CallLogDto>>>> GetCallLogs(
        [FromQuery] Guid? userId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        if (!TryGetCallerId(out var callerId))
            return Unauthorized(ApiResponse<List<CallLogDto>>.Fail("User not found."));

        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<List<CallLogDto>>.Fail("Tenant context is missing."));

        var isAdmin = IsTenantAdmin();
        Guid? targetUserId;

        if (isAdmin)
        {
            targetUserId = userId ?? callerId;
            if (userId.HasValue)
            {
                var exists = await _db.Users.AsNoTracking()
                    .AnyAsync(u => u.Id == userId.Value && u.TenantId == _tenant.TenantId, ct);
                if (!exists)
                    return NotFound(ApiResponse<List<CallLogDto>>.Fail("User not found."));
            }
        }
        else
        {
            if (userId.HasValue && userId.Value != callerId)
                return Forbid();
            targetUserId = callerId;
        }

        var logs = await QueryCallLogsAsync(targetUserId, dateFrom, dateTo, ct);
        return ApiResponse<List<CallLogDto>>.Ok(logs);
    }

    /// <summary>
    /// Tenant Admin: per-salesperson call counts (outgoing, incoming, missed) for the selected date range.
    /// </summary>
    [HttpGet("team-summary")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<List<SalesTeamCallSummaryDto>>>> GetSalesTeamCallSummary(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<List<SalesTeamCallSummaryDto>>.Fail("Tenant context is missing."));

        var tenantId = _tenant.TenantId.Value;
        var fromUtc = dateFrom.HasValue ? NormalizeDateStartUtc(dateFrom.Value) : NormalizeDateStartUtc(DateTime.UtcNow);
        var toUtcExclusive = dateTo.HasValue
            ? NormalizeDateStartUtc(dateTo.Value).AddDays(1)
            : fromUtc.AddDays(1);

        var salesUsers = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive && u.Department == UserDepartment.Sales)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Phone
            })
            .ToListAsync(ct);

        var callCounts = await _db.CallLogs.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.UserId != null && l.CreatedAt >= fromUtc && l.CreatedAt < toUtcExclusive)
            .GroupBy(l => new { l.UserId, l.Direction })
            .Select(g => new { g.Key.UserId, g.Key.Direction, Count = g.Count() })
            .ToListAsync(ct);

        var countLookup = callCounts
            .GroupBy(x => x.UserId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(x => x.Direction, x => x.Count, StringComparer.OrdinalIgnoreCase));

        var summary = salesUsers.Select(u =>
        {
            countLookup.TryGetValue(u.Id, out var byDirection);
            var outgoing = byDirection?.GetValueOrDefault("outgoing") ?? 0;
            var incoming = byDirection?.GetValueOrDefault("incoming") ?? 0;
            var missed = byDirection?.GetValueOrDefault("missed") ?? 0;
            return new SalesTeamCallSummaryDto
            {
                UserId = u.Id.ToString("D"),
                DisplayName = $"{u.FirstName} {u.LastName}".Trim(),
                Phone = u.Phone,
                Email = u.Email,
                OutgoingCount = outgoing,
                IncomingCount = incoming,
                MissedCount = missed,
                TotalCount = outgoing + incoming + missed
            };
        }).ToList();

        return ApiResponse<List<SalesTeamCallSummaryDto>>.Ok(summary);
    }

    private async Task<List<CallLogDto>> QueryCallLogsAsync(
        Guid? userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken ct)
    {
        var query = _db.CallLogs.AsNoTracking().AsQueryable();

        if (userId.HasValue)
            query = query.Where(l => l.UserId == userId.Value);

        if (dateFrom.HasValue)
        {
            var fromUtc = NormalizeDateStartUtc(dateFrom.Value);
            query = query.Where(l => l.CreatedAt >= fromUtc);
        }

        if (dateTo.HasValue)
        {
            var toUtcExclusive = NormalizeDateStartUtc(dateTo.Value).AddDays(1);
            query = query.Where(l => l.CreatedAt < toUtcExclusive);
        }

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(500)
            .Select(l => new CallLogDto
            {
                Id = l.Id.ToString("D"),
                UserId = l.UserId.HasValue ? l.UserId.Value.ToString("D") : null,
                UserDisplayName = l.User != null
                    ? (l.User.FirstName + " " + l.User.LastName).Trim()
                    : null,
                Direction = l.Direction,
                Status = l.Status,
                Provider = l.Provider,
                ProviderCallId = l.ProviderCallId,
                FromNumber = l.FromNumber,
                ToNumber = l.ToNumber,
                StartedAtUtc = l.StartedAtUtc,
                EndedAtUtc = l.EndedAtUtc,
                DurationSeconds = l.DurationSeconds,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync(ct);
    }

    private bool TryGetCallerId(out Guid callerId)
    {
        callerId = default;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out callerId);
    }

    private bool IsTenantAdmin()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return string.Equals(role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, UserRole.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime NormalizeDateStartUtc(DateTime value)
    {
        var d = value.Date;
        return DateTime.SpecifyKind(d, DateTimeKind.Utc);
    }

    private static string? NormalizeMobileDirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var d = value.Trim().ToLowerInvariant();
        return d is "incoming" or "outgoing" or "missed" ? d : null;
    }
}

