namespace TravelPathways.Api.Data.Entities;

/// <summary>
/// Reference data: area/location (e.g. Gulmarg, Srinagar). Not tenant-specific.
/// </summary>
public sealed class Area : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
