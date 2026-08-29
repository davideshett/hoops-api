using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Identity.Contracts;

/// <summary>
/// Authentication use cases: registration, login, refresh-token rotation, logout, and current-user
/// lookup. The public surface other modules and the API host depend on.
/// </summary>
public interface IAuthService
{
    /// <summary>Registers a new user and returns an initial token pair.</summary>
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Verifies credentials and returns a token pair on success.</summary>
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>Rotates a valid refresh token, returning a fresh token pair.</summary>
    Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct = default);

    /// <summary>Revokes the supplied refresh token.</summary>
    Task<Result> LogoutAsync(LogoutRequest request, CancellationToken ct = default);

    /// <summary>Returns the current user's profile and organisation memberships.</summary>
    Task<Result<CurrentUserDto>> GetCurrentUserAsync(UserId userId, CancellationToken ct = default);
}
