using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace TravelPathways.Api.Services;

/// <summary>Per-phase timings for package PDF generation (returned to the browser via X-Pdf-Timing header).</summary>
public sealed class PackagePdfTiming
{
    public long TotalMs { get; set; }
    public long DataLoadMs { get; set; }
    public long ImageInlineMs { get; set; }
    public long HtmlBuildMs { get; set; }
    public long ChromiumMs { get; set; }
    public long MergeMs { get; set; }
    public int PdfBytes { get; set; }
    public int HtmlChars { get; set; }
    public string? UploadsRoot { get; set; }
    public int ImagesInlined { get; set; }
    public int ImagesSkipped { get; set; }
    public List<string> ImageSkipReasons { get; init; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void AppendResponseHeaders(HttpResponse response)
    {
        var json = JsonSerializer.Serialize(this, JsonOptions);
        response.Headers.Append("X-Pdf-Timing", json);
    }
}

public sealed class PackagePdfInlineStats
{
    public int Inlined { get; private set; }
    public int Skipped { get; private set; }
    private readonly List<string> _skipReasons = [];
    private const int MaxSkipReasons = 8;

    public void RecordInlined() => Inlined++;

    public void RecordSkipped(string reason)
    {
        Skipped++;
        if (_skipReasons.Count < MaxSkipReasons)
            _skipReasons.Add(reason);
    }

    public IReadOnlyList<string> SkipReasons => _skipReasons;
}

public sealed record PackagePdfGenerateResult(
    byte[] PdfBytes,
    long HtmlBuildMs,
    long ChromiumRenderMs,
    int HtmlLength);
