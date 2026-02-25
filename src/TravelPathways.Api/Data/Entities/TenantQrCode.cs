namespace TravelPathways.Api.Data.Entities;

/// <summary>QR code image for a tenant (e.g. UPI), shown in package PDFs.</summary>
public sealed class TenantQrCode : EntityBase
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Label shown with the QR (e.g. "UPI", "GPay").</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>Relative URL/path to the uploaded image, e.g. /uploads/tenants/{id}/qrcodes/...</summary>
    public string ImageUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    /// <summary>Order when displaying multiple QR codes (lower first).</summary>
    public int DisplayOrder { get; set; }
}
