using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;
using TravelPathways.Api.Storage;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/drivers")]
public sealed class DriversController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly FileStorage _storage;

    public DriversController(AppDbContext db, TenantContext tenant, FileStorage storage) : base(tenant)
    {
        _db = db;
        _storage = storage;
    }

    public sealed class DriverLookupDto
    {
        public required string Id { get; init; }
        public required string FullName { get; init; }
        public required string PhoneNumber { get; init; }
        public string? TransportCompanyId { get; init; }
        public string? TransportCompanyName { get; init; }
        public required bool IsActive { get; init; }
    }

    public class DriverListItemDto
    {
        public required string Id { get; init; }
        public required string FullName { get; init; }
        public required string PhoneNumber { get; init; }
        public string? Email { get; init; }
        public string? TransportCompanyId { get; init; }
        public string? TransportCompanyName { get; init; }
        public string? LicenceNumber { get; init; }
        public string? AadharLastFour { get; init; }
        public string? VehicleNumber { get; init; }
        public string? VehicleModel { get; init; }
        public required bool IsActive { get; init; }
        public required DateTime CreatedAt { get; init; }
        public int TourCount { get; init; }
        public double? AverageRating { get; init; }
    }

    public sealed class DriverDetailDto : DriverListItemDto
    {
        public string? LicenceDocumentUrl { get; init; }
        public string? AadharDocumentUrl { get; init; }
        public string? VehicleImageUrl { get; init; }
        public string? Notes { get; init; }
        public required List<DriverAssignmentHistoryItemDto> AssignmentHistory { get; init; }
    }

    public sealed class DriverAssignmentHistoryItemDto
    {
        public required string Id { get; init; }
        public required string ReservationId { get; init; }
        public required string PackageId { get; init; }
        public required string PackageName { get; init; }
        public required string ClientName { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
        public string? VehicleNumber { get; init; }
        public int? ServiceRating { get; init; }
        public string? ServiceNotes { get; init; }
        public required DateTime AssignedAt { get; init; }
    }

    public class SaveDriverRequestDto
    {
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? TransportCompanyId { get; set; }
        public string? LicenceNumber { get; set; }
        public string? AadharLastFour { get; set; }
        public string? Notes { get; set; }
        public string? VehicleNumber { get; set; }
        public string? VehicleModel { get; set; }
        public bool? IsActive { get; set; }
    }

    public sealed class SaveDriverFormDto : SaveDriverRequestDto
    {
        public IFormFile? LicenceDocument { get; set; }
        public IFormFile? AadharDocument { get; set; }
        public IFormFile? VehicleImage { get; set; }
    }

    public sealed class TourDriverAssignmentListItemDto
    {
        public required string Id { get; init; }
        public required string ReservationId { get; init; }
        public required string PackageId { get; init; }
        public required string PackageName { get; init; }
        public required string ClientName { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
        public required string DriverId { get; init; }
        public required string DriverName { get; init; }
        public required string DriverPhone { get; init; }
        public required string VehicleNumber { get; init; }
        public string? VehicleModel { get; init; }
        public string? TransportCompanyName { get; init; }
        public int? ServiceRating { get; init; }
        public required DateTime AssignedAt { get; init; }
    }

    /// <summary>All tour–driver assignments (which driver was on which package).</summary>
    [HttpGet("assignments")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<TourDriverAssignmentListItemDto>>>> GetTourAssignments(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? driverId = null,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.PackageDriverAssignments.AsNoTracking()
            .Include(a => a.Driver)
            .Include(a => a.Package)
            .Include(a => a.TransportCompany)
            .Where(a => a.TenantId == TenantId);

        if (driverId.HasValue)
            query = query.Where(a => a.DriverId == driverId.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = PostgresSearch.ToContainsPattern(searchTerm);
            query = query.Where(a =>
                EF.Functions.ILike(a.Driver.FullName, pattern, "\\") ||
                EF.Functions.ILike(a.Package.PackageName, pattern, "\\") ||
                EF.Functions.ILike(a.Package.ClientName, pattern, "\\") ||
                EF.Functions.ILike(a.VehicleNumber, pattern, "\\"));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.Package.StartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new TourDriverAssignmentListItemDto
            {
                Id = a.Id.ToString("D"),
                ReservationId = a.ReservationId.ToString("D"),
                PackageId = a.PackageId.ToString("D"),
                PackageName = a.Package.PackageName,
                ClientName = a.Package.ClientName,
                StartDate = a.Package.StartDate,
                EndDate = a.Package.EndDate,
                DriverId = a.DriverId.ToString("D"),
                DriverName = a.Driver.FullName,
                DriverPhone = a.Driver.PhoneNumber,
                VehicleNumber = a.VehicleNumber,
                VehicleModel = a.VehicleModel,
                TransportCompanyName = a.TransportCompany != null ? a.TransportCompany.Name : null,
                ServiceRating = a.ServiceRating,
                AssignedAt = a.CreatedAt
            })
            .ToListAsync(ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return ApiResponse<PaginatedResponse<TourDriverAssignmentListItemDto>>.Ok(new PaginatedResponse<TourDriverAssignmentListItemDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<DriverListItemDto>>>> GetDrivers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? transportCompanyId = null,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Drivers.AsNoTracking()
            .Include(d => d.TransportCompany)
            .Where(d => d.TenantId == TenantId);

        if (transportCompanyId.HasValue)
            query = query.Where(d => d.TransportCompanyId == transportCompanyId.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = PostgresSearch.ToContainsPattern(searchTerm);
            query = query.Where(d =>
                EF.Functions.ILike(d.FullName, pattern, "\\") ||
                EF.Functions.ILike(d.PhoneNumber, pattern, "\\") ||
                (d.LicenceNumber != null && EF.Functions.ILike(d.LicenceNumber, pattern, "\\")));
        }

        var total = await query.CountAsync(ct);
        var drivers = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var driverIds = drivers.Select(d => d.Id).ToList();
        var stats = await _db.PackageDriverAssignments.AsNoTracking()
            .Where(a => driverIds.Contains(a.DriverId))
            .GroupBy(a => a.DriverId)
            .Select(g => new
            {
                DriverId = g.Key,
                TourCount = g.Count(),
                AverageRating = g.Where(a => a.ServiceRating != null).Average(a => (double?)a.ServiceRating)
            })
            .ToListAsync(ct);
        var statsByDriver = stats.ToDictionary(s => s.DriverId);

        var items = drivers.Select(d =>
        {
            statsByDriver.TryGetValue(d.Id, out var stat);
            return ToListItemDto(d, stat?.TourCount ?? 0, stat?.AverageRating);
        }).ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return ApiResponse<PaginatedResponse<DriverListItemDto>>.Ok(new PaginatedResponse<DriverListItemDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<DriverLookupDto>>>> GetDriverLookup(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 200,
        [FromQuery] string? searchTerm = null,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _db.Drivers.AsNoTracking()
            .Include(d => d.TransportCompany)
            .Where(d => d.TenantId == TenantId && d.IsActive);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = PostgresSearch.ToContainsPattern(searchTerm);
            query = query.Where(d =>
                EF.Functions.ILike(d.FullName, pattern, "\\") ||
                EF.Functions.ILike(d.PhoneNumber, pattern, "\\"));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(d => d.FullName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DriverLookupDto
            {
                Id = d.Id.ToString("D"),
                FullName = d.FullName,
                PhoneNumber = d.PhoneNumber,
                TransportCompanyId = d.TransportCompanyId != null ? d.TransportCompanyId.Value.ToString("D") : null,
                TransportCompanyName = d.TransportCompany != null ? d.TransportCompany.Name : null,
                IsActive = d.IsActive
            })
            .ToListAsync(ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return ApiResponse<PaginatedResponse<DriverLookupDto>>.Ok(new PaginatedResponse<DriverLookupDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DriverDetailDto>>> GetDriverById(Guid id, CancellationToken ct = default)
    {
        var driver = await _db.Drivers.AsNoTracking()
            .Include(d => d.TransportCompany)
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == TenantId, ct);
        if (driver is null)
            return NotFound(ApiResponse<DriverDetailDto>.Fail("Driver not found."));

        var stats = await _db.PackageDriverAssignments.AsNoTracking()
            .Where(a => a.DriverId == id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TourCount = g.Count(),
                AverageRating = g.Where(a => a.ServiceRating != null).Average(a => (double?)a.ServiceRating)
            })
            .FirstOrDefaultAsync(ct);

        var history = await _db.PackageDriverAssignments.AsNoTracking()
            .Include(a => a.Package)
            .Where(a => a.DriverId == id && a.TenantId == TenantId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .Select(a => new DriverAssignmentHistoryItemDto
            {
                Id = a.Id.ToString("D"),
                ReservationId = a.ReservationId.ToString("D"),
                PackageId = a.PackageId.ToString("D"),
                PackageName = a.Package.PackageName,
                ClientName = a.Package.ClientName,
                StartDate = a.Package.StartDate,
                EndDate = a.Package.EndDate,
                VehicleNumber = a.VehicleNumber,
                ServiceRating = a.ServiceRating,
                ServiceNotes = a.ServiceNotes,
                AssignedAt = a.CreatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<DriverDetailDto>.Ok(ToDetailDto(driver, stats?.TourCount ?? 0, stats?.AverageRating, history));
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<ActionResult<ApiResponse<DriverDetailDto>>> CreateDriverJson(
        [FromBody] SaveDriverRequestDto request,
        CancellationToken ct = default)
    {
        var validation = ValidateDriverRequest(request);
        if (validation != null) return validation;

        var driver = await CreateDriverEntityAsync(request, ct);
        _db.Drivers.Add(driver);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetDriverById), new { id = driver.Id },
            ApiResponse<DriverDetailDto>.Ok(ToDetailDto(driver, 0, null, [])));
    }

    [HttpPost]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    public async Task<ActionResult<ApiResponse<DriverDetailDto>>> CreateDriverForm(
        [FromForm] SaveDriverFormDto request,
        CancellationToken ct = default)
    {
        var validation = ValidateDriverRequest(request);
        if (validation != null) return validation;

        var driver = await CreateDriverEntityAsync(request, ct);
        _db.Drivers.Add(driver);
        await _db.SaveChangesAsync(ct);
        await SaveDriverDocsAsync(driver, request, ct);

        return CreatedAtAction(nameof(GetDriverById), new { id = driver.Id },
            ApiResponse<DriverDetailDto>.Ok(ToDetailDto(driver, 0, null, [])));
    }

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    public async Task<ActionResult<ApiResponse<DriverDetailDto>>> UpdateDriverJson(
        Guid id,
        [FromBody] SaveDriverRequestDto request,
        CancellationToken ct = default)
    {
        var validation = ValidateDriverRequest(request);
        if (validation != null) return validation;

        var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == TenantId, ct);
        if (driver is null)
            return NotFound(ApiResponse<DriverDetailDto>.Fail("Driver not found."));

        ApplyDriverRequest(driver, request);
        await _db.SaveChangesAsync(ct);
        return await GetDriverById(id, ct);
    }

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    public async Task<ActionResult<ApiResponse<DriverDetailDto>>> UpdateDriverForm(
        Guid id,
        [FromForm] SaveDriverFormDto request,
        CancellationToken ct = default)
    {
        var validation = ValidateDriverRequest(request);
        if (validation != null) return validation;

        var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == TenantId, ct);
        if (driver is null)
            return NotFound(ApiResponse<DriverDetailDto>.Fail("Driver not found."));

        ApplyDriverRequest(driver, request);
        await SaveDriverDocsAsync(driver, request, ct);
        await _db.SaveChangesAsync(ct);
        return await GetDriverById(id, ct);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDriver(Guid id, CancellationToken ct = default)
    {
        var driver = await _db.Drivers.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == TenantId, ct);
        if (driver is null)
            return NotFound(ApiResponse<object>.Fail("Driver not found."));

        var hasAssignments = await _db.PackageDriverAssignments.AnyAsync(a => a.DriverId == id, ct);
        if (hasAssignments)
            return BadRequest(ApiResponse<object>.Fail("Cannot delete a driver who has tour assignments. Deactivate instead."));

        driver.IsDeleted = true;
        driver.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    private ActionResult<ApiResponse<DriverDetailDto>>? ValidateDriverRequest(SaveDriverRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(ApiResponse<DriverDetailDto>.Fail("Driver name is required."));
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(ApiResponse<DriverDetailDto>.Fail("Phone number is required."));
        return null;
    }

    private async Task<Driver> CreateDriverEntityAsync(SaveDriverRequestDto request, CancellationToken ct)
    {
        Guid? transportCompanyId = null;
        if (!string.IsNullOrWhiteSpace(request.TransportCompanyId) &&
            Guid.TryParse(request.TransportCompanyId, out var tcId))
        {
            var exists = await _db.TransportCompanies.AnyAsync(c => c.Id == tcId && c.TenantId == TenantId, ct);
            if (exists) transportCompanyId = tcId;
        }

        return new Driver
        {
            TenantId = TenantId,
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = request.Email?.Trim(),
            TransportCompanyId = transportCompanyId,
            LicenceNumber = request.LicenceNumber?.Trim(),
            AadharLastFour = request.AadharLastFour?.Trim(),
            Notes = request.Notes?.Trim(),
            VehicleNumber = request.VehicleNumber?.Trim(),
            VehicleModel = request.VehicleModel?.Trim(),
            IsActive = request.IsActive ?? true
        };
    }

    private static void ApplyDriverRequest(Driver driver, SaveDriverRequestDto request)
    {
        driver.FullName = request.FullName.Trim();
        driver.PhoneNumber = request.PhoneNumber.Trim();
        driver.Email = request.Email?.Trim();
        driver.LicenceNumber = request.LicenceNumber?.Trim();
        driver.AadharLastFour = request.AadharLastFour?.Trim();
        driver.Notes = request.Notes?.Trim();
        driver.VehicleNumber = request.VehicleNumber?.Trim();
        driver.VehicleModel = request.VehicleModel?.Trim();
        if (request.IsActive.HasValue) driver.IsActive = request.IsActive.Value;
        if (!string.IsNullOrWhiteSpace(request.TransportCompanyId) &&
            Guid.TryParse(request.TransportCompanyId, out var tcId))
            driver.TransportCompanyId = tcId;
        else if (string.IsNullOrWhiteSpace(request.TransportCompanyId))
            driver.TransportCompanyId = null;
    }

    private async Task SaveDriverDocsAsync(Driver driver, SaveDriverFormDto request, CancellationToken ct)
    {
        if (request.LicenceDocument is not null)
            driver.LicenceDocumentUrl = await _storage.SaveDriverFileAsync(TenantId, driver.Id, "licence", request.LicenceDocument, ct);
        if (request.AadharDocument is not null)
            driver.AadharDocumentUrl = await _storage.SaveDriverFileAsync(TenantId, driver.Id, "aadhar", request.AadharDocument, ct);
        if (request.VehicleImage is not null)
            driver.VehicleImageUrl = await _storage.SaveDriverFileAsync(TenantId, driver.Id, "vehicle", request.VehicleImage, ct);
    }

    private static DriverListItemDto ToListItemDto(Driver d, int tourCount, double? avgRating) => new()
    {
        Id = d.Id.ToString("D"),
        FullName = d.FullName,
        PhoneNumber = d.PhoneNumber,
        Email = d.Email,
        TransportCompanyId = d.TransportCompanyId?.ToString("D"),
        TransportCompanyName = d.TransportCompany?.Name,
        LicenceNumber = d.LicenceNumber,
        AadharLastFour = d.AadharLastFour,
        VehicleNumber = d.VehicleNumber,
        VehicleModel = d.VehicleModel,
        IsActive = d.IsActive,
        CreatedAt = d.CreatedAt,
        TourCount = tourCount,
        AverageRating = avgRating.HasValue ? Math.Round(avgRating.Value, 1) : null
    };

    private static DriverDetailDto ToDetailDto(
        Driver d,
        int tourCount,
        double? avgRating,
        List<DriverAssignmentHistoryItemDto> history) => new()
    {
        Id = d.Id.ToString("D"),
        FullName = d.FullName,
        PhoneNumber = d.PhoneNumber,
        Email = d.Email,
        TransportCompanyId = d.TransportCompanyId?.ToString("D"),
        TransportCompanyName = d.TransportCompany?.Name,
        LicenceNumber = d.LicenceNumber,
        AadharLastFour = d.AadharLastFour,
        LicenceDocumentUrl = d.LicenceDocumentUrl,
        AadharDocumentUrl = d.AadharDocumentUrl,
        VehicleImageUrl = d.VehicleImageUrl,
        Notes = d.Notes,
        IsActive = d.IsActive,
        CreatedAt = d.CreatedAt,
        TourCount = tourCount,
        AverageRating = avgRating.HasValue ? Math.Round(avgRating.Value, 1) : null,
        AssignmentHistory = history
    };
}
