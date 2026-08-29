namespace Hoops.SharedKernel.Abstractions;

/// <summary>
/// An in-process notification that something meaningful happened in the domain. Handlers run within
/// the same process (ADR-004); nothing in the write path may assume a single consumer (§12.2).
/// </summary>
public interface IDomainEvent
{
    /// <summary>When the event occurred, in UTC.</summary>
    DateTimeOffset OccurredAt { get; }
}
