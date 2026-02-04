namespace TravelPathways.Api.Data.Entities;

/// <summary>
/// Reference data: city belonging to a state. Not tenant-specific.
/// </summary>
public sealed class City : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public Guid StateId { get; set; }
    public State State { get; set; } = null!;
}
