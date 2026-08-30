using System.Security.Cryptography;
using System.Text;
using Hoops.Modules.Registry.Application.Abstractions;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Microsoft.Extensions.Options;

namespace Hoops.Infrastructure.Security;

/// <summary>Registry configuration, bound from the <c>Registry</c> section.</summary>
public sealed class RegistryOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Registry";

    /// <summary>
    /// Server-side pepper for the NIN HMAC. MUST come from a secrets manager / environment variable in
    /// production — never committed to appsettings or source (ADR-008, §13). Rotating it requires
    /// re-HMAC from re-collected NINs, so treat rotation as a planned migration.
    /// </summary>
    public string NinPepper { get; init; } = string.Empty;
}

/// <summary>
/// <see cref="INinHasher"/> using HMAC-SHA256 over the normalised NIN with a server-side pepper. The
/// plaintext is used only to compute the digest and is never retained.
/// </summary>
public sealed class HmacNinHasher : INinHasher
{
    private readonly byte[] _pepper;

    /// <summary>Creates the hasher.</summary>
    public HmacNinHasher(IOptions<RegistryOptions> options)
    {
        var pepper = options.Value.NinPepper;
        if (string.IsNullOrWhiteSpace(pepper))
        {
            throw new InvalidOperationException("Registry:NinPepper is not configured.");
        }

        _pepper = Encoding.UTF8.GetBytes(pepper);
    }

    /// <inheritdoc />
    public byte[]? Hash(string? nin)
    {
        var normalised = Normalise(nin);
        if (normalised is null)
        {
            return null;
        }

        return HMACSHA256.HashData(_pepper, Encoding.UTF8.GetBytes(normalised));
    }

    // Keep only digits (Nigerian NIN is 11 digits); null if nothing usable remains.
    private static string? Normalise(string? nin)
    {
        if (string.IsNullOrWhiteSpace(nin))
        {
            return null;
        }

        var digits = new string(nin.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }
}

/// <summary>
/// Stubbed <see cref="INinVerificationProvider"/> — confirms any NIN offline. Replace with a licensed
/// channel before handling real data (§5A.2).
/// </summary>
public sealed class StubNinVerificationProvider : INinVerificationProvider
{
    private readonly IClock _clock;

    /// <summary>Creates the stub.</summary>
    public StubNinVerificationProvider(IClock clock) => _clock = clock;

    /// <inheritdoc />
    public Task<NinVerificationResult> VerifyAsync(string nin, CancellationToken ct = default)
        => Task.FromResult(new NinVerificationResult(
            Verified: true,
            Provider: "STUB",
            ProviderReference: $"stub-{Guid.CreateVersion7()}",
            VerifiedAt: _clock.UtcNow,
            DateOfBirth: null));
}

/// <summary>
/// Stubbed <see cref="IPhotoStorage"/> — issues opaque object keys and presigned-style URLs with an
/// expiry, without a real bucket. The object key never leaves the server. Swap for an S3/MinIO client
/// in hardening; the API contract (presigned, expiring, key-hidden) stays the same.
/// </summary>
public sealed class StubPhotoStorage : IPhotoStorage
{
    private const int UploadTtlMinutes = 15;
    private const int ReadTtlMinutes = 15;
    private readonly IClock _clock;

    /// <summary>Creates the stub.</summary>
    public StubPhotoStorage(IClock clock) => _clock = clock;

    /// <inheritdoc />
    public PhotoUploadTarget CreateUploadTarget(PlayerId playerId, string contentType)
    {
        var objectKey = $"players/{playerId.Value:N}/{Guid.CreateVersion7():N}";
        var url = Sign(objectKey, "PUT", UploadTtlMinutes, out var expires);
        return new PhotoUploadTarget(objectKey, new PresignedUrl(url, expires));
    }

    /// <inheritdoc />
    public PresignedUrl CreateReadUrl(string objectKey)
    {
        var url = Sign(objectKey, "GET", ReadTtlMinutes, out var expires);
        return new PresignedUrl(url, expires);
    }

    private string Sign(string objectKey, string verb, int ttlMinutes, out DateTimeOffset expiresAt)
    {
        expiresAt = _clock.UtcNow.AddMinutes(ttlMinutes);
        var sig = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var exp = expiresAt.ToUnixTimeSeconds();
        // A presigned-style URL: opaque, time-limited, and it does not reveal the private key structure
        // to the caller beyond what a real presigned S3 URL would.
        return $"https://photos.local/{objectKey}?verb={verb}&expires={exp}&sig={sig}";
    }
}
