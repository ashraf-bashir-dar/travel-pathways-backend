using Microsoft.Extensions.Configuration;

namespace TravelPathways.Api.Storage;

public sealed class FileStorage
{
    private readonly IWebHostEnvironment _env;
    private readonly string _uploadsRoot;

    public FileStorage(IWebHostEnvironment env, IConfiguration configuration)
    {
        _env = env;
        var customPath = configuration["Uploads:Path"]?.Trim() ?? configuration["Uploads__Path"]?.Trim();
        _uploadsRoot = !string.IsNullOrEmpty(customPath)
            ? customPath
            : Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");
    }

    public async Task<string> SaveTenantFileAsync(Guid tenantId, string category, IFormFile file, CancellationToken ct)
    {
        var uploadsRoot = _uploadsRoot;
        var folder = Path.Combine(uploadsRoot, "tenants", tenantId.ToString("D"), category);
        Directory.CreateDirectory(folder);

        var safeName = MakeSafeFileName(file.FileName);
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}_{safeName}";
        var fullPath = Path.Combine(folder, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        // URL served by static files middleware
        return $"/uploads/tenants/{tenantId:D}/{category}/{fileName}";
    }

    public async Task<string> SaveB2bAgentDocumentAsync(Guid tenantId, Guid agentId, IFormFile file, CancellationToken ct)
    {
        var folder = Path.Combine(_uploadsRoot, "tenants", tenantId.ToString("D"), "b2b-agents", agentId.ToString("D"), "documents");
        Directory.CreateDirectory(folder);

        var safeName = MakeSafeFileName(file.FileName);
        var ext = Path.GetExtension(safeName);
        if (string.IsNullOrEmpty(ext) || !IsPdfExtension(ext)) ext = ".pdf";
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        return $"/uploads/tenants/{tenantId:D}/b2b-agents/{agentId:D}/documents/{fileName}";
    }

    public async Task<string> SaveTransportCompanyFileAsync(Guid tenantId, Guid companyId, string category, IFormFile file, CancellationToken ct)
    {
        var uploadsRoot = _uploadsRoot;
        var folder = Path.Combine(uploadsRoot, "tenants", tenantId.ToString("D"), "transport-companies", companyId.ToString("D"), category);
        Directory.CreateDirectory(folder);

        var safeName = MakeSafeFileName(file.FileName);
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}_{safeName}";
        var fullPath = Path.Combine(folder, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        return $"/uploads/tenants/{tenantId:D}/transport-companies/{companyId:D}/{category}/{fileName}";
    }

    public async Task<string> SaveHotelImageAsync(Guid tenantId, Guid hotelId, IFormFile file, CancellationToken ct)
    {
        var uploadsRoot = _uploadsRoot;
        var folder = Path.Combine(uploadsRoot, "tenants", tenantId.ToString("D"), "hotels", hotelId.ToString("D"), "images");
        Directory.CreateDirectory(folder);

        var safeName = MakeSafeFileName(file.FileName);
        var ext = Path.GetExtension(safeName);
        if (string.IsNullOrEmpty(ext) || !IsImageExtension(ext)) ext = ".jpg";
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        return $"/uploads/tenants/{tenantId:D}/hotels/{hotelId:D}/images/{fileName}";
    }

    /// <summary>Save a payment screenshot. Returns relative URL for the saved file.</summary>
    public async Task<string> SavePaymentScreenshotAsync(Guid tenantId, Guid paymentId, IFormFile file, CancellationToken ct)
    {
        var uploadsRoot = _uploadsRoot;
        var folder = Path.Combine(uploadsRoot, "tenants", tenantId.ToString("D"), "payments");
        Directory.CreateDirectory(folder);

        var safeName = MakeSafeFileName(file.FileName);
        var ext = Path.GetExtension(safeName);
        if (string.IsNullOrEmpty(ext) || (!IsImageExtension(ext) && !IsPdfExtension(ext))) ext = ".jpg";
        var fileName = $"{paymentId:D}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        return $"/uploads/tenants/{tenantId:D}/payments/{fileName}";
    }

    /// <summary>Save a team-chat image. Returns relative URL under /uploads/...</summary>
    public async Task<string> SaveChatImageAsync(Guid tenantId, Guid groupId, Guid messageId, IFormFile file, CancellationToken ct)
    {
        var folder = Path.Combine(
            _uploadsRoot,
            "tenants",
            tenantId.ToString("D"),
            "chat",
            groupId.ToString("D"),
            messageId.ToString("D"));
        Directory.CreateDirectory(folder);

        var safeName = MakeSafeFileName(file.FileName);
        var ext = Path.GetExtension(safeName);
        if (string.IsNullOrEmpty(ext) || !IsImageExtension(ext)) ext = ".jpg";
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        return $"/uploads/tenants/{tenantId:D}/chat/{groupId:D}/{messageId:D}/{fileName}";
    }

    /// <summary>Save a reservation payment screenshot. Multiple allowed per reservation. Returns relative URL.</summary>
    public async Task<string> SaveReservationScreenshotAsync(Guid tenantId, Guid reservationId, IFormFile file, CancellationToken ct)
    {
        var uploadsRoot = _uploadsRoot;
        var folder = Path.Combine(uploadsRoot, "tenants", tenantId.ToString("D"), "reservations", reservationId.ToString("D"));
        Directory.CreateDirectory(folder);

        var safeName = MakeSafeFileName(file.FileName);
        var ext = Path.GetExtension(safeName);
        if (string.IsNullOrEmpty(ext) || (!IsImageExtension(ext) && !IsPdfExtension(ext))) ext = ".jpg";
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        return $"/uploads/tenants/{tenantId:D}/reservations/{reservationId:D}/{fileName}";
    }

    private static bool IsImageExtension(string ext)
    {
        var e = ext.ToLowerInvariant();
        return e is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp";
    }

    private static bool IsPdfExtension(string ext) => ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    private static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }
}

