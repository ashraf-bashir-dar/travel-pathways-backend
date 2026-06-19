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
[Route("api/driver-assignments")]
public sealed class DriverAssignmentsController : TenantControllerBase
{
    private readonly AppDbContext _db;

    public DriverAssignmentsController(AppDbContext db, TenantContext tenant) : base(tenant)
    {
        _db = db;
    }

    public sealed class PackageDriverAssignmentDto
    {
        public required string Id { get; init; }
        public required string PackageId { get; init; }
        public required string ReservationId { get; init; }
        public required string PackageName { get; init; }
        public required string ClientName { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
        public required string DriverId { get; init; }
        public required string DriverName { get; init; }
        public required string DriverPhone { get; init; }
        public string? DriverLicenceDocumentUrl { get; init; }
        public string? DriverAadharDocumentUrl { get; init; }
        public string? TransportCompanyId { get; init; }
        public string? TransportCompanyName { get; init; }
        public required string VehicleNumber { get; init; }
        public string? VehicleModel { get; init; }
        public string? VehicleImageUrl { get; init; }
        public int? ServiceRating { get; init; }
        public string? ServiceNotes { get; init; }
        public string? AssignedByUserName { get; init; }
        public required DateTime CreatedAt { get; init; }
        public DateTime? RatedAtUtc { get; init; }
        public bool ReservationIsLocked { get; init; }
    }

    public sealed class TourForDriverAssignmentDto
    {
        public required string ReservationId { get; init; }
        public required string PackageId { get; init; }
        public required string PackageName { get; init; }
        public required string ClientName { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
        public required string ReservationStatus { get; init; }
        public bool IsLocked { get; init; }
        public bool HasDriverAssignment { get; init; }
        public string? DriverName { get; init; }
        public string? DriverPhone { get; init; }
        public string? VehicleNumber { get; init; }
        public int? ServiceRating { get; init; }
    }

    public class SaveDriverAssignmentRequestDto
    {
        public string? DriverId { get; set; }
    }

    public sealed class RateDriverAssignmentRequestDto
    {
        public int ServiceRating { get; set; }
        public string? ServiceNotes { get; set; }
    }

    private async Task<ActionResult?> EnsureTransportModuleAsync(CancellationToken ct)
    {
        if (!HasTenantId)
            return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));
        if (!await TenantModuleResolver.HasModuleAsync(_db, User, TenantId, AppModuleKey.Transport, ct))
            return StatusCode(403, ApiResponse<object>.Fail("Transport module is not enabled for your account."));
        return null;
    }

    private Guid? CurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var uid) ? uid : null;
    }

    /// <summary>Tours (reservations) available for driver assignment — separate from hotel reservation workflow.</summary>
    [HttpGet("tours")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<TourForDriverAssignmentDto>>>> GetToursForAssignment(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? assigned = null,
        CancellationToken ct = default)
    {
        var check = await EnsureTransportModuleAsync(ct);
        if (check != null) return check;

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Reservations.AsNoTracking()
            .Include(r => r.Package)
            .Where(r => r.TenantId == TenantId && r.Status == ReservationStatus.Completed);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = PostgresSearch.ToContainsPattern(searchTerm);
            query = query.Where(r =>
                EF.Functions.ILike(r.Package.PackageName, pattern, "\\") ||
                EF.Functions.ILike(r.Package.ClientName, pattern, "\\"));
        }

        if (assigned == true)
            query = query.Where(r => _db.PackageDriverAssignments.Any(a => a.ReservationId == r.Id));
        else if (assigned == false)
            query = query.Where(r => !_db.PackageDriverAssignments.Any(a => a.ReservationId == r.Id));

        var total = await query.CountAsync(ct);
        var reservations = await query
            .OrderByDescending(r => r.Package.StartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.PackageId,
                r.Package.PackageName,
                r.Package.ClientName,
                r.Package.StartDate,
                r.Package.EndDate,
                r.Status,
                r.IsLocked
            })
            .ToListAsync(ct);

        var reservationIds = reservations.Select(r => r.Id).ToList();
        var assignments = await _db.PackageDriverAssignments.AsNoTracking()
            .Include(a => a.Driver)
            .Where(a => reservationIds.Contains(a.ReservationId))
            .ToDictionaryAsync(a => a.ReservationId, ct);

        var items = reservations.Select(r =>
        {
            assignments.TryGetValue(r.Id, out var a);
            return new TourForDriverAssignmentDto
            {
                ReservationId = r.Id.ToString("D"),
                PackageId = r.PackageId.ToString("D"),
                PackageName = r.PackageName,
                ClientName = r.ClientName,
                StartDate = r.StartDate,
                EndDate = r.EndDate,
                ReservationStatus = r.Status.ToString(),
                IsLocked = r.IsLocked,
                HasDriverAssignment = a != null,
                DriverName = a?.Driver.FullName,
                DriverPhone = a?.Driver.PhoneNumber,
                VehicleNumber = a?.VehicleNumber,
                ServiceRating = a?.ServiceRating
            };
        }).ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return ApiResponse<PaginatedResponse<TourForDriverAssignmentDto>>.Ok(new PaginatedResponse<TourForDriverAssignmentDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    [HttpGet("by-reservation/{reservationId:guid}")]
    public async Task<ActionResult<ApiResponse<PackageDriverAssignmentDto?>>> GetByReservation(
        Guid reservationId,
        CancellationToken ct = default)
    {
        var check = await EnsureTransportModuleAsync(ct);
        if (check != null) return check;

        var reservation = await LoadReservationWithPackageAsync(reservationId, ct);
        if (reservation is null)
            return NotFound(ApiResponse<PackageDriverAssignmentDto?>.Fail("Tour not found."));
        if (reservation.Status != ReservationStatus.Completed)
            return BadRequest(ApiResponse<PackageDriverAssignmentDto?>.Fail(
                "This package is not ready for transport yet. The reservation manager must mark the hotel reservation as completed first."));

        var assignment = await _db.PackageDriverAssignments.AsNoTracking()
            .Include(a => a.Driver)
            .Include(a => a.TransportCompany)
            .Include(a => a.AssignedByUser)
            .Include(a => a.Package)
            .FirstOrDefaultAsync(a => a.ReservationId == reservationId && a.TenantId == TenantId, ct);

        if (assignment is null)
            return ApiResponse<PackageDriverAssignmentDto?>.Ok(null);

        return ApiResponse<PackageDriverAssignmentDto?>.Ok(ToDto(assignment, reservation.IsLocked));
    }

    [HttpPut("by-reservation/{reservationId:guid}")]
    public async Task<ActionResult<ApiResponse<PackageDriverAssignmentDto>>> Save(
        Guid reservationId,
        [FromBody] SaveDriverAssignmentRequestDto request,
        CancellationToken ct = default) =>
        await SaveInternal(reservationId, request, ct);

    [HttpPatch("by-reservation/{reservationId:guid}/rating")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<PackageDriverAssignmentDto>>> RateAssignment(
        Guid reservationId,
        [FromBody] RateDriverAssignmentRequestDto request,
        CancellationToken ct = default)
    {
        var check = await EnsureTransportModuleAsync(ct);
        if (check != null) return check;

        if (request.ServiceRating is < 1 or > 5)
            return BadRequest(ApiResponse<PackageDriverAssignmentDto>.Fail("Service rating must be between 1 and 5."));

        var reservation = await LoadReservationWithPackageAsync(reservationId, ct);
        if (reservation is null)
            return NotFound(ApiResponse<PackageDriverAssignmentDto>.Fail("Tour not found."));

        var assignment = await _db.PackageDriverAssignments
            .Include(a => a.Driver)
            .Include(a => a.TransportCompany)
            .Include(a => a.AssignedByUser)
            .Include(a => a.Package)
            .FirstOrDefaultAsync(a => a.ReservationId == reservationId && a.TenantId == TenantId, ct);
        if (assignment is null)
            return NotFound(ApiResponse<PackageDriverAssignmentDto>.Fail("No driver assignment found for this tour."));

        assignment.ServiceRating = request.ServiceRating;
        assignment.ServiceNotes = request.ServiceNotes?.Trim();
        assignment.RatedAtUtc = DateTime.UtcNow;
        assignment.RatedByUserId = CurrentUserId();
        await _db.SaveChangesAsync(ct);

        return ApiResponse<PackageDriverAssignmentDto>.Ok(ToDto(assignment, reservation.IsLocked));
    }

    private async Task<ActionResult<ApiResponse<PackageDriverAssignmentDto>>> SaveInternal(
        Guid reservationId,
        SaveDriverAssignmentRequestDto request,
        CancellationToken ct)
    {
        var check = await EnsureTransportModuleAsync(ct);
        if (check != null) return check;

        var reservation = await _db.Reservations
            .Include(r => r.Package)
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.TenantId == TenantId, ct);
        if (reservation is null)
            return NotFound(ApiResponse<PackageDriverAssignmentDto>.Fail("Tour not found."));
        if (reservation.Status != ReservationStatus.Completed)
            return BadRequest(ApiResponse<PackageDriverAssignmentDto>.Fail(
                "Only packages with a completed hotel reservation can be assigned a driver."));

        var driverResult = await ResolveDriverAsync(request, ct);
        if (driverResult.Error != null) return driverResult.Error;
        var driver = driverResult.Driver!;

        var assignment = await _db.PackageDriverAssignments
            .Include(a => a.Driver)
            .Include(a => a.TransportCompany)
            .Include(a => a.AssignedByUser)
            .Include(a => a.Package)
            .FirstOrDefaultAsync(a => a.ReservationId == reservationId, ct);

        var userId = CurrentUserId();
        if (assignment is null)
        {
            assignment = new PackageDriverAssignment
            {
                TenantId = TenantId,
                PackageId = reservation.PackageId,
                ReservationId = reservationId,
                DriverId = driver.Id,
                AssignedByUserId = userId
            };
            _db.PackageDriverAssignments.Add(assignment);
        }
        else
        {
            assignment.DriverId = driver.Id;
            assignment.AssignedByUserId ??= userId;
        }

        assignment.TransportCompanyId = driver.TransportCompanyId;
        assignment.VehicleNumber = driver.VehicleNumber?.Trim() ?? string.Empty;
        assignment.VehicleModel = driver.VehicleModel?.Trim();
        assignment.VehicleImageUrl = driver.VehicleImageUrl;

        await _db.SaveChangesAsync(ct);

        await _db.Entry(assignment).Reference(a => a.Driver).LoadAsync(ct);
        await _db.Entry(assignment).Reference(a => a.TransportCompany).LoadAsync(ct);
        await _db.Entry(assignment).Reference(a => a.AssignedByUser).LoadAsync(ct);
        await _db.Entry(assignment).Reference(a => a.Package).LoadAsync(ct);

        return ApiResponse<PackageDriverAssignmentDto>.Ok(ToDto(assignment, reservation.IsLocked));
    }

    private async Task<Reservation?> LoadReservationWithPackageAsync(Guid reservationId, CancellationToken ct) =>
        await _db.Reservations.AsNoTracking()
            .Include(r => r.Package)
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.TenantId == TenantId, ct);

    private async Task<(Driver? Driver, ActionResult<ApiResponse<PackageDriverAssignmentDto>>? Error)> ResolveDriverAsync(
        SaveDriverAssignmentRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DriverId) || !Guid.TryParse(request.DriverId, out var driverId))
            return (null, BadRequest(ApiResponse<PackageDriverAssignmentDto>.Fail("Select a driver from the list. Add new drivers under Transport → Drivers first.")));

        var existing = await _db.Drivers.FirstOrDefaultAsync(d => d.Id == driverId && d.TenantId == TenantId, ct);
        if (existing is null)
            return (null, NotFound(ApiResponse<PackageDriverAssignmentDto>.Fail("Driver not found.")));

        return (existing, null);
    }

    private static PackageDriverAssignmentDto ToDto(PackageDriverAssignment a, bool reservationIsLocked) => new()
    {
        Id = a.Id.ToString("D"),
        PackageId = a.PackageId.ToString("D"),
        ReservationId = a.ReservationId.ToString("D"),
        PackageName = a.Package.PackageName,
        ClientName = a.Package.ClientName,
        StartDate = a.Package.StartDate,
        EndDate = a.Package.EndDate,
        DriverId = a.DriverId.ToString("D"),
        DriverName = a.Driver.FullName,
        DriverPhone = a.Driver.PhoneNumber,
        DriverLicenceDocumentUrl = a.Driver.LicenceDocumentUrl,
        DriverAadharDocumentUrl = a.Driver.AadharDocumentUrl,
        TransportCompanyId = a.TransportCompanyId?.ToString("D"),
        TransportCompanyName = a.TransportCompany?.Name,
        VehicleNumber = a.VehicleNumber,
        VehicleModel = a.VehicleModel,
        VehicleImageUrl = a.VehicleImageUrl,
        ServiceRating = a.ServiceRating,
        ServiceNotes = a.ServiceNotes,
        AssignedByUserName = a.AssignedByUser != null
            ? (a.AssignedByUser.FirstName + " " + a.AssignedByUser.LastName).Trim()
            : null,
        CreatedAt = a.CreatedAt,
        RatedAtUtc = a.RatedAtUtc,
        ReservationIsLocked = reservationIsLocked
    };
}
