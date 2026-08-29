using Hoops.Modules.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Hoops.Infrastructure.Security;

/// <summary>
/// <see cref="IPasswordHasher"/> over ASP.NET Core Identity's PBKDF2 hasher. The generic marker type
/// is unused, so a throwaway sentinel stands in for it.
/// </summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private static readonly object Sentinel = new();
    private readonly PasswordHasher<object> _hasher = new();

    /// <inheritdoc />
    public string Hash(string password) => _hasher.HashPassword(Sentinel, password);

    /// <inheritdoc />
    public bool Verify(string hash, string password)
        => _hasher.VerifyHashedPassword(Sentinel, hash, password) != PasswordVerificationResult.Failed;
}
