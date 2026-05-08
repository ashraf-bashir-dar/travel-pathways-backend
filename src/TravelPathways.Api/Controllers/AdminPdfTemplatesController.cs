using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize(Policy = "SuperAdminOnly")]
[Route("api/admin/pdf-templates")]
public sealed class AdminPdfTemplatesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminPdfTemplatesController(AppDbContext db)
    {
        _db = db;
    }

    public sealed class PdfTemplateDto
    {
        public required string Id { get; init; }
        public required string Key { get; init; }
        public required string Name { get; init; }
        public string? Description { get; init; }
        public required bool IsSystem { get; init; }
        public required bool IsActive { get; init; }
        public string? HtmlTemplate { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
    }

    public sealed class UpsertPdfTemplateRequestDto
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public string? HtmlTemplate { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PdfTemplateDto>>>> GetTemplates([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var query = _db.PdfTemplates.AsNoTracking().AsQueryable();
        if (!includeInactive) query = query.Where(t => t.IsActive);

        var list = await query.OrderByDescending(t => t.IsSystem).ThenBy(t => t.Name).ToListAsync(ct);
        return ApiResponse<List<PdfTemplateDto>>.Ok(list.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PdfTemplateDto>>> GetTemplateById([FromRoute] Guid id, CancellationToken ct)
    {
        var item = await _db.PdfTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (item is null) return NotFound(ApiResponse<PdfTemplateDto>.Fail("Template not found"));
        return ApiResponse<PdfTemplateDto>.Ok(ToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PdfTemplateDto>>> CreateTemplate([FromBody] UpsertPdfTemplateRequestDto request, CancellationToken ct)
    {
        var key = NormalizeKey(request.Key);
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name))
            return BadRequest(ApiResponse<PdfTemplateDto>.Fail("Template key and name are required."));

        var exists = await _db.PdfTemplates.AnyAsync(t => t.Key == key, ct);
        if (exists) return BadRequest(ApiResponse<PdfTemplateDto>.Fail("Template key already exists."));

        var entity = new PdfTemplate
        {
            Key = key,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsSystem = false,
            IsActive = request.IsActive,
            HtmlTemplate = string.IsNullOrWhiteSpace(request.HtmlTemplate) ? null : request.HtmlTemplate.Trim()
        };
        _db.PdfTemplates.Add(entity);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetTemplateById), new { id = entity.Id }, ApiResponse<PdfTemplateDto>.Ok(ToDto(entity)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PdfTemplateDto>>> UpdateTemplate([FromRoute] Guid id, [FromBody] UpsertPdfTemplateRequestDto request, CancellationToken ct)
    {
        var item = await _db.PdfTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (item is null) return NotFound(ApiResponse<PdfTemplateDto>.Fail("Template not found"));

        var key = NormalizeKey(request.Key);
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name))
            return BadRequest(ApiResponse<PdfTemplateDto>.Fail("Template key and name are required."));

        var keyExists = await _db.PdfTemplates.AnyAsync(t => t.Id != id && t.Key == key, ct);
        if (keyExists) return BadRequest(ApiResponse<PdfTemplateDto>.Fail("Template key already exists."));
        if (item.IsSystem && key != item.Key)
            return BadRequest(ApiResponse<PdfTemplateDto>.Fail("System template key cannot be changed."));

        item.Key = key;
        item.Name = name;
        item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        item.IsActive = request.IsActive;
        item.HtmlTemplate = string.IsNullOrWhiteSpace(request.HtmlTemplate) ? null : request.HtmlTemplate.Trim();

        await _db.SaveChangesAsync(ct);
        return ApiResponse<PdfTemplateDto>.Ok(ToDto(item));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTemplate([FromRoute] Guid id, CancellationToken ct)
    {
        var item = await _db.PdfTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (item is null) return NotFound(ApiResponse<object>.Fail("Template not found"));
        if (item.IsSystem) return BadRequest(ApiResponse<object>.Fail("System template cannot be deleted."));

        item.IsDeleted = true;
        item.DeletedAtUtc = DateTime.UtcNow;
        item.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var chars = value.Trim().ToLowerInvariant().ToCharArray();
        var safe = new string(chars.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (safe.Contains("--", StringComparison.Ordinal)) safe = safe.Replace("--", "-", StringComparison.Ordinal);
        return safe.Trim('-');
    }

    private static PdfTemplateDto ToDto(PdfTemplate t) => new()
    {
        Id = t.Id.ToString("D"),
        Key = t.Key,
        Name = t.Name,
        Description = t.Description,
        IsSystem = t.IsSystem,
        IsActive = t.IsActive,
        HtmlTemplate = t.HtmlTemplate,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}
