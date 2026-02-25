namespace TravelPathways.Api.Data.Entities;

/// <summary>Bank account details for a tenant (travel agent), shown in package PDFs.</summary>
public sealed class TenantBankAccount : EntityBase
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string AccountHolderName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string IFSC { get; set; } = string.Empty;
    public string? Branch { get; set; }
    /// <summary>Order when displaying multiple accounts (lower first).</summary>
    public int DisplayOrder { get; set; }
}
