using System.Text.Json;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TravelPathways.Api.Data;

public sealed class JsonValueConverter<T> : ValueConverter<T, string>
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public JsonValueConverter()
        : base(
            v => JsonSerializer.Serialize(v, Options),
            v => string.IsNullOrWhiteSpace(v) ? GetEmptyDefault() : JsonSerializer.Deserialize<T>(v, Options)!)
    { }

    private static T GetEmptyDefault()
    {
        // For list/collection types, return empty instead of null to avoid null refs when DB has ""
        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>))
            return (T)Activator.CreateInstance(typeof(List<>).MakeGenericType(typeof(T).GetGenericArguments()[0]))!;
        return default!;
    }
}

public sealed class JsonValueComparer<TCollection, TElement> : Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<TCollection>
    where TCollection : class, IEnumerable<TElement>
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public JsonValueComparer()
        : base(
            (l, r) => JsonSerializer.Serialize(l, Options) == JsonSerializer.Serialize(r, Options),
            v => v == null ? 0 : JsonSerializer.Serialize(v, Options).GetHashCode(),
            v => JsonSerializer.Deserialize<TCollection>(JsonSerializer.Serialize(v, Options), Options)!)
    { }
}

