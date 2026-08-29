using Hoops.SharedKernel.Abstractions;

namespace Hoops.Infrastructure.Time;

/// <summary>The real system clock. Registered as the default <see cref="IClock"/>.</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
