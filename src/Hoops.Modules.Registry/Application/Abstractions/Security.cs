using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Registry.Application.Abstractions;

/// <summary>
/// Hashes a National Identification Number to its blind-index HMAC (ADR-008). The plaintext NIN is
/// never persisted, logged, or placed in a URL — only this HMAC is stored. Implemented in
/// Infrastructure with a server-side pepper from the secrets manager.
/// </summary>
public interface INinHasher
{
    /// <summary>
    /// Returns HMAC-SHA256 over the normalised NIN, or null when <paramref name="nin"/> is null/blank
    /// (NIN is optional — every feature must work without it).
    /// </summary>
    byte[]? Hash(string? nin);
}

/// <summary>The outcome of a delegated NIN verification (§5A.2).</summary>
/// <param name="Verified">Whether the provider confirmed the NIN.</param>
/// <param name="Provider">The licensed provider's name.</param>
/// <param name="ProviderReference">The provider's reference for this check.</param>
/// <param name="VerifiedAt">When the check completed, UTC.</param>
/// <param name="DateOfBirth">Demographic DOB from the provider, if returned — used to cross-check, never to overwrite.</param>
public sealed record NinVerificationResult(
    bool Verified, string Provider, string? ProviderReference, DateTimeOffset VerifiedAt, DateOnly? DateOfBirth);

/// <summary>
/// Delegated NIN verification through a licensed channel (NIMC Act). A stubbed implementation stands
/// in until licensing is settled — do not build a direct integration assuming access.
/// </summary>
public interface INinVerificationProvider
{
    /// <summary>Verifies a NIN. The plaintext NIN is used transiently and never stored by the caller.</summary>
    Task<NinVerificationResult> VerifyAsync(string nin, CancellationToken ct = default);
}

/// <summary>A time-limited, presigned URL.</summary>
/// <param name="Url">The URL to use.</param>
/// <param name="ExpiresAt">When it stops working, UTC.</param>
public sealed record PresignedUrl(string Url, DateTimeOffset ExpiresAt);

/// <summary>An issued upload target: the private object key (server-side only) and a presigned PUT URL.</summary>
/// <param name="ObjectKey">The private bucket key. NEVER returned to a client.</param>
/// <param name="Upload">The presigned upload URL the client uses.</param>
public sealed record PhotoUploadTarget(string ObjectKey, PresignedUrl Upload);

/// <summary>
/// Player-photo storage in a private bucket (§5A.3). The table holds only the object key; the API
/// hands clients short-lived presigned URLs and never the raw key.
/// </summary>
public interface IPhotoStorage
{
    /// <summary>Issues an object key and a presigned upload URL for a player's photo.</summary>
    PhotoUploadTarget CreateUploadTarget(PlayerId playerId, string contentType);

    /// <summary>Issues a short-lived presigned read URL for an existing object key.</summary>
    PresignedUrl CreateReadUrl(string objectKey);
}
