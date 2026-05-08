namespace TravelPathways.Api.Data.Entities;

/// <summary>Reusable PDF template that can be assigned to any tenant.</summary>
public sealed class PdfTemplate : EntityBase
{
    /// <summary>Stable unique key used by tenant assignment and generator routing.</summary>
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Optional custom HTML. When empty, built-in renderer uses Key.</summary>
    public string? HtmlTemplate { get; set; }
}
