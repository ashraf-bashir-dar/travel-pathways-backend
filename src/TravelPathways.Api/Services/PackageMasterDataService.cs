using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;

namespace TravelPathways.Api.Services;

public interface IPackageMasterDataService
{
    Task EnsureSeededAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<PackageInclusionMaster>> GetInclusionsAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<PackageLocationMaster>> GetLocationsAsync(Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<string> Inclusions, IReadOnlyList<string> Exclusions)> ResolveInclusionLabelsAsync(
        Guid tenantId,
        IEnumerable<string>? inclusionIds,
        IEnumerable<string>? exclusionIds,
        string? language,
        CancellationToken ct = default);
}

public sealed class PackageMasterDataService : IPackageMasterDataService
{
    private readonly AppDbContext _db;

    public PackageMasterDataService(AppDbContext db) => _db = db;

    public async Task EnsureSeededAsync(Guid tenantId, CancellationToken ct = default)
    {
        await PackageMasterSchemaBootstrap.EnsureAsync(_db, ct);

        var hasInclusions = await _db.PackageInclusionMasters
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted, ct);

        if (!hasInclusions)
        {
            var order = 0;
            foreach (var (id, label) in InclusionOptions.All)
            {
                _db.PackageInclusionMasters.Add(new PackageInclusionMaster
                {
                    TenantId = tenantId,
                    Code = id,
                    Label = label,
                    SortOrder = order++,
                    IsInclusion = true
                });
            }
        }

        var hasLocations = await _db.PackageLocationMasters
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted, ct);

        if (!hasLocations)
        {
            var defaults = new (string Name, bool Pickup, bool Drop)[]
            {
                ("Srinagar", true, true),
                ("Jammu", true, true)
            };

            for (var i = 0; i < defaults.Length; i++)
            {
                var d = defaults[i];
                _db.PackageLocationMasters.Add(new PackageLocationMaster
                {
                    TenantId = tenantId,
                    Name = d.Name,
                    AllowPickup = d.Pickup,
                    AllowDrop = d.Drop,
                    SortOrder = i
                });
            }
        }

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PackageInclusionMaster>> GetInclusionsAsync(Guid tenantId, CancellationToken ct = default)
    {
        await EnsureSeededAsync(tenantId, ct);
        return await _db.PackageInclusionMasters.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PackageLocationMaster>> GetLocationsAsync(Guid tenantId, CancellationToken ct = default)
    {
        await EnsureSeededAsync(tenantId, ct);
        return await _db.PackageLocationMasters.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<string> Inclusions, IReadOnlyList<string> Exclusions)> ResolveInclusionLabelsAsync(
        Guid tenantId,
        IEnumerable<string>? inclusionIds,
        IEnumerable<string>? exclusionIds,
        string? language,
        CancellationToken ct = default)
    {
        var inclusionSelected = new HashSet<string>(inclusionIds ?? [], StringComparer.OrdinalIgnoreCase);
        var exclusionSelected = new HashSet<string>(exclusionIds ?? [], StringComparer.OrdinalIgnoreCase);
        var rows = await GetInclusionsAsync(tenantId, ct);

        if (rows.Count > 0)
        {
            var inclusionRows = rows.Where(x => x.IsInclusion).ToList();
            var exclusionRows = rows.Where(x => !x.IsInclusion).ToList();
            var hasExclusionTypeRows = exclusionRows.Count > 0;
            var useLegacy = !hasExclusionTypeRows && exclusionSelected.Count == 0;

            if (useLegacy)
            {
                var inclusions = inclusionRows
                    .Where(x => inclusionSelected.Contains(x.Code))
                    .Select(x => ResolveInclusionLabel(x.Code, x.Label, language))
                    .ToList();

                var exclusions = inclusionRows
                    .Where(x => !inclusionSelected.Contains(x.Code))
                    .Select(x => ResolveInclusionLabel(x.Code, x.Label, language))
                    .ToList();

                return (inclusions, exclusions);
            }

            return (
                inclusionRows
                    .Where(x => inclusionSelected.Contains(x.Code))
                    .Select(x => ResolveInclusionLabel(x.Code, x.Label, language))
                    .ToList(),
                exclusionRows
                    .Where(x => exclusionSelected.Contains(x.Code))
                    .Select(x => ResolveInclusionLabel(x.Code, x.Label, language))
                    .ToList()
            );
        }

        return (
            InclusionOptions.GetInclusionLabels(inclusionIds ?? [], language),
            InclusionOptions.GetExclusionLabels(inclusionIds ?? [], language)
        );
    }

    private static string ResolveInclusionLabel(string code, string label, string? language)
    {
        var translated = Localization.InclusionTranslations.GetLabel(code, language);
        return translated ?? label;
    }
}
