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
    private readonly ILeadExcelExportService _leadExportService;

    private const int MaxExportLeads = 5000;

    public LeadsController(
        AppDbContext db,
        TenantContext tenant,
        IEmailService emailService,
        ILeadExcelImportService leadImportService,
        ILeadExcelExportService leadExportService) : base(tenant)
    {
        _db = db;
        _emailService = emailService;
        _leadImportService = leadImportService;
        _leadExportService = leadExportService;
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
        public bool IsLocked { get; init; }
        public string? Notes { get; init; }
        public string? NextFollowUpDate { get; init; }
        public required string TenantId { get; init; }
        public string? AssignedToUserId { get; init; }
        /// <summary>Display name of the user this lead is assigned to (e.g. "John Doe").</summary>
        public string? AssignedToUserName { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
        public required string CreatedBy { get; init; }
        /// <summary>True when any package for this lead has at least one reservation.</summary>
        public bool HasReservation { get; set; }
        /// <summary>Total tour packages created for this lead.</summary>
        public int PackageCount { get; set; }
        /// <summary>Package history entries (create/update revisions) for this lead.</summary>
        public int PackageLogCount { get; set; }
        /// <summary>Confirmation date from the latest package for this lead (yyyy-MM-dd).</summary>
        public string? PackageConfirmationDate { get; set; }
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
        /// <summary>Optional. yyyy-MM-dd. Defaults to tomorrow when omitted.</summary>
        public string? NextFollowUpDate { get; set; }
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

    private async Task<List<AppModuleKey>> GetEffectiveAllowedModulesForCurrentUserAsync(CancellationToken ct)
    {
        var tenantModules = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == TenantId)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct) ?? [];

        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        var isSuperAdmin = string.Equals(role, UserRole.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase);
        if (isSuperAdmin)
            return ModuleAccess.GetEffectiveModules(tenantModules, []);

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return [];

        var userModules = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId.Value && u.TenantId == TenantId)
            .Select(u => u.AllowedModules)
            .FirstOrDefaultAsync(ct) ?? [];

        return ModuleAccess.GetEffectiveModules(tenantModules, userModules);
    }

    private async Task<ActionResult?> EnsureLeadsModuleAsync(CancellationToken ct)
    {
        if (!HasTenantId)
            return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));

        var effective = await GetEffectiveAllowedModulesForCurrentUserAsync(ct);
        if (!ModuleAccess.HasModule(effective, AppModuleKey.Leads))
            return StatusCode(403, ApiResponse<object>.Fail("Leads module is not available for your account."));

        return null;
    }

    private async Task<ActionResult?> EnsurePaymentLeadSearchModuleAsync(CancellationToken ct)
    {
        if (!HasTenantId)
            return BadRequest(ApiResponse<object>.Fail("Tenant context is missing."));

        var effective = await GetEffectiveAllowedModulesForCurrentUserAsync(ct);
        if (ModuleAccess.HasModule(effective, AppModuleKey.Leads)
            || ModuleAccess.HasModule(effective, AppModuleKey.Ledger)
            || ModuleAccess.HasModule(effective, AppModuleKey.Accounts)
            || ModuleAccess.HasModule(effective, AppModuleKey.Sales))
            return null;

        return StatusCode(403, ApiResponse<object>.Fail("Lead search for payments is not available for your account."));
    }

    /// <summary>Returns (current user id, can see all leads in tenant). Tenant admins with Leads module see all; others see assigned only.</summary>
    private async Task<(Guid? UserId, bool CanSeeAllLeads)> GetCurrentUserLeadScopeAsync(CancellationToken ct)
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        var isSuperAdmin = string.Equals(role, UserRole.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase);
        if (isSuperAdmin)
            return (null, true);

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return (null, false);

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value && u.TenantId == TenantId, ct);
        if (user is null)
            return (userId, false);

        var canSeeAll = ModulePermissionResolver.CanSeeAllLeads(user);
        return canSeeAll ? (null, true) : (userId, false);
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private async Task<AppUser?> GetCurrentAppUserAsync(CancellationToken ct) =>
        await TenantUserPermissions.LoadCurrentUserAsync(_db, TenantId, User, ct);

    private async Task<ActionResult?> DenyUnlessLeadActionAsync(ModuleAction action, CancellationToken ct)
    {
        var user = await GetCurrentAppUserAsync(ct);
        return TenantUserPermissions.DenyUnless(user, AppModuleKey.Leads, action);
    }

    private static IQueryable<Lead> ApplyActiveFollowUpScope(IQueryable<Lead> query) =>
        query.Where(l =>
            l.NextFollowUpDate != null
            && l.Status != LeadStatus.Confirmed
            && l.Status != LeadStatus.Cancelled
            && l.Status != LeadStatus.NotInterested
            && l.Status != LeadStatus.AlreadyBooked);

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
        string? nextFollowUpFilter)
    {
        var query = _db.Leads.Where(l => l.TenantId == TenantId);

        if (!canSeeAllLeads)
        {
            if (!currentUserId.HasValue)
                query = query.Where(_ => false);
            else
                query = query.Where(l => l.AssignedToUserId == currentUserId.Value);
        }
        else if (unassignedOnly)
            query = query.Where(l => l.AssignedToUserId == null);
        else if (assignedToUserId.HasValue)
            query = query.Where(l => l.AssignedToUserId == assignedToUserId.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = PostgresSearch.ToContainsPattern(searchTerm);
            query = query.Where(l =>
                EF.Functions.ILike(l.ClientName, pattern, "\\") ||
                EF.Functions.ILike(l.PhoneNumber, pattern, "\\") ||
                EF.Functions.ILike(l.Address, pattern, "\\"));
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

        if (!string.IsNullOrWhiteSpace(nextFollowUpFilter))
        {
            var today = LeadNextFollowUpHelper.Today();
            query = ApplyActiveFollowUpScope(query);
            switch (nextFollowUpFilter.Trim().ToLowerInvariant())
            {
                case "duetoday":
                    query = query.Where(l => l.NextFollowUpDate == today);
                    break;
                case "overdue":
                    query = query.Where(l => l.NextFollowUpDate < today);
                    break;
                case "upcoming":
                    var end = today.AddDays(7);
                    query = query.Where(l => l.NextFollowUpDate > today && l.NextFollowUpDate <= end);
                    break;
            }
        }

        return query;
    }

    private IQueryable<Lead> ApplyLeadScopeForFollowUpSummary(Guid? currentUserId, bool canSeeAllLeads)
    {
        var query = _db.Leads.Where(l => l.TenantId == TenantId);
        if (!canSeeAllLeads)
        {
            if (!currentUserId.HasValue)
                return query.Where(_ => false);
            return query.Where(l => l.AssignedToUserId == currentUserId.Value);
        }

        return query;
    }

    private async Task<bool> IsAssignableTenantUserAsync(Guid userId, CancellationToken ct) =>
        await _db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.TenantId == TenantId && u.IsActive, ct);

    private static bool CanAccessLead(Lead lead, bool canSeeAllLeads, Guid? userId)
    {
        if (canSeeAllLeads) return true;
        return userId.HasValue && lead.AssignedToUserId == userId.Value;
    }

    /// <summary>Soft-delete all packages (and related rows) linked to the given leads.</summary>
    private async Task SoftDeletePackagesForLeadsAsync(IReadOnlyList<Guid> leadIds, DateTime deletedAtUtc, CancellationToken ct)
    {
        if (leadIds.Count == 0) return;

        var packageIds = await _db.Packages
            .Where(p => p.TenantId == TenantId && p.LeadId != null && leadIds.Contains(p.LeadId.Value) && !p.IsDeleted)
            .Select(p => p.Id)
            .ToListAsync(ct);

        if (packageIds.Count > 0)
        {
            await _db.Packages
                .Where(p => packageIds.Contains(p.Id))
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(p => p.IsDeleted, true)
                        .SetProperty(p => p.DeletedAtUtc, deletedAtUtc)
                        .SetProperty(p => p.UpdatedAt, deletedAtUtc),
                    ct);

            await _db.DayItineraries
                .Where(d => d.TenantId == TenantId && packageIds.Contains(d.PackageId) && !d.IsDeleted)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(d => d.IsDeleted, true)
                        .SetProperty(d => d.DeletedAtUtc, deletedAtUtc)
                        .SetProperty(d => d.UpdatedAt, deletedAtUtc),
                    ct);

            await _db.Reservations
                .Where(r => r.TenantId == TenantId && packageIds.Contains(r.PackageId) && !r.IsDeleted)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(r => r.IsDeleted, true)
                        .SetProperty(r => r.DeletedAtUtc, deletedAtUtc)
                        .SetProperty(r => r.UpdatedAt, deletedAtUtc),
                    ct);
        }

        await _db.PackageLogs
            .Where(l => l.TenantId == TenantId && leadIds.Contains(l.LeadId) && !l.IsDeleted)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(l => l.IsDeleted, true)
                    .SetProperty(l => l.DeletedAtUtc, deletedAtUtc)
                    .SetProperty(l => l.UpdatedAt, deletedAtUtc),
                ct);
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
        [FromQuery] string? nextFollowUpFilter = null,
        CancellationToken ct = default)
    {
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;
        var permCheck = await DenyUnlessLeadActionAsync(ModuleAction.View, ct);
        if (permCheck != null) return permCheck;

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var (scopeUserId, canSeeAllLeads) = await GetCurrentUserLeadScopeAsync(ct);
        var currentUserId = scopeUserId ?? GetCurrentUserId();
        var followUpFilter = string.IsNullOrWhiteSpace(nextFollowUpFilter) ? null : nextFollowUpFilter.Trim();
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
                followUpFilter)
            .AsNoTracking();

        var total = await query.CountAsync(ct);
        var ordered = string.IsNullOrWhiteSpace(followUpFilter)
            ? query.OrderByDescending(l => l.CreatedAt)
            : query.OrderBy(l => l.NextFollowUpDate).ThenByDescending(l => l.CreatedAt);
        var leads = await ordered
            .Include(l => l.AssignedToUser)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = leads.Select(ToDto).ToList();
        await EnrichLeadPackageCountsAsync(items, ct);
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

    /// <summary>Search leads assigned to the current user when recording a payment (Ledger / Accounts).</summary>
    [HttpGet("payment-search")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<LeadDto>>>> SearchLeadsForPayment(
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool includePaymentHistoryLeads = false,
        [FromQuery] bool assignedToMeOnly = false,
        [FromQuery] LeadStatus? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var moduleCheck = await EnsurePaymentLeadSearchModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return ApiResponse<PaginatedResponse<LeadDto>>.Ok(new PaginatedResponse<LeadDto>
            {
                Items = [],
                TotalCount = 0,
                PageNumber = 1,
                PageSize = pageSize,
                TotalPages = 1
            });
        }

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var (_, canSeeAllLeads) = await GetCurrentUserLeadScopeAsync(ct);
        IQueryable<Lead> query;
        if (assignedToMeOnly || !canSeeAllLeads)
        {
            List<Guid>? paymentHistoryLeadIds = null;
            if (!assignedToMeOnly && includePaymentHistoryLeads)
            {
                paymentHistoryLeadIds = await _db.Payments.AsNoTracking()
                    .Where(p => p.TenantId == TenantId
                                && p.PaymentType == PaymentType.Received
                                && p.RecordedByUserId == currentUserId.Value
                                && p.LeadId != null)
                    .Select(p => p.LeadId!.Value)
                    .Distinct()
                    .ToListAsync(ct);
            }

            query = _db.Leads.AsNoTracking()
                .Where(l => l.TenantId == TenantId
                            && (l.AssignedToUserId == currentUserId.Value
                                || (!assignedToMeOnly
                                    && includePaymentHistoryLeads
                                    && paymentHistoryLeadIds != null
                                    && paymentHistoryLeadIds.Contains(l.Id))));
        }
        else
        {
            query = _db.Leads.AsNoTracking().Where(l => l.TenantId == TenantId);
        }

        if (status.HasValue)
            query = query.Where(l => l.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var s = searchTerm.Trim();
            if (Guid.TryParse(s, out var leadId))
            {
                query = query.Where(l => l.Id == leadId);
            }
            else
            {
                var pattern = PostgresSearch.ToContainsPattern(s);
                query = query.Where(l =>
                    EF.Functions.ILike(l.ClientName, pattern, "\\") ||
                    EF.Functions.ILike(l.PhoneNumber, pattern, "\\") ||
                    EF.Functions.ILike(l.Address, pattern, "\\"));
            }
        }

        var total = await query.CountAsync(ct);
        var leads = await query
            .Include(l => l.AssignedToUser)
            .OrderByDescending(l => l.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = leads.Select(ToDto).ToList();
        await EnrichLeadPackageCountsAsync(items, ct);
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
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

        var adminDenied = DenyUnlessTenantAdmin();
        if (adminDenied != null) return adminDenied;

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

    /// <summary>Counts of leads with follow-ups due today or overdue (scoped to current user).</summary>
    [HttpGet("follow-ups/summary")]
    public async Task<ActionResult<ApiResponse<LeadFollowUpSummaryDto>>> GetFollowUpSummary(CancellationToken ct)
    {
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

        var (scopeUserId, canSeeAllLeads) = await GetCurrentUserLeadScopeAsync(ct);
        var currentUserId = scopeUserId ?? GetCurrentUserId();
        var today = LeadNextFollowUpHelper.Today();
        var scoped = ApplyLeadScopeForFollowUpSummary(currentUserId, canSeeAllLeads);
        var active = ApplyActiveFollowUpScope(scoped);

        var dueToday = await active.CountAsync(l => l.NextFollowUpDate == today, ct);
        var overdue = await active.CountAsync(l => l.NextFollowUpDate < today, ct);

        return ApiResponse<LeadFollowUpSummaryDto>.Ok(new LeadFollowUpSummaryDto
        {
            DueToday = dueToday,
            Overdue = overdue
        });
    }

    public sealed class LeadFollowUpSummaryDto
    {
        public int DueToday { get; init; }
        public int Overdue { get; init; }
    }

    public sealed class LeadLookupDto
    {
        public required string Id { get; init; }
        public required string ClientName { get; init; }
        public required string PhoneNumber { get; init; }
    }

    public sealed class LeadAssignmentSummaryDto
    {
        public string? AssignedToUserId { get; init; }
        public required string AssignedToUserName { get; init; }
        public required int LeadCount { get; init; }
    }

    /// <summary>Lightweight lead list for payment dropdowns.</summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<LeadLookupDto>>>> GetLeadLookup(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 200,
        [FromQuery] string? searchTerm = null,
        CancellationToken ct = default)
    {
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var (scopeUserId, canSeeAllLeads) = await GetCurrentUserLeadScopeAsync(ct);
        var currentUserId = scopeUserId ?? GetCurrentUserId();
        var query = BuildLeadsQuery(
                currentUserId,
                canSeeAllLeads,
                searchTerm,
                null,
                null,
                null,
                false,
                null,
                null,
                null)
            .AsNoTracking();

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LeadLookupDto
            {
                Id = l.Id.ToString("D"),
                ClientName = l.ClientName,
                PhoneNumber = l.PhoneNumber
            })
            .ToListAsync(ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return ApiResponse<PaginatedResponse<LeadLookupDto>>.Ok(new PaginatedResponse<LeadLookupDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    /// <summary>Export leads in the assignment date range to Excel (includes follow-up history columns).</summary>
    [HttpGet("export")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<IActionResult> ExportLeads(
        [FromQuery] DateTime assignedFrom,
        [FromQuery] DateTime assignedTo,
        [FromQuery] string? searchTerm = null,
        [FromQuery] LeadStatus? status = null,
        [FromQuery] LeadSource? source = null,
        [FromQuery] Guid? assignedToUserId = null,
        [FromQuery] bool unassignedOnly = false,
        CancellationToken ct = default)
    {
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

        var adminDenied = DenyUnlessTenantAdmin();
        if (adminDenied != null) return adminDenied;

        var fromDate = assignedFrom.Date;
        var toDate = assignedTo.Date;
        if (toDate < fromDate)
            return BadRequest(ApiResponse<object>.Fail("Assigned To Date must be on or after Assigned From Date."));

        var query = BuildLeadsQuery(
                null,
                canSeeAllLeads: true,
                searchTerm,
                status,
                source,
                assignedToUserId,
                unassignedOnly,
                fromDate,
                toDate,
                null)
            .AsNoTracking();

        var total = await query.CountAsync(ct);
        if (total == 0)
            return BadRequest(ApiResponse<object>.Fail("No leads found for the selected date range and filters."));

        if (total > MaxExportLeads)
            return BadRequest(ApiResponse<object>.Fail(
                $"Too many leads to export ({total}). Narrow the date range or filters (maximum {MaxExportLeads})."));

        var leads = await query
            .Include(l => l.AssignedToUser)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        var leadIds = leads.Select(l => l.Id).ToList();
        var followUps = await _db.LeadFollowUps.AsNoTracking()
            .Where(f => leadIds.Contains(f.LeadId))
            .OrderBy(f => f.FollowUpDate)
            .ThenBy(f => f.CreatedAt)
            .ToListAsync(ct);

        var followUpsByLeadId = followUps
            .GroupBy(f => f.LeadId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = leads.Select(l => new LeadExcelExportRow
        {
            ClientName = l.ClientName,
            PhoneNumber = l.PhoneNumber,
            LeadSourceLabel = LeadExcelExportService.FormatLeadSource(l.LeadSource),
            StatusLabel = LeadExcelExportService.FormatLeadStatus(l.Status),
            AssignedToName = l.AssignedToUser != null
                ? $"{l.AssignedToUser.FirstName} {l.AssignedToUser.LastName}".Trim()
                : "Unassigned",
            AssignmentDateUtc = l.CreatedAt,
            FollowUps = (followUpsByLeadId.GetValueOrDefault(l.Id) ?? [])
                .Select(f => new LeadExcelFollowUpCell
                {
                    FollowUpDateUtc = f.FollowUpDate,
                    Notes = f.Notes
                })
                .ToList()
        }).ToList();

        var bytes = _leadExportService.BuildWorkbook(rows);
        var fileName =
            $"leads-{LeadExcelExportService.FormatDateForFileName(fromDate)}-to-{LeadExcelExportService.FormatDateForFileName(toDate)}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet("import-template")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<IActionResult> DownloadImportTemplate(CancellationToken ct)
    {
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

        var bytes = _leadImportService.BuildTemplate();
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "leads-import-template.xlsx");
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<LeadDto>>> GetLeadById([FromRoute] Guid id, CancellationToken ct)
    {
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null)
        {
            moduleCheck = await EnsurePaymentLeadSearchModuleAsync(ct);
            if (moduleCheck != null) return moduleCheck;
        }

        var lead = await _db.Leads.AsNoTracking()
            .Include(l => l.AssignedToUser)
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (lead is null) return NotFound(ApiResponse<LeadDto>.Fail("Lead not found"));

        var (_, canSeeAllLeads) = await GetCurrentUserLeadScopeAsync(ct);
        if (!CanAccessLead(lead, canSeeAllLeads, GetCurrentUserId()))
            return NotFound(ApiResponse<LeadDto>.Fail("Lead not found"));

        var dto = ToDto(lead);
        await EnrichLeadPackageCountsAsync([dto], ct);
        return ApiResponse<LeadDto>.Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<LeadDto>>> CreateLead([FromBody] CreateLeadRequestDto request, CancellationToken ct)
    {
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;
        var permCheck = await DenyUnlessLeadActionAsync(ModuleAction.Create, ct);
        if (permCheck != null) return permCheck;

        var createdBy = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "system";
        var (scopeUserId, canManageAssignment) = await GetCurrentUserLeadScopeAsync(ct);
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
            AssignedToUserId = assignee,
            NextFollowUpDate = LeadNextFollowUpHelper.ForNewLead(request.NextFollowUpDate)
        };

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetLeadById), new { id = lead.Id }, ApiResponse<LeadDto>.Ok(ToDto(lead)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<LeadDto>>> UpdateLead([FromRoute] Guid id, [FromBody] UpdateLeadRequestDto request, CancellationToken ct)
    {
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;
        var permCheck = await DenyUnlessLeadActionAsync(ModuleAction.Edit, ct);
        if (permCheck != null) return permCheck;

        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (lead is null) return NotFound(ApiResponse<LeadDto>.Fail("Lead not found"));

        var (_, canSeeAllLeads) = await GetCurrentUserLeadScopeAsync(ct);
        if (!CanAccessLead(lead, canSeeAllLeads, GetCurrentUserId()))
            return NotFound(ApiResponse<LeadDto>.Fail("Lead not found"));

        if (lead.IsLocked)
            return BadRequest(ApiResponse<LeadDto>.Fail("This lead is locked. Ask an admin to unlock it before editing."));

        // Once any package for this lead has a reservation, scoped users should not edit lead/package details.
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

        if (request.Status == LeadStatus.Confirmed && oldStatus != LeadStatus.Confirmed)
        {
            var latestPackage = await _db.Packages.AsNoTracking()
                .Where(p => p.LeadId == id && p.TenantId == TenantId)
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Select(p => new { p.ConfirmationDate })
                .FirstOrDefaultAsync(ct);

            if (latestPackage is null)
                return BadRequest(ApiResponse<LeadDto>.Fail("Create a package for this lead before marking it as Confirmed."));

            if (!latestPackage.ConfirmationDate.HasValue || latestPackage.ConfirmationDate.Value == default)
                return BadRequest(ApiResponse<LeadDto>.Fail(
                    "Set the package confirmation date on the package before marking this lead as Confirmed."));
        }

        lead.ClientName = request.ClientName.Trim();
        lead.PhoneNumber = request.PhoneNumber.Trim();
        lead.ClientEmail = request.ClientEmail?.Trim();
        lead.ClientState = request.ClientState?.Trim();
        lead.ClientCity = request.ClientCity?.Trim();
        lead.Address = request.Address.Trim();
        lead.LeadSource = request.LeadSource;
        lead.Notes = request.Notes?.Trim();
        lead.Status = request.Status;
        lead.NextFollowUpDate = LeadNextFollowUpHelper.ForStatus(request.Status, request.NextFollowUpDate);
        lead.IsLocked = request.Status == LeadStatus.Confirmed;

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

        // Sync package status: when lead status changes, update only the latest package for this lead.
        if (oldStatus != lead.Status)
        {
            var latestPackage = await _db.Packages
                .Where(p => p.LeadId == id && p.TenantId == TenantId)
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .FirstOrDefaultAsync(ct);
            if (latestPackage is not null)
            {
                var newPackageStatus = (PackageStatus)(int)lead.Status;
                latestPackage.Status = newPackageStatus;
                latestPackage.IsLocked = newPackageStatus == PackageStatus.Confirmed;
                if (newPackageStatus != PackageStatus.Confirmed)
                    latestPackage.ConfirmationDate = null;
                await _db.SaveChangesAsync(ct);
            }
        }

        return ApiResponse<LeadDto>.Ok(ToDto(lead));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteLead([FromRoute] Guid id, CancellationToken ct)
    {
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;
        var permCheck = await DenyUnlessLeadActionAsync(ModuleAction.Delete, ct);
        if (permCheck != null) return permCheck;

        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (lead is null) return NotFound(ApiResponse<object>.Fail("Lead not found"));

        var (_, canSeeAllLeads) = await GetCurrentUserLeadScopeAsync(ct);
        if (!CanAccessLead(lead, canSeeAllLeads, GetCurrentUserId()))
            return NotFound(ApiResponse<object>.Fail("Lead not found"));

        if (lead.IsLocked)
            return BadRequest(ApiResponse<object>.Fail("This lead is locked. Ask an admin to unlock it before deleting."));

        var deletedAt = DateTime.UtcNow;
        await SoftDeletePackagesForLeadsAsync([id], deletedAt, ct);

        lead.IsDeleted = true;
        lead.DeletedAtUtc = deletedAt;
        lead.UpdatedAt = deletedAt;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    [HttpPost("{id:guid}/unlock")]
    public async Task<ActionResult<ApiResponse<LeadDto>>> UnlockLead([FromRoute] Guid id, CancellationToken ct)
    {
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;
        var permCheck = await DenyUnlessLeadActionAsync(ModuleAction.Edit, ct);
        if (permCheck != null) return permCheck;
        var (_, canSeeAllLeads) = await GetCurrentUserLeadScopeAsync(ct);
        if (!canSeeAllLeads)
            return StatusCode(403, ApiResponse<LeadDto>.Fail("Unlocking leads requires agency-wide leads access."));

        var lead = await _db.Leads
            .Include(l => l.AssignedToUser)
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (lead is null) return NotFound(ApiResponse<LeadDto>.Fail("Lead not found"));

        lead.IsLocked = false;

        var packagesToUnlock = await _db.Packages
            .Where(p => p.LeadId == id && p.TenantId == TenantId)
            .ToListAsync(ct);
        foreach (var package in packagesToUnlock)
            package.IsLocked = false;

        await _db.SaveChangesAsync(ct);
        return ApiResponse<LeadDto>.Ok(ToDto(lead));
    }

    [HttpGet("{id:guid}/follow-ups")]
    public async Task<ActionResult<ApiResponse<List<LeadFollowUpDto>>>> GetFollowUps([FromRoute] Guid id, CancellationToken ct)
    {
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (lead is null) return NotFound(ApiResponse<List<LeadFollowUpDto>>.Fail("Lead not found"));

        var (_, canSeeAllLeads) = await GetCurrentUserLeadScopeAsync(ct);
        if (!CanAccessLead(lead, canSeeAllLeads, GetCurrentUserId()))
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
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id && l.TenantId == TenantId, ct);
        if (lead is null) return NotFound(ApiResponse<LeadFollowUpDto>.Fail("Lead not found"));

        var (_, canSeeAllLeads) = await GetCurrentUserLeadScopeAsync(ct);
        if (!CanAccessLead(lead, canSeeAllLeads, GetCurrentUserId()))
            return NotFound(ApiResponse<LeadFollowUpDto>.Fail("Lead not found"));

        if (lead.IsLocked)
            return BadRequest(ApiResponse<LeadFollowUpDto>.Fail("This lead is locked. Ask an admin to unlock it before editing."));

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
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<BulkAssignLeadsResult>>> BulkAssignLeads(
        [FromBody] BulkAssignLeadsRequest request,
        CancellationToken ct)
    {
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

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
                request.AssignedTo,
                null);

            updated = await query
                .Where(l => !l.IsLocked)
                .ExecuteUpdateAsync(
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
                .Where(l => l.TenantId == TenantId && ids.Contains(l.Id) && !l.IsLocked)
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
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

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
                request.AssignedTo,
                null);

            var leadIdsToDelete = await query
                .Where(l => !l.IsLocked)
                .Select(l => l.Id)
                .ToListAsync(ct);

            await SoftDeletePackagesForLeadsAsync(leadIdsToDelete, now, ct);

            deleted = await query
                .Where(l => !l.IsLocked)
                .ExecuteUpdateAsync(
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

            var unlockedIds = await _db.Leads
                .Where(l => l.TenantId == TenantId && ids.Contains(l.Id) && !l.IsLocked)
                .Select(l => l.Id)
                .ToListAsync(ct);

            await SoftDeletePackagesForLeadsAsync(unlockedIds, now, ct);

            deleted = await _db.Leads
                .Where(l => l.TenantId == TenantId && unlockedIds.Contains(l.Id))
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

    [HttpPost("import")]
    [Authorize(Policy = "TenantAdminOnly")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)]
    public async Task<ActionResult<ApiResponse<LeadImportResultDto>>> ImportLeads(IFormFile? file, CancellationToken ct)
    {
        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

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

        var moduleCheck = await EnsureLeadsModuleAsync(ct);
        if (moduleCheck != null) return moduleCheck;

        var (userId, canSeeAllLeads) = await GetCurrentUserLeadScopeAsync(ct);

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
                    request.AssignedTo,
                    null)
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
                leads = leads.Where(l => CanAccessLead(l, false, userId)).ToList();
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

    private async Task EnrichLeadPackageCountsAsync(List<LeadDto> items, CancellationToken ct)
    {
        if (items.Count == 0) return;

        var leadIds = items
            .Select(i => Guid.TryParse(i.Id, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
        if (leadIds.Count == 0) return;

        var packageCounts = await _db.Packages.AsNoTracking()
            .Where(p => p.TenantId == TenantId && p.LeadId != null && leadIds.Contains(p.LeadId.Value))
            .GroupBy(p => p.LeadId!.Value)
            .Select(g => new { LeadId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        Dictionary<Guid, int> packageLogByLead = new();
        var effectiveModules = await GetEffectiveAllowedModulesForCurrentUserAsync(ct);
        if (ModuleAccess.HasModule(effectiveModules, AppModuleKey.Leads))
        {
            try
            {
                var packageLogCounts = await _db.PackageLogs.AsNoTracking()
                    .Where(l => l.TenantId == TenantId && leadIds.Contains(l.LeadId))
                    .GroupBy(l => l.LeadId)
                    .Select(g => new { LeadId = g.Key, Count = g.Count() })
                    .ToListAsync(ct);
                packageLogByLead = packageLogCounts.ToDictionary(x => x.LeadId, x => x.Count);
            }
            catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn)
            {
                // PackageLogs table/columns not applied yet — counts stay zero.
            }
        }

        var hasReservationLeadIds = await _db.Reservations.AsNoTracking()
            .Join(
                _db.Packages.AsNoTracking(),
                r => r.PackageId,
                p => p.Id,
                (r, p) => new { r, p })
            .Where(x =>
                x.r.TenantId == TenantId &&
                x.p.TenantId == TenantId &&
                x.p.LeadId != null &&
                leadIds.Contains(x.p.LeadId.Value))
            .Select(x => x.p.LeadId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var latestPackageConfirmationDates = await _db.Packages.AsNoTracking()
            .Where(p =>
                p.TenantId == TenantId &&
                p.LeadId != null &&
                leadIds.Contains(p.LeadId.Value) &&
                p.IsLatestForLead)
            .Select(p => new { LeadId = p.LeadId!.Value, p.ConfirmationDate })
            .ToListAsync(ct);

        var packageByLead = packageCounts.ToDictionary(x => x.LeadId, x => x.Count);
        var reservationLeadSet = hasReservationLeadIds.ToHashSet();
        var confirmationDateByLead = latestPackageConfirmationDates.ToDictionary(
            x => x.LeadId,
            x => x.ConfirmationDate?.ToString("yyyy-MM-dd"));

        foreach (var dto in items)
        {
            if (!Guid.TryParse(dto.Id, out var leadId)) continue;
            dto.PackageCount = packageByLead.GetValueOrDefault(leadId);
            dto.PackageLogCount = packageLogByLead.GetValueOrDefault(leadId);
            dto.HasReservation = reservationLeadSet.Contains(leadId);
            dto.PackageConfirmationDate = confirmationDateByLead.GetValueOrDefault(leadId);
        }
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
            IsLocked = l.IsLocked,
            Notes = l.Notes,
            NextFollowUpDate = LeadNextFollowUpHelper.ToApiString(l.NextFollowUpDate),
            TenantId = l.TenantId.ToString("D"),
            AssignedToUserId = l.AssignedToUserId?.ToString("D"),
            AssignedToUserName = l.AssignedToUser != null
                ? $"{l.AssignedToUser.FirstName} {l.AssignedToUser.LastName}".Trim()
                : (string?)null,
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt,
            CreatedBy = l.CreatedBy,
            HasReservation = false,
            PackageCount = 0,
            PackageLogCount = 0
        };
}

