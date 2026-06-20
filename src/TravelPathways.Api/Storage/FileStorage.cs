namespace TravelPathways.Api.Storage;

public sealed class FileStorage
{
    private readonly UploadsPathProvider _uploadsPath;

    public FileStorage(UploadsPathProvider uploadsPath)
    {
        _uploadsPath = uploadsPath;
    }

    private string UploadsRoot => _uploadsPath.UploadsRoot;

    public async Task<string> SaveTenantFileAsync(Guid tenantId, string category, IFormFile file, CancellationToken ct)
    {
        var folder = Path.Combine(UploadsRoot, "tenants", tenantId.ToString("D"), category);
        var safeName = MakeSafeFileName(file.FileName);
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}_{safeName}";
        var fullPath = Path.Combine(folder, fileName);

        await SaveUploadedFileAsync(fullPath, file, ct);

        return $"/uploads/tenants/{tenantId:D}/{category}/{fileName}";
    }

    public async Task<string> SaveB2bAgentDocumentAsync(Guid tenantId, Guid agentId, IFormFile file, CancellationToken ct)
    {
        var folder = Path.Combine(UploadsRoot, "tenants", tenantId.ToString("D"), "b2b-agents", agentId.ToString("D"), "documents");
        var safeName = MakeSafeFileName(file.FileName);
        var ext = Path.GetExtension(safeName);
        if (string.IsNullOrEmpty(ext) || !IsPdfExtension(ext)) ext = ".pdf";
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await SaveUploadedFileAsync(fullPath, file, ct);

        return $"/uploads/tenants/{tenantId:D}/b2b-agents/{agentId:D}/documents/{fileName}";
    }

    public async Task<string> SaveTransportCompanyFileAsync(Guid tenantId, Guid companyId, string category, IFormFile file, CancellationToken ct)
    {
        var folder = Path.Combine(UploadsRoot, "tenants", tenantId.ToString("D"), "transport-companies", companyId.ToString("D"), category);
        var safeName = MakeSafeFileName(file.FileName);
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}_{safeName}";
        var fullPath = Path.Combine(folder, fileName);

        await SaveUploadedFileAsync(fullPath, file, ct);

        return $"/uploads/tenants/{tenantId:D}/transport-companies/{companyId:D}/{category}/{fileName}";
    }

    public async Task<string> SaveDriverFileAsync(Guid tenantId, Guid driverId, string category, IFormFile file, CancellationToken ct)
    {
        var folder = Path.Combine(UploadsRoot, "tenants", tenantId.ToString("D"), "drivers", driverId.ToString("D"), category);
        var safeName = MakeSafeFileName(file.FileName);
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}_{safeName}";
        var fullPath = Path.Combine(folder, fileName);

        await SaveUploadedFileAsync(fullPath, file, ct);

        return $"/uploads/tenants/{tenantId:D}/drivers/{driverId:D}/{category}/{fileName}";
    }

    public async Task<string> SaveDriverAssignmentVehicleImageAsync(Guid tenantId, Guid assignmentId, IFormFile file, CancellationToken ct)
    {
        var folder = Path.Combine(UploadsRoot, "tenants", tenantId.ToString("D"), "driver-assignments", assignmentId.ToString("D"), "vehicle");
        var safeName = MakeSafeFileName(file.FileName);
        var ext = Path.GetExtension(safeName);
        if (string.IsNullOrEmpty(ext) || (!IsImageExtension(ext) && !IsPdfExtension(ext))) ext = ".jpg";
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await SaveUploadedFileAsync(fullPath, file, ct);

        return $"/uploads/tenants/{tenantId:D}/driver-assignments/{assignmentId:D}/vehicle/{fileName}";
    }

    public async Task<string> SaveHotelImageAsync(Guid tenantId, Guid hotelId, IFormFile file, CancellationToken ct)
    {
        var folder = Path.Combine(UploadsRoot, "tenants", tenantId.ToString("D"), "hotels", hotelId.ToString("D"), "images");
        var safeName = MakeSafeFileName(file.FileName);
        var ext = Path.GetExtension(safeName);
        if (string.IsNullOrEmpty(ext) || !IsImageExtension(ext)) ext = ".jpg";
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await SaveUploadedFileAsync(fullPath, file, ct);

        return $"/uploads/tenants/{tenantId:D}/hotels/{hotelId:D}/images/{fileName}";
    }

    public async Task<string> SavePaymentScreenshotAsync(Guid tenantId, Guid paymentId, IFormFile file, CancellationToken ct)
    {
        var folder = Path.Combine(UploadsRoot, "tenants", tenantId.ToString("D"), "payments");
        var safeName = MakeSafeFileName(file.FileName);
        var ext = Path.GetExtension(safeName);
        if (string.IsNullOrEmpty(ext) || (!IsImageExtension(ext) && !IsPdfExtension(ext))) ext = ".jpg";
        var fileName = $"{paymentId:D}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await SaveUploadedFileAsync(fullPath, file, ct);

        return $"/uploads/tenants/{tenantId:D}/payments/{fileName}";
    }

    public async Task<string> SaveUserProfilePhotoAsync(Guid? tenantId, Guid userId, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            throw new InvalidOperationException("Profile photo file is empty.");

        var folder = tenantId.HasValue
            ? Path.Combine(UploadsRoot, "tenants", tenantId.Value.ToString("D"), "users", userId.ToString("D"))
            : Path.Combine(UploadsRoot, "platform", "users", userId.ToString("D"));
        var safeName = MakeSafeFileName(file.FileName);
        var ext = Path.GetExtension(safeName);
        if (string.IsNullOrEmpty(ext) || !IsImageExtension(ext)) ext = ".jpg";
        var fileName = $"profile{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await SaveUploadedFileAsync(fullPath, file, ct);

        return tenantId.HasValue
            ? $"/uploads/tenants/{tenantId:D}/users/{userId:D}/{fileName}"
            : $"/uploads/platform/users/{userId:D}/{fileName}";
    }

    public async Task<string> SaveReservationScreenshotAsync(Guid tenantId, Guid reservationId, IFormFile file, CancellationToken ct)
    {
        var folder = Path.Combine(UploadsRoot, "tenants", tenantId.ToString("D"), "reservations", reservationId.ToString("D"));
        var safeName = MakeSafeFileName(file.FileName);
        var ext = Path.GetExtension(safeName);
        if (string.IsNullOrEmpty(ext) || (!IsImageExtension(ext) && !IsPdfExtension(ext))) ext = ".jpg";
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await SaveUploadedFileAsync(fullPath, file, ct);

        return $"/uploads/tenants/{tenantId:D}/reservations/{reservationId:D}/{fileName}";
    }

    /// <summary>Write file to disk, flush, then verify it exists before returning.</summary>
    private static async Task SaveUploadedFileAsync(string fullPath, IFormFile file, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
        {
            await file.CopyToAsync(stream, ct);
            await stream.FlushAsync(ct);
        }

        EnsureFileSaved(fullPath);
    }

    private static void EnsureFileSaved(string fullPath)
    {
        if (!File.Exists(fullPath))
            throw new InvalidOperationException("Image upload failed. File was not saved.");

        if (new FileInfo(fullPath).Length <= 0)
            throw new InvalidOperationException("Image upload failed. File was not saved.");
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
            name = name.Replace(c, '_');
        return name;
    }
}
