using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TravelPathways.Api.Data;

namespace TravelPathways.Api.Services;

/// <summary>
/// Caches PDF HtmlTemplate bodies in memory. Each request first loads only <see cref="Data.Entities.PdfTemplate.UpdatedAt"/>
/// so PostgreSQL can avoid detoasting the large Html column when the template has not changed.
/// </summary>
public interface IPdfTemplateHtmlCache
{
    Task<PdfTemplateHtmlCacheEntry?> TryLoadActiveTemplateAsync(string templateKey, CancellationToken cancellationToken = default);

    void Invalidate(string templateKey);
}

public sealed record PdfTemplateHtmlCacheEntry(string HtmlBody, string TemplateName);

public sealed class PdfTemplateHtmlCache : IPdfTemplateHtmlCache
{
    private const string CacheKeyPrefix = "travelpathways:pdftpl:";
    private readonly AppDbContext _db;
    private readonly IMemoryCache _memoryCache;

    public PdfTemplateHtmlCache(AppDbContext db, IMemoryCache memoryCache)
    {
        _db = db;
        _memoryCache = memoryCache;
    }

    public static string MemoryCacheKey(string templateKey) => CacheKeyPrefix + templateKey.Trim();

    public async Task<PdfTemplateHtmlCacheEntry?> TryLoadActiveTemplateAsync(string templateKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateKey)) return null;
        var key = templateKey.Trim();

        var meta = await _db.PdfTemplates.AsNoTracking()
            .Where(t => t.Key == key && !t.IsDeleted && t.IsActive)
            .Select(t => new { t.UpdatedAt })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (meta is null) return null;

        var ck = MemoryCacheKey(key);
        if (_memoryCache.TryGetValue(ck, out CachedLine? line) && line != null && line.UpdatedAt == meta.UpdatedAt)
            return new PdfTemplateHtmlCacheEntry(line.Html, line.Name);

        var row = await _db.PdfTemplates.AsNoTracking()
            .Where(t => t.Key == key && !t.IsDeleted && t.IsActive)
            .Select(t => new { t.HtmlTemplate, t.Name, t.UpdatedAt })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row is null) return null;

        var html = row.HtmlTemplate?.Trim() ?? "";
        _memoryCache.Set(ck, new CachedLine(row.UpdatedAt, html, row.Name), new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(8)
        });

        return new PdfTemplateHtmlCacheEntry(html, row.Name);
    }

    public void Invalidate(string templateKey)
    {
        if (string.IsNullOrWhiteSpace(templateKey)) return;
        _memoryCache.Remove(MemoryCacheKey(templateKey));
    }

    private sealed class CachedLine
    {
        public DateTime UpdatedAt { get; }
        public string Html { get; }
        public string Name { get; }

        public CachedLine(DateTime updatedAt, string html, string name)
        {
            UpdatedAt = updatedAt;
            Html = html;
            Name = name;
        }
    }
}
