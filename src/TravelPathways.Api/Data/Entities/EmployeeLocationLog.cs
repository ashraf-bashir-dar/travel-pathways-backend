using System.ComponentModel.DataAnnotations;

namespace TravelPathways.Api.Data.Entities;

/// <summary>
/// GPS point uploaded from the company mobile app (office phone).
/// </summary>
public sealed class EmployeeLocationLog : TenantEntityBase
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Horizontal accuracy in meters, if reported by the device.</summary>
    public double? AccuracyMeters { get; set; }

    public string Provider { get; set; } = "android";

    /// <summary>Idempotency key from the mobile app.</summary>
    public string? ProviderPointId { get; set; }

    public DateTime RecordedAtUtc { get; set; }
}
