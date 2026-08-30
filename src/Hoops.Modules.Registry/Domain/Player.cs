using Hoops.SharedKernel;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Registry.Domain;

/// <summary>
/// The canonical, platform-level record of a human being (ADR-003). A <see cref="Player"/> is shared by
/// every organisation and is deliberately NOT tenant-scoped: it has no organisation_id and no global
/// query filter. The NIN is never stored in plaintext — only its HMAC (ADR-008). Identity tier is
/// derived from evidence and is never settable directly.
/// </summary>
public sealed class Player : IAuditableEntity
{
    private Player()
    {
        FirstName = null!;
        LastName = null!;
        Nationality = null!;
    }

    private Player(
        PlayerId id, string firstName, string lastName, DateOnly dateOfBirth, Gender gender,
        OrganisationId registeredByOrganisationId, UserId registeredByUserId)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        Nationality = "NG";
        RegisteredByOrganisationId = registeredByOrganisationId;
        RegisteredByUserId = registeredByUserId;
    }

    /// <summary>The permanent, public primary key — the only key that ever identifies a player.</summary>
    public PlayerId Id { get; private set; }

    /// <summary>Given name.</summary>
    public string FirstName { get; private set; }

    /// <summary>Family name.</summary>
    public string LastName { get; private set; }

    /// <summary>Middle name, if any.</summary>
    public string? MiddleName { get; private set; }

    /// <summary>Preferred / playing name, if any.</summary>
    public string? KnownAs { get; private set; }

    /// <summary>Date of birth. Required — drives age-group eligibility.</summary>
    public DateOnly DateOfBirth { get; private set; }

    /// <summary>Gender (division eligibility).</summary>
    public Gender Gender { get; private set; }

    /// <summary>ISO 3166-1 alpha-2 nationality. Defaults to NG.</summary>
    public string Nationality { get; private set; }

    /// <summary>State of origin, if recorded.</summary>
    public string? StateOfOrigin { get; private set; }

    /// <summary>Height in centimetres, if recorded.</summary>
    public int? HeightCm { get; private set; }

    /// <summary>Dominant hand, if recorded.</summary>
    public string? DominantHand { get; private set; }

    /// <summary>Private object-storage key of the photo. NEVER a public URL, NEVER returned to a client.</summary>
    public string? PhotoObjectKey { get; private set; }

    /// <summary>HMAC-SHA256 of the normalised NIN with the server pepper. NEVER the plaintext NIN.</summary>
    public byte[]? NinHmac { get; private set; }

    /// <summary>Whether the NIN was verified through a licensed provider.</summary>
    public bool NinVerified { get; private set; }

    /// <summary>When the NIN was verified, UTC.</summary>
    public DateTimeOffset? NinVerifiedAt { get; private set; }

    /// <summary>The provider's reference for the verification.</summary>
    public string? NinVerificationRef { get; private set; }

    /// <summary>The provider that verified the NIN.</summary>
    public string? NinVerificationProvider { get; private set; }

    /// <summary>DERIVED confidence tier (§5A.2). Recomputed from evidence; never set directly.</summary>
    public IdentityTier IdentityTier { get; private set; } = IdentityTier.Asserted;

    /// <summary>The kind of evidence backing the date of birth.</summary>
    public DobEvidenceType DobEvidenceType { get; private set; } = DobEvidenceType.None;

    /// <summary>When the DOB evidence was reviewed, UTC.</summary>
    public DateTimeOffset? DobVerifiedAt { get; private set; }

    /// <summary>Guardian name (for minors).</summary>
    public string? GuardianName { get; private set; }

    /// <summary>Guardian phone (for minors). Redacted from logs.</summary>
    public string? GuardianPhone { get; private set; }

    /// <summary>The consent record covering this player, if captured.</summary>
    public ConsentRecordId? GuardianConsentId { get; private set; }

    /// <summary>The organisation that first registered this player — provenance, NOT ownership.</summary>
    public OrganisationId RegisteredByOrganisationId { get; private set; }

    /// <summary>The user who registered this player.</summary>
    public UserId RegisteredByUserId { get; private set; }

    /// <summary>Lifecycle status.</summary>
    public PlayerStatus Status { get; private set; } = PlayerStatus.Active;

    /// <summary>If merged, the surviving player this record folds into (§5A.5). Resolved transitively on write.</summary>
    public PlayerId? MergedIntoId { get; private set; }

    /// <summary>When the player was anonymised for NDPA erasure (§13); null while identifiable.</summary>
    public DateTimeOffset? AnonymisedAt { get; private set; }

    /// <summary>Soft-delete marker; null while active.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>True if this record has been folded into another via merge.</summary>
    public bool IsMerged => MergedIntoId is not null;

    /// <summary>True if this record has been anonymised.</summary>
    public bool IsAnonymised => AnonymisedAt is not null;

    /// <summary>Whether the player is under 18 as of <paramref name="today"/>.</summary>
    public bool IsMinor(DateOnly today) => DateOfBirth > today.AddYears(-18);

    /// <summary>
    /// Registers a new player. Mandatory: name, DOB, gender. Optional: NIN HMAC, photo, guardian.
    /// The identity tier is derived, not supplied.
    /// </summary>
    public static Player Register(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        Gender gender,
        OrganisationId registeredByOrganisationId,
        UserId registeredByUserId,
        byte[]? ninHmac = null,
        string? middleName = null,
        string? knownAs = null,
        string? nationality = null,
        string? stateOfOrigin = null)
    {
        Guard.AgainstNullOrWhiteSpace(firstName);
        Guard.AgainstNullOrWhiteSpace(lastName);

        var player = new Player(PlayerId.New(), firstName.Trim(), lastName.Trim(), dateOfBirth, gender,
            registeredByOrganisationId, registeredByUserId)
        {
            MiddleName = Normalise(middleName),
            KnownAs = Normalise(knownAs),
            StateOfOrigin = Normalise(stateOfOrigin),
            NinHmac = ninHmac,
        };
        if (!string.IsNullOrWhiteSpace(nationality))
        {
            player.Nationality = nationality.Trim().ToUpperInvariant();
        }

        player.RecomputeTier();
        return player;
    }

    /// <summary>Attaches captured guardian details and the consent record (required for minors).</summary>
    public void SetGuardian(string? guardianName, string? guardianPhone, ConsentRecordId? consentId)
    {
        GuardianName = Normalise(guardianName);
        GuardianPhone = Normalise(guardianPhone);
        GuardianConsentId = consentId;
    }

    /// <summary>Records the result of a delegated NIN verification and recomputes the tier.</summary>
    public void ApplyNinVerification(bool verified, DateTimeOffset at, string provider, string? providerReference)
    {
        NinVerified = verified;
        NinVerifiedAt = at;
        NinVerificationProvider = provider;
        NinVerificationRef = providerReference;
        if (verified)
        {
            DobEvidenceType = DobEvidenceType.Nin;
            DobVerifiedAt = at;
        }

        RecomputeTier();
    }

    /// <summary>Records reviewed DOB evidence and recomputes the tier.</summary>
    public void ApplyDobEvidence(DobEvidenceType evidence, DateTimeOffset at)
    {
        DobEvidenceType = evidence;
        DobVerifiedAt = at;
        RecomputeTier();
    }

    /// <summary>Updates core identity fields. Callers must block this once <see cref="NinVerified"/> (§5A.5).</summary>
    public void UpdateCoreIdentity(string? firstName, string? lastName, string? middleName, string? knownAs)
    {
        if (!string.IsNullOrWhiteSpace(firstName))
        {
            FirstName = firstName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(lastName))
        {
            LastName = lastName.Trim();
        }

        if (middleName is not null)
        {
            MiddleName = Normalise(middleName);
        }

        if (knownAs is not null)
        {
            KnownAs = Normalise(knownAs);
        }
    }

    /// <summary>Sets the private photo object key.</summary>
    public void SetPhotoKey(string objectKey) => PhotoObjectKey = objectKey;

    /// <summary>Folds this record into <paramref name="survivor"/> (merge; §5A.5).</summary>
    public void MergeInto(PlayerId survivor) => MergedIntoId = survivor;

    /// <summary>Adopts <paramref name="tier"/> if it is higher than the current tier (used on merge).</summary>
    public void RaiseTierTo(IdentityTier tier)
    {
        if (tier > IdentityTier)
        {
            IdentityTier = tier;
        }
    }

    /// <summary>
    /// Anonymises the record for NDPA erasure (§13): clears names, NIN hash, photo, and guardian
    /// details, and stamps <see cref="AnonymisedAt"/>. The player_id, DOB, gender, and all derived
    /// statlines are retained — sporting results are a matter of legitimate record.
    /// </summary>
    public void Anonymise(DateTimeOffset at)
    {
        FirstName = "REDACTED";
        LastName = "REDACTED";
        MiddleName = null;
        KnownAs = null;
        NinHmac = null;
        NinVerificationRef = null;
        PhotoObjectKey = null;
        GuardianName = null;
        GuardianPhone = null;
        StateOfOrigin = null;
        AnonymisedAt = at;
    }

    // Tier is a pure function of the evidence: NIN verified => 2; documentary DOB evidence => 1; else 0.
    private void RecomputeTier()
        => IdentityTier = NinVerified
            ? IdentityTier.NinVerified
            : DobEvidenceType is DobEvidenceType.BirthCertificate or DobEvidenceType.SchoolRecord or DobEvidenceType.Passport
                ? IdentityTier.Documented
                : IdentityTier.Asserted;

    private static string? Normalise(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
