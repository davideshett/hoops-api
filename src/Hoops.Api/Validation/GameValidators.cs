using FluentValidation;
using Hoops.Modules.GameRecording.Contracts;

namespace Hoops.Api.Validation;

/// <summary>Shape validation for <see cref="CreateGameRequest"/>.</summary>
public sealed class CreateGameRequestValidator : AbstractValidator<CreateGameRequest>
{
    /// <summary>Configures the rules.</summary>
    public CreateGameRequestValidator()
    {
        RuleFor(x => x.HomeCompetitionTeamId.Value).NotEmpty().WithName("homeCompetitionTeamId");
        RuleFor(x => x.AwayCompetitionTeamId.Value).NotEmpty().WithName("awayCompetitionTeamId");
        RuleFor(x => x.ScheduledAt).NotEmpty();
    }
}

/// <summary>Shape validation for <see cref="AssignOfficialRequest"/>.</summary>
public sealed class AssignOfficialRequestValidator : AbstractValidator<AssignOfficialRequest>
{
    /// <summary>Configures the rules.</summary>
    public AssignOfficialRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Role).NotEmpty();
    }
}

/// <summary>Shape validation for <see cref="LockRosterRequest"/>.</summary>
public sealed class LockRosterRequestValidator : AbstractValidator<LockRosterRequest>
{
    /// <summary>Configures the rules.</summary>
    public LockRosterRequestValidator()
        => RuleFor(x => x.Selections).NotEmpty().WithMessage("Select the players available for this game.");
}
