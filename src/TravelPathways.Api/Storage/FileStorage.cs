namespace TravelPathways.Api.Storage;

public sealed class FileStorage
{
    private readonly IWebHostEnvironment _env;

    public FileStorage(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> SaveTenantFileAsync(Guid tenantId, string category, IFormFile file, CancellationToken ct)
    {
        var uploadsRoot = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads");
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

    public async Task<string> SaveTransportCompanyFileAsync(Guid tenantId, Guid companyId, string category, IFormFile file, CancellationToken ct)
    {
        var uploadsRoot = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads");
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
        var uploadsRoot = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads");
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

    private static bool IsImageExtension(string ext)
    {
        var e = ext.ToLowerInvariant();
        return e is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp";
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }
}

