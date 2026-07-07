using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TravelPathways.Api.Data;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lookups")]
public sealed class LookupsController : ControllerBase
{
    // States/cities/areas are seeded once at startup and have no admin CRUD/write path anywhere in
    // the app, so they are safe to cache for a long time with no invalidation logic required.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public LookupsController(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public sealed class StateDto
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public string? Code { get; init; }
    }

    public sealed class CityDto
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string StateId { get; init; }
    }

    public sealed class AreaDto
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
    }

    /// <summary>Get all states (for dropdown).</summary>
    [HttpGet("states")]
    public async Task<ActionResult<IEnumerable<StateDto>>> GetStates(CancellationToken ct)
    {
        if (_cache.TryGetValue("lookups:states", out List<StateDto>? cached) && cached is not null)
            return Ok(cached);

        var list = await _db.States
            .AsNoTracking()
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .Select(s => new StateDto
            {
                Id = s.Id.ToString("D"),
                Name = s.Name,
                Code = s.Code
            })
            .ToListAsync(ct);
        _cache.Set("lookups:states", list, CacheDuration);
        return Ok(list);
    }

    /// <summary>Get cities for a state (for cascading dropdown).</summary>
    [HttpGet("states/{stateId:guid}/cities")]
    public async Task<ActionResult<IEnumerable<CityDto>>> GetCitiesByState(Guid stateId, CancellationToken ct)
    {
        var cacheKey = $"lookups:cities:{stateId:D}";
        if (_cache.TryGetValue(cacheKey, out List<CityDto>? cached) && cached is not null)
            return Ok(cached);

        var list = await _db.Cities
            .AsNoTracking()
            .Where(c => c.StateId == stateId)
            .OrderBy(c => c.Name)
            .Select(c => new CityDto
            {
                Id = c.Id.ToString("D"),
                Name = c.Name,
                StateId = c.StateId.ToString("D")
            })
            .ToListAsync(ct);
        _cache.Set(cacheKey, list, CacheDuration);
        return Ok(list);
    }

    /// <summary>Get all areas (for dropdown).</summary>
    [HttpGet("areas")]
    public async Task<ActionResult<IEnumerable<AreaDto>>> GetAreas(CancellationToken ct)
    {
        if (_cache.TryGetValue("lookups:areas", out List<AreaDto>? cached) && cached is not null)
            return Ok(cached);

        var list = await _db.Areas
            .AsNoTracking()
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.Name)
            .Select(a => new AreaDto { Id = a.Id.ToString("D"), Name = a.Name })
            .ToListAsync(ct);
        _cache.Set("lookups:areas", list, CacheDuration);
        return Ok(list);
    }
}
