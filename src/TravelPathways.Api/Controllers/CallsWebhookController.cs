using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPathways.Api.Data;
using TravelPathways.Api.Data.Entities;
using TravelPathways.Api.MultiTenancy;

namespace TravelPathways.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/calls/webhooks")]
public sealed class CallsWebhookController : ControllerBase
{
    private const string WebhookSecretHeader = "X-TravelPathways-Webhook-Secret";
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;

    private sealed class UserPhone
    {
        public Guid Id { get; init; }
        public string? Phone { get; init; }
    }

    public CallsWebhookController(AppDbContext db, IConfiguration configuration, IWebHostEnvironment env)
    {
        _db = db;
        _configuration = configuration;
        _env = env;
    }

    [HttpPost("{provider}")]
    [RequestSizeLimit(262_144)]
    public async Task<IActionResult> ReceiveWebhook(
        [FromRoute] string provider,
        CancellationToken ct)
    {
        var tenantHeader = Request.Headers[TenantMiddleware.TenantHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantHeader) || !Guid.TryParse(tenantHeader, out var tenantId))
            return BadRequest(new { success = false, message = "Missing/invalid X-Tenant-Id header." });

        var receivedSecret = Request.Headers[WebhookSecretHeader].FirstOrDefault();
        var expectedSecret = _configuration["Calls:WebhookSecret"] ?? _configuration["Calls__WebhookSecret"];
        if (string.IsNullOrWhiteSpace(expectedSecret))
        {
            // Local/dev convenience: allow unsigned requests only in Development.
            if (!_env.IsDevelopment())
                return StatusCode(500, new { success = false, message = "Webhook secret is not configured on the server." });
        }
        else
        {
            if (!string.Equals(receivedSecret, expectedSecret, StringComparison.Ordinal))
                return Unauthorized(new { success = false, message = "Invalid webhook secret." });
        }

        provider = (provider ?? string.Empty).Trim();
        if (provider.Length == 0) provider = "generic";

        string rawPayload;
        string? eventType = null; // incoming/outgoing/missed (optional)
        string? fromNumber = null;
        string? toNumber = null;
        string? providerCallId = null;
        string? status = null;
        DateTime? startedAtUtc = null;
        DateTime? endedAtUtc = null;
        int? durationSeconds = null;

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            fromNumber = form["From"].FirstOrDefault() ?? form["from"].FirstOrDefault();
            toNumber = form["To"].FirstOrDefault() ?? form["to"].FirstOrDefault();

            providerCallId = form["CallSid"].FirstOrDefault()
                ?? form["callSid"].FirstOrDefault()
                ?? form["CallId"].FirstOrDefault()
                ?? form["callId"].FirstOrDefault();

            status = form["CallStatus"].FirstOrDefault()
                ?? form["callStatus"].FirstOrDefault()
                ?? form["Status"].FirstOrDefault();

            eventType = form["EventType"].FirstOrDefault()
                ?? form["eventType"].FirstOrDefault()
                ?? form["Direction"].FirstOrDefault()
                ?? form["direction"].FirstOrDefault();

            var durRaw = form["Duration"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(durRaw) && int.TryParse(durRaw, out var dur))
                durationSeconds = Math.Max(0, dur);

            // Twilio voice: CallStatus=no-answer is a "missed" call.
            if (string.IsNullOrWhiteSpace(eventType) && status is not null)
            {
                var s = status.Trim().ToLowerInvariant();
                if (s is "no-answer" or "noanswer" or "no_answer") eventType = "missed";
            }

            var dict = form.Keys.ToDictionary(k => k, k => form[k].ToString());
            rawPayload = JsonSerializer.Serialize(dict);
        }
        else
        {
            using var doc = await JsonDocument.ParseAsync(Request.Body, cancellationToken: ct);
            var root = doc.RootElement;
            rawPayload = root.GetRawText();

            eventType = GetStringOrNull(root, "eventType") ?? GetStringOrNull(root, "direction");
            fromNumber = GetStringOrNull(root, "from");
            toNumber = GetStringOrNull(root, "to");
            providerCallId = GetStringOrNull(root, "providerCallId")
                ?? GetStringOrNull(root, "callId")
                ?? GetStringOrNull(root, "callSid");
            status = GetStringOrNull(root, "status") ?? GetStringOrNull(root, "callStatus");

            startedAtUtc = GetDateTimeUtcOrNull(root, "startedAtUtc") ?? GetDateTimeUtcOrNull(root, "startAtUtc");
            endedAtUtc = GetDateTimeUtcOrNull(root, "endedAtUtc") ?? GetDateTimeUtcOrNull(root, "endAtUtc");

            var durVal = GetIntOrNull(root, "durationSeconds") ?? GetIntOrNull(root, "duration");
            durationSeconds = durVal.HasValue ? Math.Max(0, durVal.Value) : null;
        }

        // Load tenant users and match by phone number suffix.
        var users = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Select(u => new UserPhone { Id = u.Id, Phone = u.Phone })
            .ToListAsync(ct);

        var matchedUserIdFrom = MatchUserIdByPhoneSuffix(users, fromNumber);
        var matchedUserIdTo = MatchUserIdByPhoneSuffix(users, toNumber);

        var (direction, matchedUserId) = DetermineDirectionAndOwner(eventType, status, matchedUserIdFrom, matchedUserIdTo);

        // Idempotency: if providerCallId is provided, update the existing row.
        CallLog? existing = null;
        if (!string.IsNullOrWhiteSpace(providerCallId))
        {
            existing = await _db.CallLogs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(l =>
                    l.TenantId == tenantId &&
                    l.Provider == provider &&
                    l.ProviderCallId == providerCallId &&
                    !l.IsDeleted, ct);
        }

        if (existing is not null)
        {
            existing.Direction = direction;
            existing.Status = status;
            existing.FromNumber = fromNumber ?? existing.FromNumber;
            existing.ToNumber = toNumber ?? existing.ToNumber;
            existing.StartedAtUtc = startedAtUtc ?? existing.StartedAtUtc;
            existing.EndedAtUtc = endedAtUtc ?? existing.EndedAtUtc;
            existing.DurationSeconds = durationSeconds ?? existing.DurationSeconds;
            existing.ProviderCallId = providerCallId ?? existing.ProviderCallId;
            existing.UserId = matchedUserId;
            existing.RawPayload = rawPayload;

            await _db.SaveChangesAsync(ct);
        }
        else
        {
            var log = new CallLog
            {
                TenantId = tenantId,
                IsActive = true,
                UserId = matchedUserId,
                Direction = direction,
                Status = status,
                Provider = provider,
                ProviderCallId = providerCallId,
                FromNumber = fromNumber,
                ToNumber = toNumber,
                StartedAtUtc = startedAtUtc,
                EndedAtUtc = endedAtUtc,
                DurationSeconds = durationSeconds,
                RawPayload = string.IsNullOrWhiteSpace(rawPayload) ? "{}" : rawPayload
            };

            _db.CallLogs.Add(log);
            await _db.SaveChangesAsync(ct);
            existing = log;
        }

        return Ok(new { success = true, callLogId = existing?.Id.ToString("D") });
    }

    private static (string direction, Guid? ownerUserId) DetermineDirectionAndOwner(
        string? eventType,
        string? status,
        Guid? matchedUserIdFrom,
        Guid? matchedUserIdTo)
    {
        static string Normalize(string? v)
        {
            if (string.IsNullOrWhiteSpace(v)) return string.Empty;
            return v.Trim().ToLowerInvariant();
        }

        var normalizedEventType = Normalize(eventType);
        var normalizedStatus = Normalize(status);

        static bool IsMissedStatus(string? statusValue)
        {
            if (string.IsNullOrWhiteSpace(statusValue)) return false;
            var s = statusValue.Trim().ToLowerInvariant();
            return s is "no-answer" or "noanswer" or "no_answer";
        }

        if (normalizedEventType is "incoming" or "outgoing" or "missed")
        {
            return normalizedEventType switch
            {
                "incoming" => ("incoming", matchedUserIdTo),
                "outgoing" => ("outgoing", matchedUserIdFrom),
                "missed" => ("missed", matchedUserIdTo),
                _ => ("incoming", matchedUserIdTo)
            };
        }

        if (IsMissedStatus(normalizedStatus))
            return ("missed", matchedUserIdTo);

        // If we can match only one side, direction is unambiguous.
        if (matchedUserIdTo is not null && matchedUserIdFrom is null)
            return ("incoming", matchedUserIdTo);

        if (matchedUserIdFrom is not null && matchedUserIdTo is null)
            return ("outgoing", matchedUserIdFrom);

        // Ambiguous: default to incoming (receiver side) because missed calls are typically also incoming.
        if (matchedUserIdTo is not null)
            return ("incoming", matchedUserIdTo);

        return ("outgoing", matchedUserIdFrom);
    }

    private static Guid? MatchUserIdByPhoneSuffix(
        IEnumerable<UserPhone> users,
        string? phone)
    {
        var phoneDigits = NormalizeDigits(phone);
        if (string.IsNullOrWhiteSpace(phoneDigits)) return null;
        var targetSuffix = Last10Digits(phoneDigits);

        foreach (var u in users)
        {
            var userPhoneDigits = NormalizeDigits(u.Phone);
            if (string.IsNullOrWhiteSpace(userPhoneDigits)) continue;
            if (Last10Digits(userPhoneDigits) == targetSuffix)
                return u.Id;
        }

        return null;
    }

    private static string NormalizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch is >= '0' and <= '9') sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string Last10Digits(string digits)
    {
        if (digits.Length <= 10) return digits;
        return digits.Substring(digits.Length - 10, 10);
    }

    private static string? GetStringOrNull(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var v)) return null;
        if (v.ValueKind is JsonValueKind.String) return v.GetString();
        return v.ValueKind is JsonValueKind.Null ? null : v.ToString();
    }

    private static DateTime? GetDateTimeUtcOrNull(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var v)) return null;
        if (v.ValueKind is not (JsonValueKind.String or JsonValueKind.Number)) return null;

        if (v.ValueKind == JsonValueKind.String)
        {
            var s = v.GetString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParse(s, out var dt))
            {
                if (dt.Kind == DateTimeKind.Utc) return dt;
                return dt.ToUniversalTime();
            }
        }

        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var epoch))
        {
            // If provider sends epoch seconds, treat it as seconds.
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static int? GetIntOrNull(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var j)) return j;
        return null;
    }
}

