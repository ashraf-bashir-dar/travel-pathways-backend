using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/leads")]
public sealed class LeadsController : TenantControllerBase
{
    private readonly AppDbContext _db;

    public LeadsController(AppDbContext db, TenantContext tenant) : base(tenant)
    {
        _db = db;
    }

    public sealed class LeadDto
    {
        public required string Id { get; init; }
        public required string ClientName { get; init; }
        public required string PhoneNumber { get; init; }
        public string? ClientEmail { get; init; }
        public string? ClientState { get; init; }
        public string? ClientCity { get; init; }
        public required string Address { get; init; }
        public required LeadSource LeadSource { get; init; }
        public required LeadStatus Status { get; init; }
        public string? Notes { get; init; }
        public required string TenantId { get; init; }
        public string? AssignedToUserId { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
        public required string CreatedBy { get; init; }
    }

    public class CreateLeadRequestDto
    {
        public string ClientName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? ClientEmail { get; set; }
        public string? ClientState { get; set; }
        public string? ClientCity { get; set; }
        public string Address { get; set; } = string.Empty;
        public LeadSource LeadSource { get; set; } = LeadSource.Other;
        public string? Notes { get; set; }
    }

    public sealed class UpdateLeadRequestDto : CreateLeadRequestDto
    {
        public LeadStatus Status { get; set; } = LeadStatus.New;
        public Guid? AssignedToUserId { get; set; }
    }

    public sealed class LeadFollowUpDto
    {
        public required string Id { get; init; }
        public required string LeadId { get; init; }
        public required DateTime FollowUpDate { get; init; }
        public required FollowUpStatus Status { get; init; }
        public string? Notes { get; init; }
        public required DateTime CreatedAt { get; init; }
        public string? CreatedBy { get; init; }
    }

    public sealed class CreateFollowUpRequestDto
    {
        public FollowUpStatus Status { get; set; }
        public string? Notes { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<LeadDto>>>> GetLeads(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? searchTerm = null,
        [FromQuery] LeadStatus? status = null,
        [FromQuery] LeadSource? source = null,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Leads.AsNoTracking().Where(l => l.TenantId == TenantId);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var s = searchTerm.Trim().ToLower();
            query = query.Where(l =>
                l.ClientName.ToLower().Contains(s) ||
                l.PhoneNumber.ToLower().Contains(s) ||
                l.Address.ToLower().Contains(s));
        }
        if (status.HasValue)
            query = query.Where(l => l.Status == status.Value);
        if (source.HasValue)
            query = query.Where(l => l.LeadSource == source.Value);

        var total = await query.CountAsync(ct);
        var leads = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = leads.Select(ToDto).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        return ApiResponse<PaginatedResponse<LeadDto>>.Ok(new PaginatedResponse<LeadDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<LeadDto>>> GetLeadById([FromRoute] Guid id, CancellationToken ct)
    {
        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (lead is null) return NotFound(ApiResponse<LeadDto>.Fail("Lead not found"));
        return ApiResponse<LeadDto>.Ok(ToDto(lead));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<LeadDto>>> CreateLead([FromBody] CreateLeadRequestDto request, CancellationToken ct)
    {
        var createdBy = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "system";

        var lead = new Lead
        {
            TenantId = TenantId,
            ClientName = request.ClientName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            ClientEmail = request.ClientEmail?.Trim(),
            ClientState = request.ClientState?.Trim(),
            ClientCity = request.ClientCity?.Trim(),
            Address = request.Address.Trim(),
            LeadSource = request.LeadSource,
            Notes = request.Notes?.Trim(),
            Status = LeadStatus.New,
            CreatedBy = createdBy
        };

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetLeadById), new { id = lead.Id }, ApiResponse<LeadDto>.Ok(ToDto(lead)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<LeadDto>>> UpdateLead([FromRoute] Guid id, [FromBody] UpdateLeadRequestDto request, CancellationToken ct)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (lead is null) return NotFound(ApiResponse<LeadDto>.Fail("Lead not found"));

        var oldStatus = lead.Status;
        var oldNotes = lead.Notes?.Trim() ?? string.Empty;

        lead.ClientName = request.ClientName.Trim();
        lead.PhoneNumber = request.PhoneNumber.Trim();
        lead.ClientEmail = request.ClientEmail?.Trim();
        lead.ClientState = request.ClientState?.Trim();
        lead.ClientCity = request.ClientCity?.Trim();
        lead.Address = request.Address.Trim();
        lead.LeadSource = request.LeadSource;
        lead.Notes = request.Notes?.Trim();
        lead.Status = request.Status;
        lead.AssignedToUserId = request.AssignedToUserId;

        await _db.SaveChangesAsync(ct);

        var newNotes = lead.Notes ?? string.Empty;
        if (oldStatus != lead.Status || oldNotes != newNotes)
        {
            var createdBy = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "system";
            _db.LeadFollowUps.Add(new LeadFollowUp
            {
                LeadId = id,
                FollowUpDate = DateTime.UtcNow,
                Status = (FollowUpStatus)(int)lead.Status,
                Notes = string.IsNullOrWhiteSpace(lead.Notes) ? null : lead.Notes.Trim(),
                CreatedBy = createdBy
            });
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 208)
            {
                // 208 = Invalid object name 'LeadFollowUps' - table missing; lead was already saved
            }
        }

        // Sync package status: when lead status changes, update all packages for this lead to the same status
        if (oldStatus != lead.Status)
        {
            var packagesToUpdate = await _db.Packages.Where(p => p.LeadId == id && p.TenantId == TenantId).ToListAsync(ct);
            var newPackageStatus = (PackageStatus)(int)lead.Status;
            foreach (var p in packagesToUpdate)
                p.Status = newPackageStatus;
            if (packagesToUpdate.Count > 0)
                await _db.SaveChangesAsync(ct);
        }

        return ApiResponse<LeadDto>.Ok(ToDto(lead));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteLead([FromRoute] Guid id, CancellationToken ct)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (lead is null) return NotFound(ApiResponse<object>.Fail("Lead not found"));
        _db.Leads.Remove(lead);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    [HttpGet("{id:guid}/follow-ups")]
    public async Task<ActionResult<ApiResponse<List<LeadFollowUpDto>>>> GetFollowUps([FromRoute] Guid id, CancellationToken ct)
    {
        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (lead is null) return NotFound(ApiResponse<List<LeadFollowUpDto>>.Fail("Lead not found"));

        var list = await _db.LeadFollowUps.AsNoTracking()
            .Where(f => f.LeadId == id)
            .OrderByDescending(f => f.FollowUpDate)
            .ThenByDescending(f => f.CreatedAt)
            .Select(f => new LeadFollowUpDto
            {
                Id = f.Id.ToString("D"),
                LeadId = f.LeadId.ToString("D"),
                FollowUpDate = f.FollowUpDate,
                Status = f.Status,
                Notes = f.Notes,
                CreatedAt = f.CreatedAt,
                CreatedBy = f.CreatedBy
            })
            .ToListAsync(ct);

        return ApiResponse<List<LeadFollowUpDto>>.Ok(list);
    }

    [HttpPost("{id:guid}/follow-ups")]
    public async Task<ActionResult<ApiResponse<LeadFollowUpDto>>> AddFollowUp([FromRoute] Guid id, [FromBody] CreateFollowUpRequestDto request, CancellationToken ct)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (lead is null) return NotFound(ApiResponse<LeadFollowUpDto>.Fail("Lead not found"));

        var createdBy = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "system";

        var followUp = new LeadFollowUp
        {
            LeadId = id,
            FollowUpDate = DateTime.UtcNow,
            Status = request.Status,
            Notes = request.Notes?.Trim(),
            CreatedBy = createdBy
        };

        _db.LeadFollowUps.Add(followUp);
        await _db.SaveChangesAsync(ct);

        var dto = new LeadFollowUpDto
        {
            Id = followUp.Id.ToString("D"),
            LeadId = followUp.LeadId.ToString("D"),
            FollowUpDate = followUp.FollowUpDate,
            Status = followUp.Status,
            Notes = followUp.Notes,
            CreatedAt = followUp.CreatedAt,
            CreatedBy = followUp.CreatedBy
        };
        return CreatedAtAction(nameof(GetFollowUps), new { id }, ApiResponse<LeadFollowUpDto>.Ok(dto));
    }

    private static LeadDto ToDto(Lead l) =>
        new()
        {
            Id = l.Id.ToString("D"),
            ClientName = l.ClientName,
            PhoneNumber = l.PhoneNumber,
            ClientEmail = l.ClientEmail,
            ClientState = l.ClientState,
            ClientCity = l.ClientCity,
            Address = l.Address,
            LeadSource = l.LeadSource,
            Status = l.Status,
            Notes = l.Notes,
            TenantId = l.TenantId.ToString("D"),
            AssignedToUserId = l.AssignedToUserId?.ToString("D"),
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt,
            CreatedBy = l.CreatedBy
        };
}

