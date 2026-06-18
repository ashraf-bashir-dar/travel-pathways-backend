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
        public decimal TotalPackageCost { get; init; }
        public decimal TotalReceived { get; init; }
        public decimal BalanceAmount { get; init; }
        public bool IsCompleted { get; init; }
        public string? FinalReview { get; init; }
        public string? CompletedAt { get; init; }
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
        public decimal TotalPackageCost { get; set; }
        public Guid? TourPackageId { get; set; }
        public bool? IsCompleted { get; set; }
        public string? FinalReview { get; set; }
    }

    public sealed class SalesUserPaymentSummaryDto
    {
        public required string SalesUserId { get; init; }
        public string? SalesUserName { get; init; }
        public required int PackageCount { get; init; }
        public required decimal TotalPackageCost { get; init; }
        public required decimal TotalReceived { get; init; }
        public required decimal BalanceAmount { get; init; }
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
        var includeProfitDetails = IsTenantAdmin();
        var totalExpectedProfit = includeProfitDetails && total > 0
            ? await query.SumAsync(p => p.ExpectedProfit, ct)
            : 0m;
        var totalActualProfit = includeProfitDetails && total > 0
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
            Items = await ToDtosAsync(rows, includeProfitDetails, ct),
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalExpectedProfit = includeProfitDetails ? totalExpectedProfit : null,
            TotalActualProfit = includeProfitDetails ? totalActualProfit : null
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
        var includeProfitDetails = IsTenantAdmin();
        var totalExpectedProfit = includeProfitDetails && total > 0
            ? await query.SumAsync(p => p.ExpectedProfit, ct)
            : 0m;
        var totalActualProfit = includeProfitDetails && total > 0
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
            Items = await ToDtosAsync(rows, includeProfitDetails, ct),
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalExpectedProfit = includeProfitDetails ? totalExpectedProfit : null,
            TotalActualProfit = includeProfitDetails ? totalActualProfit : null
        });
    }

    [HttpGet("sales-user-payment-summary")]
    public async Task<ActionResult<ApiResponse<SalesUserPaymentSummaryDto>>> GetSalesUserPaymentSummary(
        [FromQuery] Guid salesUserId,
        CancellationToken ct = default)
    {
        var denied = await EnsureSalesModuleAsync(ct);
        if (denied != null) return denied;

        if (!IsTenantAdmin())
            return Forbid();

        var packages = await _db.SalesConfirmedPackages.AsNoTracking()
            .Where(p => p.TenantId == TenantId && p.RecordedByUserId == salesUserId)
            .Select(p => new { p.LeadId, p.TotalPackageCost })
            .ToListAsync(ct);

        var leadIds = packages.Where(p => p.LeadId.HasValue).Select(p => p.LeadId!.Value).Distinct().ToList();
        var costs = await ResolvePackageCostsAsync(
            packages.Select(p => (p.LeadId, p.TotalPackageCost)).ToList(),
            ct);
        var totalPackageCost = costs.Sum();
        var receivedByLead = await GetReceivedTotalsByLeadIdsAsync(leadIds, ct);
        var totalReceived = receivedByLead.Values.Sum();

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == salesUserId && u.TenantId == TenantId)
            .Select(u => new { u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync(ct);
        var userName = user == null
            ? null
            : $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(userName))
            userName = user?.Email;

        return ApiResponse<SalesUserPaymentSummaryDto>.Ok(new SalesUserPaymentSummaryDto
        {
            SalesUserId = salesUserId.ToString("D"),
            SalesUserName = userName,
            PackageCount = packages.Count,
            TotalPackageCost = totalPackageCost,
            TotalReceived = totalReceived,
            BalanceAmount = Math.Max(0, totalPackageCost - totalReceived)
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

        return ApiResponse<SalesConfirmedPackageDto>.Ok(
            (await ToDtosAsync([row], IsTenantAdmin(), ct)).Single());
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

        var (expectedProfit, actualProfit) = ResolveProfitFieldsForSave(dto, existing: null);
        var (tourPackageId, totalPackageCost) = await ResolvePackageCostForLeadAsync(
            dto.LeadId!.Value,
            dto.TourPackageId,
            dto.TotalPackageCost,
            ct);

        var now = DateTime.UtcNow;
        var entity = new SalesConfirmedPackage
        {
            TenantId = TenantId,
            ClientName = dto.ClientName.Trim(),
            ClientPhone = dto.ClientPhone.Trim(),
            ArrivalDate = dto.ArrivalDate,
            DepartureDate = dto.DepartureDate,
            ExpectedProfit = expectedProfit,
            ActualProfit = actualProfit,
            TotalPackageCost = totalPackageCost,
            TourPackageId = tourPackageId,
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
        return ApiResponse<SalesConfirmedPackageDto>.Ok(
            (await ToDtosAsync([entity], IsTenantAdmin(), ct)).Single());
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

        var (valid, error) = await ValidateDtoAsync(dto, ct, excludePackageId: id, existingPackageLeadId: entity.LeadId);
        if (!valid)
            return BadRequest(ApiResponse<SalesConfirmedPackageDto>.Fail(error!));

        var (expectedProfit, actualProfit) = ResolveProfitFieldsForSave(dto, existing: entity);

        entity.ClientName = dto.ClientName.Trim();
        entity.ClientPhone = dto.ClientPhone.Trim();
        entity.ArrivalDate = dto.ArrivalDate;
        entity.DepartureDate = dto.DepartureDate;
        entity.ExpectedProfit = expectedProfit;
        entity.ActualProfit = actualProfit;
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
        ApplyCompletionFieldsForSave(entity, dto);
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await LoadNavigationsAsync(entity, ct);
        return ApiResponse<SalesConfirmedPackageDto>.Ok(
            (await ToDtosAsync([entity], IsTenantAdmin(), ct)).Single());
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
        Guid? excludePackageId = null,
        Guid? existingPackageLeadId = null)
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

        if (!IsTenantAdmin() && dto.ActualProfit is not null)
            return (false, "Only an administrator can set actual profit.");

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

        var leadUnchangedOnUpdate = excludePackageId.HasValue
            && existingPackageLeadId.HasValue
            && existingPackageLeadId.Value == dto.LeadId.Value;

        if (!leadUnchangedOnUpdate)
        {
            var duplicateLead = await _db.SalesConfirmedPackages.AsNoTracking()
                .AnyAsync(p => p.TenantId == TenantId
                               && p.LeadId == dto.LeadId.Value
                               && (!excludePackageId.HasValue || p.Id != excludePackageId.Value), ct);
            if (duplicateLead)
                return (false, "This lead already has a confirmed package.");
        }

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

        if (IsTenantAdmin() && dto.IsCompleted == true && string.IsNullOrWhiteSpace(dto.FinalReview))
            return (false, "Final review is required when marking a package as completed.");

        return (true, null);
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task LoadNavigationsAsync(SalesConfirmedPackage entity, CancellationToken ct)
    {
        await _db.Entry(entity).Reference(p => p.Lead).LoadAsync(ct);
        await _db.Entry(entity).Reference(p => p.RecordedBy).LoadAsync(ct);
    }

    private (decimal ExpectedProfit, decimal? ActualProfit) ResolveProfitFieldsForSave(
        SaveSalesConfirmedPackageDto dto,
        SalesConfirmedPackage? existing)
    {
        if (!IsTenantAdmin())
        {
            if (existing is null)
                return (dto.ExpectedProfit, null);

            return (existing.ExpectedProfit, existing.ActualProfit);
        }

        if (existing is not null)
        {
            var departureOnOrBeforeToday = dto.DepartureDate <= EmployeeListWindowEnd();
            return departureOnOrBeforeToday
                ? (existing.ExpectedProfit, dto.ActualProfit)
                : (dto.ExpectedProfit, dto.ActualProfit);
        }

        // Admin can complete a package any time, even before departure.
        // In that case, persist the supplied actual profit immediately.
        if (dto.IsCompleted == true)
            return (dto.ExpectedProfit, dto.ActualProfit);

        var departurePassed = dto.DepartureDate <= EmployeeListWindowEnd();
        return departurePassed
            ? (dto.ExpectedProfit, dto.ActualProfit)
            : (dto.ExpectedProfit, null);
    }

    private void ApplyCompletionFieldsForSave(SalesConfirmedPackage entity, SaveSalesConfirmedPackageDto dto)
    {
        if (!IsTenantAdmin() || !dto.IsCompleted.HasValue)
            return;

        if (dto.IsCompleted.Value)
        {
            entity.IsCompleted = true;
            entity.FinalReview = TrimOrNull(dto.FinalReview);
            entity.CompletedAt = DateTime.UtcNow;
            entity.CompletedByUserId = GetCurrentUserId();
            return;
        }

        entity.IsCompleted = false;
        entity.FinalReview = null;
        entity.CompletedAt = null;
        entity.CompletedByUserId = null;
    }

    private async Task<Dictionary<Guid, decimal>> GetReceivedTotalsByLeadIdsAsync(
        IReadOnlyList<Guid> leadIds,
        CancellationToken ct)
    {
        if (leadIds.Count == 0) return new Dictionary<Guid, decimal>();

        return await _db.Payments.AsNoTracking()
            .Where(p => p.TenantId == TenantId
                        && p.PaymentType == PaymentType.Received
                        && p.LeadId != null
                        && leadIds.Contains(p.LeadId.Value))
            .GroupBy(p => p.LeadId!.Value)
            .Select(g => new { LeadId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.LeadId, x => x.Total, ct);
    }

    private async Task<List<decimal>> ResolvePackageCostsAsync(
        IReadOnlyList<(Guid? LeadId, decimal StoredCost)> packages,
        CancellationToken ct)
    {
        var leadIdsNeedingLookup = packages
            .Where(p => p.StoredCost <= 0 && p.LeadId.HasValue)
            .Select(p => p.LeadId!.Value)
            .Distinct()
            .ToList();

        var fallbackByLead = new Dictionary<Guid, decimal>();
        if (leadIdsNeedingLookup.Count > 0)
        {
            var tourPackages = await _db.Packages.AsNoTracking()
                .Where(p => p.TenantId == TenantId && p.LeadId != null && leadIdsNeedingLookup.Contains(p.LeadId.Value))
                .OrderByDescending(p => p.Status == PackageStatus.Confirmed)
                .ThenByDescending(p => p.StartDate)
                .Select(p => new { p.LeadId, p.TotalAmount, p.Discount })
                .ToListAsync(ct);

            foreach (var group in tourPackages.GroupBy(p => p.LeadId!.Value))
            {
                var best = group.First();
                fallbackByLead[group.Key] = Math.Max(0, best.TotalAmount - best.Discount);
            }
        }

        return packages.Select(p =>
        {
            if (p.StoredCost > 0) return p.StoredCost;
            if (p.LeadId.HasValue && fallbackByLead.TryGetValue(p.LeadId.Value, out var fallback))
                return fallback;
            return 0m;
        }).ToList();
    }

    private async Task<(Guid? TourPackageId, decimal TotalPackageCost)> ResolvePackageCostForLeadAsync(
        Guid leadId,
        Guid? requestedTourPackageId,
        decimal requestedTotalPackageCost,
        CancellationToken ct)
    {
        if (requestedTourPackageId.HasValue && requestedTotalPackageCost > 0)
            return (requestedTourPackageId, requestedTotalPackageCost);

        if (requestedTotalPackageCost > 0)
            return (requestedTourPackageId, requestedTotalPackageCost);

        var tourPackage = await _db.Packages.AsNoTracking()
            .Where(p => p.TenantId == TenantId && p.LeadId == leadId)
            .OrderByDescending(p => p.Status == PackageStatus.Confirmed)
            .ThenByDescending(p => p.StartDate)
            .Select(p => new { p.Id, p.TotalAmount, p.Discount })
            .FirstOrDefaultAsync(ct);

        if (tourPackage is null)
            return (requestedTourPackageId, Math.Max(0, requestedTotalPackageCost));

        return (tourPackage.Id, Math.Max(0, tourPackage.TotalAmount - tourPackage.Discount));
    }

    private async Task<IReadOnlyList<SalesConfirmedPackageDto>> ToDtosAsync(
        IReadOnlyList<SalesConfirmedPackage> rows,
        bool includeProfitDetails,
        CancellationToken ct)
    {
        if (rows.Count == 0) return [];

        var leadIds = rows.Where(r => r.LeadId.HasValue).Select(r => r.LeadId!.Value).Distinct().ToList();
        var receivedByLead = await GetReceivedTotalsByLeadIdsAsync(leadIds, ct);
        var resolvedCosts = await ResolvePackageCostsAsync(
            rows.Select(r => (r.LeadId, r.TotalPackageCost)).ToList(),
            ct);

        var dtos = new List<SalesConfirmedPackageDto>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var p = rows[i];
            var packageCost = resolvedCosts[i];
            var totalReceived = p.LeadId.HasValue && receivedByLead.TryGetValue(p.LeadId.Value, out var received)
                ? received
                : 0m;
            var balance = Math.Max(0, packageCost - totalReceived);

            var recordedBy = p.RecordedBy;
            var recordedByName = recordedBy == null
                ? null
                : $"{recordedBy.FirstName} {recordedBy.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(recordedByName))
                recordedByName = recordedBy?.Email;

            dtos.Add(new SalesConfirmedPackageDto
            {
                Id = p.Id.ToString(),
                ClientName = p.ClientName,
                ClientPhone = p.ClientPhone,
                ArrivalDate = p.ArrivalDate.ToString("yyyy-MM-dd"),
                DepartureDate = p.DepartureDate.ToString("yyyy-MM-dd"),
                ExpectedProfit = includeProfitDetails ? p.ExpectedProfit : 0m,
                ActualProfit = includeProfitDetails ? p.ActualProfit : null,
                TotalPackageCost = packageCost,
                TotalReceived = totalReceived,
                BalanceAmount = balance,
                IsCompleted = p.IsCompleted,
                FinalReview = p.FinalReview,
                CompletedAt = p.CompletedAt?.ToString("o"),
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
            });
        }

        return dtos;
    }
}
