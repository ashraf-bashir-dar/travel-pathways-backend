using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;
using TravelPathways.Api.Services;
using TravelPathways.Api.Services.Inbound;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/integrations/leads")]
public sealed class LeadIntegrationsController : TenantControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITenantLeadIntegrationResolver _resolver;
    private readonly IPasswordEncryption _encryption;
    private readonly IConfiguration _configuration;

    public LeadIntegrationsController(
        AppDbContext db,
        TenantContext tenant,
        ITenantLeadIntegrationResolver resolver,
        IPasswordEncryption encryption,
        IConfiguration configuration) : base(tenant)
    {
        _db = db;
        _resolver = resolver;
        _encryption = encryption;
        _configuration = configuration;
    }

    private async Task<ActionResult?> DenyUnlessLeadIntegrationsModuleAsync(CancellationToken ct)
    {
        var enabled = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == TenantId)
            .Select(t => t.EnabledModules)
            .FirstOrDefaultAsync(ct);
        if (enabled is { Count: > 0 } && !enabled.Contains(AppModuleKey.LeadIntegrations))
            return Forbid();
        return null;
    }

    public sealed class LeadIntegrationSettingsDto
    {
        public required bool FeatureEnabledByPlatform { get; init; }
        public required bool IsInboundEnabled { get; init; }
        public required bool AutoAssignEnabled { get; init; }
        public required string InboundKey { get; init; }
        public required string GenericWebhookUrl { get; init; }
        public required string MetaWebhookUrl { get; init; }
        public string? MetaPageId { get; init; }
        public bool MetaConnectionVerified { get; init; }
        public DateTime? MetaLastWebhookAtUtc { get; init; }
        public bool HasMetaPageToken { get; init; }
    }

    public sealed class UpdateLeadIntegrationSettingsRequest
    {
        public bool IsInboundEnabled { get; set; }
        public bool AutoAssignEnabled { get; set; }
        public string? MetaPageId { get; set; }
        public string? MetaPageAccessToken { get; set; }
    }

    public sealed class SalesAssignmentRuleDto
    {
        public required string UserId { get; init; }
        public required string FullName { get; init; }
        public required bool ParticipateInInboundAutoAssign { get; init; }
        public required int InboundDailyLeadQuota { get; init; }
        public required List<LeadSource> InboundAllowedLeadSources { get; init; }
        public required int AssignedTodayCount { get; init; }
    }

    public sealed class UpdateSalesAssignmentRulesRequest
    {
        public List<SalesAssignmentRuleUpdate> Rules { get; set; } = [];
    }

    public sealed class SalesAssignmentRuleUpdate
    {
        public string UserId { get; set; } = string.Empty;
        public bool ParticipateInInboundAutoAssign { get; set; }
        public int InboundDailyLeadQuota { get; set; }
        public List<LeadSource> InboundAllowedLeadSources { get; set; } = [];
    }

    public sealed class InboundLeadEventDto
    {
        public required string Id { get; init; }
        public required string Provider { get; init; }
        public required string Status { get; init; }
        public string? ExternalId { get; init; }
        public string? ErrorMessage { get; init; }
        public string? LeadId { get; init; }
        public required DateTime CreatedAt { get; init; }
    }

    [HttpGet("settings")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<LeadIntegrationSettingsDto>>> GetSettings(CancellationToken ct)
    {
        if (DenyUnlessTenantAdmin() is { } deny) return deny;
        if (await DenyUnlessLeadIntegrationsModuleAsync(ct) is { } modDeny) return modDeny;

        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == TenantId, ct);
        if (tenant is null)
            return NotFound(ApiResponse<LeadIntegrationSettingsDto>.Fail("Tenant not found."));

        if (!tenant.InboundLeadsFeatureEnabled)
        {
            return ApiResponse<LeadIntegrationSettingsDto>.Ok(new LeadIntegrationSettingsDto
            {
                FeatureEnabledByPlatform = false,
                IsInboundEnabled = false,
                AutoAssignEnabled = false,
                InboundKey = string.Empty,
                GenericWebhookUrl = string.Empty,
                MetaWebhookUrl = string.Empty,
                MetaConnectionVerified = false,
                HasMetaPageToken = false
            }, "Inbound leads are not enabled for your agency. Contact the platform administrator.");
        }

        var integration = await _resolver.GetOrCreateForTenantAsync(TenantId, ct);
        var baseUrl = PublicApiBaseResolver.Resolve(_configuration, HttpContext)?.TrimEnd('/')
            ?? $"{Request.Scheme}://{Request.Host}";

        return ApiResponse<LeadIntegrationSettingsDto>.Ok(new LeadIntegrationSettingsDto
        {
            FeatureEnabledByPlatform = true,
            IsInboundEnabled = integration.IsInboundEnabled,
            AutoAssignEnabled = integration.AutoAssignEnabled,
            InboundKey = integration.InboundKey,
            GenericWebhookUrl = $"{baseUrl}/api/integrations/leads/inbound/{integration.InboundKey}",
            MetaWebhookUrl = $"{baseUrl}/api/webhooks/meta/{integration.InboundKey}",
            MetaPageId = integration.MetaPageId,
            MetaConnectionVerified = integration.MetaConnectionVerified,
            MetaLastWebhookAtUtc = integration.MetaLastWebhookAtUtc,
            HasMetaPageToken = !string.IsNullOrWhiteSpace(integration.MetaPageAccessTokenEncrypted)
        });
    }

    [HttpPut("settings")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<LeadIntegrationSettingsDto>>> UpdateSettings(
        [FromBody] UpdateLeadIntegrationSettingsRequest request,
        CancellationToken ct)
    {
        if (DenyUnlessTenantAdmin() is { } deny) return deny;
        if (await DenyUnlessLeadIntegrationsModuleAsync(ct) is { } modDeny) return modDeny;

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == TenantId, ct);
        if (tenant is null)
            return NotFound(ApiResponse<LeadIntegrationSettingsDto>.Fail("Tenant not found."));
        if (!tenant.InboundLeadsFeatureEnabled)
            return BadRequest(ApiResponse<LeadIntegrationSettingsDto>.Fail("Inbound leads are not enabled for your agency."));

        var integration = await _resolver.GetOrCreateForTenantAsync(TenantId, ct);
        integration.IsInboundEnabled = request.IsInboundEnabled;
        integration.AutoAssignEnabled = request.AutoAssignEnabled;
        integration.MetaPageId = request.MetaPageId?.Trim();

        if (!string.IsNullOrWhiteSpace(request.MetaPageAccessToken))
        {
            integration.MetaPageAccessTokenEncrypted = _encryption.Encrypt(request.MetaPageAccessToken.Trim());
            integration.MetaConnectionVerified = true;
        }

        await _db.SaveChangesAsync(ct);
        return await GetSettings(ct);
    }

    [HttpPost("regenerate-key")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<LeadIntegrationSettingsDto>>> RegenerateKey(CancellationToken ct)
    {
        if (DenyUnlessTenantAdmin() is { } deny) return deny;
        if (await DenyUnlessLeadIntegrationsModuleAsync(ct) is { } modDeny) return modDeny;

        var integration = await _resolver.GetOrCreateForTenantAsync(TenantId, ct);
        integration.InboundKey = _resolver.GenerateInboundKey();
        await _db.SaveChangesAsync(ct);
        return await GetSettings(ct);
    }

    [HttpGet("assignment-rules")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<List<SalesAssignmentRuleDto>>>> GetAssignmentRules(CancellationToken ct)
    {
        if (DenyUnlessTenantAdmin() is { } deny) return deny;
        if (await DenyUnlessLeadIntegrationsModuleAsync(ct) is { } modDeny) return modDeny;

        var startOfDayUtc = DateTime.UtcNow.Date;
        var users = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == TenantId && u.Department == UserDepartment.Sales)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync(ct);

        var userIds = users.Select(u => u.Id).ToList();
        var counts = await _db.Leads.AsNoTracking()
            .Where(l =>
                l.TenantId == TenantId &&
                l.AssignedToUserId.HasValue &&
                userIds.Contains(l.AssignedToUserId.Value) &&
                l.InboundExternalId != null &&
                l.CreatedAt >= startOfDayUtc)
            .GroupBy(l => l.AssignedToUserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var countMap = counts.ToDictionary(x => x.UserId, x => x.Count);

        var rules = users.Select(u => new SalesAssignmentRuleDto
        {
            UserId = u.Id.ToString("D"),
            FullName = $"{u.FirstName} {u.LastName}".Trim(),
            ParticipateInInboundAutoAssign = u.ParticipateInInboundAutoAssign,
            InboundDailyLeadQuota = u.InboundDailyLeadQuota,
            InboundAllowedLeadSources = u.InboundAllowedLeadSources ?? [],
            AssignedTodayCount = countMap.GetValueOrDefault(u.Id, 0)
        }).ToList();

        return ApiResponse<List<SalesAssignmentRuleDto>>.Ok(rules);
    }

    [HttpPut("assignment-rules")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<List<SalesAssignmentRuleDto>>>> UpdateAssignmentRules(
        [FromBody] UpdateSalesAssignmentRulesRequest request,
        CancellationToken ct)
    {
        if (DenyUnlessTenantAdmin() is { } deny) return deny;
        if (await DenyUnlessLeadIntegrationsModuleAsync(ct) is { } modDeny) return modDeny;

        var userIds = request.Rules
            .Select(r => Guid.TryParse(r.UserId, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var usersById = await _db.Users
            .Where(u => userIds.Contains(u.Id) && u.TenantId == TenantId)
            .ToDictionaryAsync(u => u.Id, ct);

        foreach (var rule in request.Rules)
        {
            if (!Guid.TryParse(rule.UserId, out var userId))
                continue;
            if (!usersById.TryGetValue(userId, out var user) || user.Department != UserDepartment.Sales)
                continue;

            user.ParticipateInInboundAutoAssign = rule.ParticipateInInboundAutoAssign;
            user.InboundDailyLeadQuota = Math.Max(0, rule.InboundDailyLeadQuota);
            user.InboundAllowedLeadSources = rule.InboundAllowedLeadSources ?? [];
        }

        await _db.SaveChangesAsync(ct);
        return await GetAssignmentRules(ct);
    }

    [HttpGet("events")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<ActionResult<ApiResponse<List<InboundLeadEventDto>>>> GetEvents(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (DenyUnlessTenantAdmin() is { } deny) return deny;
        if (await DenyUnlessLeadIntegrationsModuleAsync(ct) is { } modDeny) return modDeny;

        limit = Math.Clamp(limit, 1, 200);
        var events = await _db.InboundLeadEvents.AsNoTracking()
            .Where(e => e.TenantId == TenantId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .Select(e => new InboundLeadEventDto
            {
                Id = e.Id.ToString("D"),
                Provider = e.Provider.ToString(),
                Status = e.Status,
                ExternalId = e.ExternalId,
                ErrorMessage = e.ErrorMessage,
                LeadId = e.LeadId.HasValue ? e.LeadId.Value.ToString("D") : null,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<InboundLeadEventDto>>.Ok(events);
    }
}
