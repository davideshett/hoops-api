using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Identity.Domain;

/// <summary>
/// A platform-level login. A user is not owned by any organisation; membership is expressed through
/// <see cref="OrganisationMembership"/>, so one person can belong to many organisations with one login.
/// </summary>
public sealed class User : IAuditableEntity
{
    // EF materialisation constructor.
    private User()
    {
        Email = null!;
        PasswordHash = null!;
        FullName = null!;
    }

    private User(UserId id, string email, string passwordHash, string fullName)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
    }

    /// <summary>The permanent primary key.</summary>
    public UserId Id { get; private set; }

    /// <summary>Login email. Case-insensitive and unique (stored as citext).</summary>
    public string Email { get; private set; }

    /// <summary>Opaque password hash produced by the configured hasher. Never a plaintext password.</summary>
    public string PasswordHash { get; private set; }

    /// <summary>Display name.</summary>
    public string FullName { get; private set; }

    /// <summary>True for platform administrators (merge approval, NIN verification — ADR-003).</summary>
    public bool IsSystemAdmin { get; private set; }

    /// <summary>Whether the email has been confirmed. Confirmation flow is out of scope for Phase 1.</summary>
    public bool EmailConfirmed { get; private set; }

    /// <summary>The last successful login, in UTC.</summary>
    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Registers a new user. The caller supplies an already-hashed password; the domain never sees
    /// plaintext.
    /// </summary>
    public static User Register(string email, string passwordHash, string fullName)
    {
        Guard.AgainstNullOrWhiteSpace(email);
        Guard.AgainstNullOrWhiteSpace(passwordHash);
        Guard.AgainstNullOrWhiteSpace(fullName);

        return new User(UserId.New(), email.Trim(), passwordHash, fullName.Trim());
    }

    /// <summary>Records a successful authentication at <paramref name="at"/>.</summary>
    public void MarkLoggedIn(DateTimeOffset at) => LastLoginAt = at;
}
