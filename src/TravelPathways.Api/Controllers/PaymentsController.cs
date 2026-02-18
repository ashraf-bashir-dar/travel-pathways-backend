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
[Route("api/payments")]
public sealed class PaymentsController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly FileStorage _storage;

    public PaymentsController(AppDbContext db, TenantContext tenant, FileStorage storage) : base(tenant)
    {
        _db = db;
        _storage = storage;
    }

    public sealed class PaymentDto
    {
        public required string Id { get; init; }
        public required PaymentType PaymentType { get; init; }
        public required decimal Amount { get; init; }
        public required DateTime PaymentDate { get; init; }
        public string? Reference { get; init; }
        public string? Notes { get; init; }
        public string? LeadId { get; init; }
        public string? ClientName { get; init; }
        public string? PackageId { get; init; }
        public string? PackageName { get; init; }
        public string? HotelId { get; init; }
        public string? HotelOrHouseboatName { get; init; }
        public bool? IsHouseboat { get; init; }
        public string? TransportCompanyId { get; init; }
        public string? TransportCompanyName { get; init; }
        public string? ScreenshotUrl { get; init; }
        public required string TenantId { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
    }

    public class CreatePaymentRequestDto
    {
        public PaymentType PaymentType { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public Guid? LeadId { get; set; }
        public Guid? PackageId { get; set; }
        public Guid? HotelId { get; set; }
        public Guid? TransportCompanyId { get; set; }
    }

    public sealed class UpdatePaymentRequestDto : CreatePaymentRequestDto { }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<PaymentDto>>>> GetPayments(
        [FromQuery] PaymentType? paymentType = null,
        [FromQuery] Guid? leadId = null,
        [FromQuery] Guid? hotelId = null,
        [FromQuery] Guid? transportCompanyId = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Payments
            .AsNoTracking()
            .Include(p => p.Lead)
            .Include(p => p.Package)
            .Include(p => p.Hotel)
            .Include(p => p.TransportCompany)
            .Where(p => p.TenantId == TenantId);

        if (paymentType.HasValue)
            query = query.Where(p => p.PaymentType == paymentType.Value);
        if (leadId.HasValue)
            query = query.Where(p => p.LeadId == leadId.Value);
        if (hotelId.HasValue)
            query = query.Where(p => p.HotelId == hotelId.Value);
        if (transportCompanyId.HasValue)
            query = query.Where(p => p.TransportCompanyId == transportCompanyId.Value);
        if (dateFrom.HasValue)
            query = query.Where(p => p.PaymentDate.Date >= dateFrom.Value.Date);
        if (dateTo.HasValue)
            query = query.Where(p => p.PaymentDate.Date <= dateTo.Value.Date);

        var total = await query.CountAsync(ct);
        var list = await query
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = list.Select(ToDto).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return ApiResponse<PaginatedResponse<PaymentDto>>.Ok(new PaginatedResponse<PaymentDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> GetPayment(Guid id, CancellationToken ct)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Lead)
            .Include(p => p.Package)
            .Include(p => p.Hotel)
            .Include(p => p.TransportCompany)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);

        if (payment == null)
            return NotFound(ApiResponse<PaymentDto>.Fail("Payment not found."));

        return ApiResponse<PaymentDto>.Ok(ToDto(payment));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> CreatePayment([FromBody] CreatePaymentRequestDto dto, CancellationToken ct)
    {
        var (valid, error) = ValidatePaymentDto(dto);
        if (!valid)
            return BadRequest(ApiResponse<PaymentDto>.Fail(error!));

        var payment = new Payment
        {
            TenantId = TenantId,
            PaymentType = dto.PaymentType,
            Amount = dto.Amount,
            PaymentDate = dto.PaymentDate,
            Reference = dto.Reference?.Trim(),
            Notes = dto.Notes?.Trim(),
            LeadId = dto.LeadId,
            PackageId = dto.PackageId,
            HotelId = dto.HotelId,
            TransportCompanyId = dto.TransportCompanyId
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        await _db.Entry(payment)
            .Reference(p => p.Lead).LoadAsync(ct);
        await _db.Entry(payment)
            .Reference(p => p.Package).LoadAsync(ct);
        await _db.Entry(payment)
            .Reference(p => p.Hotel).LoadAsync(ct);
        await _db.Entry(payment)
            .Reference(p => p.TransportCompany).LoadAsync(ct);

        return ApiResponse<PaymentDto>.Ok(ToDto(payment));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> UpdatePayment(Guid id, [FromBody] UpdatePaymentRequestDto dto, CancellationToken ct)
    {
        var (valid, error) = ValidatePaymentDto(dto);
        if (!valid)
            return BadRequest(ApiResponse<PaymentDto>.Fail(error!));

        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (payment == null)
            return NotFound(ApiResponse<PaymentDto>.Fail("Payment not found."));

        payment.PaymentType = dto.PaymentType;
        payment.Amount = dto.Amount;
        payment.PaymentDate = dto.PaymentDate;
        payment.Reference = dto.Reference?.Trim();
        payment.Notes = dto.Notes?.Trim();
        payment.LeadId = dto.LeadId;
        payment.PackageId = dto.PackageId;
        payment.HotelId = dto.HotelId;
        payment.TransportCompanyId = dto.TransportCompanyId;
        await _db.SaveChangesAsync(ct);

        await _db.Entry(payment)
            .Reference(p => p.Lead).LoadAsync(ct);
        await _db.Entry(payment)
            .Reference(p => p.Package).LoadAsync(ct);
        await _db.Entry(payment)
            .Reference(p => p.Hotel).LoadAsync(ct);
        await _db.Entry(payment)
            .Reference(p => p.TransportCompany).LoadAsync(ct);

        return ApiResponse<PaymentDto>.Ok(ToDto(payment));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePayment(Guid id, CancellationToken ct)
    {
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (payment == null)
            return NotFound(ApiResponse<object>.Fail("Payment not found."));

        _db.Payments.Remove(payment);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    /// <summary>Upload or replace screenshot for a payment (optional). Form key: file.</summary>
    [Consumes("multipart/form-data")]
    [HttpPost("{id:guid}/screenshot")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> UploadScreenshot(Guid id, IFormFile? file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<PaymentDto>.Fail("No file provided."));

        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == TenantId, ct);
        if (payment == null)
            return NotFound(ApiResponse<PaymentDto>.Fail("Payment not found."));

        var url = await _storage.SavePaymentScreenshotAsync(TenantId, id, file, ct);
        payment.ScreenshotUrl = url;
        await _db.SaveChangesAsync(ct);

        await _db.Entry(payment)
            .Reference(p => p.Lead).LoadAsync(ct);
        await _db.Entry(payment)
            .Reference(p => p.Package).LoadAsync(ct);
        await _db.Entry(payment)
            .Reference(p => p.Hotel).LoadAsync(ct);
        await _db.Entry(payment)
            .Reference(p => p.TransportCompany).LoadAsync(ct);

        return ApiResponse<PaymentDto>.Ok(ToDto(payment));
    }

    private static (bool Valid, string? Error) ValidatePaymentDto(CreatePaymentRequestDto dto)
    {
        if (dto.Amount <= 0)
            return (false, "Amount must be greater than zero.");
        if (dto.PaymentType == PaymentType.Received)
        {
            if (!dto.LeadId.HasValue)
                return (false, "Client (Lead) is required for payments received.");
        }
        else
        {
            var hasHotel = dto.HotelId.HasValue;
            var hasTransport = dto.TransportCompanyId.HasValue;
            if (hasHotel && hasTransport)
                return (false, "Specify either a hotel/houseboat or a transport company, not both.");
            if (!hasHotel && !hasTransport)
                return (false, "Either hotel/houseboat or transport company is required for payments made.");
        }
        return (true, null);
    }

    private static PaymentDto ToDto(Payment p)
    {
        return new PaymentDto
        {
            Id = p.Id.ToString(),
            PaymentType = p.PaymentType,
            Amount = p.Amount,
            PaymentDate = p.PaymentDate,
            Reference = p.Reference,
            Notes = p.Notes,
            LeadId = p.LeadId?.ToString(),
            ClientName = p.Lead?.ClientName,
            PackageId = p.PackageId?.ToString(),
            PackageName = p.Package?.PackageName,
            HotelId = p.HotelId?.ToString(),
            HotelOrHouseboatName = p.Hotel?.Name,
            IsHouseboat = p.Hotel?.IsHouseboat,
            TransportCompanyId = p.TransportCompanyId?.ToString(),
            TransportCompanyName = p.TransportCompany?.Name,
            ScreenshotUrl = p.ScreenshotUrl,
            TenantId = p.TenantId.ToString(),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
