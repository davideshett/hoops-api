using FluentValidation;
using Hoops.Modules.Registry.Contracts;

namespace Hoops.Api.Validation;

/// <summary>Shape validation for <see cref="CreatePlayerRequest"/>.</summary>
public sealed class CreatePlayerRequestValidator : AbstractValidator<CreatePlayerRequest>
{
    /// <summary>Configures the rules.</summary>
    public CreatePlayerRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Gender).NotEmpty();
        RuleFor(x => x.DateOfBirth).NotEmpty();
        RuleFor(x => x.Nationality).Length(2).When(x => !string.IsNullOrEmpty(x.Nationality));
        When(x => x.GuardianConsent is not null, () =>
        {
            RuleFor(x => x.GuardianConsent!.GuardianName).NotEmpty();
            RuleFor(x => x.GuardianConsent!.ScopeVersion).NotEmpty();
        });
    }
}

/// <summary>Shape validation for <see cref="VerifyNinRequest"/>.</summary>
public sealed class VerifyNinRequestValidator : AbstractValidator<VerifyNinRequest>
{
    /// <summary>Configures the rules.</summary>
    public VerifyNinRequestValidator() => RuleFor(x => x.Nin).NotEmpty();
}

/// <summary>Shape validation for <see cref="RaiseFlagRequest"/>.</summary>
public sealed class RaiseFlagRequestValidator : AbstractValidator<RaiseFlagRequest>
{
    /// <summary>Configures the rules.</summary>
    public RaiseFlagRequestValidator()
    {
        RuleFor(x => x.FlagType).NotEmpty();
        RuleFor(x => x.Scope).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Shape validation for <see cref="DobEvidenceRequest"/>.</summary>
public sealed class DobEvidenceRequestValidator : AbstractValidator<DobEvidenceRequest>
{
    /// <summary>Configures the rules.</summary>
    public DobEvidenceRequestValidator() => RuleFor(x => x.EvidenceType).NotEmpty();
}

/// <summary>Shape validation for <see cref="CreateMergeProposalRequest"/>.</summary>
public sealed class CreateMergeProposalRequestValidator : AbstractValidator<CreateMergeProposalRequest>
{
    /// <summary>Configures the rules.</summary>
    public CreateMergeProposalRequestValidator()
    {
        RuleFor(x => x.KeepId.Value).NotEmpty().WithName("keepId");
        RuleFor(x => x.MergeId.Value).NotEmpty().WithName("mergeId");
        RuleFor(x => x.Evidence).NotEmpty().MaximumLength(1000);
    }
}

/// <summary>Shape validation for <see cref="RegisterRosterEntryRequest"/> — enforces a real playerId.</summary>
public sealed class RegisterRosterEntryRequestValidator : AbstractValidator<RegisterRosterEntryRequest>
{
    /// <summary>Configures the rules.</summary>
    public RegisterRosterEntryRequestValidator()
    {
        RuleFor(x => x.PlayerId.Value).NotEmpty()
            .WithName("playerId")
            .WithMessage("A roster entry must reference a registry playerId, not a name.");
        RuleFor(x => x.JerseyNumber).NotEmpty().MaximumLength(3);
    }
}
