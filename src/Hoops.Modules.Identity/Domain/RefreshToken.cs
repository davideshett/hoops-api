using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Identity.Domain;

/// <summary>
/// A rotating refresh token. Only the SHA-256 hash of the token is stored, never the token itself, so
/// a database leak cannot be replayed. Rotation on use links each token to its successor, giving a
/// detectable reuse trail.
/// </summary>
public sealed class RefreshToken : IAuditableEntity
{
    // EF materialisation constructor.
    private RefreshToken()
    {
        TokenHash = null!;
    }

    private RefreshToken(RefreshTokenId id, UserId userId, string tokenHash, DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    /// <summary>The permanent primary key.</summary>
    public RefreshTokenId Id { get; private set; }

    /// <summary>The user this token authenticates.</summary>
    public UserId UserId { get; private set; }

    /// <summary>SHA-256 hash of the opaque token value.</summary>
    public string TokenHash { get; private set; }

    /// <summary>Expiry, in UTC.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When the token was revoked, if it has been.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>The token that replaced this one on rotation, if any.</summary>
    public RefreshTokenId? ReplacedByTokenId { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>True when the token is neither expired nor revoked at <paramref name="now"/>.</summary>
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    /// <summary>Issues a new refresh token for <paramref name="userId"/>.</summary>
    public static RefreshToken Issue(UserId userId, string tokenHash, DateTimeOffset expiresAt)
        => new(RefreshTokenId.New(), userId, tokenHash, expiresAt);

    /// <summary>Revokes this token, optionally recording the token that superseded it.</summary>
    public void Revoke(DateTimeOffset at, RefreshTokenId? replacedBy = null)
    {
        RevokedAt ??= at;
        ReplacedByTokenId ??= replacedBy;
    }
}
