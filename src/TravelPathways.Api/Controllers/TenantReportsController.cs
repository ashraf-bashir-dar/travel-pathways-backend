using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;

namespace TravelPathways.Api.Controllers;

/// <summary>Tenant Admin reports: confirmed packages by employee, vendor payables (hotels, houseboats, transport). Reports module must be enabled for tenant.</summary>
[ApiController]
[Authorize]
[Route("api/tenant/reports")]
public sealed class TenantReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tenant;

    public TenantReportsController(AppDbContext db, TenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>Ensure tenant has Vendor Management (or legacy Reports) module enabled for vendor endpoints. Null/empty EnabledModules = allow.</summary>
    private async Task<ActionResult?> EnsureVendorManagementModuleAsync(CancellationToken ct)
    {
        if (_tenant.IsSuperAdmin) return null;
        if (!_tenant.TenantId.HasValue) return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));
        var enabledModules = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenant.TenantId.Value)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct);
        if (enabledModules == null || enabledModules.Count == 0) return null;
        if (enabledModules.Contains(AppModuleKey.VendorManagement) || enabledModules.Contains(AppModuleKey.Reports))
            return null;
        return StatusCode(403, ApiResponse<object>.Fail("Vendor Management module is not enabled for this tenant."));
    }

    /// <summary>Ensure tenant has EmployeeManagement module enabled. Used for confirmed-packages and other employee sub-modules. Null/empty EnabledModules = allow.</summary>
    private async Task<ActionResult?> EnsureEmployeeManagementModuleAsync(CancellationToken ct)
    {
        if (_tenant.IsSuperAdmin) return null;
        if (!_tenant.TenantId.HasValue) return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));
        var enabledModules = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenant.TenantId.Value)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct);
        if (enabledModules == null || enabledModules.Count == 0) return null;
        if (enabledModules.Contains(AppModuleKey.EmployeeManagement) || enabledModules.Contains(AppModuleKey.EmployeeMonitoring))
            return null;
        return StatusCode(403, ApiResponse<object>.Fail("Employee Management module is not enabled for this tenant."));
    }

    public sealed class ConfirmedPackageItemDto
    {
        public required string Id { get; init; }
        public required string PackageName { get; init; }
        public required string ClientName { get; init; }
        public required DateTime StartDate { get; init; }
        public required decimal TotalAmount { get; init; }
        public decimal FinalAmount { get; init; }
        public required string CreatedBy { get; init; }
        public string? CreatedByUserId { get; init; }
        public string? CreatedByUserName { get; init; }
        public required DateTime CreatedAt { get; init; }
    }

    public sealed class ConfirmedPackagesSummaryDto
    {
        public required string UserId { get; init; }
        public required string UserName { get; init; }
        public int ConfirmedCount { get; init; }
        public decimal TotalAmount { get; init; }
    }

    public sealed class ConfirmedPackagesReportDto
    {
        public required List<ConfirmedPackagesSummaryDto> Summary { get; init; }
        public required List<ConfirmedPackageItemDto> Packages { get; init; }
    }

    /// <summary>Get all confirmed (TripConfirmed) packages with employee resolution. Tenant Admin only. Requires EmployeeManagement module. Optional userId filter.</summary>
    [HttpGet("confirmed-packages")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<ConfirmedPackagesReportDto>>> GetConfirmedPackages(
        [FromQuery] Guid? userId = null,
        CancellationToken ct = default)
    {
        var moduleCheck = await EnsureEmployeeManagementModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;
        if (!_tenant.TenantId.HasValue)
            return BadRequest(ApiResponse<ConfirmedPackagesReportDto>.Fail("Tenant context is missing."));

        var tenantId = _tenant.TenantId.Value;
        var query = _db.Packages.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Status == PackageStatus.TripConfirmed);

        if (userId.HasValue)
        {
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId.Value && u.TenantId == tenantId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(ct);
            if (user is null)
                return BadRequest(ApiResponse<ConfirmedPackagesReportDto>.Fail("User not found."));
            query = query.Where(p => p.CreatedBy == user);
        }

        var packages = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.PackageName,
                p.ClientName,
                p.StartDate,
                p.TotalAmount,
                p.Discount,
                p.CreatedBy,
                p.CreatedAt
            })
            .ToListAsync(ct);

        var emails = packages.Select(p => p.CreatedBy).Distinct().ToList();
        var usersByEmail = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && emails.Contains(u.Email))
            .Select(u => new { u.Email, u.Id, u.FirstName, u.LastName })
            .ToListAsync(ct);
        var byEmail = usersByEmail.ToDictionary(u => u.Email, u => new { u.Id, Name = $"{u.FirstName} {u.LastName}".Trim() });

        var packageItems = packages.Select(p =>
        {
            var resolved = byEmail.TryGetValue(p.CreatedBy, out var u);
            return new ConfirmedPackageItemDto
            {
                Id = p.Id.ToString("D"),
                PackageName = p.PackageName,
                ClientName = p.ClientName,
                StartDate = p.StartDate,
                TotalAmount = p.TotalAmount,
                FinalAmount = Math.Max(0, p.TotalAmount - p.Discount),
                CreatedBy = p.CreatedBy,
                CreatedByUserId = resolved ? u!.Id.ToString("D") : null,
                CreatedByUserName = resolved ? (u!.Name ?? "Unknown") : null,
                CreatedAt = p.CreatedAt
            };
        }).ToList();

        var summaryGroups = packageItems
            .Where(x => x.CreatedByUserId != null)
            .GroupBy(x => (UserId: x.CreatedByUserId!, UserName: x.CreatedByUserName ?? "Unknown"))
            .Select(g => new ConfirmedPackagesSummaryDto
            {
                UserId = g.Key.UserId,
                UserName = g.Key.UserName,
                ConfirmedCount = g.Count(),
                TotalAmount = g.Sum(x => x.FinalAmount)
            })
            .OrderByDescending(x => x.ConfirmedCount)
            .ToList();

        var report = new ConfirmedPackagesReportDto { Summary = summaryGroups, Packages = packageItems };
        return ApiResponse<ConfirmedPackagesReportDto>.Ok(report);
    }

    // ----- Vendor payables (Reports module) -----

    public sealed class VendorSummaryItemDto
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Type { get; init; } // "Hotel", "Houseboat", "Transport"
        public decimal Payable { get; init; }
        public decimal Paid { get; init; }
        public decimal Balance => Payable - Paid;
    }

    public sealed class VendorSummaryDto
    {
        public required List<VendorSummaryItemDto> Hotels { get; init; }
        public required List<VendorSummaryItemDto> Houseboats { get; init; }
        public required List<VendorSummaryItemDto> TransportCompanies { get; init; }
    }

    /// <summary>Vendor payables summary: all hotels, houseboats, transport with payable/paid/balance. Tenant Admin + Reports module.</summary>
    [HttpGet("vendor-summary")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<VendorSummaryDto>>> GetVendorSummary(
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        var check = await EnsureVendorManagementModuleAsync(ct);
        if (check != null) return check;
        if (!_tenant.TenantId.HasValue) return BadRequest(ApiResponse<VendorSummaryDto>.Fail("Tenant context is missing."));
        var tenantId = _tenant.TenantId.Value;

        var hotelPayable = await _db.DayItineraries.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.HotelId != null && d.Package != null && d.Package.Status == PackageStatus.TripConfirmed && !d.IsDeleted && !d.Package.IsDeleted)
            .GroupBy(d => d.HotelId!.Value)
            .Select(g => new { HotelId = g.Key, Payable = g.Sum(d => d.HotelCost) })
            .ToListAsync(ct);
        var hotelIds = hotelPayable.Select(x => x.HotelId).Distinct().ToList();
        var hotels = await _db.Hotels.AsNoTracking()
            .Where(h => hotelIds.Contains(h.Id) && h.TenantId == tenantId && !h.IsDeleted)
            .Select(h => new { h.Id, h.Name, h.IsHouseboat })
            .ToListAsync(ct);
        var hotelPaidQuery = _db.Payments.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.PaymentType == PaymentType.Made && p.HotelId != null && !p.IsDeleted);
        if (dateFrom.HasValue) hotelPaidQuery = hotelPaidQuery.Where(p => p.PaymentDate >= dateFrom.Value);
        if (dateTo.HasValue) hotelPaidQuery = hotelPaidQuery.Where(p => p.PaymentDate <= dateTo.Value);
        var hotelPaid = await hotelPaidQuery
            .GroupBy(p => p.HotelId!.Value)
            .Select(g => new { HotelId = g.Key, Paid = g.Sum(p => p.Amount) })
            .ToListAsync(ct);
        var hotelPayableDict = hotelPayable.ToDictionary(x => x.HotelId, x => x.Payable);
        var hotelPaidDict = hotelPaid.ToDictionary(x => x.HotelId, x => x.Paid);
        var hotelList = new List<VendorSummaryItemDto>();
        var houseboatList = new List<VendorSummaryItemDto>();
        foreach (var h in hotels)
        {
            var payable = hotelPayableDict.GetValueOrDefault(h.Id, 0m);
            var paid = hotelPaidDict.GetValueOrDefault(h.Id, 0m);
            var item = new VendorSummaryItemDto { Id = h.Id.ToString("D"), Name = h.Name, Type = h.IsHouseboat ? "Houseboat" : "Hotel", Payable = payable, Paid = paid };
            if (h.IsHouseboat) houseboatList.Add(item); else hotelList.Add(item);
        }

        var transportPayableList = await TransportPayableListAsync(_db, tenantId, ct);
        var transportCompanyIds = transportPayableList.Select(x => x.TransportCompanyId).Distinct().ToList();
        var companies = await _db.TransportCompanies.AsNoTracking()
            .Where(c => transportCompanyIds.Contains(c.Id) && c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(ct);
        var transportPaidQuery = _db.Payments.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.PaymentType == PaymentType.Made && p.TransportCompanyId != null && !p.IsDeleted);
        if (dateFrom.HasValue) transportPaidQuery = transportPaidQuery.Where(p => p.PaymentDate >= dateFrom.Value);
        if (dateTo.HasValue) transportPaidQuery = transportPaidQuery.Where(p => p.PaymentDate <= dateTo.Value);
        var transportPaid = await transportPaidQuery
            .GroupBy(p => p.TransportCompanyId!.Value)
            .Select(g => new { TransportCompanyId = g.Key, Paid = g.Sum(p => p.Amount) })
            .ToListAsync(ct);
        var transportPayableDict = transportPayableList.ToDictionary(x => x.TransportCompanyId, x => x.Amount);
        var transportPaidDict = transportPaid.ToDictionary(x => x.TransportCompanyId, x => x.Paid);
        var transportList = companies.Select(c => new VendorSummaryItemDto
        {
            Id = c.Id.ToString("D"),
            Name = c.Name,
            Type = "Transport",
            Payable = transportPayableDict.GetValueOrDefault(c.Id, 0m),
            Paid = transportPaidDict.GetValueOrDefault(c.Id, 0m)
        }).ToList();

        var summary = new VendorSummaryDto { Hotels = hotelList, Houseboats = houseboatList, TransportCompanies = transportList };
        return ApiResponse<VendorSummaryDto>.Ok(summary);
    }

    public sealed class HotelPayableRowDto
    {
        public required string PackageId { get; init; }
        public required string PackageName { get; init; }
        public DateTime Date { get; init; }
        public decimal Amount { get; init; }
    }

    public sealed class PaymentRowDto
    {
        public required string Id { get; init; }
        public decimal Amount { get; init; }
        public DateTime PaymentDate { get; init; }
        public string? Reference { get; init; }
        public string? ScreenshotUrl { get; init; }
    }

    public sealed class HotelPayablesDetailDto
    {
        public required string HotelId { get; init; }
        public required string HotelName { get; init; }
        public bool IsHouseboat { get; init; }
        public decimal TotalPayable { get; init; }
        public decimal TotalPaid { get; init; }
        public decimal Balance => TotalPayable - TotalPaid;
        public required List<HotelPayableRowDto> PayableBreakdown { get; init; }
        public required List<PaymentRowDto> Payments { get; init; }
    }

    /// <summary>Hotel/houseboat payables detail: payable from confirmed day itineraries, paid from payments. Optional date range filters paid list.</summary>
    [HttpGet("hotel-payables")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<HotelPayablesDetailDto>>> GetHotelPayables(
        [FromQuery] Guid hotelId,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        var check = await EnsureVendorManagementModuleAsync(ct);
        if (check != null) return check;
        if (!_tenant.TenantId.HasValue) return BadRequest(ApiResponse<HotelPayablesDetailDto>.Fail("Tenant context is missing."));
        var tenantId = _tenant.TenantId.Value;

        var hotel = await _db.Hotels.AsNoTracking()
            .Where(h => h.Id == hotelId && h.TenantId == tenantId && !h.IsDeleted)
            .Select(h => new { h.Id, h.Name, h.IsHouseboat })
            .FirstOrDefaultAsync(ct);
        if (hotel == null) return NotFound(ApiResponse<HotelPayablesDetailDto>.Fail("Hotel not found."));

        var breakdown = await _db.DayItineraries.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.HotelId == hotelId && d.Package != null && d.Package.Status == PackageStatus.TripConfirmed && !d.IsDeleted && !d.Package.IsDeleted)
            .Select(d => new { d.PackageId, d.Package!.PackageName, d.Date, d.HotelCost })
            .ToListAsync(ct);
        var totalPayable = breakdown.Sum(x => x.HotelCost);
        var payableRows = breakdown.Select(x => new HotelPayableRowDto
        {
            PackageId = x.PackageId.ToString("D"),
            PackageName = x.PackageName,
            Date = x.Date,
            Amount = x.HotelCost
        }).ToList();

        var paymentsQuery = _db.Payments.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.PaymentType == PaymentType.Made && p.HotelId == hotelId && !p.IsDeleted);
        if (dateFrom.HasValue) paymentsQuery = paymentsQuery.Where(p => p.PaymentDate >= dateFrom.Value);
        if (dateTo.HasValue) paymentsQuery = paymentsQuery.Where(p => p.PaymentDate <= dateTo.Value);
        var payments = await paymentsQuery
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new { p.Id, p.Amount, p.PaymentDate, p.Reference, p.ScreenshotUrl })
            .ToListAsync(ct);
        var totalPaid = payments.Sum(p => p.Amount);
        var paymentRows = payments.Select(p => new PaymentRowDto
        {
            Id = p.Id.ToString("D"),
            Amount = p.Amount,
            PaymentDate = p.PaymentDate,
            Reference = p.Reference,
            ScreenshotUrl = p.ScreenshotUrl
        }).ToList();

        var detail = new HotelPayablesDetailDto
        {
            HotelId = hotel.Id.ToString("D"),
            HotelName = hotel.Name,
            IsHouseboat = hotel.IsHouseboat,
            TotalPayable = totalPayable,
            TotalPaid = totalPaid,
            PayableBreakdown = payableRows,
            Payments = paymentRows
        };
        return ApiResponse<HotelPayablesDetailDto>.Ok(detail);
    }

    public sealed class TransportPayableRowDto
    {
        public required string PackageId { get; init; }
        public required string PackageName { get; init; }
        public int NumberOfDays { get; init; }
        public decimal Amount { get; init; }
    }

    public sealed class TransportPayablesDetailDto
    {
        public required string TransportCompanyId { get; init; }
        public required string TransportCompanyName { get; init; }
        public decimal TotalPayable { get; init; }
        public decimal TotalPaid { get; init; }
        public decimal Balance => TotalPayable - TotalPaid;
        public required List<TransportPayableRowDto> PayableBreakdown { get; init; }
        public required List<PaymentRowDto> Payments { get; init; }
    }

    /// <summary>Transport company payables detail. Optional date range filters paid list.</summary>
    [HttpGet("transport-payables")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<TransportPayablesDetailDto>>> GetTransportPayables(
        [FromQuery] Guid transportCompanyId,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        var check = await EnsureVendorManagementModuleAsync(ct);
        if (check != null) return check;
        if (!_tenant.TenantId.HasValue) return BadRequest(ApiResponse<TransportPayablesDetailDto>.Fail("Tenant context is missing."));
        var tenantId = _tenant.TenantId.Value;

        var company = await _db.TransportCompanies.AsNoTracking()
            .Where(c => c.Id == transportCompanyId && c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => new { c.Id, c.Name })
            .FirstOrDefaultAsync(ct);
        if (company == null) return NotFound(ApiResponse<TransportPayablesDetailDto>.Fail("Transport company not found."));

        var packages = await _db.Packages.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Status == PackageStatus.TripConfirmed && p.VehicleId != null && !p.IsDeleted
                && p.Vehicle!.TransportCompanyId == transportCompanyId)
            .Select(p => new { p.Id, p.PackageName, p.NumberOfDays, p.StartDate, p.VehicleId })
            .ToListAsync(ct);
        var vehicleIds = packages.Select(p => p.VehicleId!.Value).Distinct().ToList();
        var allPricing = await _db.VehiclePricing.AsNoTracking()
            .Where(vp => vp.TenantId == tenantId && vehicleIds.Contains(vp.VehicleId) && !vp.IsDeleted)
            .ToListAsync(ct);
        var breakdown = new List<TransportPayableRowDto>();
        decimal totalPayable = 0m;
        foreach (var pkg in packages)
        {
            var pricing = allPricing
                .Where(vp => vp.VehicleId == pkg.VehicleId && vp.FromDate <= pkg.StartDate && (vp.ToDate == null || vp.ToDate >= pkg.StartDate))
                .OrderByDescending(vp => vp.FromDate)
                .FirstOrDefault();
            var amount = pricing == null ? 0m : ApplyRateType(pricing.CostPrice, pricing.RateType, pkg.NumberOfDays);
            totalPayable += amount;
            breakdown.Add(new TransportPayableRowDto
            {
                PackageId = pkg.Id.ToString("D"),
                PackageName = pkg.PackageName,
                NumberOfDays = pkg.NumberOfDays,
                Amount = amount
            });
        }

        var paymentsQuery = _db.Payments.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.PaymentType == PaymentType.Made && p.TransportCompanyId == transportCompanyId && !p.IsDeleted);
        if (dateFrom.HasValue) paymentsQuery = paymentsQuery.Where(p => p.PaymentDate >= dateFrom.Value);
        if (dateTo.HasValue) paymentsQuery = paymentsQuery.Where(p => p.PaymentDate <= dateTo.Value);
        var payments = await paymentsQuery
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new { p.Id, p.Amount, p.PaymentDate, p.Reference, p.ScreenshotUrl })
            .ToListAsync(ct);
        var totalPaid = payments.Sum(p => p.Amount);
        var paymentRows = payments.Select(p => new PaymentRowDto
        {
            Id = p.Id.ToString("D"),
            Amount = p.Amount,
            PaymentDate = p.PaymentDate,
            Reference = p.Reference,
            ScreenshotUrl = p.ScreenshotUrl
        }).ToList();

        var detail = new TransportPayablesDetailDto
        {
            TransportCompanyId = company.Id.ToString("D"),
            TransportCompanyName = company.Name,
            TotalPayable = totalPayable,
            TotalPaid = totalPaid,
            PayableBreakdown = breakdown,
            Payments = paymentRows
        };
        return ApiResponse<TransportPayablesDetailDto>.Ok(detail);
    }

    private static decimal ApplyRateType(decimal costPrice, RateType? rateType, int numberOfDays)
    {
        switch (rateType)
        {
            case RateType.PerTrip:
            case RateType.Flat:
                return costPrice;
            case RateType.PerKm:
                return costPrice * numberOfDays;
            default:
                return costPrice * numberOfDays;
        }
    }

    private static async Task<List<(Guid TransportCompanyId, decimal Amount)>> TransportPayableListAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var packages = await db.Packages.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Status == PackageStatus.TripConfirmed && p.VehicleId != null && !p.IsDeleted)
            .Select(p => new { p.VehicleId, p.NumberOfDays, p.StartDate })
            .ToListAsync(ct);
        if (packages.Count == 0) return new List<(Guid, decimal)>();
        var vehicleIds = packages.Select(p => p.VehicleId!.Value).Distinct().ToList();
        var vehicles = await db.Vehicles.AsNoTracking()
            .Where(v => vehicleIds.Contains(v.Id))
            .Select(v => new { v.Id, v.TransportCompanyId })
            .ToListAsync(ct);
        var vehicleToCompany = vehicles.ToDictionary(v => v.Id, v => v.TransportCompanyId);
        var allPricing = await db.VehiclePricing.AsNoTracking()
            .Where(vp => vp.TenantId == tenantId && vehicleIds.Contains(vp.VehicleId) && !vp.IsDeleted)
            .ToListAsync(ct);
        var byCompany = new Dictionary<Guid, decimal>();
        foreach (var pkg in packages)
        {
            var companyId = vehicleToCompany.GetValueOrDefault(pkg.VehicleId!.Value);
            if (companyId == Guid.Empty) continue;
            var pricing = allPricing
                .Where(vp => vp.VehicleId == pkg.VehicleId && vp.FromDate <= pkg.StartDate && (vp.ToDate == null || vp.ToDate >= pkg.StartDate))
                .OrderByDescending(vp => vp.FromDate)
                .FirstOrDefault();
            var cost = pricing == null ? 0m : ApplyRateType(pricing.CostPrice, pricing.RateType, pkg.NumberOfDays);
            byCompany[companyId] = byCompany.GetValueOrDefault(companyId) + cost;
        }
        return byCompany.Select(kv => (kv.Key, kv.Value)).ToList();
    }
}
