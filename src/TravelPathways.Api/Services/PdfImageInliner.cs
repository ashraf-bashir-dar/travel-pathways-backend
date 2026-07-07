using System.Collections.Concurrent;
using TravelPathways.Api.Storage;

namespace TravelPathways.Api.Services;

/// <summary>Reads upload files from disk and embeds them as data URLs for PDF HTML (avoids Chromium network fetches).</summary>
public sealed class PdfImageInliner
{
    // Registered as a singleton, so this cache lives for the process lifetime. Keyed by the
    // resolved file path and gated on LastWriteTimeUtc, so a re-uploaded file at the same path
    // is picked up automatically instead of serving stale bytes forever. Cleared wholesale past a
    // generous size to bound memory instead of implementing a full LRU for what is a small,
    // bounded (max ~2MB per entry) set of frequently-reused tenant/hotel images.
    private const int MaxCacheEntries = 1000;
    private readonly ConcurrentDictionary<string, (DateTime LastWriteUtc, string DataUrl)> _cache = new();

    private readonly UploadsPathProvider _uploadsPath;
    private readonly int _maxBytesPerImage;

    public PdfImageInliner(UploadsPathProvider uploadsPath, IConfiguration configuration)
    {
        _uploadsPath = uploadsPath;
        var maxConfig = configuration["PdfGenerator:MaxInlinedImageBytes"]?.Trim()
            ?? configuration["PdfGenerator__MaxInlinedImageBytes"]?.Trim();
        _maxBytesPerImage = 512_000;
        if (!string.IsNullOrEmpty(maxConfig) && int.TryParse(maxConfig, out var n) && n > 0)
            _maxBytesPerImage = Math.Clamp(n, 32_000, 2_097_152);
    }

    /// <summary>
    /// Returns a data: URL when the file exists under <see cref="UploadsPathProvider.UploadsRoot"/>.
    /// Returns empty string when the file is missing, too large, or cannot be resolved — never returns
    /// an HTTP URL (that would make Chromium wait on network during PDF generation).
    /// </summary>
    public string? ToDataUrlOrEmpty(string? url, PackagePdfInlineStats? stats = null, Action<string>? onSkip = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var path = url.Trim();
        if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            stats?.RecordInlined();
            return path;
        }

        if (!path.Contains("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var reason = $"non-upload URL skipped for PDF inlining: {TruncateForLog(path)}";
                stats?.RecordSkipped(reason);
                onSkip?.Invoke(reason);
                return "";
            }
            return path;
        }

        var fullPath = _uploadsPath.ResolvePhysicalPathFromUploadUrl(path);
        if (fullPath is null || !File.Exists(fullPath))
        {
            var reason = $"upload file missing for PDF: {TruncateForLog(path)} → {fullPath ?? "(unresolved)"}";
            stats?.RecordSkipped(reason);
            onSkip?.Invoke(reason);
            return "";
        }

        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > _maxBytesPerImage)
            {
                var reason = $"upload file too large for PDF inlining ({fileInfo.Length} bytes, max {_maxBytesPerImage}): {fullPath}";
                stats?.RecordSkipped(reason);
                onSkip?.Invoke(reason);
                return "";
            }

            if (_cache.TryGetValue(fullPath, out var cached) && cached.LastWriteUtc == fileInfo.LastWriteTimeUtc)
            {
                stats?.RecordInlined();
                return cached.DataUrl;
            }

            var bytes = File.ReadAllBytes(fullPath);
            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            var mime = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
            var dataUrl = "data:" + mime + ";base64," + Convert.ToBase64String(bytes);

            if (_cache.Count >= MaxCacheEntries)
                _cache.Clear();
            _cache[fullPath] = (fileInfo.LastWriteTimeUtc, dataUrl);

            stats?.RecordInlined();
            return dataUrl;
        }
        catch (Exception ex)
        {
            var reason = $"failed to read upload for PDF ({fullPath}): {ex.Message}";
            stats?.RecordSkipped(reason);
            onSkip?.Invoke(reason);
            return "";
        }
    }

    private static string TruncateForLog(string value) =>
        value.Length <= 120 ? value : value[..117] + "...";
}
