using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TravelPathways.Api.Common;

namespace TravelPathways.Api.Data;

/// <summary>
/// Converts PackageStatus enum to/from string. Maps legacy status values from DB to the current enum so existing rows load.
/// </summary>
public sealed class PackageStatusConverter : ValueConverter<PackageStatus, string>
{
    public PackageStatusConverter()
        : base(
            v => v.ToString(),
            s => MapFromString(s))
    {
    }

    private static PackageStatus MapFromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return PackageStatus.New;

        var normalized = value.Trim();
        return normalized switch
        {
            "Matured" => PackageStatus.Matured,
            "NotInterested" => PackageStatus.NotInterested,
            "NoResponse" => PackageStatus.NoResponse,
            "TripCancelled" => PackageStatus.TripCancelled,
            "TripConfirmed" => PackageStatus.TripConfirmed,
            "PackageSent" => PackageStatus.PackageSent,
            "Followup" => PackageStatus.Followup,
            "AlreadyBooked" => PackageStatus.AlreadyBooked,
            // Legacy
            "New" => PackageStatus.New,
            "FollowUp" => PackageStatus.Followup,
            "PlanPostponed" => PackageStatus.Followup,
            "PlanCanceled" => PackageStatus.TripCancelled,
            "Confirmed" => PackageStatus.TripConfirmed,
            "Draft" => PackageStatus.Followup,
            "Quoted" => PackageStatus.PackageSent,
            "InProgress" => PackageStatus.Followup,
            "Completed" => PackageStatus.TripConfirmed,
            "Cancelled" => PackageStatus.TripCancelled,
            _ => PackageStatus.New
        };
    }
}
