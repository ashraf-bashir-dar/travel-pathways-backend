namespace TravelPathways.Api.Data.Entities;

/// <summary>
/// Reference data: state/region (e.g. Indian states). Not tenant-specific.
/// </summary>
public sealed class State : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int DisplayOrder { get; set; }

    public ICollection<City> Cities { get; set; } = new List<City>();
}
