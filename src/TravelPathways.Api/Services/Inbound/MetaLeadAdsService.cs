using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TravelPathways.Api.Common;
using TravelPathways.Api.Data;

namespace TravelPathways.Api.Services.Inbound;

public sealed class MetaOptions
{
    public string AppSecret { get; set; } = string.Empty;
    public string VerifyToken { get; set; } = string.Empty;
    public string GraphApiVersion { get; set; } = "v21.0";
}

public interface IMetaLeadAdsService
{
    bool VerifyWebhookSignature(string? signatureHeader, byte[] body);
    bool VerifyChallenge(string? mode, string? token, string? challenge, out string? responseChallenge);
    Task<IReadOnlyList<InboundLeadPayload>> ParseLeadgenWebhookAsync(
        Guid tenantId,
        string jsonBody,
        CancellationToken ct);
}

public sealed class MetaLeadAdsService : IMetaLeadAdsService
{
    private readonly AppDbContext _db;
    private readonly IPasswordEncryption _encryption;
    private readonly MetaOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public MetaLeadAdsService(
        AppDbContext db,
        IPasswordEncryption encryption,
        IOptions<MetaOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _encryption = encryption;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    public bool VerifyWebhookSignature(string? signatureHeader, byte[] body)
    {
        if (string.IsNullOrWhiteSpace(_options.AppSecret) || string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var expectedHex = signatureHeader[prefix.Length..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.AppSecret));
        var hash = hmac.ComputeHash(body);
        var actualHex = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actualHex),
            Encoding.UTF8.GetBytes(expectedHex.ToLowerInvariant()));
    }

    public bool VerifyChallenge(string? mode, string? token, string? challenge, out string? responseChallenge)
    {
        responseChallenge = null;
        if (!string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(_options.VerifyToken) || token != _options.VerifyToken)
            return false;
        responseChallenge = challenge;
        return !string.IsNullOrEmpty(challenge);
    }

    public async Task<IReadOnlyList<InboundLeadPayload>> ParseLeadgenWebhookAsync(
        Guid tenantId,
        string jsonBody,
        CancellationToken ct)
    {
        var results = new List<InboundLeadPayload>();

        var integration = await _db.TenantLeadIntegrations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && !i.IsDeleted, ct);

        if (integration is null || string.IsNullOrWhiteSpace(integration.MetaPageAccessTokenEncrypted))
            return results;

        var token = _encryption.Decrypt(integration.MetaPageAccessTokenEncrypted);
        if (string.IsNullOrWhiteSpace(token))
            return results;

        using var doc = JsonDocument.Parse(jsonBody);
        if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("field", out var field) ||
                    field.GetString() != "leadgen")
                    continue;

                if (!change.TryGetProperty("value", out var value))
                    continue;

                var leadgenId = value.TryGetProperty("leadgen_id", out var lid)
                    ? lid.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(leadgenId))
                    continue;

                var pageId = value.TryGetProperty("page_id", out var pid) ? pid.GetString() : null;
                if (!string.IsNullOrWhiteSpace(integration.MetaPageId) &&
                    !string.IsNullOrWhiteSpace(pageId) &&
                    integration.MetaPageId != pageId)
                    continue;

                var payload = await FetchLeadPayloadAsync(leadgenId, token, jsonBody, ct);
                if (payload is not null)
                    results.Add(payload);
            }
        }

        return results;
    }

    private async Task<InboundLeadPayload?> FetchLeadPayloadAsync(
        string leadgenId,
        string pageAccessToken,
        string rawWebhook,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var version = string.IsNullOrWhiteSpace(_options.GraphApiVersion) ? "v21.0" : _options.GraphApiVersion;
        var url =
            $"https://graph.facebook.com/{version}/{leadgenId}?access_token={Uri.EscapeDataString(pageAccessToken)}";

        using var response = await client.GetAsync(url, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var fieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("field_data", out var fields) && fields.ValueKind == JsonValueKind.Array)
        {
            foreach (var field in fields.EnumerateArray())
            {
                var fieldName = field.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(fieldName))
                    continue;
                if (field.TryGetProperty("values", out var values) && values.GetArrayLength() > 0)
                    fieldMap[fieldName] = values[0].GetString() ?? string.Empty;
            }
        }

        var phone = GetField(fieldMap, "phone_number", "phone", "mobile", "contact_number");
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var name = GetField(fieldMap, "full_name", "first_name", "name", "client_name");
        var email = GetField(fieldMap, "email", "email_address");
        var city = GetField(fieldMap, "city");
        var state = GetField(fieldMap, "state", "province");
        var notes = BuildNotesFromExtraFields(fieldMap);

        return new InboundLeadPayload
        {
            PhoneNumber = phone,
            ClientName = string.IsNullOrWhiteSpace(name) ? "Meta Lead" : name,
            ClientEmail = string.IsNullOrWhiteSpace(email) ? null : email,
            ClientCity = string.IsNullOrWhiteSpace(city) ? null : city,
            ClientState = string.IsNullOrWhiteSpace(state) ? null : state,
            LeadSource = LeadSource.SocialMedia,
            Notes = notes,
            ExternalId = leadgenId,
            Provider = InboundLeadProvider.Meta,
            RawPayload = rawWebhook.Length > 4000 ? rawWebhook[..4000] : rawWebhook
        };
    }

    private static string GetField(Dictionary<string, string> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (map.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return string.Empty;
    }

    private static string? BuildNotesFromExtraFields(Dictionary<string, string> map)
    {
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "full_name", "first_name", "name", "phone_number", "phone", "email", "city", "state"
        };
        var parts = map
            .Where(kv => !skip.Contains(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{kv.Key}: {kv.Value}")
            .ToList();
        return parts.Count == 0 ? "Imported from Meta Lead Ads" : string.Join("; ", parts);
    }
}
