using Hoops.Modules.Registry.Application.Abstractions;
using Hoops.Modules.Registry.Contracts;
using Hoops.Modules.Registry.Domain;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Hoops.SharedKernel.Results;

namespace Hoops.Modules.Registry.Application;

/// <summary>Player registry use cases. Enforces §5A safeguards: search specificity, NIN de-duplication,
/// guardian consent, derived tiers, audit on every search/sensitive read, and the provenance ledger.</summary>
public sealed class PlayerRegistryService : IPlayerRegistryService
{
    private const int SearchCap = 25;
    private const int MinSurnameLength = 3;

    private readonly IPlayerRepository _players;
    private readonly IPlayerOrgLinkRepository _links;
    private readonly IConsentRepository _consents;
    private readonly IEligibilityFlagRepository _flags;
    private readonly IRegistryAuditRepository _audits;
    private readonly IRegistryLedgerRepository _ledger;
    private readonly INinHasher _ninHasher;
    private readonly INinVerificationProvider _ninProvider;
    private readonly IPhotoStorage _photos;
    private readonly IRegistryUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Creates the service.</summary>
    public PlayerRegistryService(
        IPlayerRepository players, IPlayerOrgLinkRepository links, IConsentRepository consents,
        IEligibilityFlagRepository flags, IRegistryAuditRepository audits, IRegistryLedgerRepository ledger,
        INinHasher ninHasher, INinVerificationProvider ninProvider, IPhotoStorage photos,
        IRegistryUnitOfWork unitOfWork, IClock clock)
    {
        _players = players;
        _links = links;
        _consents = consents;
        _flags = flags;
        _audits = audits;
        _ledger = ledger;
        _ninHasher = ninHasher;
        _ninProvider = ninProvider;
        _photos = photos;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PlayerSummaryDto>>> SearchAsync(
        RegistryCaller caller, PlayerSearchRequest request, CancellationToken ct = default)
    {
        List<Player> matches;
        Dictionary<string, string> queryTerms;

        if (!string.IsNullOrWhiteSpace(request.Nin))
        {
            // NIN search: exact HMAC match. The raw NIN NEVER enters query_terms.
            var hmac = _ninHasher.Hash(request.Nin);
            queryTerms = new Dictionary<string, string> { ["by"] = "nin" };
            var hit = hmac is null ? null : await _players.GetByNinHmacAsync(hmac, ct);
            matches = hit is null ? [] : [hit];
        }
        else
        {
            var lastName = request.LastName?.Trim() ?? string.Empty;
            var hasDob = request.DateOfBirth is not null || request.BirthYear is not null;
            if (lastName.Length < MinSurnameLength || !hasDob)
            {
                return Error.Validation("SEARCH_TOO_BROAD",
                    "Provide a NIN, or a surname of at least 3 characters plus a date of birth or birth year.");
            }

            queryTerms = new Dictionary<string, string> { ["lastName"] = lastName };
            if (request.DateOfBirth is { } dob)
            {
                queryTerms["dateOfBirth"] = dob.ToString("O");
            }

            if (request.BirthYear is { } year)
            {
                queryTerms["birthYear"] = year.ToString();
            }

            var found = await _players.SearchAsync(lastName.ToLowerInvariant(), request.DateOfBirth, request.BirthYear, SearchCap, ct);
            matches = found.ToList();
        }

        _audits.Add(RegistryAudit.Record(caller.UserId, caller.OrganisationId, RegistryAction.Search,
            queryTerms, _clock.UtcNow, resultCount: matches.Count, ipAddress: caller.IpAddress));
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success<IReadOnlyList<PlayerSummaryDto>>(matches.Select(p => p.ToSummary(PhotoUrl(p))).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<PlayerSummaryDto>> CreateAsync(RegistryCaller caller, CreatePlayerRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<Gender>(request.Gender, ignoreCase: true, out var gender) || !Enum.IsDefined(gender))
        {
            return Error.Validation("INVALID_GENDER", $"'{request.Gender}' is not a valid gender.");
        }

        var ninHmac = _ninHasher.Hash(request.Nin);
        if (ninHmac is not null)
        {
            var existing = await _players.GetByNinHmacAsync(ninHmac, ct);
            if (existing is not null)
            {
                return Error.Conflict("PLAYER_ALREADY_REGISTERED", $"A player with this NIN already exists. playerId={existing.Id.Value}");
            }
        }

        var isMinor = request.DateOfBirth > Today.AddYears(-18);
        if (isMinor && request.GuardianConsent is null)
        {
            return Error.Validation("GUARDIAN_CONSENT_REQUIRED", "Registering a minor requires guardian consent.");
        }

        var now = _clock.UtcNow;
        var player = Player.Register(
            request.FirstName, request.LastName, request.DateOfBirth, gender,
            caller.OrganisationId, caller.UserId, ninHmac, request.MiddleName, request.KnownAs, request.Nationality, request.StateOfOrigin);

        if (request.GuardianConsent is { } gc)
        {
            var consent = ConsentRecord.Grant(player.Id, ConsentType.Guardian, gc.ScopeVersion, now, caller.UserId, gc.EvidenceObjectKey);
            _consents.Add(consent);
            player.SetGuardian(gc.GuardianName, gc.GuardianPhone, consent.Id);
        }

        _players.Add(player);
        _links.Add(PlayerOrgLink.Create(player.Id, caller.OrganisationId, now));
        _audits.Add(RegistryAudit.Record(caller.UserId, caller.OrganisationId, RegistryAction.Create,
            new Dictionary<string, string>(), now, player.Id, ipAddress: caller.IpAddress));
        await AppendLedgerAsync(LedgerEntryType.Registered, player.Id, now, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return player.ToSummary(PhotoUrl(player));
    }

    /// <inheritdoc />
    public async Task<Result<PlayerSummaryDto>> GetAsync(PlayerId id, CancellationToken ct = default)
    {
        var player = await _players.GetAsync(id, ct);
        return player is null ? PlayerNotFound() : player.ToSummary(PhotoUrl(player));
    }

    /// <inheritdoc />
    public async Task<Result<PlayerSensitiveDto>> GetSensitiveAsync(RegistryCaller caller, PlayerId id, CancellationToken ct = default)
    {
        var player = await _players.GetAsync(id, ct);
        if (player is null)
        {
            return PlayerNotFound();
        }

        _audits.Add(RegistryAudit.Record(caller.UserId, caller.OrganisationId, RegistryAction.ViewSensitive,
            new Dictionary<string, string>(), _clock.UtcNow, id, ipAddress: caller.IpAddress));
        await _unitOfWork.SaveChangesAsync(ct);

        return player.ToSensitive(Today);
    }

    /// <inheritdoc />
    public async Task<Result<PlayerSummaryDto>> UpdateAsync(RegistryCaller caller, PlayerId id, UpdatePlayerRequest request, CancellationToken ct = default)
    {
        var player = await _players.GetAsync(id, ct);
        if (player is null)
        {
            return PlayerNotFound();
        }

        if (player.NinVerified)
        {
            return Error.Conflict("IDENTITY_LOCKED", "Core identity cannot be changed once the NIN is verified.");
        }

        player.UpdateCoreIdentity(request.FirstName, request.LastName, request.MiddleName, request.KnownAs);
        _audits.Add(RegistryAudit.Record(caller.UserId, caller.OrganisationId, RegistryAction.Edit,
            new Dictionary<string, string>(), _clock.UtcNow, id, ipAddress: caller.IpAddress));
        await _unitOfWork.SaveChangesAsync(ct);
        return player.ToSummary(PhotoUrl(player));
    }

    /// <inheritdoc />
    public async Task<Result<PresignedUrlDto>> CreatePhotoUploadAsync(PlayerId id, string? contentType, CancellationToken ct = default)
    {
        var player = await _players.GetAsync(id, ct);
        if (player is null)
        {
            return PlayerNotFound();
        }

        var target = _photos.CreateUploadTarget(id, string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType);
        player.SetPhotoKey(target.ObjectKey);
        await _unitOfWork.SaveChangesAsync(ct);
        return new PresignedUrlDto(target.Upload.Url, target.Upload.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<Result<PresignedUrlDto>> GetPhotoAsync(PlayerId id, CancellationToken ct = default)
    {
        var player = await _players.GetAsync(id, ct);
        if (player is null)
        {
            return PlayerNotFound();
        }

        if (string.IsNullOrWhiteSpace(player.PhotoObjectKey))
        {
            return Error.NotFound("PHOTO_NOT_FOUND", "This player has no photo.");
        }

        var url = _photos.CreateReadUrl(player.PhotoObjectKey);
        return new PresignedUrlDto(url.Url, url.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<Result<PlayerHistoryDto>> GetHistoryAsync(PlayerId id, CancellationToken ct = default)
    {
        if (await _players.GetAsync(id, ct) is null)
        {
            return PlayerNotFound();
        }

        var links = await _links.ListForPlayerAsync(id, ct);
        return new PlayerHistoryDto(id, links.Select(l => l.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<EligibilityFlagDto>>> ListFlagsAsync(PlayerId id, CancellationToken ct = default)
    {
        var flags = await _flags.ListForPlayerAsync(id, ct);
        return Result.Success<IReadOnlyList<EligibilityFlagDto>>(flags.Select(f => f.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<EligibilityFlagDto>> RaiseFlagAsync(RegistryCaller caller, PlayerId id, RaiseFlagRequest request, CancellationToken ct = default)
    {
        var player = await _players.GetAsync(id, ct);
        if (player is null)
        {
            return PlayerNotFound();
        }

        if (!Enum.TryParse<FlagType>(request.FlagType, ignoreCase: true, out var flagType) || !Enum.IsDefined(flagType))
        {
            return Error.Validation("INVALID_FLAG_TYPE", $"'{request.FlagType}' is not a valid flag type.");
        }

        if (!Enum.TryParse<FlagScope>(request.Scope, ignoreCase: true, out var scope) || !Enum.IsDefined(scope))
        {
            return Error.Validation("INVALID_FLAG_SCOPE", $"'{request.Scope}' is not a valid flag scope.");
        }

        var flag = PlayerEligibilityFlag.Raise(id, flagType, scope, request.Reason, request.StartsOn, caller.UserId,
            request.OrganisationId, request.CompetitionId, request.EndsOn);
        _flags.Add(flag);
        await AppendLedgerAsync(LedgerEntryType.FlagRaised, id, _clock.UtcNow, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return flag.ToDto();
    }

    /// <inheritdoc />
    public async Task<Result> ResolveFlagAsync(EligibilityFlagId flagId, CancellationToken ct = default)
    {
        var flag = await _flags.GetAsync(flagId, ct);
        if (flag is null)
        {
            return Error.NotFound("FLAG_NOT_FOUND", "The flag does not exist.");
        }

        flag.Resolve(_clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<PlayerSummaryDto>> RecordDobEvidenceAsync(RegistryCaller caller, PlayerId id, DobEvidenceRequest request, CancellationToken ct = default)
    {
        var player = await _players.GetAsync(id, ct);
        if (player is null)
        {
            return PlayerNotFound();
        }

        if (!Enum.TryParse<DobEvidenceType>(request.EvidenceType, ignoreCase: true, out var evidence) || !Enum.IsDefined(evidence))
        {
            return Error.Validation("INVALID_EVIDENCE_TYPE", $"'{request.EvidenceType}' is not a valid evidence type.");
        }

        var before = player.IdentityTier;
        player.ApplyDobEvidence(evidence, _clock.UtcNow);
        _audits.Add(RegistryAudit.Record(caller.UserId, caller.OrganisationId, RegistryAction.Edit,
            new Dictionary<string, string> { ["dobEvidence"] = evidence.ToString() }, _clock.UtcNow, id, ipAddress: caller.IpAddress));
        await AppendLedgerAsync(LedgerEntryType.DobEvidence, id, _clock.UtcNow, ct);
        if (player.IdentityTier != before)
        {
            await AppendLedgerAsync(LedgerEntryType.TierChanged, id, _clock.UtcNow, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return player.ToSummary(PhotoUrl(player));
    }

    /// <inheritdoc />
    public async Task<Result<PlayerSummaryDto>> VerifyNinAsync(RegistryCaller caller, PlayerId id, VerifyNinRequest request, CancellationToken ct = default)
    {
        if (!caller.IsPlatformAdmin)
        {
            return Error.Forbidden("PLATFORM_ADMIN_REQUIRED", "Only a platform administrator can verify a NIN.");
        }

        var player = await _players.GetAsync(id, ct);
        if (player is null)
        {
            return PlayerNotFound();
        }

        var result = await _ninProvider.VerifyAsync(request.Nin, ct);

        // Cross-check demographic DOB, never overwrite: a mismatch raises an AgeDispute for human review.
        if (result.DateOfBirth is { } providerDob && providerDob != player.DateOfBirth)
        {
            _flags.Add(PlayerEligibilityFlag.Raise(id, FlagType.AgeDispute, FlagScope.Platform,
                "NIN demographic DOB does not match the recorded DOB.", Today, caller.UserId, null, null, null));
        }

        player.ApplyNinVerification(result.Verified, result.VerifiedAt, result.Provider, result.ProviderReference);
        _audits.Add(RegistryAudit.Record(caller.UserId, caller.OrganisationId, RegistryAction.VerifyNin,
            new Dictionary<string, string> { ["provider"] = result.Provider }, _clock.UtcNow, id, ipAddress: caller.IpAddress));
        await AppendLedgerAsync(LedgerEntryType.NinVerified, id, _clock.UtcNow, ct);
        await AppendLedgerAsync(LedgerEntryType.TierChanged, id, _clock.UtcNow, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return player.ToSummary(PhotoUrl(player));
    }

    /// <inheritdoc />
    public async Task<Result> AnonymiseAsync(RegistryCaller caller, PlayerId id, CancellationToken ct = default)
    {
        if (!caller.IsPlatformAdmin)
        {
            return Error.Forbidden("PLATFORM_ADMIN_REQUIRED", "Only a platform administrator can anonymise a player.");
        }

        var player = await _players.GetAsync(id, ct);
        if (player is null)
        {
            return PlayerNotFound();
        }

        player.Anonymise(_clock.UtcNow);
        _audits.Add(RegistryAudit.Record(caller.UserId, caller.OrganisationId, RegistryAction.Anonymise,
            new Dictionary<string, string>(), _clock.UtcNow, id, ipAddress: caller.IpAddress));
        await AppendLedgerAsync(LedgerEntryType.Anonymised, id, _clock.UtcNow, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<RegistryMetricsDto>> GetMetricsAsync(CancellationToken ct = default)
    {
        var c = await _players.CountAsync(Today, ct);
        return new RegistryMetricsDto(c.Total, c.Tier0, c.Tier1, c.Tier2, c.Minors, c.WithNin);
    }

    /// <inheritdoc />
    public async Task<Result<LedgerVerificationDto>> VerifyLedgerAsync(CancellationToken ct = default)
    {
        var entries = await _ledger.ListAllAsync(ct);
        var result = LedgerVerification.Verify(entries);
        return new LedgerVerificationDto(result.IsValid, result.DivergenceSequence, result.EntriesChecked);
    }

    // The chain tip for entries appended within this request. The service is scoped per request, so
    // multiple appends in one operation chain onto each other even before SaveChanges runs.
    private byte[]? _chainTip;

    private async Task AppendLedgerAsync(LedgerEntryType type, PlayerId playerId, DateTimeOffset at, CancellationToken ct)
    {
        var previous = _chainTip ?? (await _ledger.GetLastAsync(ct))?.EntryHash ?? RegistryLedgerEntry.Genesis;
        var entry = RegistryLedgerEntry.Append(previous, type, playerId, at);
        _ledger.Add(entry);
        _chainTip = entry.EntryHash;
    }

    private string? PhotoUrl(Player player)
        => string.IsNullOrWhiteSpace(player.PhotoObjectKey) ? null : _photos.CreateReadUrl(player.PhotoObjectKey).Url;

    private static Error PlayerNotFound() => Error.NotFound("PLAYER_NOT_FOUND", "The player does not exist.");
}
