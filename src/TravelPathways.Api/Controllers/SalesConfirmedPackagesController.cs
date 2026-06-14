using System.Security.Claims;
using System.Text.Json.Serialization;
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
[Route("api/sales/confirmed-packages")]
public sealed class SalesConfirmedPackagesController : TenantControllerBase
{
    private const int EmployeeHistoryMonths = 3;
    private readonly AppDbContext _db;

    public SalesConfirmedPackagesController(AppDbContext db, TenantContext tenant) : base(tenant)
    {
        _db = db;
    }

    public sealed class SalesConfirmedPackageDto
    {
        public required string Id { get; init; }
        public required string ClientName { get; init; }
        public required string ClientPhone { get; init; }
        public required string ArrivalDate { get; init; }
        public required string DepartureDate { get; init; }
        public required decimal ExpectedProfit { get; init; }
        public decimal? ActualProfit { get; init; }
        public required string ConfirmationDate { get; init; }
        public required SalesPackageSourceType SourceType { get; init; }
        public string? LeadId { get; init; }
        public string? LeadClientName { get; init; }
        public string? ReferenceName { get; init; }
        public string? ReferenceContact { get; init; }
        public SalesReferenceSourceType? ReferenceSourceType { get; init; }
        public string? RecordedByUserId { get; init; }
        public string? RecordedByName { get; init; }
        public required string TenantId { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
    }

    public class SaveSalesConfirmedPackageDto
    {
        public string ClientName { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public DateOnly ArrivalDate { get; set; }
        public DateOnly DepartureDate { get; set; }
        public decimal ExpectedProfit { get; set; }
        public decimal? ActualProfit { get; set; }
        public DateOnly ConfirmationDate { get; set; }
        public SalesPackageSourceType SourceType { get; set; }
        public Guid? LeadId { get; set; }
        public string? ReferenceName { get; set; }
        public string? ReferenceContact { get; set; }
        [JsonConverter(typeof(SalesReferenceSourceTypeJsonConverter))]
        public SalesReferenceSourceType? ReferenceSourceType { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<SalesConfirmedPackageDto>>>> GetList(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        [FromQuery] string? arrivalDate = null,
        [FromQuery] string? departureDate = null,
        [FromQuery] SalesPackageSourceType? sourceType = null,
        [FromQuery] Guid? recordedByUserId = null,
        [FromQuery] bool? periodCurrentMonth = null,
        [FromQuery] bool? periodLastMonth = null,
        [FromQuery] bool? periodLastMonths = null,
        CancellationToken ct = default)
    {
        var denied = await EnsureSalesModuleAsync(ct);
        if (denied != null) return denied;

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.SalesConfirmedPackages.AsNoTracking()
            .Where(p => p.TenantId == TenantId);

        if (IsTenantAdmin())
        {
            if (recordedByUserId.HasValue)
                query = query.Where(p => p.RecordedByUserId == recordedByUserId.Value);

            if (DateOnly.TryParse(dateFrom, out var from))
                query = query.Where(p => p.ConfirmationDate >= from);
            if (DateOnly.TryParse(dateTo, out var to))
            {
                var maxConfirmationDate = EmployeeListWindowEnd();
                if (to > maxConfirmationDate)
                    to = maxConfirmationDate;
                query = query.Where(p => p.ConfirmationDate <= to);
            }
        }
        else
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return ApiResponse<PaginatedResponse<SalesConfirmedPackageDto>>.Ok(EmptyList(pageNumber, pageSize));
            }

            var windowStart = EmployeeListWindowStart();
            var windowEnd = EmployeeListWindowEnd();
            var includeCurrent = periodCurrentMonth ?? true;
            var includeLast = periodLastMonth ?? true;
            var includeOlder = periodLastMonths ?? false;

            if (!includeCurrent && !includeLast && !includeOlder)
            {
                return ApiResponse<PaginatedResponse<SalesConfirmedPackageDto>>.Ok(EmptyList(pageNumber, pageSize));
            }

            var currentMonthStart = new DateOnly(windowEnd.Year, windowEnd.Month, 1);
            var lastMonthStart = currentMonthStart.AddMonths(-1);

            query = query.Where(p =>
                p.RecordedByUserId == currentUserId.Value &&
                (
                    (includeCurrent && p.ConfirmationDate >= currentMonthStart && p.ConfirmationDate <= windowEnd) ||
                    (includeLast && p.ConfirmationDate >= lastMonthStart && p.ConfirmationDate < currentMonthStart) ||
                    (includeOlder && p.ConfirmationDate >= windowStart && p.ConfirmationDate < lastMonthStart)
                ));
        }

        if (DateOnly.TryParse(arrivalDate, out var arrival))
            query = query.Where(p => p.ArrivalDate == arrival);
        if (DateOnly.TryParse(departureDate, out var departure))
            query = query.Where(p => p.DepartureDate == departure);
        if (sourceType.HasValue)
            query = query.Where(p => p.SourceType == sourceType.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var s = searchTerm.Trim().ToLower();
            query = query.Where(p =>
                p.ClientName.ToLower().Contains(s) ||
                p.ClientPhone.Contains(s) ||
                (p.ReferenceName != null && p.ReferenceName.ToLower().Contains(s)) ||
                (p.ReferenceContact != null && p.ReferenceContact.Contains(s)));
        }

        var total = await query.CountAsync(ct);
        var totalExpectedProfit = total > 0 ? await query.SumAsync(p => p.ExpectedProfit, ct) : 0m;
        var totalActualProfit = total > 0
            ? await query.Where(p => p.ActualProfit != null).SumAsync(p => p.ActualProfit!.Value, ct)
            : 0m;
        var rows = await query
            .OrderByDescending(p => p.ConfirmationDate)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Lead)
            .Include(p => p.RecordedBy)
            .ToListAsync(ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return ApiResponse<PaginatedResponse<SalesConfirmedPackageDto>>.Ok(new PaginatedResponse<SalesConfirmedPackageDto>
        {
            Items = rows.Select(ToDto).ToList(),
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalExpectedProfit = totalExpectedProfit,
            TotalActualProfit = totalActualProfit
        });
    }

    [HttpGet("used-lead-ids")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> GetUsedLeadIds(CancellationToken ct = default)
    {
        var denied = await EnsureSalesModuleAsync(ct);
        if (denied != null) return denied;

        var ids = await _db.SalesConfirmedPackages.AsNoTracking()
            .Where(p => p.TenantId == TenantId && p.LeadId != null)
            .Select(p => p.LeadId!.Value.ToString("D"))
            .Distinct()
            .ToListAsync(ct);

        return ApiResponse<IReadOnlyList<string>>.Ok(ids);
    }

    public sealed class EmployeePerformanceDto
    {
        public required int Rank { get; init; }
        public required string UserId { get; init; }
        public required string UserName { get; init; }
        public required int PackageCount { get; init; }
        public required decimal TotalActualProfit { get; init; }
        public required decimal TotalExpectedProfit { get; init; }
    }

    /// <summary>Admin: employee ranking by total actual profit for the current filter window.</summary>
    [HttpGet("employee-performance")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EmployeePerformanceDto>>>> GetEmployeePerformance(
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        [FromQuery] string? arrivalDate = null,
        [FromQuery] string? departureDate = null,
        [FromQuery] SalesPackageSourceType? sourceType = null,
        CancellationToken ct = default)
    {
        var denied = await EnsureSalesModuleAsync(ct);
        if (denied != null) return denied;

        if (!IsTenantAdmin())
            return Forbid();

        var query = _db.SalesConfirmedPackages.AsNoTracking()
            .Where(p => p.TenantId == TenantId);

        if (DateOnly.TryParse(dateFrom, out var from))
            query = query.Where(p => p.ConfirmationDate >= from);
        if (DateOnly.TryParse(dateTo, out var to))
        {
            var maxConfirmationDate = EmployeeListWindowEnd();
            if (to > maxConfirmationDate)
                to = maxConfirmationDate;
            query = query.Where(p => p.ConfirmationDate <= to);
        }

        query = ApplySharedPackageFilters(query, arrivalDate, departureDate, sourceType, searchTerm: null);

        var grouped = await query
            .GroupBy(p => p.RecordedByUserId)
            .Select(g => new
            {
                UserId = g.Key,
                PackageCount = g.Count(),
                TotalActualProfit = g.Sum(p => p.ActualProfit ?? 0m),
                TotalExpectedProfit = g.Sum(p => p.ExpectedProfit)
            })
            .OrderByDescending(x => x.TotalActualProfit)
            .ThenByDescending(x => x.PackageCount)
            .ToListAsync(ct);

        if (grouped.Count == 0)
            return ApiResponse<IReadOnlyList<EmployeePerformanceDto>>.Ok([]);

        var userIds = grouped.Select(x => x.UserId).Distinct().ToList();
        var users = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == TenantId && userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        var items = grouped
            .Select((row, index) =>
            {
                users.TryGetValue(row.UserId, out var user);
                var name = user == null
                    ? "Unknown"
                    : $"{user.FirstName} {user.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(name))
                    name = user?.Email ?? "Unknown";

                return new EmployeePerformanceDto
                {
                    Rank = index + 1,
                    UserId = row.UserId.ToString("D"),
                    UserName = name,
                    PackageCount = row.PackageCount,
                    TotalActualProfit = row.TotalActualProfit,
                    TotalExpectedProfit = row.TotalExpectedProfit
                };
            })
            .ToList();

        return ApiResponse<IReadOnlyList<EmployeePerformanceDto>>.Ok(items);
    }

    /// <summary>Upcoming arrivals (arrival date from today). Employees see own packages; admins see all.</summary>
    [HttpGet("arrivals")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<SalesConfirmedPackageDto>>>> GetArrivals(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? arrivalDateFrom = null,
        [FromQuery] string? arrivalDateTo = null,
        [FromQuery] Guid? recordedByUserId = null,
        CancellationToken ct = default)
    {
        var denied = await EnsureSalesModuleAsync(ct);
        if (denied != null) return denied;

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var today = EmployeeListWindowEnd();
        var hasFromFilter = DateOnly.TryParse(arrivalDateFrom, out var from);
        var hasToFilter = DateOnly.TryParse(arrivalDateTo, out var to);
        var hasDateRangeFilter = hasFromFilter || hasToFilter;

        var query = _db.SalesConfirmedPackages.AsNoTracking()
            .Where(p => p.TenantId == TenantId);

        // Default: upcoming only. With a date range, honour the selected window (including past dates in range).
        if (!hasDateRangeFilter)
            query = query.Where(p => p.ArrivalDate >= today);
        else
        {
            if (hasFromFilter)
                query = query.Where(p => p.ArrivalDate >= from);
            if (hasToFilter)
                query = query.Where(p => p.ArrivalDate <= to);
        }

        if (IsTenantAdmin())
        {
            if (recordedByUserId.HasValue)
                query = query.Where(p => p.RecordedByUserId == recordedByUserId.Value);
        }
        else
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
                return ApiResponse<PaginatedResponse<SalesConfirmedPackageDto>>.Ok(EmptyList(pageNumber, pageSize));

            query = query.Where(p => p.RecordedByUserId == currentUserId.Value);
        }

        var total = await query.CountAsync(ct);
        var totalExpectedProfit = total > 0 ? await query.SumAsync(p => p.ExpectedProfit, ct) : 0m;
        var totalActualProfit = total > 0
            ? await query.Where(p => p.ActualProfit != null).SumAsync(p => p.ActualProfit!.Value, ct)
            : 0m;
        var rows = await query
            .OrderBy(p => p.ArrivalDate)
            .ThenBy(p => p.ClientName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Lead)
            .Include(p => p.RecordedBy)
            .ToListAsync(ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return ApiResponse<PaginatedResponse<SalesConfirmedPackageDto>>.Ok(new PaginatedResponse<SalesConfirmedPackageDto>
        {
            Items = rows.Select(ToDto).ToList(),
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalExpectedProfit = totalExpectedProfit,
            TotalActualProfit = totalActualProfit
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SalesConfirmedPackageDto>>> GetById(Guid id, CancellationToken ct = default)
    {
        var denied = await EnsureSalesModuleAsync(ct);
        if (denied != null) return denied;

        var row = await _db.SalesConfirmedPackages.AsNoTracking()
            .Include(p => p.Lead)
            .Include(p => p.RecordedBy)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);

        if (row == null)
            return NotFound(ApiResponse<SalesConfirmedPackageDto>.Fail("Record not found."));

        if (!CanView(row))
            return NotFound(ApiResponse<SalesConfirmedPackageDto>.Fail("Record not found."));

        return ApiResponse<SalesConfirmedPackageDto>.Ok(ToDto(row));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SalesConfirmedPackageDto>>> Create(
        [FromBody] SaveSalesConfirmedPackageDto dto,
        CancellationToken ct = default)
    {
        var denied = await EnsureSalesModuleAsync(ct);
        if (denied != null) return denied;

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return BadRequest(ApiResponse<SalesConfirmedPackageDto>.Fail("User context is missing."));

        var (valid, error) = await ValidateDtoAsync(dto, ct, excludePackageId: null);
        if (!valid)
            return BadRequest(ApiResponse<SalesConfirmedPackageDto>.Fail(error!));

        var now = DateTime.UtcNow;
        var entity = new SalesConfirmedPackage
        {
            TenantId = TenantId,
            ClientName = dto.ClientName.Trim(),
            ClientPhone = dto.ClientPhone.Trim(),
            ArrivalDate = dto.ArrivalDate,
            DepartureDate = dto.DepartureDate,
            ExpectedProfit = dto.ExpectedProfit,
            ActualProfit = dto.ActualProfit,
            ConfirmationDate = dto.ConfirmationDate,
            SourceType = dto.SourceType,
            LeadId = dto.LeadId,
            ReferenceName = dto.SourceType == SalesPackageSourceType.Reference
                ? TrimOrNull(dto.ReferenceName)
                : null,
            ReferenceContact = dto.SourceType == SalesPackageSourceType.Reference
                ? TrimOrNull(dto.ReferenceContact)
                : null,
            ReferenceSourceType = null,
            RecordedByUserId = userId.Value,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.SalesConfirmedPackages.Add(entity);
        await _db.SaveChangesAsync(ct);

        await LoadNavigationsAsync(entity, ct);
        return ApiResponse<SalesConfirmedPackageDto>.Ok(ToDto(entity));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SalesConfirmedPackageDto>>> Update(
        Guid id,
        [FromBody] SaveSalesConfirmedPackageDto dto,
        CancellationToken ct = default)
    {
        var denied = await EnsureSalesModuleAsync(ct);
        if (denied != null) return denied;

        var entity = await _db.SalesConfirmedPackages
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);

        if (entity == null)
            return NotFound(ApiResponse<SalesConfirmedPackageDto>.Fail("Record not found."));

        if (!CanModify(entity))
            return Forbid();

        var (valid, error) = await ValidateDtoAsync(dto, ct, excludePackageId: id);
        if (!valid)
            return BadRequest(ApiResponse<SalesConfirmedPackageDto>.Fail(error!));

        entity.ClientName = dto.ClientName.Trim();
        entity.ClientPhone = dto.ClientPhone.Trim();
        entity.ArrivalDate = dto.ArrivalDate;
        entity.DepartureDate = dto.DepartureDate;
        entity.ExpectedProfit = dto.ExpectedProfit;
        entity.ActualProfit = dto.ActualProfit;
        entity.ConfirmationDate = dto.ConfirmationDate;
        entity.SourceType = dto.SourceType;
        entity.LeadId = dto.LeadId;
        entity.ReferenceName = dto.SourceType == SalesPackageSourceType.Reference
            ? TrimOrNull(dto.ReferenceName)
            : null;
        entity.ReferenceContact = dto.SourceType == SalesPackageSourceType.Reference
            ? TrimOrNull(dto.ReferenceContact)
            : null;
        entity.ReferenceSourceType = null;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await LoadNavigationsAsync(entity, ct);
        return ApiResponse<SalesConfirmedPackageDto>.Ok(ToDto(entity));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct = default)
    {
        var denied = await EnsureSalesModuleAsync(ct);
        if (denied != null) return denied;

        var entity = await _db.SalesConfirmedPackages
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);

        if (entity == null)
            return NotFound(ApiResponse<object>.Fail("Record not found."));

        if (!CanModify(entity))
            return Forbid();

        entity.IsDeleted = true;
        entity.DeletedAtUtc = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<object>.Ok(new { });
    }

    private bool CanModify(SalesConfirmedPackage entity) => CanView(entity);

    private bool CanView(SalesConfirmedPackage entity)
    {
        if (IsTenantAdmin()) return true;
        var userId = GetCurrentUserId();
        if (!userId.HasValue || entity.RecordedByUserId != userId.Value)
            return false;
        return entity.ConfirmationDate >= EmployeeListWindowStart()
            && entity.ConfirmationDate <= EmployeeListWindowEnd();
    }

    private static DateOnly EmployeeListWindowStart()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return today.AddMonths(-EmployeeHistoryMonths);
    }

    private static DateOnly EmployeeListWindowEnd() =>
        DateOnly.FromDateTime(DateTime.UtcNow);

    private static IQueryable<SalesConfirmedPackage> ApplySharedPackageFilters(
        IQueryable<SalesConfirmedPackage> query,
        string? arrivalDate,
        string? departureDate,
        SalesPackageSourceType? sourceType,
        string? searchTerm)
    {
        if (DateOnly.TryParse(arrivalDate, out var arrival))
            query = query.Where(p => p.ArrivalDate == arrival);
        if (DateOnly.TryParse(departureDate, out var departure))
            query = query.Where(p => p.DepartureDate == departure);
        if (sourceType.HasValue)
            query = query.Where(p => p.SourceType == sourceType.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var s = searchTerm.Trim().ToLower();
            query = query.Where(p =>
                p.ClientName.ToLower().Contains(s) ||
                p.ClientPhone.Contains(s) ||
                (p.ReferenceName != null && p.ReferenceName.ToLower().Contains(s)) ||
                (p.ReferenceContact != null && p.ReferenceContact.Contains(s)));
        }

        return query;
    }

    private static PaginatedResponse<SalesConfirmedPackageDto> EmptyList(int pageNumber, int pageSize) =>
        new()
        {
            Items = [],
            TotalCount = 0,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = 1,
            TotalExpectedProfit = 0,
            TotalActualProfit = 0
        };

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private async Task<ActionResult?> EnsureSalesModuleAsync(CancellationToken ct)
    {
        if (!HasTenantId)
            return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));

        if (!await TenantModuleResolver.HasModuleAsync(_db, User, TenantId, AppModuleKey.Sales, ct))
            return StatusCode(403, ApiResponse<object>.Fail("Sales module is not available for your account."));

        return null;
    }

    private async Task<(bool Valid, string? Error)> ValidateDtoAsync(
        SaveSalesConfirmedPackageDto dto,
        CancellationToken ct,
        Guid? excludePackageId = null)
    {
        if (string.IsNullOrWhiteSpace(dto.ClientName))
            return (false, "Client name is required.");
        if (string.IsNullOrWhiteSpace(dto.ClientPhone))
            return (false, "Client phone number is required.");

        var phone = dto.ClientPhone.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\d{10}$"))
            return (false, "Client phone must be exactly 10 digits.");

        if (dto.DepartureDate < dto.ArrivalDate)
            return (false, "Departure date cannot be before arrival date.");

        if (dto.ConfirmationDate == default)
            return (false, "Package confirmation date is required.");

        if (dto.ConfirmationDate > EmployeeListWindowEnd())
            return (false, "Package confirmation date cannot be in the future.");

        if (dto.ExpectedProfit < 0)
            return (false, "Expected profit cannot be negative.");

        if (dto.ActualProfit is { } actual && actual < 0)
            return (false, "Actual profit cannot be negative.");

        if (!dto.LeadId.HasValue)
            return (false, "Select a lead.");

        var leadInfo = await _db.Leads.AsNoTracking()
            .Where(l => l.Id == dto.LeadId.Value && l.TenantId == TenantId && !l.IsDeleted)
            .Select(l => new { l.AssignedToUserId, l.Status })
            .FirstOrDefaultAsync(ct);
        if (leadInfo == null)
            return (false, "Lead not found.");

        if (leadInfo.Status != LeadStatus.Confirmed)
            return (false, "Only confirmed leads can be added to confirmed packages.");

        if (!IsTenantAdmin())
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return (false, "User context is missing.");

            if (leadInfo.AssignedToUserId != userId.Value)
                return (false, "You can only confirm packages for leads assigned to you.");
        }

        var duplicateLead = await _db.SalesConfirmedPackages.AsNoTracking()
            .AnyAsync(p => p.TenantId == TenantId
                           && p.LeadId == dto.LeadId.Value
                           && (excludePackageId == null || p.Id != excludePackageId.Value), ct);
        if (duplicateLead)
            return (false, "This lead already has a confirmed package.");

        if (dto.SourceType == SalesPackageSourceType.Reference)
        {
            if (!string.IsNullOrWhiteSpace(dto.ReferenceContact))
            {
                var refPhone = dto.ReferenceContact.Trim();
                if (!System.Text.RegularExpressions.Regex.IsMatch(refPhone, @"^\d{10}$"))
                    return (false, "Reference contact must be exactly 10 digits when provided.");
            }
        }
        else if (dto.SourceType != SalesPackageSourceType.Lead)
        {
            return (false, "Invalid source type.");
        }

        return (true, null);
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task LoadNavigationsAsync(SalesConfirmedPackage entity, CancellationToken ct)
    {
        await _db.Entry(entity).Reference(p => p.Lead).LoadAsync(ct);
        await _db.Entry(entity).Reference(p => p.RecordedBy).LoadAsync(ct);
    }

    private static SalesConfirmedPackageDto ToDto(SalesConfirmedPackage p)
    {
        var recordedBy = p.RecordedBy;
        var recordedByName = recordedBy == null
            ? null
            : $"{recordedBy.FirstName} {recordedBy.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(recordedByName))
            recordedByName = recordedBy?.Email;

        return new SalesConfirmedPackageDto
        {
            Id = p.Id.ToString(),
            ClientName = p.ClientName,
            ClientPhone = p.ClientPhone,
            ArrivalDate = p.ArrivalDate.ToString("yyyy-MM-dd"),
            DepartureDate = p.DepartureDate.ToString("yyyy-MM-dd"),
            ExpectedProfit = p.ExpectedProfit,
            ActualProfit = p.ActualProfit,
            ConfirmationDate = p.ConfirmationDate.ToString("yyyy-MM-dd"),
            SourceType = p.SourceType,
            LeadId = p.LeadId?.ToString(),
            LeadClientName = p.Lead?.ClientName,
            ReferenceName = p.ReferenceName,
            ReferenceContact = p.ReferenceContact,
            ReferenceSourceType = p.ReferenceSourceType,
            RecordedByUserId = p.RecordedByUserId.ToString(),
            RecordedByName = recordedByName,
            TenantId = p.TenantId.ToString(),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
