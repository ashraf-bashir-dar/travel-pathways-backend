using System.Security.Claims;
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
[Route("api/reservations")]
public sealed class ReservationsController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly FileStorage _storage;

    public ReservationsController(AppDbContext db, TenantContext tenant, FileStorage storage) : base(tenant)
    {
        _db = db;
        _storage = storage;
    }

    private async Task<ActionResult?> EnsureReservationsModuleAsync(CancellationToken ct)
    {
        if (!HasTenantId)
            return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));
        var tenantId = TenantId;
        var tenant = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct);
        var enabled = tenant ?? new List<AppModuleKey>();
        if (!enabled.Contains(AppModuleKey.Reservations))
            return StatusCode(403, ApiResponse<object>.Fail("Reservations module is not enabled for this tenant."));
        return null;
    }

    /// <summary>Current user id, email (for CreatedBy), and role flags. Admin sees all; Reservation sees only assigned; Agent (Tour Manager) sees own packages.</summary>
    private (Guid? UserId, string? Email, bool IsAdmin, bool IsReservationRole) GetCurrentUserReservationScope()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        var isAdmin = string.Equals(role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
        var isReservation = string.Equals(role, UserRole.Reservation.ToString(), StringComparison.OrdinalIgnoreCase);
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = Guid.TryParse(userIdClaim, out var uid) ? uid : null;
        var email = User.FindFirstValue(ClaimTypes.Email)?.Trim();
        return (userId, email, isAdmin, isReservation);
    }

    public sealed class ReservationScreenshotDto
    {
        public required string Id { get; init; }
        public required string FileUrl { get; init; }
        public required string FileName { get; init; }
        public required DateTime CreatedAt { get; init; }
        /// <summary>Day number (1-based) this screenshot is for. Null = general.</summary>
        public int? DayNumber { get; init; }
    }

    public sealed class ReservationDayCompletionDto
    {
        public required int DayNumber { get; init; }
        public required bool IsDone { get; init; }
        public DateTime? DoneAt { get; init; }
    }

    /// <summary>Day-wise itinerary line for reservation detail (hotel/houseboat per day).</summary>
    public sealed class ReservationDayItineraryItemDto
    {
        public required int DayNumber { get; init; }
        public required DateTime Date { get; init; }
        public string? HotelId { get; init; }
        public string? HotelName { get; init; }
        public bool IsHouseboat { get; init; }
        public string? RoomType { get; init; }
        public int NumberOfRooms { get; init; }
        public string? CheckInTime { get; init; }
        public string? CheckOutTime { get; init; }
        public string? Notes { get; init; }
        public decimal HotelCost { get; init; }
    }

    /// <summary>Full package info for reservation person (hotels, houseboat, vehicle, inclusions, day-wise itinerary).</summary>
    public sealed class ReservationPackageDetailDto
    {
        public required string PackageName { get; init; }
        public required string ClientName { get; init; }
        public string? ClientPhone { get; init; }
        public string? ClientEmail { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
        public required int NumberOfDays { get; init; }
        public string? VehicleName { get; init; }
        public List<string>? InclusionIds { get; init; }
        public required List<ReservationDayItineraryItemDto> DayWiseItinerary { get; init; }
    }

    public class ReservationListItemDto
    {
        public required string Id { get; init; }
        public required string PackageId { get; init; }
        public required string PackageName { get; init; }
        public required string ClientName { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
        public decimal TotalAmount { get; init; }
        public decimal Discount { get; init; }
        public decimal FinalAmount { get; init; }
        public decimal AdvanceAmount { get; init; }
        public decimal BalanceAmount { get; init; }
        public required string Status { get; init; }
        public required string AssignedToUserId { get; init; }
        public required string AssignedToUserName { get; init; }
        public required DateTime CreatedAt { get; init; }
        public int ScreenshotCount { get; init; }
    }

    public sealed class ReservationDetailDto : ReservationListItemDto
    {
        public string? Notes { get; init; }
        public required List<ReservationScreenshotDto> PaymentScreenshots { get; init; }
        /// <summary>User who assigned this package (Tour Manager). "Package confirmed by" in UI.</summary>
        public string? AssignedByUserId { get; init; }
        public string? AssignedByUserName { get; init; }
        /// <summary>Final note from reservation manager when reservation is completed (optional).</summary>
        public string? FinalNotes { get; init; }
        public required List<ReservationDayCompletionDto> DayCompletions { get; init; }
        /// <summary>Full package info for reservation person: hotels, houseboat, day-wise itinerary.</summary>
        public ReservationPackageDetailDto? PackageDetail { get; init; }
    }

    public sealed class ConfirmedPackageForReservationDto
    {
        public required string Id { get; init; }
        public required string PackageName { get; init; }
        public required string ClientName { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
    }

    /// <summary>Tour Manager: confirmed packages they created, with reservation status/assignee. Used for \"My reservations\" table.</summary>
    public sealed class MyConfirmedPackageDto
    {
        public required string PackageId { get; init; }
        public required string PackageName { get; init; }
        public required string ClientName { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
        public string? ReservationId { get; init; }
        public string? ReservationStatus { get; init; }
        public string? AssignedToUserId { get; init; }
        public string? AssignedToUserName { get; init; }
    }

    /// <summary>Tour Manager: list of my confirmed packages with reservation status. Date filter on package StartDate (optional).</summary>
    [HttpGet("my-confirmed-packages")]
    public async Task<ActionResult<ApiResponse<List<MyConfirmedPackageDto>>>> GetMyConfirmedPackages(
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        var (_, email, isAdmin, _) = GetCurrentUserReservationScope();
        var tenantId = TenantId;

        var query = _db.Packages.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Status == PackageStatus.Confirmed);
        if (!isAdmin && !string.IsNullOrEmpty(email))
            query = query.Where(p => p.CreatedBy == email);

        if (dateFrom.HasValue)
            query = query.Where(p => p.StartDate.Date >= dateFrom.Value.Date);
        if (dateTo.HasValue)
            query = query.Where(p => p.StartDate.Date <= dateTo.Value.Date);

        var packages = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        var packageIds = packages.Select(p => p.Id).ToList();
        var reservations = await _db.Reservations.AsNoTracking()
            .Include(r => r.AssignedToUser)
            .Where(r => r.TenantId == tenantId && packageIds.Contains(r.PackageId))
            .ToListAsync(ct);
        var resByPackage = reservations.ToDictionary(r => r.PackageId);

        var items = packages.Select(p =>
        {
            var res = resByPackage.GetValueOrDefault(p.Id);
            return new MyConfirmedPackageDto
            {
                PackageId = p.Id.ToString("D"),
                PackageName = p.PackageName,
                ClientName = p.ClientName,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                ReservationId = res?.Id.ToString("D"),
                ReservationStatus = res?.Status.ToString(),
                AssignedToUserId = res?.AssignedToUserId.ToString("D"),
                AssignedToUserName = res != null ? (res.AssignedToUser.FirstName + " " + res.AssignedToUser.LastName).Trim() : null
            };
        }).ToList();

        return ApiResponse<List<MyConfirmedPackageDto>>.Ok(items);
    }

    /// <summary>List reservations. Status filter: Pending, Completed. Arrivals: use dateFrom/dateTo (package StartDate in range). Optional assignedTo = userId. Reservation role sees only their own.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ReservationListItemDto>>>> GetReservations(
        [FromQuery] ReservationStatus? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] Guid? assignedTo = null,
        CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        var (currentUserId, _, isAdmin, isReservationRole) = GetCurrentUserReservationScope();
        var tenantId = TenantId;

        var effectiveAssignedTo = assignedTo;
        if (isReservationRole && currentUserId.HasValue)
            effectiveAssignedTo = currentUserId;

        var query = _db.Reservations.AsNoTracking()
            .Include(r => r.Package)
            .Include(r => r.AssignedToUser)
            .Where(r => r.TenantId == tenantId);

        if (status.HasValue)
        {
            if (status.Value == ReservationStatus.Pending)
                query = query.Where(r => r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.InProcess);
            else
                query = query.Where(r => r.Status == status.Value);
        }
        if (effectiveAssignedTo.HasValue)
            query = query.Where(r => r.AssignedToUserId == effectiveAssignedTo.Value);
        if (dateFrom.HasValue || dateTo.HasValue)
        {
            if (dateFrom.HasValue)
                query = query.Where(r => r.Package.StartDate.Date >= dateFrom.Value.Date);
            if (dateTo.HasValue)
                query = query.Where(r => r.Package.StartDate.Date <= dateTo.Value.Date);
        }

        var list = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.PackageId,
                PackageName = r.Package.PackageName,
                ClientName = r.Package.ClientName,
                r.Package.StartDate,
                r.Package.EndDate,
                r.Package.TotalAmount,
                r.Package.Discount,
                FinalAmount = Math.Max(0, r.Package.TotalAmount - r.Package.Discount),
                r.Package.AdvanceAmount,
                r.Package.BalanceAmount,
                r.Status,
                r.AssignedToUserId,
                AssignedToUserName = r.AssignedToUser.FirstName + " " + r.AssignedToUser.LastName,
                r.CreatedAt,
                ScreenshotCount = r.PaymentScreenshots.Count
            })
            .ToListAsync(ct);

        var items = list.Select(x => new ReservationListItemDto
        {
            Id = x.Id.ToString("D"),
            PackageId = x.PackageId.ToString("D"),
            PackageName = x.PackageName,
            ClientName = x.ClientName,
            StartDate = x.StartDate,
            EndDate = x.EndDate,
            TotalAmount = x.TotalAmount,
            Discount = x.Discount,
            FinalAmount = x.FinalAmount,
            AdvanceAmount = x.AdvanceAmount,
            BalanceAmount = x.BalanceAmount,
            Status = x.Status.ToString(),
            AssignedToUserId = x.AssignedToUserId.ToString("D"),
            AssignedToUserName = x.AssignedToUserName.Trim(),
            CreatedAt = x.CreatedAt,
            ScreenshotCount = x.ScreenshotCount
        }).ToList();

        return ApiResponse<List<ReservationListItemDto>>.Ok(items);
    }

    /// <summary>Confirmed packages that do not yet have a reservation (for "Assign to reservation manager" dropdown). Tour Manager and Admin see all such packages in the tenant.</summary>
    [HttpGet("confirmed-packages")]
    public async Task<ActionResult<ApiResponse<List<ConfirmedPackageForReservationDto>>>> GetConfirmedPackagesWithoutReservation(CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        var tenantId = TenantId;

        var reservedPackageIds = await _db.Reservations
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.PackageId)
            .ToListAsync(ct);

        var query = _db.Packages.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Status == PackageStatus.Confirmed && !reservedPackageIds.Contains(p.Id));

        var packages = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ConfirmedPackageForReservationDto
            {
                Id = p.Id.ToString("D"),
                PackageName = p.PackageName,
                ClientName = p.ClientName,
                StartDate = p.StartDate,
                EndDate = p.EndDate
            })
            .ToListAsync(ct);

        return ApiResponse<List<ConfirmedPackageForReservationDto>>.Ok(packages);
    }

    /// <summary>Get one reservation with package summary and payment screenshots. Reservation role can only view reservations assigned to them.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ReservationDetailDto>>> GetReservation(Guid id, CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        var (currentUserId, _, isAdmin, isReservationRole) = GetCurrentUserReservationScope();
        var tenantId = TenantId;

        var r = await _db.Reservations.AsNoTracking()
            .Include(r => r.Package)
            .ThenInclude(p => p!.DayWiseItinerary!)
            .ThenInclude(d => d.Hotel)
            .Include(r => r.Package)
            .ThenInclude(p => p!.Vehicle)
            .Include(r => r.AssignedToUser)
            .Include(r => r.AssignedByUser)
            .Include(r => r.DayCompletions)
            .Include(r => r.PaymentScreenshots)
            .Where(r => r.TenantId == tenantId && r.Id == id)
            .FirstOrDefaultAsync(ct);

        if (r == null)
            return NotFound(ApiResponse<ReservationDetailDto>.Fail("Reservation not found."));
        if (isReservationRole && currentUserId.HasValue && r.AssignedToUserId != currentUserId.Value)
            return NotFound(ApiResponse<ReservationDetailDto>.Fail("Reservation not found."));
        if (r.Package == null)
            return NotFound(ApiResponse<ReservationDetailDto>.Fail("Reservation package not found."));

        var packageDetail = new ReservationPackageDetailDto
        {
            PackageName = r.Package.PackageName,
            ClientName = r.Package.ClientName,
            ClientPhone = r.Package.ClientPhone,
            ClientEmail = r.Package.ClientEmail,
            StartDate = r.Package.StartDate,
            EndDate = r.Package.EndDate,
            NumberOfDays = r.Package.NumberOfDays,
            VehicleName = r.Package.Vehicle == null ? null
                : string.IsNullOrWhiteSpace(r.Package.Vehicle.VehicleModel)
                    ? r.Package.Vehicle.VehicleType.ToString()
                    : $"{r.Package.Vehicle.VehicleType} - {r.Package.Vehicle.VehicleModel}",
            InclusionIds = r.Package.InclusionIds,
            DayWiseItinerary = (r.Package.DayWiseItinerary ?? [])
                .OrderBy(d => d.DayNumber)
                .Select(d => new ReservationDayItineraryItemDto
                {
                    DayNumber = d.DayNumber,
                    Date = d.Date,
                    HotelId = d.HotelId?.ToString("D"),
                    HotelName = d.Hotel?.Name,
                    IsHouseboat = d.Hotel?.IsHouseboat ?? false,
                    RoomType = d.RoomType,
                    NumberOfRooms = d.NumberOfRooms,
                    CheckInTime = d.CheckInTime,
                    CheckOutTime = d.CheckOutTime,
                    Notes = d.Notes,
                    HotelCost = d.HotelCost
                }).ToList()
        };

        var dto = new ReservationDetailDto
        {
            Id = r.Id.ToString("D"),
            PackageId = r.PackageId.ToString("D"),
            PackageName = r.Package.PackageName,
            ClientName = r.Package.ClientName,
            StartDate = r.Package.StartDate,
            EndDate = r.Package.EndDate,
            TotalAmount = r.Package.TotalAmount,
            Discount = r.Package.Discount,
            FinalAmount = Math.Max(0, r.Package.TotalAmount - r.Package.Discount),
            AdvanceAmount = r.Package.AdvanceAmount,
            BalanceAmount = r.Package.BalanceAmount,
            Status = r.Status.ToString(),
            AssignedToUserId = r.AssignedToUserId.ToString("D"),
            AssignedToUserName = (r.AssignedToUser.FirstName + " " + r.AssignedToUser.LastName).Trim(),
            CreatedAt = r.CreatedAt,
            ScreenshotCount = r.PaymentScreenshots.Count,
            Notes = r.Notes,
            FinalNotes = r.FinalNotes,
            PaymentScreenshots = r.PaymentScreenshots
                .OrderBy(s => s.CreatedAt)
                .Select(s => new ReservationScreenshotDto
                {
                    Id = s.Id.ToString("D"),
                    FileUrl = s.FileUrl,
                    FileName = s.FileName,
                    CreatedAt = s.CreatedAt,
                    DayNumber = s.DayNumber
                })
                .ToList(),
            AssignedByUserId = r.AssignedByUserId?.ToString("D"),
            AssignedByUserName = r.AssignedByUser != null
                ? (r.AssignedByUser.FirstName + " " + r.AssignedByUser.LastName).Trim()
                : null,
            DayCompletions = r.DayCompletions
                .OrderBy(d => d.DayNumber)
                .Select(d => new ReservationDayCompletionDto
                {
                    DayNumber = d.DayNumber,
                    IsDone = d.IsDone,
                    DoneAt = d.DoneAt
                })
                .ToList(),
            PackageDetail = packageDetail
        };

        return ApiResponse<ReservationDetailDto>.Ok(dto);
    }

    public sealed class CreateReservationRequestDto
    {
        public required Guid PackageId { get; set; }
        public required Guid AssignedToUserId { get; set; }
        /// <summary>Optional. When "InProcess", reservation is created as In Process (Tour Manager send-for-reservation flow). Default: Pending.</summary>
        public ReservationStatus? Status { get; set; }
    }

    /// <summary>Assign a confirmed package to a reservation manager (creates a reservation).</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ReservationDetailDto>>> CreateReservation([FromBody] CreateReservationRequestDto dto, CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        var tenantId = TenantId;

        var package = await _db.Packages
            .Where(p => p.TenantId == tenantId && p.Id == dto.PackageId && p.Status == PackageStatus.Confirmed)
            .FirstOrDefaultAsync(ct);
        if (package == null)
            return BadRequest(ApiResponse<ReservationDetailDto>.Fail("Package not found or not confirmed."));

        var existing = await _db.Reservations
            .Where(r => r.TenantId == tenantId && r.PackageId == dto.PackageId)
            .FirstOrDefaultAsync(ct);
        if (existing != null)
            return BadRequest(ApiResponse<ReservationDetailDto>.Fail("This package is already assigned to a reservation."));

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.Id == dto.AssignedToUserId && u.IsActive)
            .FirstOrDefaultAsync(ct);
        if (user == null)
            return BadRequest(ApiResponse<ReservationDetailDto>.Fail("Assigned user not found or inactive."));

        var (currentUserId, _, _, _) = GetCurrentUserReservationScope();
        var status = dto.Status ?? ReservationStatus.Pending;
        var reservation = new Reservation
        {
            TenantId = tenantId,
            PackageId = dto.PackageId,
            AssignedToUserId = dto.AssignedToUserId,
            AssignedByUserId = currentUserId,
            Status = status
        };
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync(ct);

        return await GetReservation(reservation.Id, ct);
    }

    public sealed class UpdateReservationRequestDto
    {
        public ReservationStatus? Status { get; set; }
        public Guid? AssignedToUserId { get; set; }
        public string? Notes { get; set; }
        public string? FinalNotes { get; set; }
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ReservationDetailDto>>> UpdateReservation(Guid id, [FromBody] UpdateReservationRequestDto dto, CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        var (currentUserId, _, isAdmin, isReservationRole) = GetCurrentUserReservationScope();
        var tenantId = TenantId;

        var reservation = await _db.Reservations
            .Include(r => r.Package)
            .Include(r => r.AssignedToUser)
            .Include(r => r.PaymentScreenshots)
            .Where(r => r.TenantId == tenantId && r.Id == id)
            .FirstOrDefaultAsync(ct);

        if (reservation == null)
            return NotFound(ApiResponse<ReservationDetailDto>.Fail("Reservation not found."));
        if (isReservationRole && currentUserId.HasValue && reservation.AssignedToUserId != currentUserId.Value)
            return NotFound(ApiResponse<ReservationDetailDto>.Fail("Reservation not found."));

        if (dto.Status.HasValue)
            reservation.Status = dto.Status.Value;
        if (dto.Notes != null)
            reservation.Notes = dto.Notes.Trim();
        if (dto.FinalNotes != null)
            reservation.FinalNotes = dto.FinalNotes.Trim();
        if (dto.AssignedToUserId.HasValue)
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.TenantId == tenantId && u.Id == dto.AssignedToUserId.Value && u.IsActive)
                .FirstOrDefaultAsync(ct);
            if (user == null)
                return BadRequest(ApiResponse<ReservationDetailDto>.Fail("Assigned user not found or inactive."));
            reservation.AssignedToUserId = dto.AssignedToUserId.Value;
        }

        await _db.SaveChangesAsync(ct);

        return await GetReservation(id, ct);
    }

    public sealed class SetDayCompletionRequestDto
    {
        public required int DayNumber { get; set; }
        public required bool IsDone { get; set; }
    }

    /// <summary>Set day-wise "mark as done" for a reservation. Reservation role can only update reservations assigned to them.</summary>
    [HttpPut("{id:guid}/day-completion")]
    public async Task<ActionResult<ApiResponse<ReservationDayCompletionDto>>> SetDayCompletion(Guid id, [FromBody] SetDayCompletionRequestDto dto, CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        var (currentUserId, _, isAdmin, isReservationRole) = GetCurrentUserReservationScope();
        var tenantId = TenantId;

        var reservation = await _db.Reservations
            .Include(r => r.DayCompletions)
            .Where(r => r.TenantId == tenantId && r.Id == id)
            .FirstOrDefaultAsync(ct);
        if (reservation == null)
            return NotFound(ApiResponse<ReservationDayCompletionDto>.Fail("Reservation not found."));
        if (isReservationRole && currentUserId.HasValue && reservation.AssignedToUserId != currentUserId.Value)
            return NotFound(ApiResponse<ReservationDayCompletionDto>.Fail("Reservation not found."));

        var completion = reservation.DayCompletions.FirstOrDefault(d => d.DayNumber == dto.DayNumber);
        if (completion == null)
        {
            completion = new ReservationDayCompletion
            {
                ReservationId = id,
                DayNumber = dto.DayNumber,
                IsDone = dto.IsDone,
                DoneAt = dto.IsDone ? DateTime.UtcNow : null
            };
            _db.ReservationDayCompletions.Add(completion);
        }
        else
        {
            completion.IsDone = dto.IsDone;
            completion.DoneAt = dto.IsDone ? DateTime.UtcNow : null;
        }
        await _db.SaveChangesAsync(ct);

        return ApiResponse<ReservationDayCompletionDto>.Ok(new ReservationDayCompletionDto
        {
            DayNumber = completion.DayNumber,
            IsDone = completion.IsDone,
            DoneAt = completion.DoneAt
        });
    }

    /// <summary>Upload a payment screenshot for this reservation. Optional dayNumber for hotel/day-wise screenshots. Reservation role can only upload for reservations assigned to them.</summary>
    [HttpPost("{id:guid}/screenshots")]
    public async Task<ActionResult<ApiResponse<ReservationScreenshotDto>>> UploadScreenshot(Guid id, IFormFile? file, [FromQuery] int? dayNumber = null, CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        if (file == null)
            return BadRequest(ApiResponse<ReservationScreenshotDto>.Fail("No file provided."));

        var (currentUserId, _, isAdmin, isReservationRole) = GetCurrentUserReservationScope();
        var tenantId = TenantId;
        var reservation = await _db.Reservations
            .Where(r => r.TenantId == tenantId && r.Id == id)
            .FirstOrDefaultAsync(ct);
        if (reservation == null)
            return NotFound(ApiResponse<ReservationScreenshotDto>.Fail("Reservation not found."));
        if (isReservationRole && currentUserId.HasValue && reservation.AssignedToUserId != currentUserId.Value)
            return NotFound(ApiResponse<ReservationScreenshotDto>.Fail("Reservation not found."));

        var url = await _storage.SaveReservationScreenshotAsync(tenantId, id, file, ct);
        var screenshot = new ReservationPaymentScreenshot
        {
            ReservationId = id,
            DayNumber = dayNumber,
            FileUrl = url,
            FileName = file.FileName
        };
        _db.ReservationPaymentScreenshots.Add(screenshot);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<ReservationScreenshotDto>.Ok(new ReservationScreenshotDto
        {
            Id = screenshot.Id.ToString("D"),
            FileUrl = screenshot.FileUrl,
            FileName = screenshot.FileName,
            CreatedAt = screenshot.CreatedAt,
            DayNumber = screenshot.DayNumber
        });
    }

    /// <summary>Remove a payment screenshot. Reservation role can only delete from reservations assigned to them.</summary>
    [HttpDelete("{id:guid}/screenshots/{screenshotId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteScreenshot(Guid id, Guid screenshotId, CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        var (currentUserId, _, isAdmin, isReservationRole) = GetCurrentUserReservationScope();
        var tenantId = TenantId;

        var screenshot = await _db.ReservationPaymentScreenshots
            .Include(s => s.Reservation)
            .Where(s => s.ReservationId == id && s.Reservation.TenantId == tenantId && s.Id == screenshotId)
            .FirstOrDefaultAsync(ct);
        if (screenshot == null)
            return NotFound(ApiResponse<object>.Fail("Screenshot not found."));
        if (isReservationRole && currentUserId.HasValue && screenshot.Reservation.AssignedToUserId != currentUserId.Value)
            return NotFound(ApiResponse<object>.Fail("Screenshot not found."));

        _db.ReservationPaymentScreenshots.Remove(screenshot);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }
}
