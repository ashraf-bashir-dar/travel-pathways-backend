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
[Route("api/transport-companies")]
public sealed class TransportCompaniesController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly FileStorage _storage;

    public TransportCompaniesController(AppDbContext db, TenantContext tenant, FileStorage storage) : base(tenant)
    {
        _db = db;
        _storage = storage;
    }

    public sealed class TransportCompanyDto
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string ContactPerson { get; init; }
        public required string PhoneNumber { get; init; }
        public string? Email { get; init; }
        public required string Address { get; init; }
        public string? GstNumber { get; init; }
        public string? PanNumber { get; init; }
        public string? AadharDocumentUrl { get; init; }
        public string? LicenceDocumentUrl { get; init; }
        public required string TenantId { get; init; }
        public required bool IsActive { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
    }

    public class CreateTransportCompanyRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Address { get; set; } = string.Empty;
        public string? GstNumber { get; set; }
        public string? PanNumber { get; set; }
    }

    public sealed class CreateTransportCompanyFormDto : CreateTransportCompanyRequestDto
    {
        public IFormFile? AadharPdf { get; set; }
        public IFormFile? LicencePdf { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<TransportCompanyDto>>>> GetCompanies(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? searchTerm = null,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.TransportCompanies.AsNoTracking().Where(c => c.TenantId == TenantId);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var s = searchTerm.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(s) || c.PhoneNumber.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct);
        var companies = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return ApiResponse<PaginatedResponse<TransportCompanyDto>>.Ok(new PaginatedResponse<TransportCompanyDto>
        {
            Items = companies.Select(ToDto).ToList(),
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<TransportCompanyDto>>> GetCompanyById([FromRoute] Guid id, CancellationToken ct)
    {
        var company = await _db.TransportCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId, ct);
        if (company is null) return NotFound(ApiResponse<TransportCompanyDto>.Fail("Transport company not found"));
        return ApiResponse<TransportCompanyDto>.Ok(ToDto(company));
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<ActionResult<ApiResponse<TransportCompanyDto>>> CreateCompanyJson([FromBody] CreateTransportCompanyRequestDto request, CancellationToken ct)
    {
        var company = new TransportCompany
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            ContactPerson = request.ContactPerson.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address.Trim(),
            GstNumber = request.GstNumber?.Trim(),
            PanNumber = request.PanNumber?.Trim()
        };
        _db.TransportCompanies.Add(company);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetCompanyById), new { id = company.Id }, ApiResponse<TransportCompanyDto>.Ok(ToDto(company)));
    }

    [HttpPost]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    public async Task<ActionResult<ApiResponse<TransportCompanyDto>>> CreateCompanyForm([FromForm] CreateTransportCompanyFormDto request, CancellationToken ct)
    {
        var company = new TransportCompany
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            ContactPerson = request.ContactPerson.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address.Trim(),
            GstNumber = request.GstNumber?.Trim(),
            PanNumber = request.PanNumber?.Trim()
        };

        _db.TransportCompanies.Add(company);
        await _db.SaveChangesAsync(ct);

        await SaveCompanyDocsAsync(company, request, ct);
        return CreatedAtAction(nameof(GetCompanyById), new { id = company.Id }, ApiResponse<TransportCompanyDto>.Ok(ToDto(company)));
    }

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    public async Task<ActionResult<ApiResponse<TransportCompanyDto>>> UpdateCompanyJson([FromRoute] Guid id, [FromBody] CreateTransportCompanyRequestDto request, CancellationToken ct)
    {
        var company = await _db.TransportCompanies.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId, ct);
        if (company is null) return NotFound(ApiResponse<TransportCompanyDto>.Fail("Transport company not found"));

        company.Name = request.Name.Trim();
        company.ContactPerson = request.ContactPerson.Trim();
        company.PhoneNumber = request.PhoneNumber.Trim();
        company.Email = request.Email?.Trim();
        company.Address = request.Address.Trim();
        company.GstNumber = request.GstNumber?.Trim();
        company.PanNumber = request.PanNumber?.Trim();

        await _db.SaveChangesAsync(ct);
        return ApiResponse<TransportCompanyDto>.Ok(ToDto(company));
    }

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    public async Task<ActionResult<ApiResponse<TransportCompanyDto>>> UpdateCompanyForm([FromRoute] Guid id, [FromForm] CreateTransportCompanyFormDto request, CancellationToken ct)
    {
        var company = await _db.TransportCompanies.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId, ct);
        if (company is null) return NotFound(ApiResponse<TransportCompanyDto>.Fail("Transport company not found"));

        company.Name = request.Name.Trim();
        company.ContactPerson = request.ContactPerson.Trim();
        company.PhoneNumber = request.PhoneNumber.Trim();
        company.Email = request.Email?.Trim();
        company.Address = request.Address.Trim();
        company.GstNumber = request.GstNumber?.Trim();
        company.PanNumber = request.PanNumber?.Trim();

        await _db.SaveChangesAsync(ct);
        await SaveCompanyDocsAsync(company, request, ct);

        return ApiResponse<TransportCompanyDto>.Ok(ToDto(company));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCompany([FromRoute] Guid id, CancellationToken ct)
    {
        var company = await _db.TransportCompanies.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId, ct);
        if (company is null) return NotFound(ApiResponse<object>.Fail("Transport company not found"));
        _db.TransportCompanies.Remove(company);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    private async Task SaveCompanyDocsAsync(TransportCompany company, CreateTransportCompanyFormDto request, CancellationToken ct)
    {
        if (request.AadharPdf is not null)
        {
            company.AadharDocumentUrl = await _storage.SaveTransportCompanyFileAsync(TenantId, company.Id, "aadhar", request.AadharPdf, ct);
        }
        if (request.LicencePdf is not null)
        {
            company.LicenceDocumentUrl = await _storage.SaveTransportCompanyFileAsync(TenantId, company.Id, "licence", request.LicencePdf, ct);
        }
        await _db.SaveChangesAsync(ct);
    }

    private static TransportCompanyDto ToDto(TransportCompany c) =>
        new()
        {
            Id = c.Id.ToString("D"),
            Name = c.Name,
            ContactPerson = c.ContactPerson,
            PhoneNumber = c.PhoneNumber,
            Email = c.Email,
            Address = c.Address,
            GstNumber = c.GstNumber,
            PanNumber = c.PanNumber,
            AadharDocumentUrl = c.AadharDocumentUrl,
            LicenceDocumentUrl = c.LicenceDocumentUrl,
            TenantId = c.TenantId.ToString("D"),
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
}

