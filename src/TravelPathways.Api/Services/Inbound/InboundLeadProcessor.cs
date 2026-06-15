using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;

namespace TravelPathways.Api.Services.Inbound;

public interface IInboundLeadProcessor
{
    Task<InboundLeadProcessResult> ProcessAsync(Guid tenantId, InboundLeadPayload payload, CancellationToken ct);
}

public sealed class InboundLeadProcessor : IInboundLeadProcessor
{
    private readonly AppDbContext _db;
    private readonly ILeadAutoAssignmentService _autoAssign;

    public InboundLeadProcessor(AppDbContext db, ILeadAutoAssignmentService autoAssign)
    {
        _db = db;
        _autoAssign = autoAssign;
    }

    public async Task<InboundLeadProcessResult> ProcessAsync(
        Guid tenantId,
        InboundLeadPayload payload,
        CancellationToken ct)
    {
        var phone = payload.PhoneNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(phone))
        {
            return Fail("Phone number is required.");
        }

        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct);

        if (tenant is null || !tenant.IsActive)
            return Fail("Tenant not found or inactive.");

        if (!tenant.InboundLeadsFeatureEnabled)
            return Fail("Inbound leads are not enabled for this tenant.");

        var integration = await _db.TenantLeadIntegrations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && !i.IsDeleted, ct);

        if (integration is null || !integration.IsInboundEnabled)
            return Fail("Inbound integration is disabled.");

        var externalId = payload.ExternalId?.Trim();
        if (!string.IsNullOrEmpty(externalId))
        {
            var exists = await _db.Leads
                .IgnoreQueryFilters()
                .AnyAsync(l =>
                    l.TenantId == tenantId &&
                    !l.IsDeleted &&
                    l.InboundProvider == payload.Provider &&
                    l.InboundExternalId == externalId,
                    ct);

            if (exists)
            {
                await LogEventAsync(tenantId, payload, "Duplicate", null, externalId, ct);
                return new InboundLeadProcessResult { Success = true, Duplicate = true };
            }
        }

        var createdBy = $"integration:{payload.Provider.ToString().ToLowerInvariant()}";
        Guid? assignee = null;
        if (integration.AutoAssignEnabled)
            assignee = await _autoAssign.PickAssigneeAsync(tenantId, payload.LeadSource, ct);

        var lead = new Lead
        {
            TenantId = tenantId,
            ClientName = string.IsNullOrWhiteSpace(payload.ClientName) ? "Unknown" : payload.ClientName.Trim(),
            PhoneNumber = phone,
            ClientEmail = payload.ClientEmail?.Trim(),
            ClientState = payload.ClientState?.Trim(),
            ClientCity = payload.ClientCity?.Trim(),
            Address = string.IsNullOrWhiteSpace(payload.Address) ? string.Empty : payload.Address.Trim(),
            LeadSource = payload.LeadSource,
            Notes = payload.Notes?.Trim(),
            Status = LeadStatus.New,
            CreatedBy = createdBy,
            AssignedToUserId = assignee,
            InboundProvider = payload.Provider,
            InboundExternalId = externalId,
            NextFollowUpDate = LeadNextFollowUpHelper.DefaultDate()
        };

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);

        await LogEventAsync(tenantId, payload, "Processed", lead.Id, externalId, ct);

        return new InboundLeadProcessResult
        {
            Success = true,
            LeadId = lead.Id,
            AssignedToUserId = assignee
        };
    }

    private async Task LogEventAsync(
        Guid tenantId,
        InboundLeadPayload payload,
        string status,
        Guid? leadId,
        string? externalId,
        CancellationToken ct)
    {
        var raw = payload.RawPayload;
        if (!string.IsNullOrEmpty(raw) && raw.Length > 8000)
            raw = raw[..8000];

        _db.InboundLeadEvents.Add(new InboundLeadEvent
        {
            TenantId = tenantId,
            Provider = payload.Provider,
            ExternalId = externalId,
            Status = status,
            RawPayload = raw,
            LeadId = leadId,
            ErrorMessage = status == "Failed" ? payload.Notes : null
        });
        await _db.SaveChangesAsync(ct);
    }

    private static InboundLeadProcessResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
