using System.Text.Json;
using System.Text.Json.Serialization;

namespace TravelPathways.Api.Common;

/// <summary>Deserializes UserRole from string (case-insensitive) or number so API accepts "Reservation" and other roles reliably.</summary>
public sealed class UserRoleJsonConverter : JsonConverter<UserRole>
{
    public override UserRole Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var num))
        {
            if (Enum.IsDefined(typeof(UserRole), num))
                return (UserRole)num;
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (!string.IsNullOrWhiteSpace(s) && Enum.TryParse<UserRole>(s, ignoreCase: true, out var role))
                return role;
        }
        throw new JsonException($"Value could not be converted to {nameof(UserRole)}.");
    }

    public override void Write(Utf8JsonWriter writer, UserRole value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
