using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelPathways.Api.Common;
using TravelPathways.Api.Services.Inbound;

namespace TravelPathways.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/integrations/leads/inbound")]
public sealed class InboundLeadsWebhookController : ControllerBase
{
    private readonly ITenantLeadIntegrationResolver _resolver;
    private readonly IInboundLeadProcessor _processor;

    public InboundLeadsWebhookController(
        ITenantLeadIntegrationResolver resolver,
        IInboundLeadProcessor processor)
    {
        _resolver = resolver;
        _processor = processor;
    }

    public sealed class GenericInboundLeadRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string? ClientName { get; set; }
        public string? ClientEmail { get; set; }
        public string? ClientState { get; set; }
        public string? ClientCity { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        public string? LeadSource { get; set; }
        public string? ExternalId { get; set; }
    }

    [HttpPost("{inboundKey}")]
  [RequestSizeLimit(262_144)]
    public async Task<IActionResult> ReceiveLead(
        [FromRoute] string inboundKey,
        [FromBody] GenericInboundLeadRequest request,
        CancellationToken ct)
    {
        var resolved = await _resolver.ResolveByInboundKeyAsync(inboundKey, ct);
        if (resolved is null || !resolved.FeatureEnabled)
            return NotFound();

        if (!Enum.TryParse<LeadSource>(request.LeadSource?.Replace(" ", "", StringComparison.Ordinal) ?? "SocialMedia",
                ignoreCase: true, out var leadSource))
            leadSource = LeadSource.SocialMedia;

        var payload = new InboundLeadPayload
        {
            PhoneNumber = request.PhoneNumber,
            ClientName = request.ClientName ?? string.Empty,
            ClientEmail = request.ClientEmail,
            ClientState = request.ClientState,
            ClientCity = request.ClientCity,
            Address = request.Address ?? string.Empty,
            Notes = request.Notes,
            LeadSource = leadSource,
            ExternalId = request.ExternalId,
            Provider = InboundLeadProvider.Generic,
            RawPayload = JsonSerializer.Serialize(request)
        };

        var result = await _processor.ProcessAsync(resolved.TenantId, payload, ct);
        if (!result.Success)
            return BadRequest(new { success = false, message = result.ErrorMessage });

        return Ok(new
        {
            success = true,
            duplicate = result.Duplicate,
            leadId = result.LeadId?.ToString("D"),
            assignedToUserId = result.AssignedToUserId?.ToString("D")
        });
    }
}
