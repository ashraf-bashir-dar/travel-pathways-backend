namespace TravelPathways.Api.Data.Entities;

/// <summary>Browser or desktop extension available for install via the Extensions module.</summary>
public sealed class ExtensionCatalogItem : TenantEntityBase
{
    /// <summary>Unique code per tenant, e.g. browser-activity.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    /// <summary>Full description shown on the extension detail panel.</summary>
    public string Details { get; set; } = string.Empty;

    public string Icon { get; set; } = "🧩";

    /// <summary>Comma-separated, e.g. Chrome, Edge.</summary>
    public string SupportedBrowsers { get; set; } = "Chrome, Edge";

    public string? ChromeStoreUrl { get; set; }

    public string? EdgeStoreUrl { get; set; }

    /// <summary>Relative API path for ZIP download (under /api/), e.g. user-activity/extension-download.</summary>
    public string? DownloadApiPath { get; set; }

    /// <summary>Optional custom install steps (plain text, one step per line).</summary>
    public string? InstallSteps { get; set; }

    public int SortOrder { get; set; }

    public bool IsPublished { get; set; } = true;
}
