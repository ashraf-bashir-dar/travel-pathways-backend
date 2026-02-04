using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize(Policy = "SuperAdminOnly")]
[Route("api/admin/plans")]
public sealed class PlansController : ControllerBase
{
    private readonly AppDbContext _db;

    public PlansController(AppDbContext db)
    {
        _db = db;
    }

    public sealed class PlanDto
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public string? Description { get; init; }
        public required bool IsActive { get; init; }
        public required DateTime CreatedAt { get; init; }
        public List<PlanPriceDto> Prices { get; init; } = [];
    }

    public sealed class PlanPriceDto
    {
        public required string Id { get; init; }
        public required string BillingCycle { get; init; }
        public required decimal BasePriceInr { get; init; }
        public required decimal PricePerUserInr { get; init; }
    }

    public sealed class CreatePlanRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public List<PlanPriceItemDto> Prices { get; set; } = [];
    }

    public sealed class PlanPriceItemDto
    {
        public BillingCycle BillingCycle { get; set; }
        public decimal BasePriceInr { get; set; }
        public decimal PricePerUserInr { get; set; }
    }

    public sealed class UpdatePlanRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public List<PlanPriceItemDto> Prices { get; set; } = [];
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PlanDto>>>> GetPlans(CancellationToken ct)
    {
        var plans = await _db.Plans.AsNoTracking()
            .Include(p => p.Prices)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
        return ApiResponse<List<PlanDto>>.Ok(plans.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PlanDto>>> GetPlanById([FromRoute] Guid id, CancellationToken ct)
    {
        var plan = await _db.Plans.AsNoTracking()
            .Include(p => p.Prices)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return NotFound(ApiResponse<PlanDto>.Fail("Plan not found"));
        return ApiResponse<PlanDto>.Ok(ToDto(plan));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PlanDto>>> CreatePlan([FromBody] CreatePlanRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(ApiResponse<PlanDto>.Fail("Name is required."));

        var plan = new Plan
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive
        };
        foreach (var p in request.Prices ?? [])
        {
            plan.Prices.Add(new PlanPrice
            {
                BillingCycle = p.BillingCycle,
                BasePriceInr = p.BasePriceInr,
                PricePerUserInr = p.PricePerUserInr
            });
        }
        _db.Plans.Add(plan);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetPlanById), new { id = plan.Id }, ApiResponse<PlanDto>.Ok(ToDto(plan)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PlanDto>>> UpdatePlan([FromRoute] Guid id, [FromBody] UpdatePlanRequestDto request, CancellationToken ct)
    {
        var plan = await _db.Plans.Include(p => p.Prices).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return NotFound(ApiResponse<PlanDto>.Fail("Plan not found"));

        plan.Name = request.Name.Trim();
        plan.Description = request.Description?.Trim();
        plan.IsActive = request.IsActive;

        plan.Prices.Clear();
        foreach (var p in request.Prices ?? [])
        {
            plan.Prices.Add(new PlanPrice
            {
                PlanId = plan.Id,
                BillingCycle = p.BillingCycle,
                BasePriceInr = p.BasePriceInr,
                PricePerUserInr = p.PricePerUserInr
            });
        }
        await _db.SaveChangesAsync(ct);

        return ApiResponse<PlanDto>.Ok(ToDto(plan));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePlan([FromRoute] Guid id, CancellationToken ct)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return NotFound(ApiResponse<object>.Fail("Plan not found"));

        var inUse = await _db.Tenants.AnyAsync(t => t.PlanId == id, ct);
        if (inUse) return BadRequest(ApiResponse<object>.Fail("Plan is in use by one or more tenants. Change their plan first."));

        _db.Plans.Remove(plan);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(new { });
    }

    private static PlanDto ToDto(Plan p) =>
        new()
        {
            Id = p.Id.ToString("D"),
            Name = p.Name,
            Description = p.Description,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            Prices = p.Prices.Select(pr => new PlanPriceDto
            {
                Id = pr.Id.ToString("D"),
                BillingCycle = pr.BillingCycle.ToString(),
                BasePriceInr = pr.BasePriceInr,
                PricePerUserInr = pr.PricePerUserInr
            }).ToList()
        };
}
