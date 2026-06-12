using System.Security.Claims;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;
using TravelPathways.Api.Services;
using TravelPathways.Api.Storage;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reservations")]
public sealed class ReservationsController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly FileStorage _storage;
    private readonly IChromiumBrowserProvider _browserProvider;

    public ReservationsController(
        AppDbContext db,
        TenantContext tenant,
        FileStorage storage,
        IChromiumBrowserProvider browserProvider) : base(tenant)
    {
        _db = db;
        _storage = storage;
        _browserProvider = browserProvider;
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

    /// <summary>Tenant Admin or Super Admin — see all confirmed packages / reservations in tenant.</summary>
    private bool IsReservationAdmin() => IsTenantAdmin();

    /// <summary>Current user id, email (for CreatedBy), and role flags. Admin sees all; Reservation sees only assigned; Agent (Tour Manager) sees own packages.</summary>
    private (Guid? UserId, string? Email, bool IsAdmin, bool IsReservationRole) GetCurrentUserReservationScope()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        var isAdmin = IsReservationAdmin();
        var isReservation = string.Equals(role, UserRole.Reservation.ToString(), StringComparison.OrdinalIgnoreCase);
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = Guid.TryParse(userIdClaim, out var uid) ? uid : null;
        var email = User.FindFirstValue(ClaimTypes.Email)?.Trim();
        return (userId, email, isAdmin, isReservation);
    }

    private static bool IsPackageCreator(TourPackage package, string? email) =>
        !string.IsNullOrWhiteSpace(email) &&
        string.Equals(package.CreatedBy.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Package is confirmed, or its lead is confirmed (covers lead updated before package status synced).</summary>
    private IQueryable<TourPackage> WhereReadyForReservation(IQueryable<TourPackage> query, Guid tenantId) =>
        query.Where(p =>
            p.Status == PackageStatus.Confirmed ||
            (p.LeadId != null &&
             _db.Leads.Any(l =>
                 l.Id == p.LeadId &&
                 l.TenantId == tenantId &&
                 !l.IsDeleted &&
                 l.Status == LeadStatus.Confirmed)));

    private IQueryable<TourPackage> ApplyPackageOwnershipFilter(
        IQueryable<TourPackage> query,
        bool isAdmin,
        string? email,
        Guid? userId,
        Guid tenantId)
    {
        if (isAdmin) return query;
        if (!userId.HasValue && string.IsNullOrEmpty(email))
            return query.Where(_ => false);

        return query.Where(p =>
            (!string.IsNullOrEmpty(email) && p.CreatedBy.ToLower() == email!.ToLower()) ||
            (userId.HasValue &&
             p.LeadId != null &&
             _db.Leads.Any(l =>
                 l.Id == p.LeadId &&
                 l.TenantId == tenantId &&
                 !l.IsDeleted &&
                 l.AssignedToUserId == userId.Value)));
    }

    private async Task<bool> CanUserAccessPackageAsync(
        TourPackage package,
        bool isAdmin,
        string? email,
        Guid? userId,
        Guid tenantId,
        CancellationToken ct)
    {
        if (isAdmin) return true;
        if (IsPackageCreator(package, email)) return true;
        if (!userId.HasValue || !package.LeadId.HasValue) return false;
        return await _db.Leads.AsNoTracking().AnyAsync(
            l => l.Id == package.LeadId &&
                 l.TenantId == tenantId &&
                 !l.IsDeleted &&
                 l.AssignedToUserId == userId.Value,
            ct);
    }

    private async Task EnsurePackageConfirmedAsync(TourPackage package, Guid tenantId, CancellationToken ct)
    {
        if (package.Status == PackageStatus.Confirmed) return;
        if (!package.LeadId.HasValue) return;
        var leadConfirmed = await _db.Leads.AsNoTracking().AnyAsync(
            l => l.Id == package.LeadId &&
                 l.TenantId == tenantId &&
                 !l.IsDeleted &&
                 l.Status == LeadStatus.Confirmed,
            ct);
        if (!leadConfirmed) return;

        await _db.Packages
            .Where(p => p.TenantId == tenantId && p.Id == package.Id && !p.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Status, PackageStatus.Confirmed)
                .SetProperty(p => p.IsLocked, true),
                ct);

        package.Status = PackageStatus.Confirmed;
        package.IsLocked = true;
        _db.Entry(package).State = EntityState.Unchanged;
    }

    private async Task SyncPackageCancelledAsync(Guid packageId, Guid? leadId, Guid tenantId, CancellationToken ct)
    {
        var packages = await _db.Packages
            .Where(p => p.TenantId == tenantId && !p.IsDeleted &&
                        (p.Id == packageId || (leadId.HasValue && p.LeadId == leadId)))
            .ToListAsync(ct);
        foreach (var pkg in packages)
        {
            pkg.Status = PackageStatus.Cancelled;
            pkg.IsLocked = false;
        }

        if (!leadId.HasValue) return;
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == leadId && l.TenantId == tenantId && !l.IsDeleted, ct);
        if (lead == null) return;
        lead.Status = LeadStatus.Cancelled;
        lead.IsLocked = false;
    }

    private static string H(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string FormatMoney(decimal value) =>
        "Rs. " + value.ToString("N0", CultureInfo.GetCultureInfo("en-IN"));

    private static string SafeFilenamePart(string? value, int maxLen = 50)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var cleaned = new string(value.Trim().Where(c => !invalid.Contains(c)).ToArray());
        cleaned = string.Join("_", cleaned.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        return cleaned.Length > maxLen ? cleaned[..maxLen] : cleaned;
    }

    private static string? ToAbsoluteUrl(string? url, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return trimmed.StartsWith("/")
            ? baseUrl.TrimEnd('/') + trimmed
            : baseUrl.TrimEnd('/') + "/" + trimmed;
    }

    private static DateTime AsUtcDate(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    private static DateTime? AsUtcDate(DateTime? value) => value.HasValue ? AsUtcDate(value.Value) : null;

    private static ReservationHotelBookingDto ToHotelBookingDto(ReservationHotelBooking booking)
    {
        return new ReservationHotelBookingDto
        {
            Id = booking.Id.ToString("D"),
            DayNumber = booking.DayNumber,
            BookingDate = booking.BookingDate,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            HotelId = booking.HotelId?.ToString("D"),
            HotelName = booking.HotelName,
            IsHouseboat = booking.IsHouseboat,
            RoomType = booking.RoomType,
            NumberOfRooms = booking.NumberOfRooms,
            ExtraBedCount = booking.ExtraBedCount,
            CnbCount = booking.CnbCount,
            NumberOfPersons = booking.NumberOfPersons,
            RatePerNight = booking.RatePerNight,
            ExtraBedRate = booking.ExtraBedRate,
            CnbRate = booking.CnbRate,
            TotalAmount = booking.TotalAmount,
            AdvancePaid = booking.AdvancePaid,
            BalanceAmount = booking.BalanceAmount,
            Status = booking.Status.ToString(),
            IsLocked = booking.IsLocked,
            CancellationReason = booking.CancellationReason?.ToString(),
            CancellationReasonDetail = booking.CancellationReasonDetail,
            ConfirmationNumber = booking.ConfirmationNumber,
            Notes = booking.Notes,
            Documents = (booking.Documents ?? [])
                .OrderBy(d => d.CreatedAt)
                .Select(d => new ReservationHotelBookingDocumentDto
                {
                    Id = d.Id.ToString("D"),
                    Type = d.Type.ToString(),
                    Amount = d.Amount,
                    PaymentDate = d.PaymentDate,
                    FileUrl = d.FileUrl,
                    FileName = d.FileName,
                    CreatedAt = d.CreatedAt
                })
                .ToList()
        };
    }

    /// <summary>Package itineraries include a final departure day with no hotel stay.</summary>
    private static IReadOnlyList<DayItinerary> GetAccommodationDays(IReadOnlyList<DayItinerary>? itinerary)
    {
        if (itinerary is not { Count: > 0 }) return Array.Empty<DayItinerary>();
        var maxDayNumber = itinerary.Max(d => d.DayNumber);
        return itinerary.Where(d => d.DayNumber != maxDayNumber).ToList();
    }

    private static ReservationHotelBooking CreateHotelBookingFromDay(Reservation reservation, DayItinerary day)
    {
        var package = reservation.Package;
        var totalPersons = (package?.NumberOfAdults ?? 0) + (package?.NumberOfChildren ?? 0);
        var rate = day.HotelCost;
        return new ReservationHotelBooking
        {
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            DayNumber = day.DayNumber,
            BookingDate = AsUtcDate(day.Date),
            CheckInDate = AsUtcDate(day.Date),
            CheckOutDate = AsUtcDate(day.Date.AddDays(1)),
            HotelId = day.HotelId,
            HotelName = day.Hotel?.Name ?? string.Empty,
            IsHouseboat = day.Hotel?.IsHouseboat ?? false,
            RoomType = day.RoomType,
            NumberOfRooms = day.NumberOfRooms,
            ExtraBedCount = day.ExtraBedCount ?? 0,
            CnbCount = day.CnbCount ?? 0,
            NumberOfPersons = totalPersons,
            RatePerNight = rate,
            TotalAmount = rate,
            AdvancePaid = 0,
            BalanceAmount = rate,
            Status = ReservationHotelBookingStatus.Pending,
            Notes = day.Notes
        };
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
        public int ExtraBedCount { get; init; }
        public int CnbCount { get; init; }
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
        public required int NumberOfAdults { get; init; }
        public required int NumberOfChildren { get; init; }
        public string? VehicleName { get; init; }
        public List<string>? InclusionIds { get; init; }
        public required List<ReservationDayItineraryItemDto> DayWiseItinerary { get; init; }
    }

    public sealed class ReservationHotelBookingDocumentDto
    {
        public required string Id { get; init; }
        public required string Type { get; init; }
        public decimal? Amount { get; init; }
        public DateTime? PaymentDate { get; init; }
        public required string FileUrl { get; init; }
        public required string FileName { get; init; }
        public required DateTime CreatedAt { get; init; }
    }

    public sealed class ReservationHotelBookingDto
    {
        public required string Id { get; init; }
        public required int DayNumber { get; init; }
        public required DateTime BookingDate { get; init; }
        public DateTime? CheckInDate { get; init; }
        public DateTime? CheckOutDate { get; init; }
        public string? HotelId { get; init; }
        public required string HotelName { get; init; }
        public bool IsHouseboat { get; init; }
        public string? RoomType { get; init; }
        public int NumberOfRooms { get; init; }
        public int ExtraBedCount { get; init; }
        public int CnbCount { get; init; }
        public int NumberOfPersons { get; init; }
        public decimal RatePerNight { get; init; }
        public decimal ExtraBedRate { get; init; }
        public decimal CnbRate { get; init; }
        public decimal TotalAmount { get; init; }
        public decimal AdvancePaid { get; init; }
        public decimal BalanceAmount { get; init; }
        public required string Status { get; init; }
        public required bool IsLocked { get; init; }
        public string? CancellationReason { get; init; }
        public string? CancellationReasonDetail { get; init; }
        public string? ConfirmationNumber { get; init; }
        public string? Notes { get; init; }
        public required List<ReservationHotelBookingDocumentDto> Documents { get; init; }
    }

    public sealed class CancelReservationHotelBookingRequest
    {
        public required ReservationHotelBookingCancellationReason Reason { get; init; }
        public string? ReasonDetail { get; init; }
    }

    public sealed class UpsertReservationHotelBookingRequest
    {
        public int DayNumber { get; init; }
        public DateTime? BookingDate { get; init; }
        public DateTime? CheckInDate { get; init; }
        public DateTime? CheckOutDate { get; init; }
        public string? HotelName { get; init; }
        public bool? IsHouseboat { get; init; }
        public string? RoomType { get; init; }
        public int? NumberOfRooms { get; init; }
        public int? ExtraBedCount { get; init; }
        public int? CnbCount { get; init; }
        public int? NumberOfPersons { get; init; }
        public decimal? RatePerNight { get; init; }
        public decimal? ExtraBedRate { get; init; }
        public decimal? CnbRate { get; init; }
        public decimal? TotalAmount { get; init; }
        public decimal? AdvancePaid { get; init; }
        public decimal? BalanceAmount { get; init; }
        public ReservationHotelBookingStatus? Status { get; init; }
        public string? ConfirmationNumber { get; init; }
        public string? Notes { get; init; }
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
        public bool IsLocked { get; init; }
        public required string AssignedToUserId { get; init; }
        public required string AssignedToUserName { get; init; }
        public required DateTime CreatedAt { get; init; }
        public int ScreenshotCount { get; init; }
    }

    public sealed class ReservationDetailDto : ReservationListItemDto
    {
        public string? Notes { get; init; }
        public required List<ReservationScreenshotDto> PaymentScreenshots { get; init; }
        public required List<ReservationHotelBookingDto> HotelBookings { get; init; }
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

    /// <summary>Confirmed packages with reservation status. Admin: all in tenant; others: packages they own or leads assigned to them.</summary>
    [HttpGet("my-confirmed-packages")]
    public async Task<ActionResult<ApiResponse<List<MyConfirmedPackageDto>>>> GetMyConfirmedPackages(
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        var (userId, email, isAdmin, _) = GetCurrentUserReservationScope();
        var tenantId = TenantId;

        var query = ApplyPackageOwnershipFilter(
            WhereReadyForReservation(_db.Packages.AsNoTracking(), tenantId),
            isAdmin,
            email,
            userId,
            tenantId);

        if (dateFrom.HasValue)
        {
            var d = dateFrom.Value.Date;
            var startUtc = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
            query = query.Where(p => p.StartDate >= startUtc);
        }
        if (dateTo.HasValue)
        {
            var d = dateTo.Value.Date;
            var endUtcExclusive = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
            query = query.Where(p => p.StartDate < endUtcExclusive);
        }

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
            {
                var d = dateFrom.Value.Date;
                var startUtc = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
                query = query.Where(r => r.Package.StartDate >= startUtc);
            }
            if (dateTo.HasValue)
            {
                var d = dateTo.Value.Date;
                var endUtcExclusive = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
                query = query.Where(r => r.Package.StartDate < endUtcExclusive);
            }
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
                r.IsLocked,
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
            IsLocked = x.IsLocked,
            AssignedToUserId = x.AssignedToUserId.ToString("D"),
            AssignedToUserName = x.AssignedToUserName.Trim(),
            CreatedAt = x.CreatedAt,
            ScreenshotCount = x.ScreenshotCount
        }).ToList();

        return ApiResponse<List<ReservationListItemDto>>.Ok(items);
    }

    /// <summary>Confirmed packages without a reservation. Admins see all in tenant; others see only packages they created.</summary>
    [HttpGet("confirmed-packages")]
    public async Task<ActionResult<ApiResponse<List<ConfirmedPackageForReservationDto>>>> GetConfirmedPackagesWithoutReservation(CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        var (userId, email, isAdmin, _) = GetCurrentUserReservationScope();
        var tenantId = TenantId;

        var reservedPackageIds = await _db.Reservations
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.PackageId)
            .ToListAsync(ct);

        var query = ApplyPackageOwnershipFilter(
            WhereReadyForReservation(_db.Packages.AsNoTracking(), tenantId)
                .Where(p => !reservedPackageIds.Contains(p.Id)),
            isAdmin,
            email,
            userId,
            tenantId);

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

        var r = await _db.Reservations
            .Include(r => r.Package)
            .ThenInclude(p => p!.DayWiseItinerary!)
            .ThenInclude(d => d.Hotel)
            .Include(r => r.Package)
            .ThenInclude(p => p!.Vehicle)
            .Include(r => r.AssignedToUser)
            .Include(r => r.AssignedByUser)
            .Include(r => r.DayCompletions)
            .Include(r => r.PaymentScreenshots)
            .Include(r => r.HotelBookings)
            .ThenInclude(b => b.Documents)
            .Where(r => r.TenantId == tenantId && r.Id == id)
            .FirstOrDefaultAsync(ct);

        if (r == null)
            return NotFound(ApiResponse<ReservationDetailDto>.Fail("Reservation not found."));
        if (isReservationRole && currentUserId.HasValue && r.AssignedToUserId != currentUserId.Value)
            return NotFound(ApiResponse<ReservationDetailDto>.Fail("Reservation not found."));
        if (r.Package == null)
            return NotFound(ApiResponse<ReservationDetailDto>.Fail("Reservation package not found."));

        var itineraryDays = (r.Package.DayWiseItinerary ?? []).ToList();
        var accommodationDays = GetAccommodationDays(itineraryDays);
        var accommodationDayNumbers = accommodationDays.Select(d => d.DayNumber).ToHashSet();

        var hotelBookings = r.HotelBookings.ToList();
        var orphanBookings = hotelBookings.Where(b => !accommodationDayNumbers.Contains(b.DayNumber)).ToList();
        if (orphanBookings.Count > 0)
        {
            _db.ReservationHotelBookings.RemoveRange(orphanBookings);
            await _db.SaveChangesAsync(ct);
            foreach (var orphan in orphanBookings)
                hotelBookings.Remove(orphan);
        }

        var existingBookingDays = hotelBookings.Select(b => b.DayNumber).ToHashSet();
        var missingBookingDays = accommodationDays
            .Where(d => !existingBookingDays.Contains(d.DayNumber))
            .ToList();
        if (missingBookingDays.Count > 0)
        {
            var newBookings = missingBookingDays
                .Select(day => CreateHotelBookingFromDay(r, day))
                .ToList();
            try
            {
                _db.ReservationHotelBookings.AddRange(newBookings);
                await _db.SaveChangesAsync(ct);
                hotelBookings.AddRange(newBookings);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Do not fail a read-only details page because a best-effort booking seed raced or hit stale tracked state.
                _db.ChangeTracker.Clear();
                r = await _db.Reservations
                    .Include(r => r.Package)
                    .ThenInclude(p => p!.DayWiseItinerary!)
                    .ThenInclude(d => d.Hotel)
                    .Include(r => r.Package)
                    .ThenInclude(p => p!.Vehicle)
                    .Include(r => r.AssignedToUser)
                    .Include(r => r.AssignedByUser)
                    .Include(r => r.DayCompletions)
                    .Include(r => r.PaymentScreenshots)
                    .Include(r => r.HotelBookings)
                    .ThenInclude(b => b.Documents)
                    .Where(r => r.TenantId == tenantId && r.Id == id)
                    .FirstOrDefaultAsync(ct);

                if (r == null)
                    return NotFound(ApiResponse<ReservationDetailDto>.Fail("Reservation not found."));
                if (r.Package == null)
                    return NotFound(ApiResponse<ReservationDetailDto>.Fail("Reservation package not found."));
                hotelBookings = r.HotelBookings.ToList();
            }
        }

        var packageDetail = new ReservationPackageDetailDto
        {
            PackageName = r.Package.PackageName,
            ClientName = r.Package.ClientName,
            ClientPhone = r.Package.ClientPhone,
            ClientEmail = r.Package.ClientEmail,
            StartDate = r.Package.StartDate,
            EndDate = r.Package.EndDate,
            NumberOfDays = r.Package.NumberOfDays,
            NumberOfAdults = r.Package.NumberOfAdults,
            NumberOfChildren = r.Package.NumberOfChildren,
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
                    ExtraBedCount = d.ExtraBedCount ?? 0,
                    CnbCount = d.CnbCount ?? 0,
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
            IsLocked = r.IsLocked,
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
            HotelBookings = hotelBookings
                .OrderBy(b => b.DayNumber)
                .Select(ToHotelBookingDto)
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

    [HttpGet("{id:guid}/tour-planner")]
    [ProducesResponseType(typeof(FileResult), 200)]
    public async Task<IActionResult> DownloadTourPlanner(
        Guid id,
        [FromQuery] string copy = "client",
        CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        var (currentUserId, _, isAdmin, isReservationRole) = GetCurrentUserReservationScope();
        var tenantId = TenantId;

        var reservation = await _db.Reservations.AsNoTracking()
            .Include(r => r.AssignedToUser)
            .Include(r => r.Package)
                .ThenInclude(p => p.DayWiseItinerary!)
                .ThenInclude(d => d.Hotel)
            .Include(r => r.Package)
                .ThenInclude(p => p.DayWiseItinerary!)
                .ThenInclude(d => d.ItineraryTemplate)
            .Include(r => r.Package)
                .ThenInclude(p => p.Vehicle)
            .Where(r => r.TenantId == tenantId && r.Id == id)
            .FirstOrDefaultAsync(ct);

        if (reservation == null || reservation.Package == null)
            return NotFound(ApiResponse<object>.Fail("Reservation not found."));
        if (!isAdmin)
        {
            if (!isReservationRole || !currentUserId.HasValue || reservation.AssignedToUserId != currentUserId.Value)
                return NotFound(ApiResponse<object>.Fail("Reservation not found."));
        }
        if (reservation.Status != ReservationStatus.Completed)
            return BadRequest(ApiResponse<object>.Fail("Tour planner is available only after the reservation is completed."));

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        var isDriverCopy = string.Equals(copy, "driver", StringComparison.OrdinalIgnoreCase);
        var agencyLogoUrl = ToAbsoluteUrl(tenant?.LogoUrl, $"{Request.Scheme}://{Request.Host}");
        var html = isDriverCopy
            ? BuildDriverTourPlannerHtml(reservation, agencyLogoUrl)
            : BuildTourPlannerHtml(reservation, tenant, agencyLogoUrl);
        var pdfBytes = await _browserProvider.RunWithPageAsync(async page =>
        {
            await page.SetJavaScriptEnabledAsync(false);
            await page.SetContentAsync(html, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Load } });
            return await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions
                {
                    Top = "14mm",
                    Right = "12mm",
                    Bottom = "14mm",
                    Left = "12mm"
                }
            });
        }, ct);

        var pkg = reservation.Package;
        var filenameBase = string.Join("_", new[]
        {
            "tour-planner",
            isDriverCopy ? "driver-copy" : "client-copy",
            SafeFilenamePart(pkg.ClientName, 35),
            pkg.StartDate.ToString("ddMMMyyyy", CultureInfo.InvariantCulture)
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        return File(pdfBytes, "application/pdf", $"{filenameBase}.pdf");
    }

    private static string BuildDriverTourPlannerHtml(Reservation reservation, string? agencyLogoUrl)
    {
        var pkg = reservation.Package;
        var india = CultureInfo.GetCultureInfo("en-IN");
        string FmtDate(DateTime value) => value.ToString("d MMM yyyy", india);
        var vehicle = pkg.Vehicle == null
            ? "Not selected"
            : string.IsNullOrWhiteSpace(pkg.Vehicle.VehicleModel)
                ? pkg.Vehicle.VehicleType.ToString()
                : $"{pkg.Vehicle.VehicleType} - {pkg.Vehicle.VehicleModel}";

        var summaryHtml = string.Join("", (pkg.DayWiseItinerary ?? [])
            .OrderBy(d => d.DayNumber)
            .Select(d =>
            {
                var title = d.ItineraryTemplate?.Title?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                    title = d.Activities?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a))?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                    title = $"Day {d.DayNumber}";

                var notes = new List<string>();
                if (d.Activities?.Count > 0)
                    notes.Add(string.Join(", ", d.Activities.Where(a => !string.IsNullOrWhiteSpace(a))));
                if (!string.IsNullOrWhiteSpace(d.Notes))
                    notes.Add(d.Notes.Trim());
                var summary = notes.Count == 0 ? "—" : string.Join(" ", notes);

                return $"""
                    <section class="day-row">
                      <div class="day-no">Day {d.DayNumber}</div>
                      <div>
                        <h2>{H(title)}</h2>
                        <p class="date">{H(FmtDate(d.Date))}</p>
                        <p>{H(summary)}</p>
                      </div>
                    </section>
                    """;
            }));
        var hotelDetailsHtml = BuildHotelDetailsHtml(pkg);
        var logoHtml = string.IsNullOrWhiteSpace(agencyLogoUrl)
            ? ""
            : $"<img class=\"agency-logo\" src=\"{H(agencyLogoUrl)}\" alt=\"Agency logo\" />";

        return $$"""
<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <style>
    * { box-sizing: border-box; }
    body { margin: 0; font-family: Arial, Helvetica, sans-serif; color: #111827; background: #ffffff; font-size: 12px; }
    .doc-header { display: flex; justify-content: space-between; align-items: center; gap: 18px; padding: 18px 20px; border: 1px solid #dbeafe; border-top: 6px solid #0f3d5e; border-radius: 14px; background: #f8fafc; margin-bottom: 18px; }
    .agency-logo { max-height: 68px; max-width: 210px; object-fit: contain; }
    .title-block { text-align: right; }
    h1 { margin: 2px 0 0; font-size: 30px; color: #0f172a; }
    .copy-label { margin: 0; font-size: 12px; font-weight: 800; color: #0f766e; letter-spacing: .12em; text-transform: uppercase; }
    .summary { display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; margin-bottom: 18px; }
    .field { border: 1px solid #e5e7eb; border-left: 4px solid #0ea5e9; border-radius: 10px; padding: 11px; background: #ffffff; }
    .label { display: block; color: #6b7280; font-size: 10px; text-transform: uppercase; letter-spacing: .08em; margin-bottom: 4px; }
    .value { font-size: 13px; font-weight: 700; }
    .section-title { margin: 20px 0 12px; font-size: 17px; color: #0f3d5e; border-bottom: 1px solid #cbd5e1; padding-bottom: 7px; }
    .day-row { break-inside: avoid; display: grid; grid-template-columns: 72px 1fr; gap: 12px; border: 1px solid #e2e8f0; border-radius: 12px; padding: 12px; margin-bottom: 10px; background: #ffffff; }
    .day-no { background: #0f3d5e; color: #ffffff; border-radius: 8px; padding: 8px 9px; height: fit-content; text-align: center; font-weight: 800; }
    .hotel-section { break-before: page; }
    .hotel-card { break-inside: avoid; border: 1px solid #e5e7eb; border-radius: 12px; padding: 12px; margin-bottom: 10px; background: #ffffff; }
    .hotel-head { display: flex; justify-content: space-between; gap: 12px; border-bottom: 1px solid #e5e7eb; padding-bottom: 7px; margin-bottom: 8px; }
    .hotel-head h3 { margin: 0; }
    .hotel-head span { color: #64748b; font-size: 11px; }
    .hotel-row { margin-top: 6px; line-height: 1.45; }
    h2 { margin: 0 0 3px; font-size: 15px; color: #172554; }
    p { margin: 6px 0 0; line-height: 1.5; color: #334155; }
    .date { color: #64748b; margin-top: 0; }
  </style>
</head>
<body>
  <section class="doc-header">
    <div>{{logoHtml}}</div>
    <div class="title-block">
      <p class="copy-label">Driver Copy</p>
      <h1>Tour Planner</h1>
    </div>
  </section>

  <section class="summary">
    <div class="field"><span class="label">Client Name</span><span class="value">{{H(pkg.ClientName)}}</span></div>
    <div class="field"><span class="label">Vehicle</span><span class="value">{{H(vehicle)}}</span></div>
    <div class="field"><span class="label">Arrival Date</span><span class="value">{{H(FmtDate(pkg.StartDate))}}</span></div>
    <div class="field"><span class="label">Departure Date</span><span class="value">{{H(FmtDate(pkg.EndDate))}}</span></div>
    <div class="field"><span class="label">Pick location</span><span class="value">{{H(pkg.ClientPickupLocation)}}</span></div>
    <div class="field"><span class="label">Drop location</span><span class="value">{{H(pkg.ClientDropLocation)}}</span></div>
  </section>

  <h2 class="section-title">Day wise Summary</h2>
  {{summaryHtml}}

  <section class="hotel-section">
    <h2 class="section-title">Hotel Details</h2>
    {{hotelDetailsHtml}}
  </section>
</body>
</html>
""";
    }

    private static string BuildHotelDetailsHtml(TourPackage pkg)
    {
        var hotels = (pkg.DayWiseItinerary ?? [])
            .Where(d => d.Hotel != null)
            .GroupBy(d => d.HotelId)
            .Select(g =>
            {
                var first = g.OrderBy(d => d.DayNumber).First();
                return new { Hotel = first.Hotel!, Days = g.Select(d => d.DayNumber).OrderBy(x => x).ToList() };
            })
            .ToList();

        if (hotels.Count == 0)
            return "<p>No hotel details added.</p>";

        return string.Join("", hotels.Select(x =>
        {
            var h = x.Hotel;
            var address = string.Join(", ", new[] { h.Address, h.City, h.State, h.Pincode }
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim()));
            if (string.IsNullOrWhiteSpace(address)) address = "—";
            var contact = string.Join(" | ", new[] { h.PhoneNumber, h.Email }
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim()));
            if (string.IsNullOrWhiteSpace(contact)) contact = "—";
            var dayText = string.Join(", ", x.Days.Select(d => $"Day {d}"));

            return $"""
                <section class="hotel-card">
                  <div class="hotel-head">
                    <h3>{H(h.Name)}</h3>
                    <span>{H(h.IsHouseboat ? "Houseboat" : "Hotel")} | {H(dayText)}</span>
                  </div>
                  <div class="hotel-row"><strong>Address:</strong> {H(address)}</div>
                  <div class="hotel-row"><strong>Contact:</strong> {H(contact)}</div>
                </section>
                """;
        }));
    }

    private static string BuildTourPlannerHtml(Reservation reservation, Tenant? tenant, string? agencyLogoUrl)
    {
        var pkg = reservation.Package;
        var india = CultureInfo.GetCultureInfo("en-IN");
        string FmtDate(DateTime value) => value.ToString("d MMM yyyy", india);
        var finalAmount = Math.Max(0, pkg.TotalAmount - pkg.Discount);
        var agencyName = string.IsNullOrWhiteSpace(tenant?.Name) ? "Travel Pathways" : tenant!.Name.Trim();
        var agencyContact = string.Join(" | ", new[] { tenant?.Phone, tenant?.Email }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim()));
        if (string.IsNullOrWhiteSpace(agencyContact))
            agencyContact = tenant?.Address?.Trim() ?? "—";

        var daysHtml = string.Join("", (pkg.DayWiseItinerary ?? [])
            .OrderBy(d => d.DayNumber)
            .Select(d =>
            {
                var activityTitle = d.ItineraryTemplate?.Title?.Trim();
                if (string.IsNullOrWhiteSpace(activityTitle))
                    activityTitle = d.Activities?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a))?.Trim();
                if (string.IsNullOrWhiteSpace(activityTitle))
                    activityTitle = $"Day {d.DayNumber} activity";

                var activityLines = new List<string>();
                if (d.Activities?.Count > 0)
                    activityLines.Add("Activities: " + string.Join(", ", d.Activities.Where(a => !string.IsNullOrWhiteSpace(a))));
                if (!string.IsNullOrWhiteSpace(d.Notes))
                    activityLines.Add(d.Notes.Trim());

                var activityDetailsHtml = activityLines.Count == 0
                    ? "<p>No activity details added.</p>"
                    : string.Join("", activityLines.Select(line => $"<p>{H(line)}</p>"));

                return $"""
                    <section class="day-card">
                      <div class="day-head">
                        <span class="day-pill">Day {d.DayNumber}</span>
                        <div>
                          <h2>{H(FmtDate(d.Date))}</h2>
                          <p>Activity planner</p>
                        </div>
                      </div>
                      <div class="activity-box">
                        <span class="activity-label">Day-wise activity</span>
                        <h3>{H(activityTitle)}</h3>
                        {activityDetailsHtml}
                      </div>
                    </section>
                    """;
            }));

        var logoHtml = string.IsNullOrWhiteSpace(agencyLogoUrl)
            ? ""
            : $"<img class=\"agency-logo\" src=\"{H(agencyLogoUrl)}\" alt=\"Agency logo\" />";

        return $$"""
<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <style>
    * { box-sizing: border-box; }
    body { margin: 0; font-family: Arial, Helvetica, sans-serif; color: #1f2937; background: #ffffff; font-size: 12px; }
    .doc-header { display: flex; justify-content: space-between; align-items: center; gap: 18px; padding: 18px 20px; border: 1px solid #dbeafe; border-top: 6px solid #0f3d5e; border-radius: 14px; background: #f8fafc; margin-bottom: 18px; }
    .agency-logo { max-height: 68px; max-width: 210px; object-fit: contain; }
    .title-block { text-align: right; }
    .brand-line { margin: 0; font-size: 12px; font-weight: 800; color: #0f766e; letter-spacing: .12em; text-transform: uppercase; }
    h1 { margin: 2px 0 0; font-size: 32px; line-height: 1.15; color: #0f172a; }
    .subtitle { margin: 8px 0 0; font-size: 13px; opacity: .9; }
    .summary { display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; margin-bottom: 18px; }
    .tour-summary { grid-template-areas: "arrival departure" "pickup drop" "guests vehicle"; }
    .tour-arrival { grid-area: arrival; }
    .tour-departure { grid-area: departure; }
    .tour-pickup { grid-area: pickup; }
    .tour-drop { grid-area: drop; }
    .tour-guests { grid-area: guests; }
    .tour-vehicle { grid-area: vehicle; }
    .summary-card { border: 1px solid #e5e7eb; border-left: 4px solid #0ea5e9; border-radius: 12px; padding: 12px; background: #ffffff; }
    .label { display: block; font-size: 10px; text-transform: uppercase; letter-spacing: .08em; color: #6b7280; margin-bottom: 4px; }
    .value { font-size: 13px; font-weight: 700; color: #111827; }
    .cost-row { display: flex; justify-content: space-between; gap: 12px; border-top: 1px solid #e5e7eb; padding-top: 7px; margin-top: 7px; }
    .cost-row:first-child { border-top: 0; padding-top: 0; margin-top: 0; }
    .day-card { break-inside: avoid; border: 1px solid #e2e8f0; border-radius: 14px; padding: 14px; margin-bottom: 12px; background: #ffffff; }
    .day-head { display: flex; gap: 12px; align-items: flex-start; margin-bottom: 10px; }
    .day-pill { flex: 0 0 auto; background: #0f3d5e; color: white; border-radius: 8px; padding: 8px 10px; font-weight: 800; }
    .section-title { margin: 20px 0 12px; font-size: 17px; color: #0f3d5e; border-bottom: 1px solid #cbd5e1; padding-bottom: 7px; }
    h2 { margin: 0 0 3px; font-size: 16px; color: #172554; }
    h3 { margin: 4px 0 8px; font-size: 17px; color: #111827; }
    .day-head p { margin: 0; color: #64748b; }
    .activity-box { background: #f8fafc; border: 1px solid #e2e8f0; border-left: 4px solid #f59e0b; border-radius: 12px; padding: 12px; margin-bottom: 10px; }
    .activity-label { display: block; font-size: 10px; text-transform: uppercase; letter-spacing: .1em; color: #b45309; font-weight: 800; margin-bottom: 5px; }
    .activity-box p { color: #334155; }
    .detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 6px; margin: 8px 0; }
    .detail-grid span { background: #eff6ff; border-radius: 8px; padding: 7px 8px; }
    p { margin: 8px 0 0; line-height: 1.5; }
    .agency-footer { margin-top: 20px; padding: 12px 14px; border-top: 2px solid #bfdbfe; background: #f8fafc; display: flex; justify-content: space-between; gap: 16px; color: #334155; font-size: 12px; }
  </style>
</head>
<body>
  <section class="doc-header">
    <div>{{logoHtml}}</div>
    <div class="title-block">
      <p class="brand-line">Travel Pathways</p>
      <h1>Tour Planner</h1>
    </div>
  </section>

  <h2 class="section-title">Client Details</h2>
  <section class="summary">
    <div class="summary-card"><span class="label">Client</span><span class="value">{{H(pkg.ClientName)}}</span></div>
  </section>

  <h2 class="section-title">Tour Details</h2>
  <section class="summary tour-summary">
    <div class="summary-card tour-arrival"><span class="label">Arrival Date</span><span class="value">{{H(FmtDate(pkg.StartDate))}}</span></div>
    <div class="summary-card tour-departure"><span class="label">Departure Date</span><span class="value">{{H(FmtDate(pkg.EndDate))}}</span></div>
    <div class="summary-card tour-pickup"><span class="label">Pickup</span><span class="value">{{H(pkg.ClientPickupLocation)}}</span></div>
    <div class="summary-card tour-drop"><span class="label">Drop</span><span class="value">{{H(pkg.ClientDropLocation)}}</span></div>
    <div class="summary-card tour-guests"><span class="label">Guests</span><span class="value">{{pkg.NumberOfAdults}} adults, {{pkg.NumberOfChildren}} children</span></div>
    <div class="summary-card tour-vehicle"><span class="label">Vehicle</span><span class="value">{{H(pkg.Vehicle == null ? "Not selected" : (string.IsNullOrWhiteSpace(pkg.Vehicle.VehicleModel) ? pkg.Vehicle.VehicleType.ToString() : pkg.Vehicle.VehicleType + " - " + pkg.Vehicle.VehicleModel))}}</span></div>
  </section>

  <section class="summary-card" style="margin-bottom:18px;">
    <div class="cost-row"><span>Total package cost</span><strong>{{H(FormatMoney(pkg.TotalAmount))}}</strong></div>
    <div class="cost-row"><span>Advance / Balance</span><strong>{{H(FormatMoney(pkg.AdvanceAmount))}} / {{H(FormatMoney(Math.Max(0, finalAmount - pkg.AdvanceAmount)))}}</strong></div>
  </section>

  <h2 class="section-title">Day-wise Activities</h2>
  {{daysHtml}}

  <section class="agency-footer">
    <div><strong>Agency Name:</strong> {{H(agencyName)}}</div>
    <div><strong>Agency Contact:</strong> {{H(agencyContact)}}</div>
  </section>
</body>
</html>
""";
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
            .Where(p => p.TenantId == tenantId && p.Id == dto.PackageId)
            .FirstOrDefaultAsync(ct);
        if (package == null)
            return BadRequest(ApiResponse<ReservationDetailDto>.Fail("Package not found."));

        var (currentUserId, email, isAdmin, _) = GetCurrentUserReservationScope();
        if (!await CanUserAccessPackageAsync(package, isAdmin, email, currentUserId, tenantId, ct))
            return Forbid();

        await EnsurePackageConfirmedAsync(package, tenantId, ct);
        if (package.Status != PackageStatus.Confirmed)
            return BadRequest(ApiResponse<ReservationDetailDto>.Fail(
                "Package is not confirmed. Mark the lead or package as Confirmed before sending for reservation."));

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

        var status = dto.Status ?? ReservationStatus.Pending;
        var reservation = new Reservation
        {
            TenantId = tenantId,
            PackageId = dto.PackageId,
            AssignedToUserId = dto.AssignedToUserId,
            AssignedByUserId = currentUserId,
            Status = status,
            IsLocked = status == ReservationStatus.Completed
        };
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();

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

        if (reservation.IsLocked)
            return BadRequest(ApiResponse<ReservationDetailDto>.Fail("This reservation is locked. Ask an admin to unlock it before editing."));

        var oldStatus = reservation.Status;
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
        if (dto.Status.HasValue && oldStatus != dto.Status.Value)
            reservation.IsLocked = dto.Status.Value == ReservationStatus.Completed;

        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();

        return await GetReservation(id, ct);
    }

    [HttpPost("{id:guid}/unlock")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<ReservationDetailDto>>> UnlockReservation(Guid id, CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;

        var reservation = await _db.Reservations
            .Where(r => r.TenantId == TenantId && r.Id == id)
            .FirstOrDefaultAsync(ct);
        if (reservation == null)
            return NotFound(ApiResponse<ReservationDetailDto>.Fail("Reservation not found."));

        reservation.IsLocked = false;
        if (reservation.Status == ReservationStatus.Completed)
            reservation.Status = ReservationStatus.InProcess;
        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();

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
        if (reservation.IsLocked)
            return BadRequest(ApiResponse<ReservationDayCompletionDto>.Fail("This reservation is locked. Ask an admin to unlock it before editing."));

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

    [HttpPatch("{id:guid}/hotel-bookings/{bookingId:guid}")]
    public async Task<ActionResult<ApiResponse<ReservationHotelBookingDto>>> UpdateHotelBooking(Guid id, Guid bookingId, [FromBody] UpsertReservationHotelBookingRequest dto, CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        var (currentUserId, _, isAdmin, isReservationRole) = GetCurrentUserReservationScope();
        var tenantId = TenantId;
        var booking = await _db.ReservationHotelBookings
            .Include(b => b.Reservation)
            .Include(b => b.Documents)
            .Where(b => b.TenantId == tenantId && b.ReservationId == id && b.Id == bookingId)
            .FirstOrDefaultAsync(ct);
        if (booking == null)
            return NotFound(ApiResponse<ReservationHotelBookingDto>.Fail("Hotel booking not found."));
        if (isReservationRole && currentUserId.HasValue && booking.Reservation.AssignedToUserId != currentUserId.Value)
            return NotFound(ApiResponse<ReservationHotelBookingDto>.Fail("Hotel booking not found."));
        if (booking.Reservation.IsLocked)
            return BadRequest(ApiResponse<ReservationHotelBookingDto>.Fail("This reservation is locked. Ask an admin to unlock it before editing."));
        if (booking.IsLocked)
            return BadRequest(ApiResponse<ReservationHotelBookingDto>.Fail("This booking is locked. Ask an admin to unlock it before editing."));

        if (!isReservationRole)
        {
            if (dto.BookingDate.HasValue) booking.BookingDate = AsUtcDate(dto.BookingDate.Value);
            booking.CheckInDate = AsUtcDate(dto.CheckInDate);
            booking.CheckOutDate = AsUtcDate(dto.CheckOutDate);
            if (dto.HotelName != null) booking.HotelName = dto.HotelName.Trim();
            if (dto.IsHouseboat.HasValue) booking.IsHouseboat = dto.IsHouseboat.Value;
            if (dto.RoomType != null) booking.RoomType = dto.RoomType.Trim();
            if (dto.NumberOfRooms.HasValue) booking.NumberOfRooms = Math.Max(0, dto.NumberOfRooms.Value);
            if (dto.ExtraBedCount.HasValue) booking.ExtraBedCount = Math.Max(0, dto.ExtraBedCount.Value);
            if (dto.CnbCount.HasValue) booking.CnbCount = Math.Max(0, dto.CnbCount.Value);
            if (dto.NumberOfPersons.HasValue) booking.NumberOfPersons = Math.Max(0, dto.NumberOfPersons.Value);
        }
        if (dto.RatePerNight.HasValue) booking.RatePerNight = Math.Max(0, dto.RatePerNight.Value);
        if (dto.ExtraBedRate.HasValue) booking.ExtraBedRate = Math.Max(0, dto.ExtraBedRate.Value);
        if (dto.CnbRate.HasValue) booking.CnbRate = Math.Max(0, dto.CnbRate.Value);
        if (dto.TotalAmount.HasValue) booking.TotalAmount = Math.Max(0, dto.TotalAmount.Value);
        if (dto.AdvancePaid.HasValue) booking.AdvancePaid = Math.Max(0, dto.AdvancePaid.Value);
        booking.BalanceAmount = Math.Max(0, dto.BalanceAmount ?? (booking.TotalAmount - booking.AdvancePaid));
        if (dto.Status.HasValue)
        {
            if (dto.Status.Value == ReservationHotelBookingStatus.Cancelled)
                return BadRequest(ApiResponse<ReservationHotelBookingDto>.Fail("Use the cancel booking action and provide a cancellation reason."));
            booking.Status = dto.Status.Value;
        }
        if (booking.Status == ReservationHotelBookingStatus.Confirmed) booking.IsLocked = true;
        if (dto.ConfirmationNumber != null) booking.ConfirmationNumber = dto.ConfirmationNumber.Trim();
        if (dto.Notes != null) booking.Notes = dto.Notes.Trim();

        await _db.SaveChangesAsync(ct);
        return ApiResponse<ReservationHotelBookingDto>.Ok(ToHotelBookingDto(booking));
    }

    [HttpPost("{id:guid}/hotel-bookings/{bookingId:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<ReservationHotelBookingDto>>> CancelHotelBooking(
        Guid id,
        Guid bookingId,
        [FromBody] CancelReservationHotelBookingRequest dto,
        CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        var (currentUserId, _, isAdmin, isReservationRole) = GetCurrentUserReservationScope();
        if (!isAdmin && !isReservationRole)
            return Forbid();

        var tenantId = TenantId;
        var booking = await _db.ReservationHotelBookings
            .Include(b => b.Reservation)
            .ThenInclude(r => r.Package)
            .Include(b => b.Documents)
            .Where(b => b.TenantId == tenantId && b.ReservationId == id && b.Id == bookingId)
            .FirstOrDefaultAsync(ct);
        if (booking == null)
            return NotFound(ApiResponse<ReservationHotelBookingDto>.Fail("Hotel booking not found."));
        if (isReservationRole && currentUserId.HasValue && booking.Reservation.AssignedToUserId != currentUserId.Value)
            return NotFound(ApiResponse<ReservationHotelBookingDto>.Fail("Hotel booking not found."));
        if (booking.Status == ReservationHotelBookingStatus.Cancelled)
            return BadRequest(ApiResponse<ReservationHotelBookingDto>.Fail("This booking is already cancelled."));

        if (dto.Reason == ReservationHotelBookingCancellationReason.Other &&
            string.IsNullOrWhiteSpace(dto.ReasonDetail))
            return BadRequest(ApiResponse<ReservationHotelBookingDto>.Fail("Please describe the cancellation reason."));

        booking.Status = ReservationHotelBookingStatus.Cancelled;
        booking.CancellationReason = dto.Reason;
        booking.CancellationReasonDetail = dto.Reason == ReservationHotelBookingCancellationReason.Other
            ? dto.ReasonDetail!.Trim()
            : null;
        booking.IsLocked = true;

        if (dto.Reason == ReservationHotelBookingCancellationReason.PackageCancelled)
        {
            var package = booking.Reservation.Package;
            if (package != null)
                await SyncPackageCancelledAsync(package.Id, package.LeadId, tenantId, ct);
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<ReservationHotelBookingDto>.Ok(ToHotelBookingDto(booking));
    }

    [HttpPost("{id:guid}/hotel-bookings/{bookingId:guid}/unlock")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<ReservationHotelBookingDto>>> UnlockHotelBooking(Guid id, Guid bookingId, CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;

        var booking = await _db.ReservationHotelBookings
            .Include(b => b.Documents)
            .Where(b => b.TenantId == TenantId && b.ReservationId == id && b.Id == bookingId)
            .FirstOrDefaultAsync(ct);
        if (booking == null)
            return NotFound(ApiResponse<ReservationHotelBookingDto>.Fail("Hotel booking not found."));

        booking.IsLocked = false;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<ReservationHotelBookingDto>.Ok(ToHotelBookingDto(booking));
    }

    [HttpPost("{id:guid}/hotel-bookings/{bookingId:guid}/documents")]
    public async Task<ActionResult<ApiResponse<ReservationHotelBookingDocumentDto>>> UploadHotelBookingDocument(
        Guid id,
        Guid bookingId,
        IFormFile? file,
        [FromQuery] ReservationHotelBookingDocumentType type = ReservationHotelBookingDocumentType.PaymentProof,
        [FromQuery] decimal? amount = null,
        [FromQuery] DateTime? paymentDate = null,
        CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;
        if (file == null)
            return BadRequest(ApiResponse<ReservationHotelBookingDocumentDto>.Fail("No file provided."));

        var (currentUserId, _, isAdmin, isReservationRole) = GetCurrentUserReservationScope();
        var tenantId = TenantId;
        var booking = await _db.ReservationHotelBookings
            .Include(b => b.Reservation)
            .Where(b => b.TenantId == tenantId && b.ReservationId == id && b.Id == bookingId)
            .FirstOrDefaultAsync(ct);
        if (booking == null)
            return NotFound(ApiResponse<ReservationHotelBookingDocumentDto>.Fail("Hotel booking not found."));
        if (isReservationRole && currentUserId.HasValue && booking.Reservation.AssignedToUserId != currentUserId.Value)
            return NotFound(ApiResponse<ReservationHotelBookingDocumentDto>.Fail("Hotel booking not found."));
        if (booking.Reservation.IsLocked)
            return BadRequest(ApiResponse<ReservationHotelBookingDocumentDto>.Fail("This reservation is locked. Ask an admin to unlock it before editing."));
        if (booking.IsLocked)
            return BadRequest(ApiResponse<ReservationHotelBookingDocumentDto>.Fail("This booking is locked. Ask an admin to unlock it before editing."));

        var url = await _storage.SaveReservationScreenshotAsync(tenantId, id, file, ct);
        var document = new ReservationHotelBookingDocument
        {
            ReservationHotelBookingId = bookingId,
            Type = type,
            Amount = amount,
            PaymentDate = AsUtcDate(paymentDate),
            FileUrl = url,
            FileName = file.FileName
        };
        _db.ReservationHotelBookingDocuments.Add(document);
        await _db.SaveChangesAsync(ct);

        return ApiResponse<ReservationHotelBookingDocumentDto>.Ok(new ReservationHotelBookingDocumentDto
        {
            Id = document.Id.ToString("D"),
            Type = document.Type.ToString(),
            Amount = document.Amount,
            PaymentDate = document.PaymentDate,
            FileUrl = document.FileUrl,
            FileName = document.FileName,
            CreatedAt = document.CreatedAt
        });
    }

    [HttpDelete("{id:guid}/hotel-bookings/{bookingId:guid}/documents/{documentId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteHotelBookingDocument(Guid id, Guid bookingId, Guid documentId, CancellationToken ct = default)
    {
        var check = await EnsureReservationsModuleAsync(ct);
        if (check != null) return check;

        var (currentUserId, _, isAdmin, isReservationRole) = GetCurrentUserReservationScope();
        var tenantId = TenantId;
        var document = await _db.ReservationHotelBookingDocuments
            .Include(d => d.ReservationHotelBooking)
            .ThenInclude(b => b.Reservation)
            .Where(d =>
                d.Id == documentId &&
                d.ReservationHotelBookingId == bookingId &&
                d.ReservationHotelBooking.TenantId == tenantId &&
                d.ReservationHotelBooking.ReservationId == id)
            .FirstOrDefaultAsync(ct);
        if (document == null)
            return NotFound(ApiResponse<object>.Fail("Booking proof not found."));
        if (isReservationRole && currentUserId.HasValue && document.ReservationHotelBooking.Reservation.AssignedToUserId != currentUserId.Value)
            return NotFound(ApiResponse<object>.Fail("Booking proof not found."));
        if (document.ReservationHotelBooking.Reservation.IsLocked)
            return BadRequest(ApiResponse<object>.Fail("This reservation is locked. Ask an admin to unlock it before editing."));
        if (document.ReservationHotelBooking.IsLocked)
            return BadRequest(ApiResponse<object>.Fail("This booking is locked. Ask an admin to unlock it before editing."));

        _db.ReservationHotelBookingDocuments.Remove(document);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
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
        if (reservation.IsLocked)
            return BadRequest(ApiResponse<ReservationScreenshotDto>.Fail("This reservation is locked. Ask an admin to unlock it before editing."));

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
    [Authorize(Policy = "TenantAdminOnly")]
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
        if (screenshot.Reservation.IsLocked)
            return BadRequest(ApiResponse<object>.Fail("This reservation is locked. Ask an admin to unlock it before editing."));

        _db.ReservationPaymentScreenshots.Remove(screenshot);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }
}
