namespace Hoops.SharedKernel.Abstractions;

/// <summary>
/// The authenticated caller for the current request, resolved from JWT claims in middleware.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The user id, or null when the request is anonymous.</summary>
    Guid? UserId { get; }

    /// <summary>True when the request carries a valid authenticated identity.</summary>
    bool IsAuthenticated { get; }

    /// <summary>True when the caller is a platform administrator (ADR-003 privileged operations).</summary>
    bool IsSystemAdmin { get; }
}
