using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Data.Entities;

namespace TravelPathways.Api.Data;

public static class SeedAreas
{
    private static readonly (string Name, int Order)[] DefaultAreas =
    {
        ("Gulmarg", 1),
        ("Sonmarg", 2),
        ("Pahalgam", 3),
        ("Srinagar", 4),
        ("Jammu", 5),
        ("Katra", 6)
    };

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Areas.AnyAsync(ct))
            return;

        foreach (var (name, order) in DefaultAreas)
        {
            db.Areas.Add(new Area { Name = name, DisplayOrder = order });
        }

        await db.SaveChangesAsync(ct);
    }
}
