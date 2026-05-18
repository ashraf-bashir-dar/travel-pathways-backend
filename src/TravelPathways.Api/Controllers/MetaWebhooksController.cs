using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Data;
using TravelPathways.Api.Services.Inbound;

namespace TravelPathways.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/webhooks/meta")]
public sealed class MetaWebhooksController : ControllerBase
{
    private readonly ITenantLeadIntegrationResolver _resolver;
    private readonly IMetaLeadAdsService _meta;
    private readonly IInboundLeadProcessor _processor;
    private readonly AppDbContext _db;

    public MetaWebhooksController(
        ITenantLeadIntegrationResolver resolver,
        IMetaLeadAdsService meta,
        IInboundLeadProcessor processor,
        AppDbContext db)
    {
        _resolver = resolver;
        _meta = meta;
        _processor = processor;
        _db = db;
    }

    [HttpGet("{inboundKey}")]
    public IActionResult Verify(
        [FromRoute] string inboundKey,
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (!_meta.VerifyChallenge(mode, token, challenge, out var response))
            return Unauthorized();
        return Content(response ?? string.Empty, "text/plain");
    }

    [HttpPost("{inboundKey}")]
    [RequestSizeLimit(262_144)]
    public async Task<IActionResult> Receive(
        [FromRoute] string inboundKey,
        CancellationToken ct)
    {
        var resolved = await _resolver.ResolveByInboundKeyAsync(inboundKey, ct);
        if (resolved is null || !resolved.FeatureEnabled)
            return NotFound();

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(ct);
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        if (!_meta.VerifyWebhookSignature(Request.Headers["X-Hub-Signature-256"], bodyBytes))
            return Unauthorized();

        var integration = await _db.TenantLeadIntegrations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == resolved.Integration.Id, ct);
        if (integration is not null)
        {
            integration.MetaLastWebhookAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        var payloads = await _meta.ParseLeadgenWebhookAsync(resolved.TenantId, body, ct);
        var processed = 0;
        var duplicates = 0;

        foreach (var payload in payloads)
        {
            var result = await _processor.ProcessAsync(resolved.TenantId, payload, ct);
            if (result.Duplicate) duplicates++;
            else if (result.Success) processed++;
        }

        return Ok(new { success = true, processed, duplicates });
    }
}
