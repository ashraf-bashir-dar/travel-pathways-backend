namespace TravelPathways.Api.Data.Entities;

/// <summary>Per-tenant inbound lead integration settings (webhook URL, auto-assign, Meta page).</summary>
public sealed class TenantLeadIntegration : TenantEntityBase
{
  /// <summary>Public segment in webhook URLs; unique across all tenants.</summary>
  public string InboundKey { get; set; } = string.Empty;

  /// <summary>Tenant master switch (only effective when <see cref="Tenant.InboundLeadsFeatureEnabled"/>).</summary>
  public bool IsInboundEnabled { get; set; }

  /// <summary>When true, eligible sales users receive inbound leads automatically.</summary>
  public bool AutoAssignEnabled { get; set; }

  /// <summary>Facebook Page ID connected for Lead Ads.</summary>
  public string? MetaPageId { get; set; }

  /// <summary>Encrypted long-lived page access token for Graph API.</summary>
  public string? MetaPageAccessTokenEncrypted { get; set; }

  public bool MetaConnectionVerified { get; set; }

  public DateTime? MetaLastWebhookAtUtc { get; set; }
}
