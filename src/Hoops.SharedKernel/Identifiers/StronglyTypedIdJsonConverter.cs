using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hoops.SharedKernel.Identifiers;

/// <summary>
/// Serialises any <see cref="IStronglyTypedId{TSelf}"/> as its bare Guid string, so ids appear on the
/// wire exactly as a plain Guid would. Applied per-type via <see cref="JsonConverterAttribute"/> and
/// registered globally so ids nested in collections/dictionaries convert too.
/// </summary>
public sealed class StronglyTypedIdJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsValueType && typeof(IStronglyTypedId).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(StronglyTypedIdJsonConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>Reads and writes a single strongly-typed id as a Guid string.</summary>
/// <typeparam name="TId">The id type.</typeparam>
public sealed class StronglyTypedIdJsonConverter<TId> : JsonConverter<TId>
    where TId : struct, IStronglyTypedId<TId>
{
    /// <inheritdoc />
    public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JsonException($"Expected a Guid string for {typeToConvert.Name}, got null or empty.");
        }

        return TId.FromGuid(Guid.Parse(raw));
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);

    /// <inheritdoc />
    public override TId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => TId.FromGuid(Guid.Parse(reader.GetString()!));

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value.ToString());
}
