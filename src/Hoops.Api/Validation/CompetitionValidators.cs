using FluentValidation;
using Hoops.Modules.Competitions.Contracts;

namespace Hoops.Api.Validation;

/// <summary>Shape validation for <see cref="CreateSeasonRequest"/>.</summary>
public sealed class CreateSeasonRequestValidator : AbstractValidator<CreateSeasonRequest>
{
    /// <summary>Configures the rules.</summary>
    public CreateSeasonRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.EndsOn).GreaterThanOrEqualTo(x => x.StartsOn)
            .WithMessage("A season cannot end before it starts.");
    }
}

/// <summary>Shape validation for <see cref="CreateCompetitionRequest"/>.</summary>
public sealed class CreateCompetitionRequestValidator : AbstractValidator<CreateCompetitionRequest>
{
    /// <summary>Configures the rules.</summary>
    public CreateCompetitionRequestValidator()
    {
        RuleFor(x => x.SeasonId.Value).NotEmpty().WithName("seasonId");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(80)
            .Matches("^[a-zA-Z0-9-]+$").WithMessage("Slug may contain only letters, digits, and hyphens.");
        RuleFor(x => x.Format).NotEmpty();
        RuleFor(x => x.Timezone).NotEmpty();
    }
}

/// <summary>Shape validation for <see cref="ChangeCompetitionStatusRequest"/>.</summary>
public sealed class ChangeCompetitionStatusRequestValidator : AbstractValidator<ChangeCompetitionStatusRequest>
{
    /// <summary>Configures the rules.</summary>
    public ChangeCompetitionStatusRequestValidator() => RuleFor(x => x.Status).NotEmpty();
}

/// <summary>Shape validation for <see cref="CreateStageRequest"/>.</summary>
public sealed class CreateStageRequestValidator : AbstractValidator<CreateStageRequest>
{
    /// <summary>Configures the rules.</summary>
    public CreateStageRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.StageType).NotEmpty();
        RuleFor(x => x.Sequence).GreaterThan(0);
    }
}

/// <summary>Shape validation for <see cref="CreateGroupRequest"/>.</summary>
public sealed class CreateGroupRequestValidator : AbstractValidator<CreateGroupRequest>
{
    /// <summary>Configures the rules.</summary>
    public CreateGroupRequestValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
}

/// <summary>Shape validation for <see cref="CreateTeamRequest"/>.</summary>
public sealed class CreateTeamRequestValidator : AbstractValidator<CreateTeamRequest>
{
    /// <summary>Configures the rules.</summary>
    public CreateTeamRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ShortName).NotEmpty().MaximumLength(12);
        RuleFor(x => x.Abbreviation).Length(3).When(x => !string.IsNullOrEmpty(x.Abbreviation));
    }
}

/// <summary>Shape validation for <see cref="EnterTeamRequest"/>.</summary>
public sealed class EnterTeamRequestValidator : AbstractValidator<EnterTeamRequest>
{
    /// <summary>Configures the rules.</summary>
    public EnterTeamRequestValidator() => RuleFor(x => x.TeamId.Value).NotEmpty().WithName("teamId");
}

/// <summary>Shape validation for <see cref="AddStaffRequest"/>.</summary>
public sealed class AddStaffRequestValidator : AbstractValidator<AddStaffRequest>
{
    /// <summary>Configures the rules.</summary>
    public AddStaffRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Role).NotEmpty();
    }
}

/// <summary>Shape validation for <see cref="CreateVenueRequest"/>.</summary>
public sealed class CreateVenueRequestValidator : AbstractValidator<CreateVenueRequest>
{
    /// <summary>Configures the rules.</summary>
    public CreateVenueRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Timezone).NotEmpty();
        RuleFor(x => x.CourtCount).GreaterThan(0).When(x => x.CourtCount.HasValue);
    }
}
