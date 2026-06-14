using System.Text.Json;
using System.Text.Json.Serialization;

namespace TravelPathways.Api.Common;

/// <summary>Accepts current and legacy reference source enum names from the UI.</summary>
public sealed class SalesReferenceSourceTypeJsonConverter : JsonConverter<SalesReferenceSourceType?>
{
    public override SalesReferenceSourceType? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Reference source type must be a string.");

        return Parse(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, SalesReferenceSourceType? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString());
    }

    public static SalesReferenceSourceType? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim() switch
        {
            nameof(SalesReferenceSourceType.OfficeReference)
                or "InsideReference"
                or "TravelPathwaysReference" => SalesReferenceSourceType.OfficeReference,
            nameof(SalesReferenceSourceType.PersonalReference)
                or "OutsideReference" => SalesReferenceSourceType.PersonalReference,
            _ => Enum.TryParse<SalesReferenceSourceType>(value, ignoreCase: true, out var parsed)
                ? parsed
                : null
        };
    }
}
