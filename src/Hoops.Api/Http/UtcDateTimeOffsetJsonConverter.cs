using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hoops.Api.Http;

/// <summary>
/// Writes every <see cref="DateTimeOffset"/> in a response as UTC. The database stores UTC, but an
/// entity returned straight from a create still carries the offset the caller sent, so without this
/// the same fixture would read <c>+01:00</c> from the POST and <c>Z</c> from the GET.
/// </summary>
public sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    /// <inheritdoc />
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTimeOffset();

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToUniversalTime());
}
