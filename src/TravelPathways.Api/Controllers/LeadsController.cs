using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;
using TravelPathways.Api.Services;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/leads")]
public sealed class LeadsController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ILeadExcelImportService _leadImportService;

    public LeadsController(
        AppDbContext db,
        TenantContext tenant,
        IEmailService emailService,
        ILeadExcelImportService leadImportService) : base(tenant)
    {
        _db = db;
        _emailService = emailService;
        _leadImportService = leadImportService;
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
        /// <summary>Display name of the user this lead is assigned to (e.g. "John Doe").</summary>
        public string? AssignedToUserName { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
        public required string CreatedBy { get; init; }
        /// <summary>True when any package for this lead has at least one reservation.</summary>
        public bool HasReservation { get; set; }
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
        /// <summary>Optional. Assign this lead to a user (sales team member) in the same tenant.</summary>
        public Guid? AssignedToUserId { get; set; }
    }

    public sealed class UpdateLeadRequestDto : CreateLeadRequestDto
    {
        public LeadStatus Status { get; set; } = LeadStatus.New;
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

    /// <summary>Returns (current user id, can see all leads in tenant). Sales/Viewer can only see their own assigned leads.</summary>
    private (Guid? UserId, bool CanSeeAllLeads) GetCurrentUserLeadScope()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        var isAdmin = string.Equals(role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
        var isSuperAdmin = string.Equals(role, UserRole.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase);
        if (isAdmin || isSuperAdmin)
            return (null, true);

        return (GetCurrentUserId(), false);
    }

    private bool CanManageLeadAssignment() => GetCurrentUserLeadScope().CanSeeAllLeads;

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private string? GetCurrentUserEmail()
    {
        var email = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Email);
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    private IQueryable<Lead> BuildLeadsQuery(
        Guid? currentUserId,
        bool canSeeAllLeads,
        string? searchTerm,
        LeadStatus? status,
        LeadSource? source,
        Guid? assignedToUserId,
        bool unassignedOnly,
        DateTime? assignedFrom,
        DateTime? assignedTo,
        string? currentUserEmail = null)
    {
        var query = _db.Leads.Where(l => l.TenantId == TenantId);

        if (!canSeeAllLeads)
        {
            var uid = currentUserId;
            var email = currentUserEmail?.Trim();
            if (!uid.HasValue && string.IsNullOrEmpty(email))
            {
                query = query.Where(_ => false);
            }
            else
            {
                // Sales see leads assigned to them, or leads they created (legacy / self-service).
                query = query.Where(l =>
                    (uid.HasValue && l.AssignedToUserId == uid.Value) ||
                    (!string.IsNullOrEmpty(email) &&
                     l.CreatedBy.ToLower() == email!.ToLower()));
            }
        }
        else if (unassignedOnly)
            query = query.Where(l => l.AssignedToUserId == null);
        else if (assignedToUserId.HasValue)
            query = query.Where(l => l.AssignedToUserId == assignedToUserId.Value);

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
        if (assignedFrom.HasValue)
        {
            var d = assignedFrom.Value.Date;
            var startUtc = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
            query = query.Where(l => l.CreatedAt >= startUtc);
        }
        if (assignedTo.HasValue)
        {
            var d = assignedTo.Value.Date;
            var endUtcExclusive = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
            query = query.Where(l => l.CreatedAt < endUtcExclusive);
        }

        return query;
    }

    private async Task<bool> IsAssignableTenantUserAsync(Guid userId, CancellationToken ct) =>
        await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.TenantId == TenantId && u.IsActive, ct);

    private static bool CanAccessLead(Lead lead, bool canSeeAllLeads, Guid? userId, string? email)
    {
        if (canSeeAllLeads) return true;
        if (userId.HasValue && lead.AssignedToUserId == userId.Value) return true;
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(lead.CreatedBy) &&
            string.Equals(lead.CreatedBy.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<LeadDto>>>> GetLeads(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? searchTerm = null,
        [FromQuery] LeadStatus? status = null,
        [FromQuery] LeadSource? source = null,
        [FromQuery] Guid? assignedToUserId = null,
        [FromQuery] bool unassignedOnly = false,
        [FromQuery] DateTime? assignedFrom = null,
        [FromQuery] DateTime? assignedTo = null,
        CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var (scopeUserId, canSeeAllLeads) = GetCurrentUserLeadScope();
        var currentUserId = scopeUserId ?? GetCurrentUserId();
        var currentUserEmail = GetCurrentUserEmail();
        var query = BuildLeadsQuery(
                currentUserId,
                canSeeAllLeads,
                searchTerm,
                status,
                source,
                assignedToUserId,
                unassignedOnly && canSeeAllLeads,
                assignedFrom,
                assignedTo,
                currentUserEmail)
            .AsNoTracking();

        var total = await query.CountAsync(ct);
        var leads = await query
            .Include(l => l.AssignedToUser)
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

    /// <summary>Lead count per user for the current tenant. Admin only; sales person cannot see other users' counts.</summary>
    [HttpGet("assignment-summary")]
    public async Task<ActionResult<ApiResponse<List<LeadAssignmentSummaryDto>>>> GetAssignmentSummary(CancellationToken ct)
    {
        var (_, canSeeAllLeads) = GetCurrentUserLeadScope();
        if (!canSeeAllLeads)
            return Forbid();

        var grouped = await _db.Leads
            .Where(l => l.TenantId == TenantId)
            .GroupBy(l => l.AssignedToUserId)
            .Select(g => new { AssignedToUserId = g.Key, LeadCount = g.Count() })
            .ToListAsync(ct);

        var userIds = grouped.Where(x => x.AssignedToUserId.HasValue).Select(x => x.AssignedToUserId!.Value).Distinct().ToList();
        var userMap = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToListAsync(ct);
        var nameByUserId = userMap.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

        var result = grouped.Select(g =>
        {
            var name = g.AssignedToUserId.HasValue && nameByUserId.TryGetValue(g.AssignedToUserId.Value, out var n)
                ? n
                : null;
            return new LeadAssignmentSummaryDto
            {
                AssignedToUserId = g.AssignedToUserId?.ToString("D"),
                AssignedToUserName = name ?? (g.AssignedToUserId == null ? "Unassigned" : "Unknown"),
                LeadCount = g.LeadCount
            };
        }).OrderByDescending(x => x.LeadCount).ToList();

        return ApiResponse<List<LeadAssignmentSummaryDto>>.Ok(result);
    }

    public sealed class LeadAssignmentSummaryDto
    {
        public string? AssignedToUserId { get; init; }
        public required string AssignedToUserName { get; init; }
        public required int LeadCount { get; init; }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<LeadDto>>> GetLeadById([FromRoute] Guid id, CancellationToken ct)
    {
        var lead = await _db.Leads.AsNoTracking()
            .Include(l => l.AssignedToUser)
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (lead is null) return NotFound(ApiResponse<LeadDto>.Fail("Lead not found"));

        if (!CanAccessLead(lead, GetCurrentUserLeadScope().CanSeeAllLeads, GetCurrentUserId(), GetCurrentUserEmail()))
            return NotFound(ApiResponse<LeadDto>.Fail("Lead not found"));

        var hasReservationForLead = await _db.Reservations.AsNoTracking()
            .Join(_db.Packages.AsNoTracking(),
                r => r.PackageId,
                p => p.Id,
                (r, p) => new { r, p })
            .AnyAsync(x =>
                x.r.TenantId == TenantId &&
                x.p.TenantId == TenantId &&
                x.p.LeadId == id,
                ct);

        var dto = ToDto(lead);
        dto.HasReservation = hasReservationForLead;
        return ApiResponse<LeadDto>.Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<LeadDto>>> CreateLead([FromBody] CreateLeadRequestDto request, CancellationToken ct)
    {
        var createdBy = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "system";
        var (scopeUserId, canManageAssignment) = GetCurrentUserLeadScope();
        var currentUserId = scopeUserId ?? GetCurrentUserId();

        Guid? assignee = null;
        if (canManageAssignment)
        {
            if (request.AssignedToUserId.HasValue)
            {
                if (!await IsAssignableTenantUserAsync(request.AssignedToUserId.Value, ct))
                    return BadRequest(ApiResponse<LeadDto>.Fail("Assigned user not found or inactive."));
                assignee = request.AssignedToUserId;
            }
        }
        else if (currentUserId.HasValue)
        {
            assignee = currentUserId;
        }
        else
        {
            return BadRequest(ApiResponse<LeadDto>.Fail("Could not determine your user account. Please sign in again."));
        }

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
            CreatedBy = createdBy,
            AssignedToUserId = assignee
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

        var (_, canSeeAllLeads) = GetCurrentUserLeadScope();
        if (!CanAccessLead(lead, canSeeAllLeads, GetCurrentUserId(), GetCurrentUserEmail()))
            return NotFound(ApiResponse<LeadDto>.Fail("Lead not found"));

        // Once any package for this lead has a reservation, Tour Manager should not edit lead/package details.
        // Allow Admin/SuperAdmin (canSeeAllLeads) to override if needed.
        if (!canSeeAllLeads)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
            var isTourManager = string.Equals(role, UserRole.Agent.ToString(), StringComparison.OrdinalIgnoreCase);
            if (isTourManager)
            {
                // Any reservation tied to any package for this lead in this tenant.
                var hasReservationForLead = await _db.Reservations.AsNoTracking()
                    .Join(_db.Packages.AsNoTracking(),
                        r => r.PackageId,
                        p => p.Id,
                        (r, p) => new { r, p })
                    .AnyAsync(x =>
                        x.r.TenantId == TenantId &&
                        x.p.TenantId == TenantId &&
                        x.p.LeadId == id,
                        ct);
                if (hasReservationForLead)
                    return BadRequest(ApiResponse<LeadDto>.Fail("This lead has a package that has been sent for reservation and cannot be edited."));
            }
        }

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

        if (canSeeAllLeads)
        {
            if (request.AssignedToUserId.HasValue)
            {
                if (!await IsAssignableTenantUserAsync(request.AssignedToUserId.Value, ct))
                    return BadRequest(ApiResponse<LeadDto>.Fail("Assigned user not found or inactive."));
                lead.AssignedToUserId = request.AssignedToUserId;
            }
            else
                lead.AssignedToUserId = null;
        }

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
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "42P01")
            {
                // 42P01 = undefined_table - table missing; lead was already saved
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
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteLead([FromRoute] Guid id, CancellationToken ct)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (lead is null) return NotFound(ApiResponse<object>.Fail("Lead not found"));

        var (_, canSeeAllLeads) = GetCurrentUserLeadScope();
        if (!CanAccessLead(lead, canSeeAllLeads, GetCurrentUserId(), GetCurrentUserEmail()))
            return NotFound(ApiResponse<object>.Fail("Lead not found"));

        lead.IsDeleted = true;
        lead.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    [HttpGet("{id:guid}/follow-ups")]
    public async Task<ActionResult<ApiResponse<List<LeadFollowUpDto>>>> GetFollowUps([FromRoute] Guid id, CancellationToken ct)
    {
        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (lead is null) return NotFound(ApiResponse<List<LeadFollowUpDto>>.Fail("Lead not found"));

        var (_, canSeeAllLeads) = GetCurrentUserLeadScope();
        if (!CanAccessLead(lead, canSeeAllLeads, GetCurrentUserId(), GetCurrentUserEmail()))
            return NotFound(ApiResponse<List<LeadFollowUpDto>>.Fail("Lead not found"));

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

        var (_, canSeeAllLeads) = GetCurrentUserLeadScope();
        if (!CanAccessLead(lead, canSeeAllLeads, GetCurrentUserId(), GetCurrentUserEmail()))
            return NotFound(ApiResponse<LeadFollowUpDto>.Fail("Lead not found"));

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

    public sealed class BulkAssignLeadsRequest
    {
        public List<string> LeadIds { get; set; } = [];
        public Guid AssignedToUserId { get; set; }
        /// <summary>When true, assign all leads matching filter fields (admin only). LeadIds is ignored.</summary>
        public bool UseCurrentFilters { get; set; }
        public string? SearchTerm { get; set; }
        public LeadStatus? Status { get; set; }
        public LeadSource? Source { get; set; }
        public Guid? FilterAssignedToUserId { get; set; }
        public bool UnassignedOnly { get; set; }
        public DateTime? AssignedFrom { get; set; }
        public DateTime? AssignedTo { get; set; }
    }

    public sealed class BulkAssignLeadsResult
    {
        public required int UpdatedCount { get; init; }
    }

    [HttpPost("bulk-assign")]
    public async Task<ActionResult<ApiResponse<BulkAssignLeadsResult>>> BulkAssignLeads(
        [FromBody] BulkAssignLeadsRequest request,
        CancellationToken ct)
    {
        if (!CanManageLeadAssignment())
            return Forbid();

        if (!await IsAssignableTenantUserAsync(request.AssignedToUserId, ct))
            return BadRequest(ApiResponse<BulkAssignLeadsResult>.Fail("Assigned user not found or inactive."));

        int updated;
        if (request.UseCurrentFilters)
        {
            var query = BuildLeadsQuery(
                null,
                canSeeAllLeads: true,
                request.SearchTerm,
                request.Status,
                request.Source,
                request.FilterAssignedToUserId,
                request.UnassignedOnly,
                request.AssignedFrom,
                request.AssignedTo);

            updated = await query.ExecuteUpdateAsync(
                s => s.SetProperty(l => l.AssignedToUserId, request.AssignedToUserId)
                    .SetProperty(l => l.UpdatedAt, DateTime.UtcNow),
                ct);
        }
        else
        {
            if (request.LeadIds is null || request.LeadIds.Count == 0)
                return BadRequest(ApiResponse<BulkAssignLeadsResult>.Fail("Select at least one lead or use filter-based assignment."));

            var ids = new List<Guid>();
            foreach (var raw in request.LeadIds.Distinct())
            {
                if (Guid.TryParse(raw, out var id)) ids.Add(id);
            }
            if (ids.Count == 0)
                return BadRequest(ApiResponse<BulkAssignLeadsResult>.Fail("No valid lead ids provided."));

            updated = await _db.Leads
                .Where(l => l.TenantId == TenantId && ids.Contains(l.Id))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(l => l.AssignedToUserId, request.AssignedToUserId)
                        .SetProperty(l => l.UpdatedAt, DateTime.UtcNow),
                    ct);
        }

        return ApiResponse<BulkAssignLeadsResult>.Ok(new BulkAssignLeadsResult { UpdatedCount = updated });
    }

    public sealed class BulkDeleteLeadsRequest
    {
        public List<string> LeadIds { get; set; } = [];
        public bool UseCurrentFilters { get; set; }
        public string? SearchTerm { get; set; }
        public LeadStatus? Status { get; set; }
        public LeadSource? Source { get; set; }
        public Guid? FilterAssignedToUserId { get; set; }
        public bool UnassignedOnly { get; set; }
        public DateTime? AssignedFrom { get; set; }
        public DateTime? AssignedTo { get; set; }
    }

    public sealed class BulkDeleteLeadsResult
    {
        public required int DeletedCount { get; init; }
    }

    [HttpPost("bulk-delete")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<BulkDeleteLeadsResult>>> BulkDeleteLeads(
        [FromBody] BulkDeleteLeadsRequest request,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        int deleted;
        if (request.UseCurrentFilters)
        {
            var query = BuildLeadsQuery(
                null,
                canSeeAllLeads: true,
                request.SearchTerm,
                request.Status,
                request.Source,
                request.FilterAssignedToUserId,
                request.UnassignedOnly,
                request.AssignedFrom,
                request.AssignedTo);

            deleted = await query.ExecuteUpdateAsync(
                s => s.SetProperty(l => l.IsDeleted, true)
                    .SetProperty(l => l.DeletedAtUtc, now)
                    .SetProperty(l => l.UpdatedAt, now),
                ct);
        }
        else
        {
            if (request.LeadIds is null || request.LeadIds.Count == 0)
                return BadRequest(ApiResponse<BulkDeleteLeadsResult>.Fail("Select at least one lead or use filter-based delete."));

            var ids = new List<Guid>();
            foreach (var raw in request.LeadIds.Distinct())
            {
                if (Guid.TryParse(raw, out var id)) ids.Add(id);
            }
            if (ids.Count == 0)
                return BadRequest(ApiResponse<BulkDeleteLeadsResult>.Fail("No valid lead ids provided."));

            deleted = await _db.Leads
                .Where(l => l.TenantId == TenantId && ids.Contains(l.Id))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(l => l.IsDeleted, true)
                        .SetProperty(l => l.DeletedAtUtc, now)
                        .SetProperty(l => l.UpdatedAt, now),
                    ct);
        }

        return ApiResponse<BulkDeleteLeadsResult>.Ok(new BulkDeleteLeadsResult { DeletedCount = deleted });
    }

    public sealed class BulkEmailGreetingRequest
    {
        public List<string> LeadIds { get; set; } = [];
        public bool UseCurrentFilters { get; set; }
        public string? SearchTerm { get; set; }
        public LeadStatus? Status { get; set; }
        public LeadSource? Source { get; set; }
        public Guid? FilterAssignedToUserId { get; set; }
        public bool UnassignedOnly { get; set; }
        public DateTime? AssignedFrom { get; set; }
        public DateTime? AssignedTo { get; set; }
        public string? Subject { get; set; }
        public string? Message { get; set; }
    }

    public sealed class BulkEmailGreetingResult
    {
        public required int SentCount { get; init; }
        public required int SkippedNoEmailCount { get; init; }
        public required int FailedCount { get; init; }
        public List<string> Errors { get; init; } = [];
    }

    public sealed class LeadImportResultDto
    {
        public required int ImportedCount { get; init; }
        public required int SkippedCount { get; init; }
        public required int FailedCount { get; init; }
        public List<LeadImportRowErrorDto> Errors { get; init; } = [];
    }

    public sealed class LeadImportRowErrorDto
    {
        public required int RowNumber { get; init; }
        public required string Message { get; init; }
    }

    [HttpGet("import-template")]
    [Authorize(Policy = "TenantAdminOnly")]
    public IActionResult DownloadImportTemplate()
    {
        if (!CanManageLeadAssignment())
            return Forbid();

        var bytes = _leadImportService.BuildTemplate();
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "leads-import-template.xlsx");
    }

    [HttpPost("import")]
    [Authorize(Policy = "TenantAdminOnly")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)]
    public async Task<ActionResult<ApiResponse<LeadImportResultDto>>> ImportLeads(IFormFile? file, CancellationToken ct)
    {
        if (!CanManageLeadAssignment())
            return Forbid();

        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<LeadImportResultDto>.Fail("Please upload an Excel file (.xlsx)."));

        var ext = Path.GetExtension(file.FileName);
        if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<LeadImportResultDto>.Fail("Only .xlsx files are supported. Download the template and save as Excel Workbook (.xlsx)."));

        if (file.Length > 10_485_760)
            return BadRequest(ApiResponse<LeadImportResultDto>.Fail("File is too large. Maximum size is 10 MB."));

        var createdBy = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "import";

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _leadImportService.ImportAsync(stream, TenantId, createdBy, ct);
            var dto = new LeadImportResultDto
            {
                ImportedCount = result.ImportedCount,
                SkippedCount = result.SkippedCount,
                FailedCount = result.FailedCount,
                Errors = result.Errors.Select(e => new LeadImportRowErrorDto
                {
                    RowNumber = e.RowNumber,
                    Message = e.Message
                }).ToList()
            };

            var summary = result.ImportedCount > 0
                ? $"Imported {result.ImportedCount} lead(s)."
                : result.FailedCount > 0
                    ? "No leads were imported. See row errors below."
                    : "No data rows found in the file.";

            if (result.FailedCount > 0 && result.ImportedCount > 0)
                summary += $" {result.FailedCount} row(s) failed.";

            return ApiResponse<LeadImportResultDto>.Ok(dto, summary);
        }
        catch (Exception)
        {
            return BadRequest(ApiResponse<LeadImportResultDto>.Fail(
                "Could not read the Excel file. Use the downloaded template and ensure the file is a valid .xlsx workbook."));
        }
    }

    [HttpPost("bulk-email-greeting")]
    public async Task<ActionResult<ApiResponse<BulkEmailGreetingResult>>> BulkEmailGreeting(
        [FromBody] BulkEmailGreetingRequest request,
        CancellationToken ct)
    {
        if (!_emailService.IsConfigured)
            return BadRequest(ApiResponse<BulkEmailGreetingResult>.Fail(
                "Email is not configured on the server. Ask your administrator to set Email:SmtpHost and related settings in appsettings."));

        var (userId, canSeeAllLeads) = GetCurrentUserLeadScope();
        var userEmail = GetCurrentUserEmail();

        List<Lead> leads;
        if (request.UseCurrentFilters)
        {
            if (!canSeeAllLeads)
                return Forbid();

            leads = await BuildLeadsQuery(
                    null,
                    canSeeAllLeads: true,
                    request.SearchTerm,
                    request.Status,
                    request.Source,
                    request.FilterAssignedToUserId,
                    request.UnassignedOnly,
                    request.AssignedFrom,
                    request.AssignedTo)
                .ToListAsync(ct);
        }
        else
        {
            if (request.LeadIds is null || request.LeadIds.Count == 0)
                return BadRequest(ApiResponse<BulkEmailGreetingResult>.Fail("Select at least one lead."));

            var ids = new List<Guid>();
            foreach (var raw in request.LeadIds.Distinct())
            {
                if (Guid.TryParse(raw, out var id)) ids.Add(id);
            }
            if (ids.Count == 0)
                return BadRequest(ApiResponse<BulkEmailGreetingResult>.Fail("No valid lead ids provided."));

            leads = await _db.Leads
                .Where(l => l.TenantId == TenantId && ids.Contains(l.Id))
                .ToListAsync(ct);

            if (!canSeeAllLeads)
                leads = leads.Where(l => CanAccessLead(l, false, userId, userEmail)).ToList();
        }

        if (leads.Count == 0)
            return BadRequest(ApiResponse<BulkEmailGreetingResult>.Fail("No accessible leads found for this request."));

        const int maxPerRequest = 100;
        if (leads.Count > maxPerRequest)
            return BadRequest(ApiResponse<BulkEmailGreetingResult>.Fail($"Send at most {maxPerRequest} emails per request. You selected {leads.Count} leads."));

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == TenantId, ct);
        var tenantName = tenant?.Name?.Trim() ?? "Travel Pathways";

        var subjectTemplate = string.IsNullOrWhiteSpace(request.Subject)
            ? "Thank you for your interest in our travel services"
            : request.Subject.Trim();

        var messageTemplate = string.IsNullOrWhiteSpace(request.Message)
            ? "Hello {{ClientName}},\n\nThank you for your interest in our travel services. How can we assist you today?\n\nBest regards,\n{{TenantName}}"
            : request.Message.Trim();

        var sent = 0;
        var skippedNoEmail = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var lead in leads)
        {
            var to = lead.ClientEmail?.Trim();
            if (string.IsNullOrWhiteSpace(to) || !to.Contains('@'))
            {
                skippedNoEmail++;
                continue;
            }

            var name = string.IsNullOrWhiteSpace(lead.ClientName) ? "there" : lead.ClientName.Trim();
            var subject = subjectTemplate.Replace("{{ClientName}}", name, StringComparison.OrdinalIgnoreCase)
                .Replace("{{TenantName}}", tenantName, StringComparison.OrdinalIgnoreCase);
            var body = messageTemplate.Replace("{{ClientName}}", name, StringComparison.OrdinalIgnoreCase)
                .Replace("{{TenantName}}", tenantName, StringComparison.OrdinalIgnoreCase);

            var ok = await _emailService.SendEmailAsync(to, subject, body, ct);
            if (ok)
                sent++;
            else
            {
                failed++;
                if (errors.Count < 5)
                    errors.Add($"{name} ({to}): send failed");
            }
        }

        return ApiResponse<BulkEmailGreetingResult>.Ok(new BulkEmailGreetingResult
        {
            SentCount = sent,
            SkippedNoEmailCount = skippedNoEmail,
            FailedCount = failed,
            Errors = errors
        });
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
            AssignedToUserName = l.AssignedToUser != null
                ? $"{l.AssignedToUser.FirstName} {l.AssignedToUser.LastName}".Trim()
                : (string?)null,
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt,
            CreatedBy = l.CreatedBy,
            HasReservation = false
        };
}

