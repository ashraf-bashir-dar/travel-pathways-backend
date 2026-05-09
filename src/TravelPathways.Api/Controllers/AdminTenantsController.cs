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
        public required string Id { get; init; }
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
        public string? PdfCoverTitle { get; init; }
        public string? PdfPrimaryColor { get; init; }
        public string? PdfSecondaryColor { get; init; }
        public string? PdfTemplateKey { get; init; }
        public bool? PdfShowBankDetails { get; init; }
        public bool? PdfShowQrCodes { get; init; }
        public List<string> TermsAndConditions { get; init; } = [];
        public List<string> CancellationPolicy { get; init; } = [];
        public List<string> SupplementCosts { get; init; } = [];
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
        public List<string> TermsAndConditions { get; set; } = [];
        public List<string> CancellationPolicy { get; set; } = [];
        public List<string> SupplementCosts { get; set; } = [];

        public IFormFile? LogoFile { get; set; }
        public IFormFile? RegistrationPdf { get; set; }
        public IFormFile? PanPdf { get; set; }
        public IFormFile? GstPdf { get; set; }
        public IFormFile? PdfCoverPage { get; set; }
        public List<IFormFile>? PdfAppendixPages { get; set; }
    }

    public sealed class UpdateTenantRequestDto : CreateTenantRequestDto
    {
        public bool IsActive { get; set; } = true;
        public string? PdfCoverTitle { get; set; }
        public string? PdfPrimaryColor { get; set; }
        public string? PdfSecondaryColor { get; set; }
        public string? PdfTemplateKey { get; set; }
        public bool? PdfShowBankDetails { get; set; }
        public bool? PdfShowQrCodes { get; set; }
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

        var tenantEmail = request.Email?.Trim();
        if (!string.IsNullOrWhiteSpace(tenantEmail))
        {
            var emailExists = await _db.Tenants.AnyAsync(t => t.Email == tenantEmail, ct);
            if (emailExists) return BadRequest(ApiResponse<TenantDto>.Fail("A tenant with this email already exists. Use a different email."));
        }

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            ContactPerson = request.ContactPerson.Trim(),
            Email = tenantEmail ?? string.Empty,
            Phone = request.Phone.Trim(),
            Address = request.Address.Trim(),
            EnabledModules = request.EnabledModules?.ToList() ?? [],
            TermsAndConditions = NormalizePolicyLines(request.TermsAndConditions),
            CancellationPolicy = NormalizePolicyLines(request.CancellationPolicy),
            SupplementCosts = NormalizePolicyLines(request.SupplementCosts)
        };

        _db.Tenants.Add(tenant);
        await UpsertTenantDocumentsAsync(tenant, request, ct);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetTenantById), new { id = tenant.Id }, ApiResponse<TenantDto>.Ok(ToDto(tenant)));
    }

    [HttpPut("{id}")]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> UpdateTenant([FromRoute] Guid id, [FromForm] UpdateTenantRequestDto request, CancellationToken ct)
    {
        var tenant = await _db.Tenants.Include(t => t.Documents).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null) return NotFound(ApiResponse<TenantDto>.Fail("Tenant not found"));

        var newCode = request.Code.Trim();
        if (tenant.Code != newCode)
        {
            if (await _db.Tenants.AnyAsync(t => t.Code == newCode && t.Id != id, ct))
                return BadRequest(ApiResponse<TenantDto>.Fail("Tenant code already exists."));
        }

        var newEmail = request.Email?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(newEmail) && tenant.Email != newEmail)
        {
            if (await _db.Tenants.AnyAsync(t => t.Email == newEmail && t.Id != id, ct))
                return BadRequest(ApiResponse<TenantDto>.Fail("A tenant with this email already exists. Use a different email."));
        }

        var pdfTplError = await ValidateTenantPdfTemplateKeyAsync(request.PdfTemplateKey, ct);
        if (pdfTplError is not null) return BadRequest(ApiResponse<TenantDto>.Fail(pdfTplError));

        ApplyTenantUpdates(tenant, request, newCode, newEmail);

        // When tenant is deactivated, deactivate all its users
        if (!request.IsActive)
        {
            var usersToDeactivate = await _db.Users.IgnoreQueryFilters()
                .Where(u => u.TenantId == id && u.IsActive)
                .ToListAsync(ct);
            foreach (var u in usersToDeactivate)
            {
                u.IsActive = false;
            }
        }

        try
        {
            await UpsertTenantDocumentsAsync(tenant, request, ct);
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Retry once with a fresh entity snapshot to handle benign races.
            _db.ChangeTracker.Clear();
            var freshTenant = await _db.Tenants
                .IgnoreQueryFilters()
                .Include(t => t.Documents)
                .FirstOrDefaultAsync(t => t.Id == id, ct);

            if (freshTenant is null)
            {
                return NotFound(ApiResponse<TenantDto>.Fail("Tenant not found (it may have been deleted)."));
            }

            ApplyTenantUpdates(freshTenant, request, newCode, newEmail);
            try
            {
                await UpsertTenantDocumentsAsync(freshTenant, request, ct);
                await _db.SaveChangesAsync(ct);
                tenant = freshTenant;
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(ApiResponse<TenantDto>.Fail("Tenant was modified by another request. Please refresh and try again."));
            }
        }

        var activeUserCount = await _db.Users.CountAsync(u => u.TenantId == tenant.Id && u.IsActive, ct);
        return ApiResponse<TenantDto>.Ok(ToDto(tenant, activeUserCount));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTenant([FromRoute] Guid id, CancellationToken ct)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null) return NotFound(ApiResponse<object>.Fail("Tenant not found"));
        if (tenant.IsDeleted) return Ok(ApiResponse<object>.Ok(new { })); // already soft-deleted

        tenant.IsDeleted = true;
        tenant.DeletedAtUtc = DateTime.UtcNow;
        tenant.IsActive = false;

        // Deactivate all users of this tenant
        var users = await _db.Users.IgnoreQueryFilters().Where(u => u.TenantId == id).ToListAsync(ct);
        foreach (var u in users)
        {
            u.IsActive = false;
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    /// <summary>
    /// Soft-deletes one tenant-owned document (logo, registration PDF, PDF cover, appendix PDF, etc.).
    /// </summary>
    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> DeleteTenantDocument(
        [FromRoute] Guid id,
        [FromRoute] Guid documentId,
        CancellationToken ct)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        if (tenant is null) return NotFound(ApiResponse<TenantDto>.Fail("Tenant not found"));

        var doc = await _db.TenantDocuments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == id && !d.IsDeleted, ct);
        if (doc is null) return NotFound(ApiResponse<TenantDto>.Fail("Document not found"));

        var now = DateTime.UtcNow;
        doc.IsDeleted = true;
        doc.DeletedAtUtc = now;

        if (doc.Type == TenantDocumentType.Logo)
        {
            if (!string.IsNullOrWhiteSpace(tenant.LogoUrl) && !string.IsNullOrWhiteSpace(doc.Url) &&
                string.Equals(tenant.LogoUrl.Trim(), doc.Url.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var nextLogoUrl = await _db.TenantDocuments.AsNoTracking()
                    .Where(d => d.TenantId == id && d.Type == TenantDocumentType.Logo && !d.IsDeleted && d.Id != documentId)
                    .OrderByDescending(d => d.CreatedAt)
                    .Select(d => d.Url)
                    .FirstOrDefaultAsync(ct);
                tenant.LogoUrl = string.IsNullOrWhiteSpace(nextLogoUrl) ? null : nextLogoUrl;
            }
        }

        await _db.SaveChangesAsync(ct);

        var fresh = await _db.Tenants.AsNoTracking()
            .Include(t => t.Documents)
            .FirstAsync(t => t.Id == id, ct);
        var activeUserCount = await _db.Users.CountAsync(u => u.TenantId == id && u.IsActive, ct);
        return ApiResponse<TenantDto>.Ok(ToDto(fresh, activeUserCount));
    }

    private async Task UpsertTenantDocumentsAsync(Tenant tenant, CreateTenantRequestDto request, CancellationToken ct)
    {

        if (request.LogoFile is not null)
        {
            var url = await _storage.SaveTenantFileAsync(tenant.Id, "logo", request.LogoFile, ct);
            tenant.LogoUrl = url;
            _db.TenantDocuments.Add(new TenantDocument { TenantId = tenant.Id, Type = TenantDocumentType.Logo, FileName = request.LogoFile.FileName, Url = url });
        }

        if (request.RegistrationPdf is not null)
        {
            var url = await _storage.SaveTenantFileAsync(tenant.Id, "documents", request.RegistrationPdf, ct);
            _db.TenantDocuments.Add(new TenantDocument { TenantId = tenant.Id, Type = TenantDocumentType.Registration, FileName = request.RegistrationPdf.FileName, Url = url });
        }
        if (request.PanPdf is not null)
        {
            var url = await _storage.SaveTenantFileAsync(tenant.Id, "documents", request.PanPdf, ct);
            _db.TenantDocuments.Add(new TenantDocument { TenantId = tenant.Id, Type = TenantDocumentType.PAN, FileName = request.PanPdf.FileName, Url = url });
        }
        if (request.GstPdf is not null)
        {
            var url = await _storage.SaveTenantFileAsync(tenant.Id, "documents", request.GstPdf, ct);
            _db.TenantDocuments.Add(new TenantDocument { TenantId = tenant.Id, Type = TenantDocumentType.GST, FileName = request.GstPdf.FileName, Url = url });
        }

        if (request.PdfCoverPage is not null)
        {
            var now = DateTime.UtcNow;
            await _db.TenantDocuments
                .IgnoreQueryFilters()
                .Where(d => d.TenantId == tenant.Id && d.Type == TenantDocumentType.PdfCoverPage && !d.IsDeleted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(d => d.IsDeleted, _ => true)
                    .SetProperty(d => d.DeletedAtUtc, _ => now), ct);

            var url = await _storage.SaveTenantFileAsync(tenant.Id, "pdf-cover", request.PdfCoverPage, ct);
            _db.TenantDocuments.Add(new TenantDocument
            {
                TenantId = tenant.Id,
                Type = TenantDocumentType.PdfCoverPage,
                FileName = request.PdfCoverPage.FileName,
                Url = url
            });
        }

        if (request.PdfAppendixPages is { Count: > 0 })
        {
            foreach (var appendix in request.PdfAppendixPages.Where(a => a is not null))
            {
                var url = await _storage.SaveTenantFileAsync(tenant.Id, "pdf-appendix", appendix, ct);
                _db.TenantDocuments.Add(new TenantDocument
                {
                    TenantId = tenant.Id,
                    Type = TenantDocumentType.PdfAppendixPage,
                    FileName = appendix.FileName,
                    Url = url
                });
            }
        }

    }

    private async Task<string?> ValidateTenantPdfTemplateKeyAsync(string? pdfTemplateKey, CancellationToken ct)
    {
        var key = string.IsNullOrWhiteSpace(pdfTemplateKey) ? null : pdfTemplateKey.Trim();
        if (key is null)
            return "Select a PDF template for this agency (Admin → Travel agency → PDF template). Each tenant must use a library template that includes HTML.";

        var t = await _db.PdfTemplates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key && !x.IsDeleted && x.IsActive, ct);
        if (t is null)
            return $"PDF template '{key}' was not found or is inactive.";
        if (string.IsNullOrWhiteSpace(t.HtmlTemplate))
            return $"PDF template '{t.Name}' has no HTML. Edit it under Admin → PDF templates and save.";
        return null;
    }

    private static void ApplyTenantUpdates(Tenant tenant, UpdateTenantRequestDto request, string newCode, string newEmail)
    {
        tenant.Name = request.Name.Trim();
        tenant.Code = newCode;
        tenant.ContactPerson = request.ContactPerson.Trim();
        tenant.Email = newEmail;
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
        tenant.PdfCoverTitle = string.IsNullOrWhiteSpace(request.PdfCoverTitle) ? null : request.PdfCoverTitle.Trim();
        tenant.PdfPrimaryColor = string.IsNullOrWhiteSpace(request.PdfPrimaryColor) ? null : request.PdfPrimaryColor.Trim();
        tenant.PdfSecondaryColor = string.IsNullOrWhiteSpace(request.PdfSecondaryColor) ? null : request.PdfSecondaryColor.Trim();
        tenant.PdfTemplateKey = string.IsNullOrWhiteSpace(request.PdfTemplateKey) ? null : request.PdfTemplateKey.Trim();
        tenant.PdfShowBankDetails = request.PdfShowBankDetails;
        tenant.PdfShowQrCodes = request.PdfShowQrCodes;
        tenant.TermsAndConditions = NormalizePolicyLines(request.TermsAndConditions);
        tenant.CancellationPolicy = NormalizePolicyLines(request.CancellationPolicy);
        tenant.SupplementCosts = NormalizePolicyLines(request.SupplementCosts);
    }

    private static List<string> NormalizePolicyLines(List<string>? lines) =>
        (lines ?? [])
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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
            PdfCoverTitle = t.PdfCoverTitle,
            PdfPrimaryColor = t.PdfPrimaryColor,
            PdfSecondaryColor = t.PdfSecondaryColor,
            PdfTemplateKey = t.PdfTemplateKey,
            PdfShowBankDetails = t.PdfShowBankDetails,
            PdfShowQrCodes = t.PdfShowQrCodes,
            TermsAndConditions = t.TermsAndConditions ?? [],
            CancellationPolicy = t.CancellationPolicy ?? [],
            SupplementCosts = t.SupplementCosts ?? [],
            Documents = t.Documents.Select(d => new TenantDocumentDto
                {
                    Id = d.Id.ToString("D"),
                    Type = d.Type,
                    FileName = d.FileName,
                    Url = d.Url
                })
                .ToList(),
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

