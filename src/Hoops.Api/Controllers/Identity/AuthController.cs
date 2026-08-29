using Hoops.Api.Auth;
using Hoops.Modules.Identity.Contracts;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.Api.Controllers.Identity;

/// <summary>Registration, login, refresh-token rotation, logout, and current-user lookup.</summary>
[Route("api/v1/[controller]")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICurrentUser _currentUser;

    /// <summary>Creates the controller.</summary>
    public AuthController(IAuthService auth, ICurrentUser currentUser)
    {
        _auth = auth;
        _currentUser = currentUser;
    }

    /// <summary>Registers a new platform user and returns an initial token pair.</summary>
    /// <param name="request">The registration payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">The account was created; the body carries the token pair.</response>
    /// <response code="400">The request payload was invalid.</response>
    /// <response code="409">An account with this email already exists.</response>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
        => Created(await _auth.RegisterAsync(request, ct));

    /// <summary>Verifies credentials and returns a token pair.</summary>
    /// <param name="request">The login payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Authenticated; the body carries the token pair.</response>
    /// <response code="400">The request payload was invalid.</response>
    /// <response code="401">Email or password is incorrect.</response>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
        => Ok(await _auth.LoginAsync(request, ct));

    /// <summary>Exchanges a valid refresh token for a fresh token pair, rotating the refresh token.</summary>
    /// <param name="request">The refresh payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">A fresh token pair was issued.</response>
    /// <response code="400">The request payload was invalid.</response>
    /// <response code="401">The refresh token is invalid or expired.</response>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
        => Ok(await _auth.RefreshAsync(request, ct));

    /// <summary>Revokes a refresh token. Idempotent.</summary>
    /// <param name="request">The logout payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="204">The token was revoked (or was already unknown/revoked).</response>
    /// <response code="401">The caller is not authenticated.</response>
    [Authorize(Policy = AuthPolicies.Authenticated)]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
        => NoContent(await _auth.LogoutAsync(request, ct));

    /// <summary>Returns the authenticated user's profile and organisation memberships.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The current user.</response>
    /// <response code="401">The caller is not authenticated.</response>
    /// <response code="404">The user no longer exists.</response>
    [Authorize(Policy = AuthPolicies.Authenticated)]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct)
        => Ok(await _auth.GetCurrentUserAsync(UserId.FromGuid(_currentUser.UserId!.Value), ct));
}
