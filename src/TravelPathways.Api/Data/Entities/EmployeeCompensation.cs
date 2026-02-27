using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data.Entities;

/// <summary>Record of salary, incentive, or bonus paid to an employee. Admin can track total paid per employee.</summary>
public sealed class EmployeeCompensation : TenantEntityBase
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public CompensationType Type { get; set; }
    public decimal Amount { get; set; }
    /// <summary>E.g. "January 2025" or "Q1 2025".</summary>
    public string? PeriodLabel { get; set; }
    /// <summary>Date paid (or period end).</summary>
    public DateTime? PaidOn { get; set; }
    public string? Notes { get; set; }
}
