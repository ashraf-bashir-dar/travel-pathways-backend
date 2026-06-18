using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;
using TravelPathways.Api.Localization;
using TravelPathways.Api.Services;
using System.Globalization;
using System.Text.Json;
using Npgsql;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/packages")]
public sealed class PackagesController : TenantControllerBase
{
    private static readonly JsonSerializerOptions PackageLogJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _db;
    private readonly IPackagePdfGenerator _pdfGenerator;
    private readonly IPdfTemplateHtmlCache _pdfTemplateHtmlCache;
    private readonly IPackageMasterDataService _packageMasterData;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;

    public PackagesController(
        AppDbContext db,
        TenantContext tenant,
        IPackagePdfGenerator pdfGenerator,
        IPdfTemplateHtmlCache pdfTemplateHtmlCache,
        IPackageMasterDataService packageMasterData,
        IConfiguration configuration,
        IWebHostEnvironment env) : base(tenant)
    {
        _db = db;
        _pdfGenerator = pdfGenerator;
        _pdfTemplateHtmlCache = pdfTemplateHtmlCache;
        _packageMasterData = packageMasterData;
        _configuration = configuration;
        _env = env;
    }

    private static int DaysInclusive(DateTime start, DateTime end)
    {
        var s = start.Date;
        var e = end.Date;
        var days = (e - s).Days + 1;
        return Math.Max(1, days);
    }

    /// <summary>Returns (current user email for CreatedBy match, can see all packages in tenant). Only Admin/SuperAdmin sees all.</summary>
    private (string? CurrentUserEmail, bool CanSeeAllPackages) GetCurrentUserPackageScope()
    {
        if (IsTenantAdmin())
            return (null, true);

        var email = User.FindFirstValue(ClaimTypes.Email);
        return (string.IsNullOrWhiteSpace(email) ? null : email.Trim(), false);
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>Packages the user created or packages for leads assigned to them (sales, reservation, tour manager).</summary>
    private IQueryable<TourPackage> ApplyPackageOwnershipFilter(
        IQueryable<TourPackage> query,
        bool canSeeAll,
        string? email,
        Guid? userId)
    {
        if (canSeeAll) return query;
        if (!userId.HasValue && string.IsNullOrEmpty(email))
            return query.Where(_ => false);

        return query.Where(p =>
            (!string.IsNullOrEmpty(email) && p.CreatedBy.ToLower() == email!.ToLower()) ||
            (userId.HasValue &&
             p.LeadId != null &&
             _db.Leads.Any(l =>
                 l.Id == p.LeadId &&
                 l.TenantId == TenantId &&
                 !l.IsDeleted &&
                 l.AssignedToUserId == userId.Value)));
    }

    public sealed class DayItineraryDto
    {
        public required string Id { get; init; }
        public required string PackageId { get; init; }
        public required int DayNumber { get; init; }
        public required DateTime Date { get; init; }
        public string? HotelId { get; init; }
        public string? HotelName { get; init; }
        public string? RoomType { get; init; }
        public required int NumberOfRooms { get; init; }
        public string? CheckInTime { get; init; }
        public string? CheckOutTime { get; init; }
        public AccommodationMealPlan? MealPlan { get; init; }
        public int? ExtraBedCount { get; init; }
        public int? CnbCount { get; init; }
        public List<string>? Activities { get; init; }
        public List<string>? Meals { get; init; }
        public string? Notes { get; init; }
        [JsonPropertyName("templateId")]
        public string? TemplateId { get; init; }
        [JsonPropertyName("templateTitle")]
        public string? TemplateTitle { get; init; }
        public required decimal HotelCost { get; init; }
    }

    public sealed class PackageDto
    {
        public required string Id { get; init; }
        public string? LeadId { get; init; }
        public required string ClientName { get; init; }
        public required string ClientPhone { get; init; }
        public string? ClientEmail { get; init; }
        public string? ClientCity { get; init; }
        public string? ClientState { get; init; }
        public required string ClientPickupLocation { get; init; }
        public required string ClientDropLocation { get; init; }
        public required string PackageName { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
        public required int NumberOfDays { get; init; }
        public required int NumberOfAdults { get; init; }
        public required int NumberOfChildren { get; init; }
        public string? VehicleId { get; init; }
        public string? VehicleName { get; init; }
        public decimal? VehicleRate { get; init; }
        public required decimal TotalAmount { get; init; }
        public decimal MarginAmount { get; init; }
        public decimal Discount { get; init; }
        public decimal FinalAmount { get; init; }
        public required decimal AdvanceAmount { get; init; }
        public required decimal BalanceAmount { get; init; }
        public required PackageStatus Status { get; init; }
        public bool IsLocked { get; init; }
        public List<string>? InclusionIds { get; init; }
        public List<string>? ExclusionIds { get; init; }
        public List<DayItineraryDto>? DayWiseItinerary { get; init; }
        public required string TenantId { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
        public required string CreatedBy { get; init; }
        /// <summary>True when any reservation exists for this package in the tenant.</summary>
        public bool HasReservation { get; set; }
        /// <summary>How many package records exist for this lead (including older revisions).</summary>
        public int LeadPackageVersionCount { get; set; } = 1;
        /// <summary>How many package log entries exist for this lead (each create/update sent to the client).</summary>
        public int PackageLogCount { get; set; }
    }

    public sealed class CreateDayItineraryRequestDto
    {
        public int DayNumber { get; set; }
        public DateTime Date { get; set; }
        public Guid? HotelId { get; set; }
        public string? RoomType { get; set; }
        public int NumberOfRooms { get; set; }
        public string? CheckInTime { get; set; }
        public string? CheckOutTime { get; set; }
        public AccommodationMealPlan? MealPlan { get; set; }
        public int? ExtraBedCount { get; set; }
        public int? CnbCount { get; set; }
        public List<string>? Activities { get; set; }
        public List<string>? Meals { get; set; }
        public string? Notes { get; set; }
        public Guid? ItineraryTemplateId { get; set; }
        public decimal HotelCost { get; set; }
    }

    public class CreatePackageRequestDto
    {
        public required Guid LeadId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public string? ClientEmail { get; set; }
        public string? ClientCity { get; set; }
        public string? ClientState { get; set; }
        public string ClientPickupLocation { get; set; } = string.Empty;
        public string ClientDropLocation { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int NumberOfAdults { get; set; }
        public int NumberOfChildren { get; set; }
        public Guid? VehicleId { get; set; }
        public List<CreateDayItineraryRequestDto> DayWiseItinerary { get; set; } = [];
        public List<string>? InclusionIds { get; set; }
        public List<string>? ExclusionIds { get; set; }
        public decimal TotalAmount { get; set; }
        /// <summary>Optional margin (INR) stored for PDF / reporting when using price override.</summary>
        public decimal MarginAmount { get; set; }
        public decimal Discount { get; set; }
    }

    public sealed class UpdatePackageRequestDto : CreatePackageRequestDto
    {
        public new decimal TotalAmount { get; set; }
        public new decimal Discount { get; set; }
        public decimal AdvanceAmount { get; set; }
        /// <summary>Package status. When set, the lead's status and all packages for that lead are synced to this value.</summary>
        public PackageStatus Status { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<PackageDto>>>> GetPackages(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? searchTerm = null,
        [FromQuery] PackageStatus? status = null,
        [FromQuery] DateTime? arrivalDateFrom = null,
        [FromQuery] DateTime? arrivalDateTo = null,
        [FromQuery] Guid? assignedToUserId = null,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var (currentUserEmail, canSeeAllPackages) = GetCurrentUserPackageScope();
        var userId = GetCurrentUserId();

        var query = ApplyPackageOwnershipFilter(
            _db.Packages.AsNoTracking()
                .Include(p => p.DayWiseItinerary)
                .Include(p => p.Vehicle)
                .Where(p => p.TenantId == TenantId),
            canSeeAllPackages,
            currentUserEmail,
            userId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var s = searchTerm.Trim().ToLower();
            query = query.Where(p =>
                p.ClientName.ToLower().Contains(s) ||
                p.ClientPhone.ToLower().Contains(s) ||
                p.PackageName.ToLower().Contains(s));
        }

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (arrivalDateFrom.HasValue)
        {
            var d = arrivalDateFrom.Value.Date;
            var startUtc = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
            query = query.Where(p => p.StartDate >= startUtc);
        }
        if (arrivalDateTo.HasValue)
        {
            var d = arrivalDateTo.Value.Date;
            var endUtcExclusive = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
            query = query.Where(p => p.StartDate < endUtcExclusive);
        }

        if (assignedToUserId.HasValue)
        {
            query = query.Where(p =>
                p.LeadId != null &&
                _db.Leads.Any(l =>
                    l.Id == p.LeadId &&
                    l.TenantId == TenantId &&
                    !l.IsDeleted &&
                    l.AssignedToUserId == assignedToUserId.Value));
        }

        // List page shows only the latest package per lead; older revisions stay in DB and appear in package logs.
        var latestPackageIds = await query
            .Where(p => p.LeadId != null)
            .GroupBy(p => p.LeadId!.Value)
            .Select(g => g.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).First().Id)
            .ToListAsync(ct);
        var latestIdSet = latestPackageIds.ToHashSet();
        query = query.Where(p => p.LeadId == null || latestIdSet.Contains(p.Id));

        var total = await query.CountAsync(ct);
        var list = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var leadIds = list.Where(p => p.LeadId.HasValue).Select(p => p.LeadId!.Value).Distinct().ToList();
        var versionCounts = leadIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _db.Packages.AsNoTracking()
                .Where(p => p.TenantId == TenantId && p.LeadId != null && leadIds.Contains(p.LeadId.Value))
                .GroupBy(p => p.LeadId!.Value)
                .Select(g => new { LeadId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.LeadId, x => x.Count, ct);

        var packageLogCounts = await GetPackageLogCountsByLeadAsync(leadIds, ct);

        var items = list.Select(p =>
        {
            var dto = ToDto(p);
            if (p.LeadId.HasValue)
            {
                if (versionCounts.TryGetValue(p.LeadId.Value, out var count))
                    dto.LeadPackageVersionCount = count;
                dto.PackageLogCount = packageLogCounts.GetValueOrDefault(p.LeadId.Value);
            }
            return dto;
        }).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return ApiResponse<PaginatedResponse<PackageDto>>.Ok(new PaginatedResponse<PackageDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    /// <summary>Package change history for a lead (auto-logged on create/update).</summary>
    [HttpGet("logs/by-lead/{leadId:guid}")]
    public async Task<ActionResult<ApiResponse<List<PackageLogDto>>>> GetPackageLogsByLead(
        [FromRoute] Guid leadId,
        CancellationToken ct = default)
    {
        if (!await CanViewLeadPackageLogsAsync(leadId, ct))
            return NotFound(ApiResponse<List<PackageLogDto>>.Fail("Lead not found."));

        try
        {
            var logs = await _db.PackageLogs.AsNoTracking()
                .Where(l => l.TenantId == TenantId && l.LeadId == leadId)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync(ct);

            return ApiResponse<List<PackageLogDto>>.Ok(logs.Select(ToPackageLogDto).ToList());
        }
        catch (PostgresException ex) when (IsPackageLogSchemaException(ex))
        {
            return ApiResponse<List<PackageLogDto>>.Ok([]);
        }
        catch (Exception)
        {
            return ApiResponse<List<PackageLogDto>>.Ok([]);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PackageDto>>> GetPackageById([FromRoute] Guid id, CancellationToken ct)
    {
        var (currentUserEmail, canSeeAllPackages) = GetCurrentUserPackageScope();

        var pkg = await _db.Packages.AsNoTracking()
            .Include(p => p.DayWiseItinerary)
            .ThenInclude(d => d.Hotel)
            .Include(p => p.DayWiseItinerary)
            .ThenInclude(d => d.ItineraryTemplate)
            .Include(p => p.Vehicle)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);

        if (pkg is null) return NotFound(ApiResponse<PackageDto>.Fail("Package not found"));
        if (!canSeeAllPackages)
        {
            var isCreator = !string.IsNullOrWhiteSpace(currentUserEmail) &&
                            string.Equals(pkg.CreatedBy.Trim(), currentUserEmail.Trim(), StringComparison.OrdinalIgnoreCase);
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isLeadAssignee = Guid.TryParse(userIdClaim, out var uid) &&
                                 pkg.LeadId.HasValue &&
                                 await _db.Leads.AsNoTracking().AnyAsync(
                                     l => l.Id == pkg.LeadId &&
                                          l.TenantId == TenantId &&
                                          !l.IsDeleted &&
                                          l.AssignedToUserId == uid,
                                     ct);
            if (!isCreator && !isLeadAssignee)
                return NotFound(ApiResponse<PackageDto>.Fail("Package not found"));
        }
        var hasReservation = await _db.Reservations.AsNoTracking()
            .AnyAsync(r => r.TenantId == TenantId && r.PackageId == pkg.Id, ct);
        var dto = ToDto(pkg);
        dto.HasReservation = hasReservation;
        return ApiResponse<PackageDto>.Ok(dto);
    }

    [HttpGet("by-lead/{leadId:guid}")]
    public async Task<ActionResult<ApiResponse<List<PackageDto>>>> GetByLead([FromRoute] Guid leadId, CancellationToken ct)
    {
        var (currentUserEmail, canSeeAllPackages) = GetCurrentUserPackageScope();
        var userId = GetCurrentUserId();

        var query = ApplyPackageOwnershipFilter(
            _db.Packages.AsNoTracking()
                .Include(p => p.DayWiseItinerary)
                .Include(p => p.Vehicle)
                .Where(p => p.TenantId == TenantId && p.LeadId == leadId),
            canSeeAllPackages,
            currentUserEmail,
            userId);

        var pkgs = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        var dtos = pkgs.Select(ToDto).ToList(); // HasReservation left default (false) for this list.
        return ApiResponse<List<PackageDto>>.Ok(dtos);
    }

    public sealed class PackageLogDto
    {
        public required string Id { get; init; }
        public required string LeadId { get; init; }
        public required string PackageId { get; init; }
        public required PackageLogAction Action { get; init; }
        public required string PackageName { get; init; }
        public required decimal FinalAmount { get; init; }
        public required decimal MarginAmount { get; init; }
        public required PackageStatus Status { get; init; }
        public required DateTime LoggedAt { get; init; }
        public string? ChangedByUserId { get; init; }
        public required string ChangedByDisplayName { get; init; }
        public PackageLogSnapshot? Snapshot { get; init; }
        public string? SnapshotJson { get; init; }
    }

    private async Task<bool> CanViewLeadPackageLogsAsync(Guid leadId, CancellationToken ct)
    {
        if (!await TenantModuleResolver.HasModuleAsync(_db, User, TenantId, AppModuleKey.Leads, ct))
            return false;

        var lead = await _db.Leads.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leadId && l.TenantId == TenantId && !l.IsDeleted, ct);
        if (lead is null) return false;

        if (IsTenantAdmin()) return true;

        var userId = GetCurrentUserId();
        return userId.HasValue && lead.AssignedToUserId == userId.Value;
    }

    private async Task AppendPackageLogAsync(
        TourPackage pkg,
        PackageLogAction action,
        CancellationToken ct)
    {
        if (!pkg.LeadId.HasValue) return;

        var leadRowExists = await _db.Leads.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(l => l.Id == pkg.LeadId.Value && l.TenantId == TenantId, ct);
        if (!leadRowExists) return;

        try
        {
            var userId = GetCurrentUserId();
            var email = User.FindFirstValue(ClaimTypes.Email);
            var changedBy = await ResolveChangedByDisplayNameAsync(userId, email, ct);
            var snapshotJson = await BuildPackageLogSnapshotJsonAsync(pkg, ct);

            _db.PackageLogs.Add(new PackageLog
            {
                TenantId = TenantId,
                LeadId = pkg.LeadId.Value,
                PackageId = pkg.Id,
                Action = action,
                PackageName = pkg.PackageName,
                FinalAmount = Math.Max(0, pkg.TotalAmount - pkg.Discount),
                MarginAmount = pkg.MarginAmount,
                Status = pkg.Status,
                ChangedByUserId = userId,
                ChangedByDisplayName = changedBy,
                SnapshotJson = snapshotJson
            });
        }
        catch (Exception)
        {
            // Do not block package save if history table is unavailable.
        }
    }

    private async Task SaveChangesWithOptionalPackageLogAsync(CancellationToken ct)
    {
        var pendingLogs = _db.ChangeTracker.Entries<PackageLog>().Any();
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception) when (pendingLogs)
        {
            DetachPendingPackageLogs();
            if (_db.ChangeTracker.HasChanges())
                await _db.SaveChangesAsync(ct);
        }
    }

    private void DetachPendingPackageLogs()
    {
        foreach (var entry in _db.ChangeTracker.Entries<PackageLog>().ToList())
            entry.State = EntityState.Detached;
    }

    private static bool IsPackageLogSchemaException(PostgresException ex) =>
        ex.SqlState == PostgresErrorCodes.UndefinedTable
        || ex.SqlState == PostgresErrorCodes.UndefinedColumn;

    private async Task<string> ResolveChangedByDisplayNameAsync(Guid? userId, string? email, CancellationToken ct)
    {
        if (userId.HasValue)
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId.Value && u.TenantId == TenantId)
                .Select(u => new { u.FirstName, u.LastName, u.Email })
                .FirstOrDefaultAsync(ct);
            if (user is not null)
            {
                var name = $"{user.FirstName} {user.LastName}".Trim();
                if (!string.IsNullOrWhiteSpace(name)) return name;
                if (!string.IsNullOrWhiteSpace(user.Email)) return user.Email;
            }
        }

        return string.IsNullOrWhiteSpace(email) ? "Unknown" : email.Trim();
    }

    private async Task<string> BuildPackageLogSnapshotJsonAsync(TourPackage pkg, CancellationToken ct)
    {
        var full = await _db.Packages.AsNoTracking()
            .Include(p => p.DayWiseItinerary).ThenInclude(d => d.Hotel)
            .Include(p => p.DayWiseItinerary).ThenInclude(d => d.ItineraryTemplate)
            .Include(p => p.Vehicle)
            .FirstOrDefaultAsync(p => p.Id == pkg.Id && p.TenantId == TenantId, ct) ?? pkg;

        var vehicleName = full.Vehicle is null
            ? null
            : $"{full.Vehicle.VehicleType}{(string.IsNullOrWhiteSpace(full.Vehicle.VehicleModel) ? "" : " - " + full.Vehicle.VehicleModel)}";

        var snapshot = new PackageLogSnapshot
        {
            LeadId = full.LeadId?.ToString("D"),
            ClientName = full.ClientName,
            ClientPhone = full.ClientPhone,
            ClientEmail = full.ClientEmail,
            ClientCity = full.ClientCity,
            ClientState = full.ClientState,
            ClientPickupLocation = full.ClientPickupLocation,
            ClientDropLocation = full.ClientDropLocation,
            PackageName = full.PackageName,
            StartDate = full.StartDate,
            EndDate = full.EndDate,
            NumberOfDays = full.NumberOfDays,
            NumberOfAdults = full.NumberOfAdults,
            NumberOfChildren = full.NumberOfChildren,
            VehicleId = full.VehicleId?.ToString("D"),
            VehicleName = vehicleName,
            TotalAmount = full.TotalAmount,
            MarginAmount = full.MarginAmount,
            Discount = full.Discount,
            FinalAmount = Math.Max(0, full.TotalAmount - full.Discount),
            AdvanceAmount = full.AdvanceAmount,
            BalanceAmount = full.BalanceAmount,
            Status = full.Status.ToString(),
            IsLocked = full.IsLocked,
            InclusionIds = full.InclusionIds ?? [],
            ExclusionIds = full.ExclusionIds ?? [],
            DayWiseItinerary = full.DayWiseItinerary
                .OrderBy(d => d.DayNumber)
                .Select(d => new PackageLogDaySnapshot
                {
                    DayNumber = d.DayNumber,
                    Date = d.Date,
                    HotelId = d.HotelId?.ToString("D"),
                    HotelName = d.Hotel?.Name,
                    RoomType = d.RoomType,
                    NumberOfRooms = d.NumberOfRooms,
                    CheckInTime = d.CheckInTime,
                    CheckOutTime = d.CheckOutTime,
                    MealPlan = d.MealPlan.ToString(),
                    ExtraBedCount = d.ExtraBedCount,
                    CnbCount = d.CnbCount,
                    Activities = d.Activities ?? [],
                    Meals = d.Meals ?? [],
                    Notes = d.Notes,
                    TemplateId = d.ItineraryTemplateId?.ToString("D"),
                    TemplateTitle = d.ItineraryTemplate?.Title,
                    HotelCost = d.HotelCost
                })
                .ToList()
        };

        return JsonSerializer.Serialize(snapshot, PackageLogJsonOptions);
    }

    private static PackageLogDto ToPackageLogDto(PackageLog log)
    {
        PackageLogSnapshot? snapshot = null;
        if (!string.IsNullOrWhiteSpace(log.SnapshotJson) && log.SnapshotJson != "{}")
        {
            try
            {
                snapshot = JsonSerializer.Deserialize<PackageLogSnapshot>(log.SnapshotJson, PackageLogJsonOptions);
            }
            catch (JsonException)
            {
                // Legacy or corrupt row — summary columns still available.
            }
        }

        return new PackageLogDto
        {
            Id = log.Id.ToString("D"),
            LeadId = log.LeadId.ToString("D"),
            PackageId = log.PackageId.ToString("D"),
            Action = log.Action,
            PackageName = log.PackageName,
            FinalAmount = log.FinalAmount,
            MarginAmount = log.MarginAmount,
            Status = log.Status,
            LoggedAt = log.CreatedAt,
            ChangedByUserId = log.ChangedByUserId?.ToString("D"),
            ChangedByDisplayName = log.ChangedByDisplayName,
            Snapshot = snapshot,
            SnapshotJson = string.IsNullOrWhiteSpace(log.SnapshotJson) || log.SnapshotJson == "{}"
                ? null
                : log.SnapshotJson
        };
    }

    /// <summary>Generate and download package PDF (client-facing proposal).</summary>
    [HttpGet("{id:guid}/pdf")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetPackagePdf([FromRoute] Guid id, [FromQuery] string? lang, CancellationToken ct)
    {
        var (currentUserEmail, canSeeAllPackages) = GetCurrentUserPackageScope();

        var pkg = await _db.Packages.AsNoTracking()
            .Include(p => p.Vehicle)
            .Include(p => p.DayWiseItinerary!)
            .ThenInclude(d => d.Hotel!)
            .ThenInclude(h => h.Area)
            .Include(p => p.DayWiseItinerary!)
            .ThenInclude(d => d.ItineraryTemplate)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);

        if (pkg is null) return NotFound();
        if (!canSeeAllPackages)
        {
            // Admin/SuperAdmin: canSeeAllPackages = true (already bypassed).
            // Others: allow Tour Manager (creator) OR Reservation user assigned to this package.
            var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
            var isReservation = string.Equals(role, UserRole.Reservation.ToString(), StringComparison.OrdinalIgnoreCase);
            Guid? currentUserId = null;
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out var uid))
                currentUserId = uid;

            var isCreator = !string.IsNullOrWhiteSpace(currentUserEmail) && pkg.CreatedBy == currentUserEmail;
            if (!isCreator)
            {
                if (isReservation && currentUserId.HasValue)
                {
                    var hasAssignedReservation = await _db.Reservations.AsNoTracking()
                        .AnyAsync(r =>
                            r.TenantId == TenantId &&
                            r.PackageId == pkg.Id &&
                            r.AssignedToUserId == currentUserId.Value,
                            ct);
                    if (!hasAssignedReservation)
                        return NotFound();
                }
                else
                {
                    return NotFound();
                }
            }
        }

        Tenant? tenant = null;
        if (pkg.TenantId != Guid.Empty)
            tenant = await _db.Tenants.AsNoTracking()
                .Include(t => t.BankAccounts)
                .Include(t => t.QrCodes)
                .Include(t => t.Documents)
                .FirstOrDefaultAsync(t => t.Id == pkg.TenantId, ct);

        try
        {
            var pdfBytes = await GeneratePackagePdfWithTenantAssetsAsync(pkg, tenant, lang, ct);
            var filename = ToSafeAsciiDownloadFileName(BuildPdfFilename(pkg));
            Response.Headers.Append("X-Content-Type-Options", "nosniff");
            return File(pdfBytes, "application/pdf", filename);
        }
        catch (PdfTemplateConfigurationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, ApiResponse<object>.Fail($"PDF generation failed: {inner}"));
        }
    }

    /// <summary>Generate package PDF and return inline response for browser preview.</summary>
    [HttpGet("{id:guid}/pdf-preview")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> PreviewPackagePdf([FromRoute] Guid id, [FromQuery] string? lang, CancellationToken ct)
    {
        var (currentUserEmail, canSeeAllPackages) = GetCurrentUserPackageScope();

        var pkg = await _db.Packages.AsNoTracking()
            .Include(p => p.Vehicle)
            .Include(p => p.DayWiseItinerary!)
            .ThenInclude(d => d.Hotel!)
            .ThenInclude(h => h.Area)
            .Include(p => p.DayWiseItinerary!)
            .ThenInclude(d => d.ItineraryTemplate)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);

        if (pkg is null) return NotFound();
        if (!canSeeAllPackages && !string.IsNullOrWhiteSpace(currentUserEmail) && pkg.CreatedBy != currentUserEmail)
            return NotFound();

        Tenant? tenant = null;
        if (pkg.TenantId != Guid.Empty)
            tenant = await _db.Tenants.AsNoTracking()
                .Include(t => t.BankAccounts)
                .Include(t => t.QrCodes)
                .Include(t => t.Documents)
                .FirstOrDefaultAsync(t => t.Id == pkg.TenantId, ct);

        try
        {
            var pdfBytes = await GeneratePackagePdfWithTenantAssetsAsync(pkg, tenant, lang, ct);
            var filename = ToSafeAsciiDownloadFileName(BuildPdfFilename(pkg));
            Response.Headers.Append("X-Content-Type-Options", "nosniff");
            Response.Headers.ContentDisposition = $"inline; filename=\"{filename}\"";
            return File(pdfBytes, "application/pdf");
        }
        catch (PdfTemplateConfigurationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, ApiResponse<object>.Fail($"PDF preview generation failed: {inner}"));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PackageDto>>> CreatePackage([FromBody] CreatePackageRequestDto request, CancellationToken ct)
    {
        var createdBy = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "system";

        // Validate referenced entities in same tenant
        if (request.VehicleId is not null)
        {
            var vehicleOk = await _db.Vehicles.AnyAsync(v => v.Id == request.VehicleId && v.TenantId == TenantId, ct);
            if (!vehicleOk) return BadRequest(ApiResponse<PackageDto>.Fail("Vehicle not found"));
        }

        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == request.LeadId && l.TenantId == TenantId, ct);
        if (lead is null) return BadRequest(ApiResponse<PackageDto>.Fail("Lead not found"));
        if (lead.IsLocked)
            return BadRequest(ApiResponse<PackageDto>.Fail("This lead is locked. Ask an admin to unlock it before creating another package."));

        var totalAmount = request.TotalAmount;
        var discount = request.Discount;
        var finalAmount = Math.Max(0, totalAmount - discount);
        var normalizedStartDate = NormalizeUtc(request.StartDate);
        var normalizedEndDate = NormalizeUtc(request.EndDate);
        var pkg = new TourPackage
        {
            TenantId = TenantId,
            LeadId = request.LeadId,
            ClientName = request.ClientName.Trim(),
            ClientPhone = request.ClientPhone.Trim(),
            ClientEmail = request.ClientEmail?.Trim(),
            ClientCity = request.ClientCity?.Trim(),
            ClientState = request.ClientState?.Trim(),
            ClientPickupLocation = request.ClientPickupLocation.Trim(),
            ClientDropLocation = request.ClientDropLocation.Trim(),
            PackageName = request.PackageName.Trim(),
            StartDate = normalizedStartDate,
            EndDate = normalizedEndDate,
            // Use the date range as the source of truth so transport/hotel/PDF stay consistent.
            NumberOfDays = DaysInclusive(normalizedStartDate, normalizedEndDate),
            NumberOfAdults = request.NumberOfAdults,
            NumberOfChildren = request.NumberOfChildren,
            VehicleId = request.VehicleId,
            TotalAmount = totalAmount,
            MarginAmount = request.MarginAmount,
            Discount = discount,
            AdvanceAmount = 0,
            BalanceAmount = finalAmount,
            Status = (PackageStatus)lead.Status,
            InclusionIds = request.InclusionIds ?? new List<string>(),
            ExclusionIds = request.ExclusionIds ?? new List<string>(),
            CreatedBy = createdBy
        };

        _db.Packages.Add(pkg);
        await _db.SaveChangesAsync(ct);

        foreach (var d in request.DayWiseItinerary)
        {
            _db.DayItineraries.Add(new DayItinerary
            {
                TenantId = TenantId,
                PackageId = pkg.Id,
                DayNumber = d.DayNumber,
                Date = NormalizeUtc(d.Date),
                HotelId = d.HotelId,
                RoomType = d.RoomType?.Trim(),
                NumberOfRooms = d.NumberOfRooms,
                CheckInTime = d.CheckInTime?.Trim(),
                CheckOutTime = d.CheckOutTime?.Trim(),
                MealPlan = d.MealPlan ?? AccommodationMealPlan.MAP,
                ExtraBedCount = d.ExtraBedCount,
                CnbCount = d.CnbCount,
                Activities = d.Activities ?? [],
                Meals = d.Meals ?? [],
                Notes = d.Notes?.Trim(),
                ItineraryTemplateId = d.ItineraryTemplateId,
                HotelCost = d.HotelCost
            });
        }

        await _db.SaveChangesAsync(ct);

        await AppendPackageLogAsync(pkg, PackageLogAction.Created, ct);
        await SaveChangesWithOptionalPackageLogAsync(ct);

        var created = await _db.Packages.AsNoTracking()
            .Include(p => p.DayWiseItinerary).ThenInclude(d => d.Hotel)
            .Include(p => p.DayWiseItinerary).ThenInclude(d => d.ItineraryTemplate)
            .Include(p => p.Vehicle)
            .FirstAsync(p => p.Id == pkg.Id, ct);

        return CreatedAtAction(nameof(GetPackageById), new { id = pkg.Id }, ApiResponse<PackageDto>.Ok(ToDto(created)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PackageDto>>> UpdatePackage([FromRoute] Guid id, [FromBody] UpdatePackageRequestDto request, CancellationToken ct)
    {
        var (currentUserEmail, canSeeAllPackages) = GetCurrentUserPackageScope();

        var pkg = await _db.Packages.Include(p => p.DayWiseItinerary).FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (pkg is null) return NotFound(ApiResponse<PackageDto>.Fail("Package not found"));
        if (!canSeeAllPackages)
        {
            var isCreator = !string.IsNullOrWhiteSpace(currentUserEmail) &&
                            string.Equals(pkg.CreatedBy.Trim(), currentUserEmail.Trim(), StringComparison.OrdinalIgnoreCase);
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isLeadAssignee = Guid.TryParse(userIdClaim, out var uid) &&
                                 pkg.LeadId.HasValue &&
                                 await _db.Leads.AsNoTracking().AnyAsync(
                                     l => l.Id == pkg.LeadId &&
                                          l.TenantId == TenantId &&
                                          !l.IsDeleted &&
                                          l.AssignedToUserId == uid,
                                     ct);
            if (!isCreator && !isLeadAssignee)
                return NotFound(ApiResponse<PackageDto>.Fail("Package not found"));
        }

        if (request.LeadId == Guid.Empty)
            return BadRequest(ApiResponse<PackageDto>.Fail("Please select a lead for this package."));

        if (pkg.IsLocked)
            return BadRequest(ApiResponse<PackageDto>.Fail("This package is locked. Ask an admin to unlock it before editing."));

        // Once sent for reservation, creator (tour manager or reservation user) cannot edit package details here.
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        var isSalesSideRole = string.Equals(role, UserRole.Agent.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, UserRole.Reservation.ToString(), StringComparison.OrdinalIgnoreCase);
        if (isSalesSideRole && !string.IsNullOrWhiteSpace(currentUserEmail) &&
            string.Equals(pkg.CreatedBy.Trim(), currentUserEmail.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var hasAnyReservation = await _db.Reservations.AsNoTracking()
                .AnyAsync(r => r.TenantId == TenantId && r.PackageId == pkg.Id, ct);
            if (hasAnyReservation)
                return BadRequest(ApiResponse<PackageDto>.Fail("This package has been sent for reservation and cannot be edited."));
        }

        // Include soft-deleted leads so packages linked before a lead was removed can still be edited.
        var requestLead = await _db.Leads.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.LeadId && l.TenantId == TenantId, ct);

        if (requestLead is null)
        {
            if (pkg.LeadId != request.LeadId)
                return BadRequest(ApiResponse<PackageDto>.Fail("Lead not found. Select a valid lead for this package."));
            // Orphaned lead id on package: allow saving package fields without lead sync.
        }
        else if (requestLead.IsDeleted)
        {
            if (pkg.LeadId != request.LeadId)
                return BadRequest(ApiResponse<PackageDto>.Fail("Lead not found. Select a valid lead for this package."));
            // Lead was deleted; keep historical link but do not sync lead status.
        }
        else if (requestLead.IsLocked && pkg.LeadId != request.LeadId)
        {
            return BadRequest(ApiResponse<PackageDto>.Fail("This lead is locked. Ask an admin to unlock it before moving packages to it."));
        }

        var syncLeadAndPackages = requestLead is { IsDeleted: false };

        if (request.VehicleId is not null)
        {
            var vehicleOk = await _db.Vehicles.AnyAsync(v => v.Id == request.VehicleId && v.TenantId == TenantId, ct);
            if (!vehicleOk) return BadRequest(ApiResponse<PackageDto>.Fail("Vehicle not found"));
        }

        if (!HasPackageUpdateChanges(pkg, request))
        {
            var unchanged = await _db.Packages.AsNoTracking()
                .Include(p => p.DayWiseItinerary).ThenInclude(d => d.Hotel)
                .Include(p => p.DayWiseItinerary).ThenInclude(d => d.ItineraryTemplate)
                .Include(p => p.Vehicle)
                .FirstAsync(p => p.Id == pkg.Id, ct);
            return ApiResponse<PackageDto>.Ok(ToDto(unchanged));
        }

        var shouldAppendPackageLog = HasPackageDataChanges(pkg, request);

        pkg.LeadId = request.LeadId;
        pkg.ClientName = request.ClientName.Trim();
        pkg.ClientPhone = request.ClientPhone.Trim();
        pkg.ClientEmail = request.ClientEmail?.Trim();
        pkg.ClientCity = request.ClientCity?.Trim();
        pkg.ClientState = request.ClientState?.Trim();
        pkg.ClientPickupLocation = request.ClientPickupLocation.Trim();
        pkg.ClientDropLocation = request.ClientDropLocation.Trim();
        pkg.PackageName = request.PackageName.Trim();
        pkg.StartDate = NormalizeUtc(request.StartDate);
        pkg.EndDate = NormalizeUtc(request.EndDate);
        // Use the date range as the source of truth so transport/hotel/PDF stay consistent.
        pkg.NumberOfDays = DaysInclusive(pkg.StartDate, pkg.EndDate);
        pkg.NumberOfAdults = request.NumberOfAdults;
        pkg.NumberOfChildren = request.NumberOfChildren;
        pkg.VehicleId = request.VehicleId;
        pkg.TotalAmount = request.TotalAmount;
        pkg.MarginAmount = request.MarginAmount;
        pkg.Discount = request.Discount;
        var finalAmount = Math.Max(0, pkg.TotalAmount - request.Discount);
        pkg.AdvanceAmount = request.AdvanceAmount;
        pkg.BalanceAmount = finalAmount - request.AdvanceAmount;
        pkg.Status = request.Status;
        pkg.IsLocked = request.Status == PackageStatus.Confirmed;
        pkg.InclusionIds = request.InclusionIds ?? new List<string>();
        pkg.ExclusionIds = request.ExclusionIds ?? new List<string>();

        // When the latest package for a lead changes status, sync that status to the lead only.
        if (syncLeadAndPackages && pkg.LeadId.HasValue && await IsLatestPackageForLeadAsync(pkg, ct))
        {
            var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == pkg.LeadId.Value && l.TenantId == TenantId, ct);
            if (lead != null)
            {
                lead.Status = (LeadStatus)(int)request.Status;
                lead.IsLocked = request.Status == PackageStatus.Confirmed;
            }
        }

        // Replace itinerary
        var existing = await _db.DayItineraries.Where(d => d.PackageId == pkg.Id && d.TenantId == TenantId).ToListAsync(ct);
        _db.DayItineraries.RemoveRange(existing);

        foreach (var d in request.DayWiseItinerary)
        {
            var hotelId = d.HotelId is { } hid && hid != Guid.Empty ? hid : (Guid?)null;
            var templateId = d.ItineraryTemplateId is { } tid && tid != Guid.Empty ? tid : (Guid?)null;
            _db.DayItineraries.Add(new DayItinerary
            {
                TenantId = TenantId,
                PackageId = pkg.Id,
                DayNumber = d.DayNumber,
                Date = NormalizeUtc(d.Date),
                HotelId = hotelId,
                RoomType = d.RoomType?.Trim(),
                NumberOfRooms = d.NumberOfRooms,
                CheckInTime = d.CheckInTime?.Trim(),
                CheckOutTime = d.CheckOutTime?.Trim(),
                MealPlan = d.MealPlan ?? AccommodationMealPlan.MAP,
                ExtraBedCount = d.ExtraBedCount,
                CnbCount = d.CnbCount,
                Activities = d.Activities ?? [],
                Meals = d.Meals ?? [],
                Notes = d.Notes?.Trim(),
                ItineraryTemplateId = templateId,
                HotelCost = d.HotelCost
            });
        }

        await SaveChangesWithOptionalPackageLogAsync(ct);

        if (shouldAppendPackageLog)
        {
            await AppendPackageLogAsync(pkg, PackageLogAction.Updated, ct);
            await SaveChangesWithOptionalPackageLogAsync(ct);
        }

        var updated = await _db.Packages.AsNoTracking()
            .Include(p => p.DayWiseItinerary).ThenInclude(d => d.Hotel)
            .Include(p => p.DayWiseItinerary).ThenInclude(d => d.ItineraryTemplate)
            .Include(p => p.Vehicle)
            .FirstAsync(p => p.Id == pkg.Id, ct);

        return ApiResponse<PackageDto>.Ok(ToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePackage([FromRoute] Guid id, CancellationToken ct)
    {
        var (currentUserEmail, canSeeAllPackages) = GetCurrentUserPackageScope();

        var pkg = await _db.Packages.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (pkg is null) return NotFound(ApiResponse<object>.Fail("Package not found"));
        if (!canSeeAllPackages && !string.IsNullOrWhiteSpace(currentUserEmail) && pkg.CreatedBy != currentUserEmail)
            return NotFound(ApiResponse<object>.Fail("Package not found"));
        if (pkg.IsLocked)
            return BadRequest(ApiResponse<object>.Fail("This package is locked. Ask an admin to unlock it before deleting."));

        pkg.IsDeleted = true;
        pkg.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    [HttpPost("{id:guid}/unlock")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<PackageDto>>> UnlockPackage([FromRoute] Guid id, CancellationToken ct)
    {
        var pkg = await _db.Packages
            .Include(p => p.DayWiseItinerary).ThenInclude(d => d.Hotel)
            .Include(p => p.DayWiseItinerary).ThenInclude(d => d.ItineraryTemplate)
            .Include(p => p.Vehicle)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (pkg is null) return NotFound(ApiResponse<PackageDto>.Fail("Package not found"));

        pkg.IsLocked = false;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<PackageDto>.Ok(ToDto(pkg));
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string SanitizeFilenamePart(string? s, int maxLen = 50)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = string.Join("", s.Trim().Where(c => !@"<>:""/\|?*".ToCharArray().Contains(c))).Trim();
        t = string.Join("_", t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        return t.Length > maxLen ? t.Substring(0, maxLen) : t;
    }

    private static string BuildPdfFilename(TourPackage pkg)
    {
        var client = SanitizeFilenamePart(pkg.ClientName ?? "", 40);
        var pickUp = SanitizeFilenamePart(pkg.ClientPickupLocation ?? "", 30);
        var drop = SanitizeFilenamePart(pkg.ClientDropLocation ?? "", 30);
        var dateTime = pkg.StartDate.ToString("ddMMMyyyy_HHmm", CultureInfo.GetCultureInfo("en-IN"));
        var uniqueId = pkg.Id.ToString("N")[..8]; // first 8 chars of GUID (hex) for a unique suffix
        var parts = new List<string> { client, pickUp, drop }.Where(s => !string.IsNullOrEmpty(s)).ToList();
        parts.Add(dateTime);
        parts.Add(uniqueId);
        var name = string.Join("_", parts);
        if (string.IsNullOrEmpty(name)) name = "package";
        return $"{name}.pdf";
    }

    private static string GetLocationString(Hotel h)
    {
        var parts = new[] { h.City, h.State }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var loc = string.Join(", ", parts).Trim();
        return string.IsNullOrEmpty(loc) ? (h.Address?.Trim() ?? "–") : loc;
    }

    /// <summary>Area name for PDF labels (matches UI "Name - Area"); falls back to city/state.</summary>
    private static string GetHotelAreaString(Hotel h)
    {
        var area = h.Area?.Name?.Trim();
        if (!string.IsNullOrEmpty(area)) return area;
        return GetLocationString(h);
    }

    private static PackagePdfModel BuildPdfModel(
        TourPackage pkg,
        Tenant? tenant,
        string baseUrl,
        string? language,
        IReadOnlyList<string>? inclusionLabels = null,
        IReadOnlyList<string>? exclusionLabels = null)
    {
        var labels = PdfLocalizedStrings.ForLanguage(language);
        var culture = labels.Culture;
        var days = (pkg.DayWiseItinerary ?? []).OrderBy(d => d.DayNumber).ToList();
        var nights = Math.Max(0, pkg.NumberOfDays - 1);
        var daysLabel = labels.DaysDurationLabel(nights, pkg.NumberOfDays);

        var resolvedInclusions = (inclusionLabels ?? InclusionOptions.GetInclusionLabels(pkg.InclusionIds ?? [], language)).ToList();
        var resolvedExclusions = (exclusionLabels ?? InclusionOptions.GetExclusionLabels(pkg.InclusionIds ?? [], language)).ToList();

        string ToAbsolute(string? url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url;
            return url.StartsWith("/") ? baseUrl + url : baseUrl + "/" + url;
        }

        var pdfDays = days.Select(d =>
        {
            var title = (d.ItineraryTemplate?.Title ?? d.Hotel?.Name ?? labels.DayActivities).Trim();
            if (string.IsNullOrEmpty(title)) title = labels.DayActivities;
            var descParts = new List<string>();
            if (d.Activities?.Count > 0) descParts.Add(string.Join(". ", d.Activities));
            if (!string.IsNullOrWhiteSpace(d.Notes)) descParts.Add(d.Notes.Trim());
            var description = descParts.Count > 0 ? string.Join(" ", descParts) : "–";
            return new DayItem
            {
                DayNumber = d.DayNumber,
                DateLabel = d.Date.ToString("d MMM yyyy", culture),
                HotelName = d.Hotel?.Name,
                HotelLocation = d.Hotel is null ? null : GetHotelAreaString(d.Hotel),
                DayImageUrl = d.Hotel?.ImageUrls?.Select(ToAbsolute).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                Title = title,
                Description = description,
                ExtraBedCount = d.ExtraBedCount ?? 0,
                CnbCount = d.CnbCount ?? 0
            };
        }).ToList();

        var seenHotels = new Dictionary<Guid, (Hotel Hotel, string MealPlan, int Nights, int MaxExtraBeds, int MaxCnb)>();
        foreach (var d in days)
        {
            if (d.HotelId is null || d.Hotel is null) continue;
            var meal = MealPlanTranslations.Label(d.MealPlan, language);
            var eb = d.ExtraBedCount ?? 0;
            var cnb = d.CnbCount ?? 0;
            if (seenHotels.TryGetValue(d.HotelId.Value, out var existing))
            {
                seenHotels[d.HotelId.Value] = (
                    existing.Hotel,
                    existing.MealPlan,
                    existing.Nights + 1,
                    Math.Max(existing.MaxExtraBeds, eb),
                    Math.Max(existing.MaxCnb, cnb));
            }
            else
            {
                seenHotels[d.HotelId.Value] = (d.Hotel, meal, 1, eb, cnb);
            }
        }

        var pdfHotels = seenHotels.Values.Select(x => new HotelItem
        {
            Name = x.Hotel.Name ?? "–",
            Location = GetHotelAreaString(x.Hotel),
            StarRating = x.Hotel.StarRating ?? 0,
            MealPlan = x.MealPlan,
            Nights = x.Nights,
            ExtraBedCount = x.MaxExtraBeds,
            CnbCount = x.MaxCnb,
            IsHouseboat = x.Hotel.IsHouseboat,
            Amenities = x.Hotel.Amenities ?? [],
            ImageUrls = (x.Hotel.ImageUrls ?? []).Select(ToAbsolute).Where(u => !string.IsNullOrEmpty(u)).ToList()
        }).ToList();

        var coverImageUrls = pdfHotels.SelectMany(h => h.ImageUrls).Take(4).ToList();

        var firstDay = days.FirstOrDefault();
        var hotelNights = days.Where(d => d.HotelId is not null).ToList();
        // Peak counts per night (not sum): agents usually enter the same nightly extra bed/CNB on each day; summing inflated PDF totals.
        var peakExtraBeds = hotelNights.Count == 0 ? 0 : hotelNights.Max(d => d.ExtraBedCount ?? 0);
        var peakCnb = hotelNights.Count == 0 ? 0 : hotelNights.Max(d => d.CnbCount ?? 0);
        string Fmt(decimal v) => v.ToString("N0", culture);
        string FmtDate(DateTime dt) => dt.ToString("d MMM yyyy", culture);
        // PDF totals use stored package amounts only (no automatic add-ons).
        var totalForPdf = pkg.TotalAmount;
        var totalPackagePriceStr = "Rs. " + Fmt(totalForPdf);
        var marginDisplay = pkg.MarginAmount > 0 ? "Rs. " + Fmt(pkg.MarginAmount) : "–";
        var finalAmount = Math.Max(0, totalForPdf - pkg.Discount);
        var balanceForPdf = Math.Max(0, finalAmount - pkg.AdvanceAmount);
        var chargeablePax = pkg.NumberOfAdults + pkg.NumberOfChildren;
        var perPerson = chargeablePax > 0 ? finalAmount / chargeablePax : finalAmount;

        var clientAddress = string.Join(", ", new[] { pkg.ClientCity, pkg.ClientState }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        if (string.IsNullOrEmpty(clientAddress) && !string.IsNullOrWhiteSpace(pkg.ClientPickupLocation))
            clientAddress = pkg.ClientPickupLocation.Trim();
        List<string> NormalizeLines(List<string>? lines) =>
            (lines ?? [])
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        var termsAndConditions = NormalizeLines(tenant?.TermsAndConditions);
        var cancellationPolicy = NormalizeLines(tenant?.CancellationPolicy);
        var supplementCosts = NormalizeLines(tenant?.SupplementCosts);

        var contactPerson = tenant?.ContactPerson?.Trim();
        return new PackagePdfModel
        {
            Labels = labels,
            PackageName = pkg.PackageName ?? "Package",
            ClientName = pkg.ClientName ?? "–",
            ClientPhone = pkg.ClientPhone?.Trim(),
            ClientEmail = pkg.ClientEmail?.Trim(),
            ClientAddress = string.IsNullOrWhiteSpace(clientAddress) ? null : clientAddress,
            StartDate = FmtDate(pkg.StartDate),
            EndDate = FmtDate(pkg.EndDate),
            DaysLabel = daysLabel,
            PickUpLocation = string.IsNullOrWhiteSpace(pkg.ClientPickupLocation) ? null : pkg.ClientPickupLocation.Trim(),
            DropLocation = string.IsNullOrWhiteSpace(pkg.ClientDropLocation) ? null : pkg.ClientDropLocation.Trim(),
            VehicleName = PackagePdfHtmlFragments.FormatVehicleName(pkg.Vehicle),
            TransportRoute = PackagePdfHtmlFragments.FormatTransportRoute(pkg.ClientPickupLocation, pkg.ClientDropLocation),
            NumberOfAdults = pkg.NumberOfAdults,
            NumberOfChildren = pkg.NumberOfChildren,
            MealPlanLabel = firstDay is not null ? MealPlanTranslations.Label(firstDay.MealPlan, language) : "–",
            FirstDayRooms = firstDay?.NumberOfRooms ?? 1,
            TotalExtraBeds = peakExtraBeds,
            TotalCnbCount = peakCnb,
            TotalAmount = totalPackagePriceStr,
            TotalPackagePrice = totalPackagePriceStr,
            MarginAmountDisplay = marginDisplay,
            Discount = pkg.Discount > 0 ? "Rs. " + Fmt(pkg.Discount) : "–",
            FinalAmount = "Rs. " + Fmt(finalAmount),
            PerPersonAmount = "Rs. " + Fmt(perPerson),
            AdvanceAmount = "Rs. " + Fmt(pkg.AdvanceAmount),
            BalanceAmount = "Rs. " + Fmt(balanceForPdf),
            Days = pdfDays,
            Hotels = pdfHotels,
            CoverImageUrls = coverImageUrls,
            InclusionLabels = resolvedInclusions,
            ExclusionLabels = resolvedExclusions,
            AgencyName = tenant?.Name?.Trim(),
            AgencyPhone = tenant?.Phone?.Trim(),
            AgencyEmail = tenant?.Email?.Trim(),
            AgencyLogoUrl = tenant?.LogoUrl != null ? ToAbsolute(tenant.LogoUrl) : null,
            ManagingDirectorName = contactPerson,
            SalesHeadName = contactPerson,
            RegisteredOfficeAddressHtml = PdfTemplatePostProcessor.FormatRegisteredOfficeAddressHtml(
                tenant?.Address, labels),
            AgencyWebsite = PdfTemplatePostProcessor.WebsiteFromEmail(tenant?.Email),
            GeneratedDate = FmtDate(DateTime.UtcNow),
            BankAccounts = (tenant?.BankAccounts ?? [])
                .OrderBy(b => b.DisplayOrder)
                .ThenBy(b => b.CreatedAt)
                .Select(b => new BankAccountItem
                {
                    AccountHolderName = b.AccountHolderName,
                    BankName = b.BankName,
                    AccountNumber = b.AccountNumber,
                    IFSC = b.IFSC,
                    Branch = b.Branch
                }).ToList(),
            QrCodes = (tenant?.QrCodes ?? [])
                .OrderBy(q => q.DisplayOrder)
                .ThenBy(q => q.CreatedAt)
                .Select(q => new QrCodeItem { Label = q.Label, ImageUrl = q.ImageUrl }).ToList(),
            PrimaryColor = tenant?.PdfPrimaryColor?.Trim(),
            SecondaryColor = tenant?.PdfSecondaryColor?.Trim(),
            CoverTitle = tenant?.PdfCoverTitle?.Trim(),
            TemplateKey = tenant?.PdfTemplateKey?.Trim(),
            ShowBankDetails = tenant?.PdfShowBankDetails,
            ShowQrCodes = tenant?.PdfShowQrCodes,
            TermsAndConditions = termsAndConditions,
            CancellationPolicy = cancellationPolicy,
            SupplementCosts = supplementCosts
        };
    }

    /// <summary>Resolve image URLs to data URLs by reading from disk so Chromium doesn't fetch over network (faster PDF).</summary>
    private PackagePdfModel InlinePdfImagesFromDisk(PackagePdfModel model)
    {
        var customUploads = _configuration["Uploads:Path"]?.Trim() ?? _configuration["Uploads__Path"]?.Trim();
        var uploadsRoot = !string.IsNullOrEmpty(customUploads)
            ? customUploads
            : Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads");
        if (!Directory.Exists(uploadsRoot)) return model;

        var maxImagesPerHotel = 2;
        var maxConfig = _configuration["PdfGenerator:MaxImagesPerHotel"]?.Trim() ?? _configuration["PdfGenerator__MaxImagesPerHotel"]?.Trim();
        if (!string.IsNullOrEmpty(maxConfig) && int.TryParse(maxConfig, out var n) && n >= 0)
            maxImagesPerHotel = Math.Min(n, 4);

        string? ToDataUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var path = url.Trim();
            if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return path;
            if (!path.Contains("/uploads/", StringComparison.OrdinalIgnoreCase)) return path;
            var pathSegment = path.Contains("?") ? path[..path.IndexOf('?')] : path;
            if (pathSegment.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || pathSegment.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try { pathSegment = new Uri(pathSegment).AbsolutePath; } catch { return path; }
            }
            pathSegment = pathSegment.TrimStart('/');
            var relativePath = pathSegment.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase)
                ? pathSegment["uploads/".Length..]
                : pathSegment;
            var fullPath = Path.Combine(uploadsRoot, relativePath);
            if (!System.IO.File.Exists(fullPath)) return path;
            try
            {
                var bytes = System.IO.File.ReadAllBytes(fullPath);
                var ext = Path.GetExtension(fullPath).ToLowerInvariant();
                var mime = ext switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".gif" => "image/gif", ".webp" => "image/webp", _ => "image/jpeg" };
                return "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
            }
            catch { return path; }
        }

        var inlinedHotels = model.Hotels.Select(h => new HotelItem
        {
            Name = h.Name,
            Location = h.Location,
            StarRating = h.StarRating,
            MealPlan = h.MealPlan,
            Nights = h.Nights,
            ExtraBedCount = h.ExtraBedCount,
            CnbCount = h.CnbCount,
            IsHouseboat = h.IsHouseboat,
            Amenities = h.Amenities,
            ImageUrls = (h.ImageUrls ?? []).Take(maxImagesPerHotel).Select(ToDataUrl).Where(u => !string.IsNullOrEmpty(u)).Select(u => u!).ToList()
        }).ToList();
        var inlinedCover = inlinedHotels.SelectMany(h => h.ImageUrls).Take(4).ToList();

        var inlinedDays = model.Days.Select(d => new DayItem
        {
            DayNumber = d.DayNumber,
            DateLabel = d.DateLabel,
            HotelName = d.HotelName,
            HotelLocation = d.HotelLocation,
            DayImageUrl = ToDataUrl(d.DayImageUrl) ?? d.DayImageUrl,
            Title = d.Title,
            Description = d.Description,
            ExtraBedCount = d.ExtraBedCount,
            CnbCount = d.CnbCount
        }).ToList();

        return new PackagePdfModel
        {
            Labels = model.Labels,
            PackageName = model.PackageName,
            ClientName = model.ClientName,
            ClientPhone = model.ClientPhone,
            ClientEmail = model.ClientEmail,
            ClientAddress = model.ClientAddress,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            DaysLabel = model.DaysLabel,
            PickUpLocation = model.PickUpLocation,
            DropLocation = model.DropLocation,
            VehicleName = model.VehicleName,
            TransportRoute = model.TransportRoute,
            NumberOfAdults = model.NumberOfAdults,
            NumberOfChildren = model.NumberOfChildren,
            MealPlanLabel = model.MealPlanLabel,
            FirstDayRooms = model.FirstDayRooms,
            TotalExtraBeds = model.TotalExtraBeds,
            TotalCnbCount = model.TotalCnbCount,
            TotalAmount = model.TotalAmount,
            TotalPackagePrice = model.TotalPackagePrice,
            MarginAmountDisplay = model.MarginAmountDisplay,
            Discount = model.Discount,
            FinalAmount = model.FinalAmount,
            PerPersonAmount = model.PerPersonAmount,
            AdvanceAmount = model.AdvanceAmount,
            BalanceAmount = model.BalanceAmount,
            Days = inlinedDays,
            Hotels = inlinedHotels,
            CoverImageUrls = inlinedCover,
            InclusionLabels = model.InclusionLabels,
            ExclusionLabels = model.ExclusionLabels,
            AgencyName = model.AgencyName,
            AgencyPhone = model.AgencyPhone,
            AgencyEmail = model.AgencyEmail,
            AgencyLogoUrl = ToDataUrl(model.AgencyLogoUrl) ?? model.AgencyLogoUrl,
            ManagingDirectorName = model.ManagingDirectorName,
            SalesHeadName = model.SalesHeadName,
            RegisteredOfficeAddressHtml = model.RegisteredOfficeAddressHtml,
            AgencyWebsite = model.AgencyWebsite,
            GeneratedDate = model.GeneratedDate,
            BankAccounts = model.BankAccounts,
            QrCodes = (model.QrCodes ?? []).Select(q => new QrCodeItem
            {
                Label = q.Label,
                ImageUrl = ToDataUrl(q.ImageUrl) ?? q.ImageUrl
            }).ToList(),
            PrimaryColor = model.PrimaryColor,
            SecondaryColor = model.SecondaryColor,
            CoverTitle = model.CoverTitle,
            TemplateKey = model.TemplateKey,
            CustomHtmlTemplate = model.CustomHtmlTemplate,
            ShowBankDetails = model.ShowBankDetails,
            ShowQrCodes = model.ShowQrCodes,
            TermsAndConditions = model.TermsAndConditions,
            CancellationPolicy = model.CancellationPolicy,
            SupplementCosts = model.SupplementCosts
        };
    }

    private async Task<byte[]> GeneratePackagePdfWithTenantAssetsAsync(TourPackage pkg, Tenant? tenant, string? language, CancellationToken ct)
    {
        // Use configured base URL so PDF images load in production; Request.Scheme/Host can be wrong behind a proxy.
        var baseUrl = _configuration["Api:BaseUrl"]?.Trim()
            ?? _configuration["Api__BaseUrl"]?.Trim()
            ?? _configuration["PdfGenerator:BaseUrl"]?.Trim()
            ?? _configuration["PdfGenerator__BaseUrl"]?.Trim();
        if (string.IsNullOrEmpty(baseUrl))
            baseUrl = $"{Request.Scheme}://{Request.Host}";
        baseUrl = baseUrl.TrimEnd('/');

        var (inclusionLabels, exclusionLabels) = await _packageMasterData.ResolveInclusionLabelsAsync(
            pkg.TenantId,
            pkg.InclusionIds,
            pkg.ExclusionIds,
            language,
            ct);

        var model = BuildPdfModel(pkg, tenant, baseUrl, language, inclusionLabels.ToList(), exclusionLabels.ToList());
        var templateKey = model.TemplateKey?.Trim();
        if (string.IsNullOrWhiteSpace(templateKey))
            throw new PdfTemplateConfigurationException(
                "This agency has no PDF template selected. Open the travel agency in Admin and assign a PDF template that includes HTML.");

        var templateEntry = await _pdfTemplateHtmlCache.TryLoadActiveTemplateAsync(templateKey, ct).ConfigureAwait(false);
        if (templateEntry is null)
            throw new PdfTemplateConfigurationException(
                $"The assigned PDF template key \"{templateKey}\" does not exist or is inactive.");

        var htmlBody = templateEntry.HtmlBody;
        if (string.IsNullOrWhiteSpace(htmlBody))
            throw new PdfTemplateConfigurationException(
                $"The PDF template \"{templateEntry.TemplateName}\" ({templateKey}) has no HTML saved. Edit it under Admin → PDF templates and paste or design the Html template.");

        model = new PackagePdfModel
        {
            Labels = model.Labels,
            PackageName = model.PackageName,
            ClientName = model.ClientName,
            ClientPhone = model.ClientPhone,
            ClientEmail = model.ClientEmail,
            ClientAddress = model.ClientAddress,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            DaysLabel = model.DaysLabel,
            PickUpLocation = model.PickUpLocation,
            DropLocation = model.DropLocation,
            VehicleName = model.VehicleName,
            TransportRoute = model.TransportRoute,
            NumberOfAdults = model.NumberOfAdults,
            NumberOfChildren = model.NumberOfChildren,
            MealPlanLabel = model.MealPlanLabel,
            FirstDayRooms = model.FirstDayRooms,
            TotalExtraBeds = model.TotalExtraBeds,
            TotalCnbCount = model.TotalCnbCount,
            TotalAmount = model.TotalAmount,
            TotalPackagePrice = model.TotalPackagePrice,
            MarginAmountDisplay = model.MarginAmountDisplay,
            Discount = model.Discount,
            FinalAmount = model.FinalAmount,
            PerPersonAmount = model.PerPersonAmount,
            AdvanceAmount = model.AdvanceAmount,
            BalanceAmount = model.BalanceAmount,
            Days = model.Days,
            Hotels = model.Hotels,
            CoverImageUrls = model.CoverImageUrls,
            InclusionLabels = model.InclusionLabels,
            ExclusionLabels = model.ExclusionLabels,
            AgencyName = model.AgencyName,
            AgencyPhone = model.AgencyPhone,
            AgencyEmail = model.AgencyEmail,
            AgencyLogoUrl = model.AgencyLogoUrl,
            ManagingDirectorName = model.ManagingDirectorName,
            SalesHeadName = model.SalesHeadName,
            RegisteredOfficeAddressHtml = model.RegisteredOfficeAddressHtml,
            AgencyWebsite = model.AgencyWebsite,
            GeneratedDate = model.GeneratedDate,
            BankAccounts = model.BankAccounts,
            QrCodes = model.QrCodes,
            PrimaryColor = model.PrimaryColor,
            SecondaryColor = model.SecondaryColor,
            CoverTitle = model.CoverTitle,
            TemplateKey = model.TemplateKey,
            CustomHtmlTemplate = htmlBody,
            ShowBankDetails = model.ShowBankDetails,
            ShowQrCodes = model.ShowQrCodes,
            TermsAndConditions = model.TermsAndConditions,
            CancellationPolicy = model.CancellationPolicy,
            SupplementCosts = model.SupplementCosts
        };
        model = InlinePdfImagesFromDisk(model);
        var generatedPdf = await _pdfGenerator.GenerateAsync(model, ct);
        generatedPdf = PackagePdfSanitizer.StripDangerousCatalogEntries(generatedPdf);
        return MergePdfWithTenantAssets(generatedPdf, tenant);
    }

    private byte[] MergePdfWithTenantAssets(byte[] generatedPdf, Tenant? tenant)
    {
        var uploadsRoot = ResolveUploadsRoot();
        if (tenant?.Documents is null || tenant.Documents.Count == 0 || !Directory.Exists(uploadsRoot))
            return generatedPdf;

        var coverDoc = tenant.Documents
            .Where(d => !d.IsDeleted && d.Type == TenantDocumentType.PdfCoverPage)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefault();
        var appendixDocs = tenant.Documents
            .Where(d => !d.IsDeleted && d.Type == TenantDocumentType.PdfAppendixPage)
            .OrderBy(d => d.CreatedAt)
            .ToList();

        if (coverDoc is null && appendixDocs.Count == 0)
            return generatedPdf;

        var merged = new PdfDocument();

        void AppendPdfBytes(byte[] bytes)
        {
            using var source = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
            for (var i = 0; i < source.PageCount; i++)
                merged.AddPage(source.Pages[i]);
        }

        if (coverDoc is not null)
        {
            var coverBytes = TryReadTenantDocumentPdfBytes(coverDoc.Url, uploadsRoot);
            if (coverBytes is not null) AppendPdfBytes(PackagePdfSanitizer.StripDangerousCatalogEntries(coverBytes));
        }

        AppendPdfBytes(generatedPdf);

        foreach (var appendix in appendixDocs)
        {
            var appendixBytes = TryReadTenantDocumentPdfBytes(appendix.Url, uploadsRoot);
            if (appendixBytes is not null) AppendPdfBytes(PackagePdfSanitizer.StripDangerousCatalogEntries(appendixBytes));
        }

        using var ms = new MemoryStream();
        merged.Save(ms, false);
        return PackagePdfSanitizer.StripDangerousCatalogEntries(ms.ToArray());
    }

    /// <summary>Chrome is stricter about download filenames with non-ASCII or odd punctuation; keep a short ASCII name for Content-Disposition.</summary>
    private static string ToSafeAsciiDownloadFileName(string suggested)
    {
        var ext = ".pdf";
        var baseName = Path.GetFileNameWithoutExtension(suggested);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "TravelPathways-package";
        var forbidden = @"<>:""/\|?*";
        var sb = new StringBuilder(Math.Min(baseName.Length, 128));
        foreach (var c in baseName.Trim())
        {
            if (forbidden.Contains(c)) continue;
            if (c >= 0x20 && c < 0x7f) sb.Append(c);
            else sb.Append('_');
        }
        var s = sb.ToString().Trim('_');
        if (string.IsNullOrEmpty(s)) s = "TravelPathways-package";
        if (s.Length > 120) s = s[..120];
        return s + ext;
    }

    private string ResolveUploadsRoot()
    {
        var customUploads = _configuration["Uploads:Path"]?.Trim() ?? _configuration["Uploads__Path"]?.Trim();
        return !string.IsNullOrEmpty(customUploads)
            ? customUploads
            : Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads");
    }

    private static byte[]? TryReadTenantDocumentPdfBytes(string? url, string uploadsRoot)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var pathSegment = url.Trim();
        if (pathSegment.Contains("?")) pathSegment = pathSegment[..pathSegment.IndexOf('?')];
        if (pathSegment.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            pathSegment.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                pathSegment = new Uri(pathSegment).AbsolutePath;
            }
            catch
            {
                return null;
            }
        }

        pathSegment = pathSegment.TrimStart('/');
        if (!pathSegment.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            return null;

        var relativePath = pathSegment["uploads/".Length..];
        var fullPath = Path.Combine(uploadsRoot, relativePath);
        if (!System.IO.File.Exists(fullPath))
            return null;
        if (!string.Equals(Path.GetExtension(fullPath), ".pdf", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            return System.IO.File.ReadAllBytes(fullPath);
        }
        catch
        {
            return null;
        }
    }

    private static bool HasPackageUpdateChanges(TourPackage pkg, UpdatePackageRequestDto request) =>
        HasPackageDataChanges(pkg, request) || pkg.Status != request.Status;

    private static bool HasPackageDataChanges(TourPackage pkg, UpdatePackageRequestDto request)
    {
        if (pkg.LeadId != request.LeadId) return true;
        if (!StringEqual(pkg.ClientName, request.ClientName)) return true;
        if (!StringEqual(pkg.ClientPhone, request.ClientPhone)) return true;
        if (!StringEqual(pkg.ClientEmail, request.ClientEmail)) return true;
        if (!StringEqual(pkg.ClientCity, request.ClientCity)) return true;
        if (!StringEqual(pkg.ClientState, request.ClientState)) return true;
        if (!StringEqual(pkg.ClientPickupLocation, request.ClientPickupLocation)) return true;
        if (!StringEqual(pkg.ClientDropLocation, request.ClientDropLocation)) return true;
        if (!StringEqual(pkg.PackageName, request.PackageName)) return true;
        if (NormalizeUtc(pkg.StartDate).Date != NormalizeUtc(request.StartDate).Date) return true;
        if (NormalizeUtc(pkg.EndDate).Date != NormalizeUtc(request.EndDate).Date) return true;
        if (pkg.NumberOfAdults != request.NumberOfAdults) return true;
        if (pkg.NumberOfChildren != request.NumberOfChildren) return true;
        if (pkg.VehicleId != request.VehicleId) return true;
        if (pkg.TotalAmount != request.TotalAmount) return true;
        if (pkg.MarginAmount != request.MarginAmount) return true;
        if (pkg.Discount != request.Discount) return true;
        if (pkg.AdvanceAmount != request.AdvanceAmount) return true;
        if (!StringListEqual(pkg.InclusionIds, request.InclusionIds)) return true;
        if (!StringListEqual(pkg.ExclusionIds, request.ExclusionIds)) return true;
        return !ItineraryEqual(pkg.DayWiseItinerary, request.DayWiseItinerary);
    }

    private static bool StringEqual(string? a, string? b) =>
        string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.Ordinal);

    private static bool StringListEqual(IReadOnlyList<string>? a, IReadOnlyList<string>? b)
    {
        var left = (a ?? []).Select(x => x.Trim()).Where(x => x.Length > 0).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var right = (b ?? []).Select(x => x.Trim()).Where(x => x.Length > 0).OrderBy(x => x, StringComparer.Ordinal).ToList();
        return left.SequenceEqual(right, StringComparer.Ordinal);
    }

    private static bool ItineraryEqual(
        IReadOnlyList<DayItinerary> existing,
        IReadOnlyList<CreateDayItineraryRequestDto> requested)
    {
        var left = existing.OrderBy(d => d.DayNumber).ToList();
        var right = requested.OrderBy(d => d.DayNumber).ToList();
        if (left.Count != right.Count) return false;

        for (var i = 0; i < left.Count; i++)
        {
            var e = left[i];
            var r = right[i];
            if (e.DayNumber != r.DayNumber) return false;
            if (NormalizeUtc(e.Date).Date != NormalizeUtc(r.Date).Date) return false;
            if (e.HotelId != NormalizeOptionalGuid(r.HotelId)) return false;
            if (!StringEqual(e.RoomType, r.RoomType)) return false;
            if (e.NumberOfRooms != r.NumberOfRooms) return false;
            if (!StringEqual(e.CheckInTime, r.CheckInTime)) return false;
            if (!StringEqual(e.CheckOutTime, r.CheckOutTime)) return false;
            if (e.MealPlan != (r.MealPlan ?? AccommodationMealPlan.MAP)) return false;
            if (e.ExtraBedCount != r.ExtraBedCount) return false;
            if (e.CnbCount != r.CnbCount) return false;
            if (e.HotelCost != r.HotelCost) return false;
            if (!StringEqual(e.Notes, r.Notes)) return false;
            if (e.ItineraryTemplateId != NormalizeOptionalGuid(r.ItineraryTemplateId)) return false;
            if (!StringListEqual(e.Activities, r.Activities)) return false;
            if (!StringListEqual(e.Meals, r.Meals)) return false;
        }

        return true;
    }

    private static Guid? NormalizeOptionalGuid(Guid? id) =>
        id is { } g && g != Guid.Empty ? g : null;

    private async Task<Dictionary<Guid, int>> GetPackageLogCountsByLeadAsync(List<Guid> leadIds, CancellationToken ct)
    {
        if (leadIds.Count == 0)
            return new Dictionary<Guid, int>();

        try
        {
            return await _db.PackageLogs.AsNoTracking()
                .Where(l => l.TenantId == TenantId && leadIds.Contains(l.LeadId))
                .GroupBy(l => l.LeadId)
                .Select(g => new { LeadId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.LeadId, x => x.Count, ct);
        }
        catch (PostgresException ex) when (IsPackageLogSchemaException(ex))
        {
            return new Dictionary<Guid, int>();
        }
    }

    private async Task<bool> IsLatestPackageForLeadAsync(TourPackage pkg, CancellationToken ct)
    {
        if (!pkg.LeadId.HasValue) return false;

        var latestId = await _db.Packages.AsNoTracking()
            .Where(x => x.LeadId == pkg.LeadId && x.TenantId == TenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(ct);

        return latestId == pkg.Id;
    }

    private static PackageDto ToDto(TourPackage p)
    {
        var vehicleName = p.Vehicle is null
            ? null
            : $"{p.Vehicle.VehicleType}{(string.IsNullOrWhiteSpace(p.Vehicle.VehicleModel) ? "" : " - " + p.Vehicle.VehicleModel)}";

        return new PackageDto
        {
            Id = p.Id.ToString("D"),
            LeadId = p.LeadId?.ToString("D"),
            ClientName = p.ClientName,
            ClientPhone = p.ClientPhone,
            ClientEmail = p.ClientEmail,
            ClientCity = p.ClientCity,
            ClientState = p.ClientState,
            ClientPickupLocation = p.ClientPickupLocation,
            ClientDropLocation = p.ClientDropLocation,
            PackageName = p.PackageName,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            NumberOfDays = p.NumberOfDays,
            NumberOfAdults = p.NumberOfAdults,
            NumberOfChildren = p.NumberOfChildren,
            VehicleId = p.VehicleId?.ToString("D"),
            VehicleName = vehicleName,
            VehicleRate = null,
            TotalAmount = p.TotalAmount,
            MarginAmount = p.MarginAmount,
            Discount = p.Discount,
            FinalAmount = Math.Max(0, p.TotalAmount - p.Discount),
            AdvanceAmount = p.AdvanceAmount,
            BalanceAmount = p.BalanceAmount,
            Status = p.Status,
            IsLocked = p.IsLocked,
            InclusionIds = p.InclusionIds ?? new List<string>(),
            ExclusionIds = p.ExclusionIds ?? new List<string>(),
            DayWiseItinerary = p.DayWiseItinerary
                .OrderBy(d => d.DayNumber)
                .Select(d => new DayItineraryDto
                {
                    Id = d.Id.ToString("D"),
                    PackageId = d.PackageId.ToString("D"),
                    DayNumber = d.DayNumber,
                    Date = d.Date,
                    HotelId = d.HotelId?.ToString("D"),
                    HotelName = d.Hotel?.Name,
                    RoomType = d.RoomType,
                    NumberOfRooms = d.NumberOfRooms,
                    CheckInTime = d.CheckInTime,
                    CheckOutTime = d.CheckOutTime,
                    MealPlan = d.MealPlan,
                    ExtraBedCount = d.ExtraBedCount,
                    CnbCount = d.CnbCount,
                    Activities = d.Activities,
                    Meals = d.Meals,
                    Notes = d.Notes,
                    TemplateId = d.ItineraryTemplateId?.ToString("D"),
                    TemplateTitle = d.ItineraryTemplate?.Title,
                    HotelCost = d.HotelCost
                }).ToList(),
            TenantId = p.TenantId.ToString("D"),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            CreatedBy = p.CreatedBy
        };
    }
}

