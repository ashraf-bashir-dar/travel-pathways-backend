using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/itinerary-templates")]
public sealed class ItineraryTemplatesController : TenantControllerBase
{
    private readonly AppDbContext _db;

    public ItineraryTemplatesController(AppDbContext db, TenantContext tenant) : base(tenant)
    {
        _db = db;
    }

    public sealed class ItineraryTemplateDto
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required string Description { get; init; }
        public required string TenantId { get; init; }
        public required bool IsActive { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
    }

    public sealed class CreateItineraryTemplateRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public sealed class UpdateItineraryTemplateRequestDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ItineraryTemplateDto>>>> GetAll(CancellationToken ct = default)
    {
        var list = await _db.ItineraryTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == TenantId && t.IsActive)
            .OrderBy(t => t.Title)
            .ToListAsync(ct);

        var items = list.Select(ToDto).ToList();
        return ApiResponse<List<ItineraryTemplateDto>>.Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ItineraryTemplateDto>>> GetById([FromRoute] Guid id, CancellationToken ct = default)
    {
        var t = await _db.ItineraryTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId, ct);

        if (t is null) return NotFound(ApiResponse<ItineraryTemplateDto>.Fail("Template not found"));
        return ApiResponse<ItineraryTemplateDto>.Ok(ToDto(t));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ItineraryTemplateDto>>> Create(
        [FromBody] CreateItineraryTemplateRequestDto request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(ApiResponse<ItineraryTemplateDto>.Fail("Title is required"));

        var entity = new ItineraryTemplate
        {
            TenantId = TenantId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty
        };
        _db.ItineraryTemplates.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<ItineraryTemplateDto>.Ok(ToDto(entity));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ItineraryTemplateDto>>> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateItineraryTemplateRequestDto request,
        CancellationToken ct = default)
    {
        var entity = await _db.ItineraryTemplates
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId, ct);

        if (entity is null) return NotFound(ApiResponse<ItineraryTemplateDto>.Fail("Template not found"));

        if (request.Title is { } title) entity.Title = title.Trim();
        if (request.Description is { } desc) entity.Description = desc.Trim();
        if (request.IsActive is { } active) entity.IsActive = active;

        await _db.SaveChangesAsync(ct);
        return ApiResponse<ItineraryTemplateDto>.Ok(ToDto(entity));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete([FromRoute] Guid id, CancellationToken ct = default)
    {
        var entity = await _db.ItineraryTemplates
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId, ct);

        if (entity is null) return NotFound(ApiResponse<object>.Fail("Template not found"));

        _db.ItineraryTemplates.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    private static ItineraryTemplateDto ToDto(ItineraryTemplate t) => new()
    {
        Id = t.Id.ToString(),
        Title = t.Title,
        Description = t.Description,
        TenantId = t.TenantId.ToString(),
        IsActive = t.IsActive,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}
