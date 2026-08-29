using System.Security.Cryptography;
using Hoops.Modules.Identity.Application.Abstractions;
using Hoops.Modules.Identity.Contracts;
using Hoops.Modules.Identity.Domain;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Identity.Application;

/// <summary>Orchestrates registration, login, refresh-token rotation, logout, and current-user lookup.</summary>
public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IMembershipRepository _memberships;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenGenerator _accessTokens;
    private readonly AuthOptions _options;
    private readonly IClock _clock;

    /// <summary>Creates the service.</summary>
    public AuthService(
        IUserRepository users,
        IMembershipRepository memberships,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IAccessTokenGenerator accessTokens,
        AuthOptions options,
        IClock clock)
    {
        _users = users;
        _memberships = memberships;
        _refreshTokens = refreshTokens;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _accessTokens = accessTokens;
        _options = options;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();
        if (await _users.ExistsByEmailAsync(email, ct))
        {
            return Error.Conflict("EMAIL_ALREADY_REGISTERED", "An account with this email already exists.");
        }

        var user = User.Register(email, _passwordHasher.Hash(request.Password), request.FullName);
        _users.Add(user);

        var response = await IssueTokensAsync(user, Array.Empty<(OrganisationMembership, Organisation)>(), ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return response;
    }

    /// <inheritdoc />
    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(request.Email.Trim(), ct);
        if (user is null || !_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            // One generic message so the endpoint cannot be used to enumerate registered emails.
            return Error.Unauthorized("INVALID_CREDENTIALS", "Email or password is incorrect.");
        }

        user.MarkLoggedIn(_clock.UtcNow);
        var memberships = await _memberships.ListForUserAsync(user.Id, ct);

        var response = await IssueTokensAsync(user, memberships, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return response;
    }

    /// <inheritdoc />
    public async Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var presentedHash = HashToken(request.RefreshToken);
        var existing = await _refreshTokens.GetByHashAsync(presentedHash, ct);
        if (existing is null || !existing.IsActive(now))
        {
            return Error.Unauthorized("INVALID_REFRESH_TOKEN", "The refresh token is invalid or has expired.");
        }

        var user = await _users.GetByIdAsync(existing.UserId, ct);
        if (user is null)
        {
            return Error.Unauthorized("INVALID_REFRESH_TOKEN", "The refresh token is invalid or has expired.");
        }

        var memberships = await _memberships.ListForUserAsync(user.Id, ct);
        var (plaintext, replacement) = CreateRefreshToken(user.Id, now);
        existing.Revoke(now, replacement.Id);
        _refreshTokens.Add(replacement);

        var response = BuildResponse(user, memberships, plaintext);
        await _unitOfWork.SaveChangesAsync(ct);
        return response;
    }

    /// <inheritdoc />
    public async Task<Result> LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        var existing = await _refreshTokens.GetByHashAsync(HashToken(request.RefreshToken), ct);
        if (existing is not null && existing.RevokedAt is null)
        {
            existing.Revoke(_clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        // Logout is idempotent: an unknown or already-revoked token is still a successful logout.
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<CurrentUserDto>> GetCurrentUserAsync(UserId userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return Error.NotFound("USER_NOT_FOUND", "The user no longer exists.");
        }

        var memberships = await _memberships.ListForUserAsync(userId, ct);
        return new CurrentUserDto(
            user.Id,
            user.Email,
            user.FullName,
            user.IsSystemAdmin,
            memberships.Select(ToSummary).ToList());
    }

    private Task<AuthResponse> IssueTokensAsync(
        User user,
        IReadOnlyList<(OrganisationMembership Membership, Organisation Organisation)> memberships,
        CancellationToken ct)
    {
        var (plaintext, refreshToken) = CreateRefreshToken(user.Id, _clock.UtcNow);
        _refreshTokens.Add(refreshToken);
        return Task.FromResult(BuildResponse(user, memberships, plaintext));
    }

    private AuthResponse BuildResponse(
        User user,
        IReadOnlyList<(OrganisationMembership Membership, Organisation Organisation)> memberships,
        string refreshTokenPlaintext)
    {
        var claims = memberships
            .Select(m => new MembershipClaim(m.Membership.OrganisationId, m.Membership.Role))
            .ToList();

        var access = _accessTokens.Generate(user.Id, user.Email, user.IsSystemAdmin, claims);

        return new AuthResponse(
            access.Value,
            refreshTokenPlaintext,
            access.ExpiresAt,
            "Bearer",
            memberships.Select(ToSummary).ToList());
    }

    private (string Plaintext, RefreshToken Entity) CreateRefreshToken(UserId userId, DateTimeOffset now)
    {
        var plaintext = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var entity = RefreshToken.Issue(userId, HashToken(plaintext), now.Add(_options.RefreshTokenLifetime));
        return (plaintext, entity);
    }

    private static OrganisationMembershipSummary ToSummary(
        (OrganisationMembership Membership, Organisation Organisation) pair)
        => new(pair.Organisation.Id, pair.Organisation.Name, pair.Organisation.Slug, pair.Membership.Role.ToString());

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
