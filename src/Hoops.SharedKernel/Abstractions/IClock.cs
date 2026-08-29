namespace Hoops.SharedKernel.Abstractions;

/// <summary>
/// The current time, behind an interface so application code never calls <c>DateTimeOffset.UtcNow</c>
/// directly and can be tested deterministically. (The projector's purity rule forbids ambient time
/// entirely; elsewhere, inject this.)
/// </summary>
public interface IClock
{
    /// <summary>The current instant in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
