namespace Hoops.Api.Auth;

/// <summary>
/// JWT bearer configuration, bound from the <c>Jwt</c> configuration section. The signing key must be
/// supplied out of band in production (environment variable or secrets manager), never committed.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// The obvious placeholder Development substitutes when no secret is configured. It is named here
    /// rather than left in <c>appsettings.json</c> for one reason: a value in that file binds in EVERY
    /// environment, so a deploy that forgot to set <c>Jwt__SigningKey</c> would boot and sign tokens
    /// with a key published in this repository. Held here it is instead a value the startup guard
    /// REJECTS outside Development, and a missing key fails fast as it should.
    /// </summary>
    public const string DevelopmentPlaceholderKey = "dev-only-insecure-signing-key-change-me-0123456789abcdef";

    /// <summary>Token issuer.</summary>
    public string Issuer { get; init; } = "hoops-api";

    /// <summary>Token audience.</summary>
    public string Audience { get; init; } = "hoops-clients";

    /// <summary>Symmetric signing key (HMAC-SHA256). At least 32 bytes.</summary>
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Access-token lifetime in minutes.</summary>
    public int AccessTokenMinutes { get; init; } = 60;

    /// <summary>Refresh-token lifetime in days.</summary>
    public int RefreshTokenDays { get; init; } = 14;
}
