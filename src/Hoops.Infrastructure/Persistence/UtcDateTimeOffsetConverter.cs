using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hoops.Infrastructure.Persistence;

/// <summary>
/// Normalises every <see cref="DateTimeOffset"/> to UTC on the way to the database. Npgsql refuses a
/// non-zero offset for <c>timestamptz</c>, and a Nigerian client sends <c>+01:00</c> as a matter of
/// course — so without this, the first real fixture scheduled from Lagos would be a 500. The instant
/// is unchanged; only its representation is. Reads come back as UTC, per the convention.
/// </summary>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    /// <summary>Creates the converter.</summary>
    public UtcDateTimeOffsetConverter()
        : base(value => value.ToUniversalTime(), value => value)
    {
    }
}
