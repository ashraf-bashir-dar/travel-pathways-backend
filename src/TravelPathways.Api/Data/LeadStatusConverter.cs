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
            "TripCancelled" => LeadStatus.TripCancelled,
            "TripConfirmed" => LeadStatus.TripConfirmed,
            "PackageSent" => LeadStatus.PackageSent,
            "Followup" => LeadStatus.Followup,
            "AlreadyBooked" => LeadStatus.AlreadyBooked,
            "New" => LeadStatus.New,
            // Legacy (old enum names)
            "FollowUp" => LeadStatus.Followup,
            "PlanPostponed" => LeadStatus.Followup,
            "PlanCanceled" => LeadStatus.TripCancelled,
            "Confirmed" => LeadStatus.TripConfirmed,
            "Contacted" => LeadStatus.Followup,
            "Qualified" => LeadStatus.Followup,
            "Converted" => LeadStatus.TripConfirmed,
            "Lost" => LeadStatus.NotInterested,
            _ => LeadStatus.New
        };
    }
}
