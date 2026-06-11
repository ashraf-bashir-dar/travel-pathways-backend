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
[Route("api/location")]
public sealed class LocationController : ControllerBase
{
    private const string MobileProvider = "android";

    public sealed class MobileLocationSyncItemDto
    {
        public string ProviderPointId { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? AccuracyMeters { get; set; }
        public DateTime? RecordedAtUtc { get; set; }
    }

    public sealed class MobileLocationSyncRequestDto
    {
        public List<MobileLocationSyncItemDto> Points { get; set; } = [];
    }

    public sealed class MobileLocationSyncResponseDto
    {
        public required int Received { get; init; }
        public required int Created { get; init; }
        public required int Skipped { get; init; }
    }

    public sealed class TeamLocationDto
    {
        public required string UserId { get; init; }
        public required string DisplayName { get; init; }
        public string? Phone { get; init; }
        public required double Latitude { get; init; }
        public required double Longitude { get; init; }
        public double? AccuracyMeters { get; init; }
        public required DateTime RecordedAtUtc { get; init; }
    }

    public sealed class LocationHistoryDto
    {
        public required string Id { get; init; }
        public required double Latitude { get; init; }
        public required double Longitude { get; init; }
        public double? AccuracyMeters { get; init; }
        public required DateTime RecordedAtUtc { get; init; }
    }

    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public LocationController(AppDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpPost("sync")]
    public async Task<ActionResult<ApiResponse<MobileLocationSyncResponseDto>>> SyncMobileLocations(
        [FromBody] MobileLocationSyncRequestDto request,
        CancellationToken ct)
    {
        if (!TryGetCallerId(out var callerId))
            return Unauthorized(ApiResponse<MobileLocationSyncResponseDto>.Fail("User not found."));

        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<MobileLocationSyncResponseDto>.Fail("Tenant context is missing."));

        var tenantId = _tenant.TenantId.Value;
        var items = request.Points ?? [];
        if (items.Count == 0)
        {
            return ApiResponse<MobileLocationSyncResponseDto>.Ok(new MobileLocationSyncResponseDto
            {
                Received = 0,
                Created = 0,
                Skipped = 0
            });
        }

        if (items.Count > 200)
            return BadRequest(ApiResponse<MobileLocationSyncResponseDto>.Fail("Maximum 200 location points per sync request."));

        var created = 0;
        var skipped = 0;

        foreach (var item in items)
        {
            var pointId = (item.ProviderPointId ?? string.Empty).Trim();
            if (pointId.Length == 0 || !IsValidCoordinate(item.Latitude, item.Longitude))
            {
                skipped++;
                continue;
            }

            var exists = await _db.EmployeeLocationLogs.AsNoTracking()
                .AnyAsync(l =>
                    l.TenantId == tenantId &&
                    l.Provider == MobileProvider &&
                    l.ProviderPointId == pointId &&
                    !l.IsDeleted, ct);

            if (exists)
            {
                skipped++;
                continue;
            }

            var recordedAt = item.RecordedAtUtc.HasValue
                ? DateTime.SpecifyKind(item.RecordedAtUtc.Value, DateTimeKind.Utc)
                : DateTime.UtcNow;

            _db.EmployeeLocationLogs.Add(new EmployeeLocationLog
            {
                TenantId = tenantId,
                IsActive = true,
                UserId = callerId,
                Latitude = item.Latitude,
                Longitude = item.Longitude,
                AccuracyMeters = item.AccuracyMeters,
                Provider = MobileProvider,
                ProviderPointId = pointId,
                RecordedAtUtc = recordedAt
            });
            created++;
        }

        await _db.SaveChangesAsync(ct);

        return ApiResponse<MobileLocationSyncResponseDto>.Ok(new MobileLocationSyncResponseDto
        {
            Received = items.Count,
            Created = created,
            Skipped = skipped
        });
    }

    [HttpGet("team/latest")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<List<TeamLocationDto>>>> GetTeamLatestLocations(
        [FromQuery] DateTime? date,
        CancellationToken ct)
    {
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<List<TeamLocationDto>>.Fail("Tenant context is missing."));

        var tenantId = _tenant.TenantId.Value;
        var dayStart = date.HasValue ? NormalizeDateStartUtc(date.Value) : NormalizeDateStartUtc(DateTime.UtcNow);
        var dayEnd = dayStart.AddDays(1);

        var salesUsers = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive && u.Department == UserDepartment.Sales)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Phone
            })
            .ToListAsync(ct);

        var latestPoints = await _db.EmployeeLocationLogs.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.RecordedAtUtc >= dayStart && l.RecordedAtUtc < dayEnd)
            .GroupBy(l => l.UserId)
            .Select(g => g.OrderByDescending(x => x.RecordedAtUtc).First())
            .ToListAsync(ct);

        var lookup = latestPoints.ToDictionary(x => x.UserId);

        var result = salesUsers.Select(u =>
        {
            if (!lookup.TryGetValue(u.Id, out var point))
            {
                return null;
            }

            return new TeamLocationDto
            {
                UserId = u.Id.ToString("D"),
                DisplayName = $"{u.FirstName} {u.LastName}".Trim(),
                Phone = u.Phone,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                AccuracyMeters = point.AccuracyMeters,
                RecordedAtUtc = point.RecordedAtUtc
            };
        }).Where(x => x is not null).Cast<TeamLocationDto>().ToList();

        return ApiResponse<List<TeamLocationDto>>.Ok(result);
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<List<LocationHistoryDto>>>> GetLocationHistory(
        [FromQuery] Guid? userId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        if (!TryGetCallerId(out var callerId))
            return Unauthorized(ApiResponse<List<LocationHistoryDto>>.Fail("User not found."));

        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<List<LocationHistoryDto>>.Fail("Tenant context is missing."));

        var isAdmin = IsTenantAdmin();
        Guid targetUserId;

        if (isAdmin && userId.HasValue)
        {
            var exists = await _db.Users.AsNoTracking()
                .AnyAsync(u => u.Id == userId.Value && u.TenantId == _tenant.TenantId, ct);
            if (!exists)
                return NotFound(ApiResponse<List<LocationHistoryDto>>.Fail("User not found."));
            targetUserId = userId.Value;
        }
        else if (isAdmin && !userId.HasValue)
        {
            targetUserId = callerId;
        }
        else
        {
            if (userId.HasValue && userId.Value != callerId)
                return Forbid();
            targetUserId = callerId;
        }

        var fromUtc = dateFrom.HasValue ? NormalizeDateStartUtc(dateFrom.Value) : NormalizeDateStartUtc(DateTime.UtcNow);
        var toUtcExclusive = dateTo.HasValue
            ? NormalizeDateStartUtc(dateTo.Value).AddDays(1)
            : fromUtc.AddDays(1);

        var history = await _db.EmployeeLocationLogs.AsNoTracking()
            .Where(l => l.UserId == targetUserId && l.RecordedAtUtc >= fromUtc && l.RecordedAtUtc < toUtcExclusive)
            .OrderByDescending(l => l.RecordedAtUtc)
            .Take(500)
            .Select(l => new LocationHistoryDto
            {
                Id = l.Id.ToString("D"),
                Latitude = l.Latitude,
                Longitude = l.Longitude,
                AccuracyMeters = l.AccuracyMeters,
                RecordedAtUtc = l.RecordedAtUtc
            })
            .ToListAsync(ct);

        return ApiResponse<List<LocationHistoryDto>>.Ok(history);
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

    private static bool IsValidCoordinate(double lat, double lng)
        => lat is >= -90 and <= 90 && lng is >= -180 and <= 180 && !(lat == 0 && lng == 0);
}
