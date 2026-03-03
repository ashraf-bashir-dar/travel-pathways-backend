using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data;

/// <summary>
/// Converts LeadStatus enum to/from string. Maps legacy status values from DB to the current enum so existing rows load.
/// </summary>
public sealed class LeadStatusConverter : ValueConverter<LeadStatus, string>
{
    public LeadStatusConverter()
        : base(
            v => v.ToString(),
            s => MapFromString(s))
    {
    }

    private static LeadStatus MapFromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return LeadStatus.New;

        var normalized = value.Trim();
        return normalized switch
        {
            "Matured" => LeadStatus.Matured,
            "NotInterested" => LeadStatus.NotInterested,
            "NoResponse" => LeadStatus.NoResponse,
            "Cancelled" => LeadStatus.Cancelled,
            "Confirmed" => LeadStatus.Confirmed,
            "PackageSent" => LeadStatus.PackageSent,
            "Followup" => LeadStatus.Followup,
            "AlreadyBooked" => LeadStatus.AlreadyBooked,
            "New" => LeadStatus.New,
            // Legacy (stored with "Trip" prefix or old names)
            "TripCancelled" => LeadStatus.Cancelled,
            "TripConfirmed" => LeadStatus.Confirmed,
            "FollowUp" => LeadStatus.Followup,
            "PlanPostponed" => LeadStatus.Followup,
            "PlanCanceled" => LeadStatus.Cancelled,
            "Contacted" => LeadStatus.Followup,
            "Qualified" => LeadStatus.Followup,
            "Converted" => LeadStatus.Confirmed,
            "Lost" => LeadStatus.NotInterested,
            _ => LeadStatus.New
        };
    }
}
