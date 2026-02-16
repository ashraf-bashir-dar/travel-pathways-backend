using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;
using TravelPathways.Api.Services;
using System.Globalization;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/packages")]
public sealed class PackagesController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPackagePdfGenerator _pdfGenerator;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;

    public PackagesController(AppDbContext db, TenantContext tenant, IPackagePdfGenerator pdfGenerator, IConfiguration configuration, IWebHostEnvironment env) : base(tenant)
    {
        _db = db;
        _pdfGenerator = pdfGenerator;
        _configuration = configuration;
        _env = env;
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
        public decimal Discount { get; init; }
        public decimal FinalAmount { get; init; }
        public required decimal AdvanceAmount { get; init; }
        public required decimal BalanceAmount { get; init; }
        public required PackageStatus Status { get; init; }
        public List<string>? InclusionIds { get; init; }
        public List<DayItineraryDto>? DayWiseItinerary { get; init; }
        public required string TenantId { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
        public required string CreatedBy { get; init; }
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
        public decimal TotalAmount { get; set; }
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
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Packages.AsNoTracking()
            .Include(p => p.DayWiseItinerary)
            .Include(p => p.Vehicle)
            .Where(p => p.TenantId == TenantId);

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
            query = query.Where(p => p.StartDate.Date >= arrivalDateFrom.Value.Date);
        if (arrivalDateTo.HasValue)
            query = query.Where(p => p.StartDate.Date <= arrivalDateTo.Value.Date);

        var total = await query.CountAsync(ct);
        var list = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = list.Select(ToDto).ToList();
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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PackageDto>>> GetPackageById([FromRoute] Guid id, CancellationToken ct)
    {
        var pkg = await _db.Packages.AsNoTracking()
            .Include(p => p.DayWiseItinerary)
            .ThenInclude(d => d.Hotel)
            .Include(p => p.DayWiseItinerary)
            .ThenInclude(d => d.ItineraryTemplate)
            .Include(p => p.Vehicle)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);

        if (pkg is null) return NotFound(ApiResponse<PackageDto>.Fail("Package not found"));
        return ApiResponse<PackageDto>.Ok(ToDto(pkg));
    }

    [HttpGet("by-lead/{leadId:guid}")]
    public async Task<ActionResult<ApiResponse<List<PackageDto>>>> GetByLead([FromRoute] Guid leadId, CancellationToken ct)
    {
        var pkgs = await _db.Packages.AsNoTracking()
            .Include(p => p.DayWiseItinerary)
            .Include(p => p.Vehicle)
            .Where(p => p.TenantId == TenantId && p.LeadId == leadId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return ApiResponse<List<PackageDto>>.Ok(pkgs.Select(ToDto).ToList());
    }

    /// <summary>Generate and download package PDF (client-facing proposal).</summary>
    [HttpGet("{id:guid}/pdf")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetPackagePdf([FromRoute] Guid id, CancellationToken ct)
    {
        var pkg = await _db.Packages.AsNoTracking()
            .Include(p => p.DayWiseItinerary!)
            .ThenInclude(d => d.Hotel)
            .Include(p => p.DayWiseItinerary!)
            .ThenInclude(d => d.ItineraryTemplate)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);

        if (pkg is null) return NotFound();

        Tenant? tenant = null;
        if (pkg.TenantId != Guid.Empty)
            tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == pkg.TenantId, ct);

        try
        {
            // Use configured base URL so PDF images load on live (Azure). Request.Scheme/Host can be wrong behind a proxy.
            var baseUrl = _configuration["Api:BaseUrl"]?.Trim()
                ?? _configuration["Api__BaseUrl"]?.Trim()
                ?? _configuration["PdfGenerator:BaseUrl"]?.Trim()
                ?? _configuration["PdfGenerator__BaseUrl"]?.Trim();
            if (string.IsNullOrEmpty(baseUrl))
                baseUrl = $"{Request.Scheme}://{Request.Host}";
            baseUrl = baseUrl.TrimEnd('/');
            var model = BuildPdfModel(pkg, tenant, baseUrl);
            model = InlinePdfImagesFromDisk(model);
            var pdfBytes = await _pdfGenerator.GenerateAsync(model, ct);

            var filename = BuildPdfFilename(pkg);
            return File(pdfBytes, "application/pdf", filename);
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, ApiResponse<object>.Fail($"PDF generation failed: {inner}"));
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

        var totalAmount = request.TotalAmount;
        var discount = request.Discount;
        var finalAmount = Math.Max(0, totalAmount - discount);
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
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NumberOfDays = Math.Max(1, request.DayWiseItinerary.Count),
            NumberOfAdults = request.NumberOfAdults,
            NumberOfChildren = request.NumberOfChildren,
            VehicleId = request.VehicleId,
            TotalAmount = totalAmount,
            Discount = discount,
            AdvanceAmount = 0,
            BalanceAmount = finalAmount,
            Status = (PackageStatus)lead.Status,
            InclusionIds = request.InclusionIds ?? new List<string>(),
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
                Date = d.Date,
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
        var pkg = await _db.Packages.Include(p => p.DayWiseItinerary).FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (pkg is null) return NotFound(ApiResponse<PackageDto>.Fail("Package not found"));

        var leadOk = await _db.Leads.AnyAsync(l => l.Id == request.LeadId && l.TenantId == TenantId, ct);
        if (!leadOk) return BadRequest(ApiResponse<PackageDto>.Fail("Lead not found"));

        if (request.VehicleId is not null)
        {
            var vehicleOk = await _db.Vehicles.AnyAsync(v => v.Id == request.VehicleId && v.TenantId == TenantId, ct);
            if (!vehicleOk) return BadRequest(ApiResponse<PackageDto>.Fail("Vehicle not found"));
        }

        pkg.LeadId = request.LeadId;
        pkg.ClientName = request.ClientName.Trim();
        pkg.ClientPhone = request.ClientPhone.Trim();
        pkg.ClientEmail = request.ClientEmail?.Trim();
        pkg.ClientCity = request.ClientCity?.Trim();
        pkg.ClientState = request.ClientState?.Trim();
        pkg.ClientPickupLocation = request.ClientPickupLocation.Trim();
        pkg.ClientDropLocation = request.ClientDropLocation.Trim();
        pkg.PackageName = request.PackageName.Trim();
        pkg.StartDate = request.StartDate;
        pkg.EndDate = request.EndDate;
        pkg.NumberOfDays = Math.Max(1, request.DayWiseItinerary.Count);
        pkg.NumberOfAdults = request.NumberOfAdults;
        pkg.NumberOfChildren = request.NumberOfChildren;
        pkg.VehicleId = request.VehicleId;
        pkg.TotalAmount = request.TotalAmount;
        pkg.Discount = request.Discount;
        var finalAmount = Math.Max(0, pkg.TotalAmount - request.Discount);
        pkg.AdvanceAmount = request.AdvanceAmount;
        pkg.BalanceAmount = finalAmount - request.AdvanceAmount;
        pkg.Status = request.Status;
        pkg.InclusionIds = request.InclusionIds ?? new List<string>();

        // When package status changes: sync to the lead and to all packages for that lead so Leads and Packages tabs stay in sync
        if (pkg.LeadId.HasValue)
        {
            var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == pkg.LeadId.Value && l.TenantId == TenantId, ct);
            if (lead != null)
            {
                lead.Status = (LeadStatus)(int)request.Status;
                var allPackagesForLead = await _db.Packages.Where(x => x.LeadId == pkg.LeadId && x.TenantId == TenantId).ToListAsync(ct);
                foreach (var p in allPackagesForLead)
                    p.Status = request.Status;
            }
        }

        // Replace itinerary
        var existing = await _db.DayItineraries.Where(d => d.PackageId == pkg.Id && d.TenantId == TenantId).ToListAsync(ct);
        _db.DayItineraries.RemoveRange(existing);

        foreach (var d in request.DayWiseItinerary)
        {
            _db.DayItineraries.Add(new DayItinerary
            {
                TenantId = TenantId,
                PackageId = pkg.Id,
                DayNumber = d.DayNumber,
                Date = d.Date,
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

        var updated = await _db.Packages.AsNoTracking()
            .Include(p => p.DayWiseItinerary).ThenInclude(d => d.Hotel)
            .Include(p => p.DayWiseItinerary).ThenInclude(d => d.ItineraryTemplate)
            .Include(p => p.Vehicle)
            .FirstAsync(p => p.Id == pkg.Id, ct);

        return ApiResponse<PackageDto>.Ok(ToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePackage([FromRoute] Guid id, CancellationToken ct)
    {
        var pkg = await _db.Packages.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (pkg is null) return NotFound(ApiResponse<object>.Fail("Package not found"));
        _db.Packages.Remove(pkg);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    /// <summary>Shikara charge: 700 × ceil(adults/4). Always added into package total; not shown in PDF.</summary>
    private static decimal GetShikaraCharge(int numberOfAdults)
    {
        var numShikaras = numberOfAdults <= 0 ? 0 : (numberOfAdults + 3) / 4;
        return 700m * numShikaras;
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

    private static string MealPlanLabel(AccommodationMealPlan? plan)
    {
        if (!plan.HasValue) return "–";
        return plan.Value switch
        {
            AccommodationMealPlan.AP => "Breakfast + Lunch + Dinner",
            AccommodationMealPlan.MAP => "MAP (Dinner + Breakfast)",
            AccommodationMealPlan.CP => "CP",
            AccommodationMealPlan.BreakfastOnly => "Breakfast Only",
            AccommodationMealPlan.RoomOnly => "EP (Room only)",
            _ => plan.Value.ToString()
        };
    }

    private static PackagePdfModel BuildPdfModel(TourPackage pkg, Tenant? tenant, string baseUrl)
    {
        var days = (pkg.DayWiseItinerary ?? []).OrderBy(d => d.DayNumber).ToList();
        var nights = Math.Max(0, pkg.NumberOfDays - 1);
        var daysLabel = $"{nights}N / {pkg.NumberOfDays}D";

        var inclusionLabels = InclusionOptions.GetInclusionLabels(pkg.InclusionIds ?? []).ToList();
        var exclusionLabels = InclusionOptions.GetExclusionLabels(pkg.InclusionIds ?? []).ToList();

        string ToAbsolute(string? url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url;
            return url.StartsWith("/") ? baseUrl + url : baseUrl + "/" + url;
        }

        var pdfDays = days.Select(d =>
        {
            var title = (d.ItineraryTemplate?.Title ?? d.Hotel?.Name ?? "Day activities").Trim();
            if (string.IsNullOrEmpty(title)) title = "Day activities";
            var descParts = new List<string>();
            if (d.Activities?.Count > 0) descParts.Add(string.Join(". ", d.Activities));
            if (!string.IsNullOrWhiteSpace(d.Notes)) descParts.Add(d.Notes.Trim());
            var description = descParts.Count > 0 ? string.Join(" ", descParts) : "–";
            return new DayItem { DayNumber = d.DayNumber, Title = title, Description = description };
        }).ToList();

        var seenHotels = new Dictionary<Guid, (Hotel Hotel, string MealPlan, int Nights)>();
        foreach (var d in days)
        {
            if (d.HotelId is null || d.Hotel is null) continue;
            var meal = MealPlanLabel(d.MealPlan);
            if (seenHotels.TryGetValue(d.HotelId.Value, out var existing))
                seenHotels[d.HotelId.Value] = (existing.Hotel, existing.MealPlan, existing.Nights + 1);
            else
                seenHotels[d.HotelId.Value] = (d.Hotel, meal, 1);
        }

        var pdfHotels = seenHotels.Values.Select(x => new HotelItem
        {
            Name = x.Hotel.Name ?? "–",
            Location = GetLocationString(x.Hotel),
            StarRating = x.Hotel.StarRating ?? 0,
            MealPlan = x.MealPlan,
            Nights = x.Nights,
            IsHouseboat = x.Hotel.IsHouseboat,
            Amenities = x.Hotel.Amenities ?? [],
            ImageUrls = (x.Hotel.ImageUrls ?? []).Select(ToAbsolute).Where(u => !string.IsNullOrEmpty(u)).ToList()
        }).ToList();

        var coverImageUrls = pdfHotels.SelectMany(h => h.ImageUrls).Take(4).ToList();

        var firstDay = days.FirstOrDefault();
        string Fmt(decimal v) => v.ToString("N0", CultureInfo.GetCultureInfo("en-IN"));
        string FmtDate(DateTime dt) => dt.ToString("d MMM yyyy", CultureInfo.GetCultureInfo("en-IN"));
        // PDF amounts always include Shikara (700 × ceil(adults/4)) so Total and Final show the full package amount
        var shikaraCharge = GetShikaraCharge(pkg.NumberOfAdults);
        var totalWithShikara = pkg.TotalAmount + shikaraCharge;
        var finalAmount = Math.Max(0, totalWithShikara - pkg.Discount);
        var balanceWithShikara = Math.Max(0, finalAmount - pkg.AdvanceAmount);
        var chargeablePax = pkg.NumberOfAdults + pkg.NumberOfChildren; // use NumberOfChildrenAbove6 when available
        var perPerson = chargeablePax > 0 ? finalAmount / chargeablePax : finalAmount;

        var clientAddress = string.Join(", ", new[] { pkg.ClientCity, pkg.ClientState }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        if (string.IsNullOrEmpty(clientAddress) && !string.IsNullOrWhiteSpace(pkg.ClientPickupLocation))
            clientAddress = pkg.ClientPickupLocation.Trim();

        return new PackagePdfModel
        {
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
            NumberOfAdults = pkg.NumberOfAdults,
            NumberOfChildren = pkg.NumberOfChildren,
            MealPlanLabel = firstDay is not null ? MealPlanLabel(firstDay.MealPlan) : "–",
            FirstDayRooms = firstDay?.NumberOfRooms ?? 1,
            TotalAmount = "Rs. " + Fmt(totalWithShikara),
            Discount = pkg.Discount > 0 ? "Rs. " + Fmt(pkg.Discount) : "–",
            FinalAmount = "Rs. " + Fmt(finalAmount),
            PerPersonAmount = "Rs. " + Fmt(perPerson),
            AdvanceAmount = "Rs. " + Fmt(pkg.AdvanceAmount),
            BalanceAmount = "Rs. " + Fmt(balanceWithShikara),
            Days = pdfDays,
            Hotels = pdfHotels,
            CoverImageUrls = coverImageUrls,
            InclusionLabels = inclusionLabels,
            ExclusionLabels = exclusionLabels,
            AgencyName = tenant?.Name?.Trim(),
            AgencyPhone = tenant?.Phone?.Trim(),
            AgencyEmail = tenant?.Email?.Trim(),
            ManagingDirectorName = tenant?.ContactPerson?.Trim(),
            GeneratedDate = FmtDate(DateTime.UtcNow)
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
            IsHouseboat = h.IsHouseboat,
            Amenities = h.Amenities,
            ImageUrls = (h.ImageUrls ?? []).Select(ToDataUrl).Where(u => !string.IsNullOrEmpty(u)).Select(u => u!).ToList()
        }).ToList();
        var inlinedCover = inlinedHotels.SelectMany(h => h.ImageUrls).Take(4).ToList();

        return new PackagePdfModel
        {
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
            NumberOfAdults = model.NumberOfAdults,
            NumberOfChildren = model.NumberOfChildren,
            MealPlanLabel = model.MealPlanLabel,
            FirstDayRooms = model.FirstDayRooms,
            TotalAmount = model.TotalAmount,
            Discount = model.Discount,
            FinalAmount = model.FinalAmount,
            PerPersonAmount = model.PerPersonAmount,
            AdvanceAmount = model.AdvanceAmount,
            BalanceAmount = model.BalanceAmount,
            Days = model.Days,
            Hotels = inlinedHotels,
            CoverImageUrls = inlinedCover,
            InclusionLabels = model.InclusionLabels,
            ExclusionLabels = model.ExclusionLabels,
            AgencyName = model.AgencyName,
            AgencyPhone = model.AgencyPhone,
            AgencyEmail = model.AgencyEmail,
            ManagingDirectorName = model.ManagingDirectorName,
            GeneratedDate = model.GeneratedDate
        };
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
            Discount = p.Discount,
            FinalAmount = Math.Max(0, p.TotalAmount - p.Discount),
            AdvanceAmount = p.AdvanceAmount,
            BalanceAmount = p.BalanceAmount,
            Status = p.Status,
            InclusionIds = p.InclusionIds ?? new List<string>(),
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

