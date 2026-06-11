namespace TravelPathways.Api.Data.Entities;

/// <summary>Tenant-managed B2B travel agent / partner.</summary>
public sealed class B2bAgent : TenantEntityBase
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

    public List<B2bAgentDocument> Documents { get; set; } = [];
}

public sealed class B2bAgentDocument : EntityBase
{
    public Guid B2bAgentId { get; set; }
    public B2bAgent B2bAgent { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
