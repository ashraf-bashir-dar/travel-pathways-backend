using System.ComponentModel.DataAnnotations;

namespace TravelPathways.Api.Data.Entities;

/// <summary>
/// Phone call event captured via a calling provider webhook (incoming/outgoing/missed).
/// For multi-tenant apps, TenantId is used for isolation and filtering.
/// </summary>
public sealed class CallLog : TenantEntityBase
{
    /// <summary>
    /// The app user whose phone number was involved (e.g., recipient for incoming, caller for outgoing).
    /// </summary>
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    /// <summary>
    /// "incoming" | "outgoing" | "missed"
    /// </summary>
    [Required]
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// Provider status (e.g. answered/completed/no-answer/busy/failed).
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Calling provider name (e.g. "twilio", "generic").
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Provider call identifier (e.g. Twilio CallSid) used for idempotency/debugging.
    /// </summary>
    public string? ProviderCallId { get; set; }

    /// <summary>Raw phone number as sent by the provider (may include formatting).</summary>
    public string? FromNumber { get; set; }

    /// <summary>Raw phone number as sent by the provider (may include formatting).</summary>
    public string? ToNumber { get; set; }

    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }

    /// <summary>Call duration in seconds (if provided by provider).</summary>
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// Serialized raw webhook payload for debugging/auditing.
    /// </summary>
    public string RawPayload { get; set; } = "{}";
}

