using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;

namespace TravelPathways.Api.Services.Inbound;

public sealed class ResolvedTenantIntegration
{
    public required Guid TenantId { get; init; }
    public required TenantLeadIntegration Integration { get; init; }
    public required bool FeatureEnabled { get; init; }
}

public interface ITenantLeadIntegrationResolver
{
    Task<ResolvedTenantIntegration?> ResolveByInboundKeyAsync(string inboundKey, CancellationToken ct);
    Task<TenantLeadIntegration> GetOrCreateForTenantAsync(Guid tenantId, CancellationToken ct);
    string GenerateInboundKey();
}

public sealed class TenantLeadIntegrationResolver : ITenantLeadIntegrationResolver
{
    private readonly AppDbContext _db;

    public TenantLeadIntegrationResolver(AppDbContext db) => _db = db;

    public async Task<ResolvedTenantIntegration?> ResolveByInboundKeyAsync(string inboundKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(inboundKey))
            return null;

        var key = inboundKey.Trim();
        var integration = await _db.TenantLeadIntegrations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InboundKey == key && !i.IsDeleted, ct);

        if (integration is null)
            return null;

        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == integration.TenantId && !t.IsDeleted, ct);

        if (tenant is null)
            return null;

        return new ResolvedTenantIntegration
        {
            TenantId = integration.TenantId,
            Integration = integration,
            FeatureEnabled = tenant.InboundLeadsFeatureEnabled
        };
    }

    public async Task<TenantLeadIntegration> GetOrCreateForTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var existing = await _db.TenantLeadIntegrations
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && !i.IsDeleted, ct);

        if (existing is not null)
            return existing;

        var integration = new TenantLeadIntegration
        {
            TenantId = tenantId,
            InboundKey = GenerateInboundKey(),
            IsInboundEnabled = false,
            AutoAssignEnabled = false
        };
        _db.TenantLeadIntegrations.Add(integration);
        await _db.SaveChangesAsync(ct);
        return integration;
    }

    public string GenerateInboundKey() =>
        Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
