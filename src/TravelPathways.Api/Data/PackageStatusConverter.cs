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
            "Cancelled" => PackageStatus.Cancelled,
            "Confirmed" => PackageStatus.Confirmed,
            "PackageSent" => PackageStatus.PackageSent,
            "Followup" => PackageStatus.Followup,
            "AlreadyBooked" => PackageStatus.AlreadyBooked,
            // Legacy (stored with "Trip" prefix or old names)
            "TripCancelled" => PackageStatus.Cancelled,
            "TripConfirmed" => PackageStatus.Confirmed,
            "New" => PackageStatus.New,
            "FollowUp" => PackageStatus.Followup,
            "PlanPostponed" => PackageStatus.Followup,
            "PlanCanceled" => PackageStatus.Cancelled,
            "Draft" => PackageStatus.Followup,
            "Quoted" => PackageStatus.PackageSent,
            "InProgress" => PackageStatus.Followup,
            "Completed" => PackageStatus.Confirmed,
            _ => PackageStatus.New
        };
    }
}
