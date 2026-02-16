using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data;

/// <summary>
/// Converts FollowUpStatus enum to/from string. Maps legacy values from DB to the current enum so existing rows load.
/// </summary>
public sealed class FollowUpStatusConverter : ValueConverter<FollowUpStatus, string>
{
    public FollowUpStatusConverter()
        : base(
            v => v.ToString(),
            s => MapFromString(s))
    {
    }

    private static FollowUpStatus MapFromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return FollowUpStatus.New;

        var normalized = value.Trim();
        return normalized switch
        {
            "Matured" => FollowUpStatus.Matured,
            "NotInterested" => FollowUpStatus.NotInterested,
            "NoResponse" => FollowUpStatus.NoResponse,
            "TripCancelled" => FollowUpStatus.TripCancelled,
            "TripConfirmed" => FollowUpStatus.TripConfirmed,
            "PackageSent" => FollowUpStatus.PackageSent,
            "Followup" => FollowUpStatus.Followup,
            "AlreadyBooked" => FollowUpStatus.AlreadyBooked,
            "New" => FollowUpStatus.New,
            // Legacy
            "InProgress" => FollowUpStatus.Followup,
            "Contacted" => FollowUpStatus.Followup,
            "PlanPostponed" => FollowUpStatus.Followup,
            "PlanCanceled" => FollowUpStatus.TripCancelled,
            "Confirmed" => FollowUpStatus.TripConfirmed,
            "CallbackScheduled" => FollowUpStatus.Followup,
            "FollowUp" => FollowUpStatus.Followup,
            _ => FollowUpStatus.New
        };
    }
}
