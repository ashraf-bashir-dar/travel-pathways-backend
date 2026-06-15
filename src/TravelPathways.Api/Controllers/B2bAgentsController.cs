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
[Route("api/b2b-agents")]
public sealed class B2bAgentsController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly FileStorage _storage;

    public B2bAgentsController(AppDbContext db, TenantContext tenant, FileStorage storage) : base(tenant)
    {
        _db = db;
        _storage = storage;
    }

    public sealed class B2bAgentDocumentDto
    {
        public required string Id { get; init; }
        public required string FileName { get; init; }
        public required string Url { get; init; }
        public required DateTime CreatedAt { get; init; }
    }

    public sealed class B2bAgentDto
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string ContactPerson { get; init; }
        public string? ContactNumber1 { get; init; }
        public string? ContactNumber2 { get; init; }
        public string? Email { get; init; }
        public string? WebsiteUrl { get; init; }
        public string? State { get; init; }
        public string? City { get; init; }
        public string? Country { get; init; }
        public string? PinCode { get; init; }
        public required bool IsActive { get; init; }
        public required string TenantId { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
        public List<B2bAgentDocumentDto> Documents { get; init; } = [];
    }

    public class SaveB2bAgentRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string? ContactNumber1 { get; set; }
        public string? ContactNumber2 { get; set; }
        public string? Email { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? State { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? PinCode { get; set; }
        public bool? IsActive { get; set; }
    }

    public sealed class SaveB2bAgentFormDto : SaveB2bAgentRequestDto
    {
        public List<IFormFile>? PdfFiles { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<B2bAgentDto>>>> GetAgents(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? state = null,
        [FromQuery] string? city = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        await B2bAgentSchemaBootstrap.EnsureAsync(_db, ct);

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.B2bAgents.AsNoTracking().Where(a => a.TenantId == TenantId);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var s = searchTerm.Trim().ToLower();
            query = query.Where(a =>
                a.Name.ToLower().Contains(s) ||
                a.ContactPerson.ToLower().Contains(s) ||
                (a.Email != null && a.Email.ToLower().Contains(s)) ||
                (a.City != null && a.City.ToLower().Contains(s)) ||
                (a.ContactNumber1 != null && a.ContactNumber1.Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            var stateFilter = state.Trim().ToLower();
            query = query.Where(a => a.State != null && a.State.ToLower() == stateFilter);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var cityFilter = city.Trim().ToLower();
            query = query.Where(a => a.City != null && a.City.ToLower() == cityFilter);
        }

        if (isActive is { } active)
            query = query.Where(a => a.IsActive == active);

        var total = await query.CountAsync(ct);
        var agents = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return ApiResponse<PaginatedResponse<B2bAgentDto>>.Ok(new PaginatedResponse<B2bAgentDto>
        {
            Items = agents.Select(a => ToDto(a, includeDocuments: false)).ToList(),
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<B2bAgentDto>>> GetAgentById([FromRoute] Guid id, CancellationToken ct)
    {
        await B2bAgentSchemaBootstrap.EnsureAsync(_db, ct);

        var agent = await _db.B2bAgents.AsNoTracking()
            .Include(a => a.Documents.OrderBy(d => d.CreatedAt))
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == TenantId, ct);

        if (agent is null)
            return NotFound(ApiResponse<B2bAgentDto>.Fail("B2B agent not found."));

        return ApiResponse<B2bAgentDto>.Ok(ToDto(agent, includeDocuments: true));
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<ActionResult<ApiResponse<B2bAgentDto>>> CreateAgentJson(
        [FromBody] SaveB2bAgentRequestDto request,
        CancellationToken ct)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
            return BadRequest(ApiResponse<B2bAgentDto>.Fail(validation));

        await B2bAgentSchemaBootstrap.EnsureAsync(_db, ct);

        var agent = MapToEntity(new B2bAgent { TenantId = TenantId }, request);
        _db.B2bAgents.Add(agent);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetAgentById), new { id = agent.Id }, ApiResponse<B2bAgentDto>.Ok(ToDto(agent)));
    }

    [HttpPost]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    public async Task<ActionResult<ApiResponse<B2bAgentDto>>> CreateAgentForm(
        [FromForm] SaveB2bAgentFormDto request,
        CancellationToken ct)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
            return BadRequest(ApiResponse<B2bAgentDto>.Fail(validation));

        await B2bAgentSchemaBootstrap.EnsureAsync(_db, ct);

        var agent = MapToEntity(new B2bAgent { TenantId = TenantId }, request);
        _db.B2bAgents.Add(agent);
        await _db.SaveChangesAsync(ct);

        await SavePdfFilesAsync(agent, request.PdfFiles, ct);
        return CreatedAtAction(nameof(GetAgentById), new { id = agent.Id }, ApiResponse<B2bAgentDto>.Ok(ToDto(agent)));
    }

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    public async Task<ActionResult<ApiResponse<B2bAgentDto>>> UpdateAgentJson(
        [FromRoute] Guid id,
        [FromBody] SaveB2bAgentRequestDto request,
        CancellationToken ct)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
            return BadRequest(ApiResponse<B2bAgentDto>.Fail(validation));

        var agent = await _db.B2bAgents
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == TenantId, ct);

        if (agent is null)
            return NotFound(ApiResponse<B2bAgentDto>.Fail("B2B agent not found."));

        MapToEntity(agent, request);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<B2bAgentDto>.Ok(ToDto(agent, includeDocuments: true));
    }

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    public async Task<ActionResult<ApiResponse<B2bAgentDto>>> UpdateAgentForm(
        [FromRoute] Guid id,
        [FromForm] SaveB2bAgentFormDto request,
        CancellationToken ct)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
            return BadRequest(ApiResponse<B2bAgentDto>.Fail(validation));

        var agent = await _db.B2bAgents
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == TenantId, ct);

        if (agent is null)
            return NotFound(ApiResponse<B2bAgentDto>.Fail("B2B agent not found."));

        MapToEntity(agent, request);
        await _db.SaveChangesAsync(ct);
        await SavePdfFilesAsync(agent, request.PdfFiles, ct);

        return ApiResponse<B2bAgentDto>.Ok(ToDto(agent, includeDocuments: true));
    }

    [HttpPost("{id:guid}/documents")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<B2bAgentDocumentDto>>> UploadDocument(
        [FromRoute] Guid id,
        IFormFile? file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<B2bAgentDocumentDto>.Fail("PDF file is required."));

        var agent = await _db.B2bAgents.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == TenantId, ct);
        if (agent is null)
            return NotFound(ApiResponse<B2bAgentDocumentDto>.Fail("B2B agent not found."));

        var doc = await AddDocumentAsync(agent, file, ct);
        return ApiResponse<B2bAgentDocumentDto>.Ok(ToDocumentDto(doc));
    }

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDocument(
        [FromRoute] Guid id,
        [FromRoute] Guid documentId,
        CancellationToken ct)
    {
        var doc = await _db.B2bAgentDocuments
            .Include(d => d.B2bAgent)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.B2bAgentId == id && d.B2bAgent.TenantId == TenantId, ct);

        if (doc is null)
            return NotFound(ApiResponse<object>.Fail("Document not found."));

        doc.IsDeleted = true;
        doc.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAgent([FromRoute] Guid id, CancellationToken ct)
    {
        var agent = await _db.B2bAgents.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == TenantId, ct);
        if (agent is null)
            return NotFound(ApiResponse<object>.Fail("B2B agent not found."));

        agent.IsDeleted = true;
        agent.DeletedAtUtc = DateTime.UtcNow;
        agent.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    private async Task SavePdfFilesAsync(B2bAgent agent, List<IFormFile>? files, CancellationToken ct)
    {
        if (files is null || files.Count == 0) return;

        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            await AddDocumentAsync(agent, file, ct);
        }
    }

    private async Task<B2bAgentDocument> AddDocumentAsync(B2bAgent agent, IFormFile file, CancellationToken ct)
    {
        var url = await _storage.SaveB2bAgentDocumentAsync(TenantId, agent.Id, file, ct);
        var doc = new B2bAgentDocument
        {
            B2bAgentId = agent.Id,
            FileName = file.FileName.Trim(),
            Url = url
        };
        _db.B2bAgentDocuments.Add(doc);
        agent.Documents.Add(doc);
        await _db.SaveChangesAsync(ct);
        return doc;
    }

    private static string? ValidateRequest(SaveB2bAgentRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Name is required.";
        if (string.IsNullOrWhiteSpace(request.ContactPerson))
            return "Contact person is required.";
        return null;
    }

    private static B2bAgent MapToEntity(B2bAgent agent, SaveB2bAgentRequestDto request)
    {
        agent.Name = request.Name.Trim();
        agent.ContactPerson = request.ContactPerson.Trim();
        agent.ContactNumber1 = TrimOrNull(request.ContactNumber1);
        agent.ContactNumber2 = TrimOrNull(request.ContactNumber2);
        agent.Email = TrimOrNull(request.Email);
        agent.WebsiteUrl = TrimOrNull(request.WebsiteUrl);
        agent.State = TrimOrNull(request.State);
        agent.City = TrimOrNull(request.City);
        agent.Country = TrimOrNull(request.Country);
        agent.PinCode = TrimOrNull(request.PinCode);
        if (request.IsActive is { } active)
            agent.IsActive = active;
        return agent;
    }

    private static string? TrimOrNull(string? value)
    {
        var s = value?.Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private B2bAgentDto ToDto(B2bAgent agent, bool includeDocuments = false) => new()
    {
        Id = agent.Id.ToString("D"),
        Name = agent.Name,
        ContactPerson = agent.ContactPerson,
        ContactNumber1 = agent.ContactNumber1,
        ContactNumber2 = agent.ContactNumber2,
        Email = agent.Email,
        WebsiteUrl = agent.WebsiteUrl,
        State = agent.State,
        City = agent.City,
        Country = agent.Country,
        PinCode = agent.PinCode,
        IsActive = agent.IsActive,
        TenantId = agent.TenantId.ToString("D"),
        CreatedAt = agent.CreatedAt,
        UpdatedAt = agent.UpdatedAt,
        Documents = includeDocuments
            ? agent.Documents.Where(d => !d.IsDeleted).OrderBy(d => d.CreatedAt).Select(ToDocumentDto).ToList()
            : []
    };

    private static B2bAgentDocumentDto ToDocumentDto(B2bAgentDocument doc) => new()
    {
        Id = doc.Id.ToString("D"),
        FileName = doc.FileName,
        Url = doc.Url,
        CreatedAt = doc.CreatedAt
    };
}
