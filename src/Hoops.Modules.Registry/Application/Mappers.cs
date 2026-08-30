using Hoops.Modules.Registry.Contracts;
using Hoops.Modules.Registry.Domain;

namespace Hoops.Modules.Registry.Application;

/// <summary>Hand-written domain → DTO mappings (no AutoMapper).</summary>
internal static class Mappers
{
    public static PlayerSummaryDto ToSummary(this Player p, string? photoUrl)
        => new(p.Id, FullName(p), p.DateOfBirth, p.Gender.ToString(), photoUrl, p.IdentityTier.ToString());

    public static PlayerSensitiveDto ToSensitive(this Player p, DateOnly today)
        => new(p.Id, FullName(p), p.DateOfBirth, p.Gender.ToString(), p.IdentityTier.ToString(),
            NinPresent: p.NinHmac is not null, p.NinVerified, p.DobEvidenceType.ToString(),
            p.IsMinor(today), p.GuardianName, p.GuardianPhone, p.Status.ToString());

    public static EligibilityFlagDto ToDto(this PlayerEligibilityFlag f)
        => new(f.Id, f.PlayerId, f.FlagType.ToString(), f.Scope.ToString(), f.Reason, f.StartsOn, f.EndsOn, f.ResolvedAt is not null);

    public static PlayerOrgLinkDto ToDto(this PlayerOrgLink l)
        => new(l.OrganisationId, l.FirstLinkedAt, l.LastLinkedAt);

    public static MergeProposalDto ToDto(this MergeProposal m)
        => new(m.Id, m.KeepPlayerId, m.MergePlayerId, m.Evidence, m.Status.ToString(), m.ProposedAt);

    public static RosterEntryDto ToDto(this RosterEntry r)
        => new(r.Id, r.CompetitionTeamId, r.PlayerId, r.VerifiedTier, r.JerseyNumber, r.Position, r.IsCaptain,
            r.Status.ToString(), r.RegisteredAt);

    private static string FullName(Player p)
        => string.IsNullOrWhiteSpace(p.MiddleName) ? $"{p.FirstName} {p.LastName}" : $"{p.FirstName} {p.MiddleName} {p.LastName}";
}
