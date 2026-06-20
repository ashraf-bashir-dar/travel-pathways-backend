namespace TravelPathways.Api.Storage;

/// <summary>
/// Single configured root for all uploaded files (hotels, logos, PDFs, screenshots, etc.).
/// Set <c>Uploads:Path</c> in config (production: <c>/app/wwwroot/uploads</c>).
/// </summary>
public sealed class UploadsPathProvider
{
    public string UploadsRoot { get; }

    public UploadsPathProvider(IWebHostEnvironment env, IConfiguration configuration)
    {
        var customPath = configuration["Uploads:Path"]?.Trim()
            ?? configuration["Uploads__Path"]?.Trim();

        UploadsRoot = !string.IsNullOrEmpty(customPath)
            ? Path.GetFullPath(customPath)
            : Path.GetFullPath(Path.Combine(
                env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"),
                "uploads"));

        Directory.CreateDirectory(UploadsRoot);
    }

    /// <summary>Map stored URL <c>/uploads/tenants/...</c> or absolute URL to a path under <see cref="UploadsRoot"/>.</summary>
    public string? ResolvePhysicalPathFromUploadUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var pathSegment = url.Trim();
        if (pathSegment.Contains('?', StringComparison.Ordinal))
            pathSegment = pathSegment[..pathSegment.IndexOf('?', StringComparison.Ordinal)];

        if (pathSegment.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            pathSegment.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try { pathSegment = new Uri(pathSegment).AbsolutePath; }
            catch { return null; }
        }

        if (!pathSegment.Contains("/uploads/", StringComparison.OrdinalIgnoreCase))
            return null;

        pathSegment = pathSegment.TrimStart('/');
        var relativePath = pathSegment.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase)
            ? pathSegment["uploads/".Length..]
            : pathSegment;

        return Path.Combine(UploadsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
