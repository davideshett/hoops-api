namespace Hoops.Modules.Registry.Domain;

/// <summary>A player's gender, used for division eligibility. Stored as text.</summary>
public enum Gender
{
    /// <summary>Male.</summary>
    Male = 0,

    /// <summary>Female.</summary>
    Female = 1,
}

/// <summary>
/// Identity confidence tier (§5A.2). DERIVED from evidence, never settable through any endpoint.
/// Stored as a smallint.
/// </summary>
public enum IdentityTier
{
    /// <summary>Organiser typed a name and DOB. Open-age competitions only.</summary>
    Asserted = 0,

    /// <summary>Birth certificate, school record, or passport reviewed. Age-group competitions.</summary>
    Documented = 1,

    /// <summary>NIN verified through a licensed provider. Everything, including national selection.</summary>
    NinVerified = 2,
}

/// <summary>The kind of evidence backing a player's date of birth. Stored as text.</summary>
public enum DobEvidenceType
{
    /// <summary>No documentary evidence.</summary>
    None = 0,

    /// <summary>Birth certificate reviewed.</summary>
    BirthCertificate = 1,

    /// <summary>School record reviewed.</summary>
    SchoolRecord = 2,

    /// <summary>Passport reviewed.</summary>
    Passport = 3,

    /// <summary>Backed by a verified NIN.</summary>
    Nin = 4,
}

/// <summary>A player's lifecycle status. Stored as text.</summary>
public enum PlayerStatus
{
    /// <summary>Active.</summary>
    Active = 0,

    /// <summary>Suspended.</summary>
    Suspended = 1,

    /// <summary>Retired.</summary>
    Retired = 2,

    /// <summary>Deceased.</summary>
    Deceased = 3,
}

/// <summary>Who granted consent for a player's data (NDPA lawful basis). Stored as text.</summary>
public enum ConsentType
{
    /// <summary>The player consented for themselves (adult).</summary>
    PlayerSelf = 0,

    /// <summary>A guardian consented on behalf of a minor.</summary>
    Guardian = 1,
}

/// <summary>The kind of eligibility flag on a player. Stored as text.</summary>
public enum FlagType
{
    /// <summary>Suspension.</summary>
    Suspension = 0,

    /// <summary>Disputed age.</summary>
    AgeDispute = 1,

    /// <summary>Disputed identity.</summary>
    IdentityDispute = 2,

    /// <summary>Medical hold.</summary>
    MedicalHold = 3,
}

/// <summary>How far an eligibility flag reaches. Stored as text.</summary>
public enum FlagScope
{
    /// <summary>Binds every league on the platform.</summary>
    Platform = 0,

    /// <summary>Binds one organisation.</summary>
    Organisation = 1,

    /// <summary>Binds one competition.</summary>
    Competition = 2,
}

/// <summary>Lifecycle of a merge proposal. Stored as text.</summary>
public enum MergeProposalStatus
{
    /// <summary>Awaiting a platform admin.</summary>
    Pending = 0,

    /// <summary>Approved and executed.</summary>
    Approved = 1,

    /// <summary>Rejected.</summary>
    Rejected = 2,
}

/// <summary>The kind of entry appended to the provenance ledger (§5A.6). Stored as text.</summary>
public enum LedgerEntryType
{
    /// <summary>A player was registered.</summary>
    Registered = 0,

    /// <summary>DOB evidence was reviewed.</summary>
    DobEvidence = 1,

    /// <summary>A NIN was verified.</summary>
    NinVerified = 2,

    /// <summary>The identity tier changed.</summary>
    TierChanged = 3,

    /// <summary>Two players were merged.</summary>
    Merged = 4,

    /// <summary>An eligibility flag was raised.</summary>
    FlagRaised = 5,

    /// <summary>A player was anonymised (NDPA erasure).</summary>
    Anonymised = 6,
}

/// <summary>What a registry action did, for the audit trail (§5A.4). Stored as text.</summary>
public enum RegistryAction
{
    /// <summary>Searched the registry.</summary>
    Search = 0,

    /// <summary>Read sensitive fields.</summary>
    ViewSensitive = 1,

    /// <summary>Created a player.</summary>
    Create = 2,

    /// <summary>Edited core identity.</summary>
    Edit = 3,

    /// <summary>Merged players.</summary>
    Merge = 4,

    /// <summary>Verified a NIN.</summary>
    VerifyNin = 5,

    /// <summary>Anonymised a player.</summary>
    Anonymise = 6,
}

/// <summary>A player's status within one competition-team roster. Stored as text.</summary>
public enum RosterEntryStatus
{
    /// <summary>Active.</summary>
    Active = 0,

    /// <summary>Injured.</summary>
    Injured = 1,

    /// <summary>Suspended.</summary>
    Suspended = 2,

    /// <summary>Removed (frees the jersey number).</summary>
    Removed = 3,
}
