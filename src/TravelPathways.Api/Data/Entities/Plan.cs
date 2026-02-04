using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

public sealed class Plan : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public List<PlanPrice> Prices { get; set; } = [];
}

/// <summary>Price for a plan for a specific billing cycle. Currency: INR.</summary>
public sealed class PlanPrice : EntityBase
{
    public Guid PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public BillingCycle BillingCycle { get; set; }

    /// <summary>Base fee (website cost) in INR.</summary>
    public decimal BasePriceInr { get; set; }

    /// <summary>Price per user (seat) per billing cycle in INR.</summary>
    public decimal PricePerUserInr { get; set; }
}
