using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.Storage;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize(Policy = "SuperAdminOnly")]
[Route("api/admin/tenants")]
public sealed class AdminTenantsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly FileStorage _storage;

    public AdminTenantsController(AppDbContext db, FileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public sealed class TenantDocumentDto
    {
        public required TenantDocumentType Type { get; init; }
        public required string FileName { get; init; }
        public required string Url { get; init; }
    }

    public sealed class TenantDto
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Code { get; init; }
        public required string Email { get; init; }
        public required string Phone { get; init; }
        public required string Address { get; init; }
        public string? ContactPerson { get; init; }
        public string? LogoUrl { get; init; }
        public List<TenantDocumentDto>? Documents { get; init; }
        public List<AppModuleKey>? EnabledModules { get; init; }
        public required bool IsActive { get; init; }
        public required DateTime CreatedAt { get; init; }
        public string? DefaultUserId { get; init; }
        public string? PlanId { get; init; }
        public string? BillingCycle { get; init; }
        public int SeatsPurchased { get; init; }
        public string? SubscriptionStatus { get; init; }
        public DateTime? SubscriptionStartUtc { get; init; }
        public DateTime? SubscriptionEndUtc { get; init; }
        public int? ActiveUserCount { get; init; }
    }

    public class CreateTenantRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public List<AppModuleKey> EnabledModules { get; set; } = [];

        public IFormFile? LogoFile { get; set; }
        public IFormFile? RegistrationPdf { get; set; }
        public IFormFile? PanPdf { get; set; }
        public IFormFile? GstPdf { get; set; }
    }

    public sealed class UpdateTenantRequestDto : CreateTenantRequestDto
    {
        public bool IsActive { get; set; } = true;
        public string? DefaultUserId { get; set; }
        public string? PlanId { get; set; }
        public BillingCycle? BillingCycle { get; set; }
        public int SeatsPurchased { get; set; }
        public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Active;
        public DateTime? SubscriptionStartUtc { get; set; }
        public DateTime? SubscriptionEndUtc { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<TenantDto>>>> GetTenants(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? searchTerm = null,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Tenants.AsNoTracking().Include(t => t.Documents).AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var s = searchTerm.Trim().ToLower();
            query = query.Where(t =>
                t.Name.ToLower().Contains(s) ||
                t.Code.ToLower().Contains(s) ||
                t.Email.ToLower().Contains(s) ||
                t.Phone.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtoItems = items.Select(t => ToDto(t)).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return ApiResponse<PaginatedResponse<TenantDto>>.Ok(new PaginatedResponse<TenantDto>
        {
            Items = dtoItems,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> GetTenantById([FromRoute] Guid id, CancellationToken ct)
    {
        var tenant = await _db.Tenants.AsNoTracking().Include(t => t.Documents).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null) return NotFound(ApiResponse<TenantDto>.Fail("Tenant not found"));
        var activeUserCount = await _db.Users.CountAsync(u => u.TenantId == id && u.IsActive, ct);
        return ApiResponse<TenantDto>.Ok(ToDto(tenant, activeUserCount));
    }

    [HttpPost]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> CreateTenant([FromForm] CreateTenantRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(ApiResponse<TenantDto>.Fail("Name and Code are required."));
        }

        var exists = await _db.Tenants.AnyAsync(t => t.Code == request.Code, ct);
        if (exists) return BadRequest(ApiResponse<TenantDto>.Fail("Tenant code already exists."));

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            ContactPerson = request.ContactPerson.Trim(),
            Email = request.Email?.Trim() ?? string.Empty,
            Phone = request.Phone.Trim(),
            Address = request.Address.Trim(),
            EnabledModules = request.EnabledModules?.ToList() ?? []
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        await UpsertTenantDocumentsAsync(tenant, request, ct);

        return CreatedAtAction(nameof(GetTenantById), new { id = tenant.Id }, ApiResponse<TenantDto>.Ok(ToDto(tenant)));
    }

    [HttpPut("{id}")]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> UpdateTenant([FromRoute] Guid id, [FromForm] UpdateTenantRequestDto request, CancellationToken ct)
    {
        var tenant = await _db.Tenants.Include(t => t.Documents).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null) return NotFound(ApiResponse<TenantDto>.Fail("Tenant not found"));

        tenant.Name = request.Name.Trim();
        tenant.Code = request.Code.Trim();
        tenant.ContactPerson = request.ContactPerson.Trim();
        tenant.Email = request.Email?.Trim() ?? string.Empty;
        tenant.Phone = request.Phone.Trim();
        tenant.Address = request.Address.Trim();
        tenant.IsActive = request.IsActive;
        tenant.EnabledModules = request.EnabledModules?.ToList() ?? [];
        tenant.DefaultUserId = Guid.TryParse(request.DefaultUserId, out var du) ? du : null;
        tenant.PlanId = Guid.TryParse(request.PlanId, out var pid) ? pid : null;
        tenant.BillingCycle = request.BillingCycle;
        tenant.SeatsPurchased = request.SeatsPurchased;
        tenant.SubscriptionStatus = request.SubscriptionStatus;
        tenant.SubscriptionStartUtc = request.SubscriptionStartUtc;
        tenant.SubscriptionEndUtc = request.SubscriptionEndUtc;

        await _db.SaveChangesAsync(ct);
        await UpsertTenantDocumentsAsync(tenant, request, ct);

        var activeUserCount = await _db.Users.CountAsync(u => u.TenantId == tenant.Id && u.IsActive, ct);
        return ApiResponse<TenantDto>.Ok(ToDto(tenant, activeUserCount));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTenant([FromRoute] Guid id, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null) return NotFound(ApiResponse<object>.Fail("Tenant not found"));

        _db.Tenants.Remove(tenant);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    private async Task UpsertTenantDocumentsAsync(Tenant tenant, CreateTenantRequestDto request, CancellationToken ct)
    {
        tenant.Documents ??= [];

        if (request.LogoFile is not null)
        {
            var url = await _storage.SaveTenantFileAsync(tenant.Id, "logo", request.LogoFile, ct);
            tenant.LogoUrl = url;
            tenant.Documents.Add(new TenantDocument { TenantId = tenant.Id, Type = TenantDocumentType.Logo, FileName = request.LogoFile.FileName, Url = url });
        }

        if (request.RegistrationPdf is not null)
        {
            var url = await _storage.SaveTenantFileAsync(tenant.Id, "documents", request.RegistrationPdf, ct);
            tenant.Documents.Add(new TenantDocument { TenantId = tenant.Id, Type = TenantDocumentType.Registration, FileName = request.RegistrationPdf.FileName, Url = url });
        }
        if (request.PanPdf is not null)
        {
            var url = await _storage.SaveTenantFileAsync(tenant.Id, "documents", request.PanPdf, ct);
            tenant.Documents.Add(new TenantDocument { TenantId = tenant.Id, Type = TenantDocumentType.PAN, FileName = request.PanPdf.FileName, Url = url });
        }
        if (request.GstPdf is not null)
        {
            var url = await _storage.SaveTenantFileAsync(tenant.Id, "documents", request.GstPdf, ct);
            tenant.Documents.Add(new TenantDocument { TenantId = tenant.Id, Type = TenantDocumentType.GST, FileName = request.GstPdf.FileName, Url = url });
        }

        await _db.SaveChangesAsync(ct);
    }

    private static TenantDto ToDto(Tenant t, int? activeUserCount = null) =>
        new()
        {
            Id = t.Id.ToString("D"),
            Name = t.Name,
            Code = t.Code,
            Email = t.Email,
            Phone = t.Phone,
            Address = t.Address,
            ContactPerson = t.ContactPerson,
            LogoUrl = t.LogoUrl,
            Documents = t.Documents.Select(d => new TenantDocumentDto { Type = d.Type, FileName = d.FileName, Url = d.Url }).ToList(),
            EnabledModules = t.EnabledModules.ToList(),
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            DefaultUserId = t.DefaultUserId?.ToString("D"),
            PlanId = t.PlanId?.ToString("D"),
            BillingCycle = t.BillingCycle?.ToString(),
            SeatsPurchased = t.SeatsPurchased,
            SubscriptionStatus = t.SubscriptionStatus.ToString(),
            SubscriptionStartUtc = t.SubscriptionStartUtc,
            SubscriptionEndUtc = t.SubscriptionEndUtc,
            ActiveUserCount = activeUserCount
        };
}

