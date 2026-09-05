namespace Hoops.Modules.GameRecording.Domain.Validation;

/// <summary>The outcome of running the whole pipeline against a candidate event.</summary>
/// <param name="IsAccepted">True when the event may be recorded.</param>
/// <param name="Failure">The first failing rule, if any.</param>
/// <param name="WasOverridden">True when a tier-2 failure was overridden by an authorised caller.</param>
public sealed record ValidationOutcome(bool IsAccepted, RuleResult? Failure, bool WasOverridden)
{
    /// <summary>An accepted event with no violations.</summary>
    public static readonly ValidationOutcome Accepted = new(true, null, false);

    /// <summary>An accepted event whose tier-2 violation was overridden.</summary>
    public static ValidationOutcome Overridden(RuleResult failure) => new(true, failure, true);

    /// <summary>A rejected event.</summary>
    public static ValidationOutcome Rejected(RuleResult failure) => new(false, failure, false);
}

/// <summary>
/// The ordered rule pipeline (§10). Tier-1 failures are always rejected. Tier-2 failures are rejected
/// by default but may be overridden by a CompetitionManager with a reason — because a system that
/// cannot record what actually happened is worse than one that records it and flags it.
/// </summary>
public sealed class EventValidationPipeline
{
    private readonly IReadOnlyList<IEventRule> _rules;

    /// <summary>Creates a pipeline with the standard §10 rule set, in evaluation order.</summary>
    public EventValidationPipeline()
        : this(DefaultRules())
    {
    }

    /// <summary>Creates a pipeline with an explicit rule list (used by tests).</summary>
    public EventValidationPipeline(IReadOnlyList<IEventRule> rules) => _rules = rules;

    /// <summary>Every rule code the pipeline can report.</summary>
    public IReadOnlyList<string> Codes => _rules.Select(r => r.Code).ToList();

    /// <summary>The standard rules, tier 1 first so the most serious violation is reported.</summary>
    public static IReadOnlyList<IEventRule> DefaultRules() =>
    [
        // Tier 1 — always reject.
        new GameInProgressRule(),
        new EventBeforeGameStartRule(),
        new PlayerOnRosterRule(),
        new WrongTeamRule(),
        new ShotInBoundsRule(),
        new AssistSelfRule(),
        new AssistTeammateRule(),
        new StealOpponentRule(),
        new BlockOpponentRule(),
        new FoulDrawnOpponentRule(),
        new FreeThrowSequenceRule(),
        new TiedAtGameEndRule(),

        // Tier 2 — reject by default, overridable.
        new PlayerFouledOutRule(),
        new PlayerOnCourtRule(),
        new LineupSizeRule(),
        new SubstitutionWhileLiveRule(),
        new ClockMonotonicRule(),
        new ClockInRangeRule(),
        new ReboundAfterMissRule(),
        new FreeThrowSourceRule(),
        new ShotZoneMatchesSubtypeRule(),
        new TimeoutLimitRule(),
        new PeriodCompleteRule(),
    ];

    /// <summary>
    /// Evaluates every rule in order and returns the first violation. <paramref name="allowOverride"/>
    /// lets an authorised caller accept a tier-2 violation, which is then flagged on the event.
    /// </summary>
    public ValidationOutcome Evaluate(ValidationContext context, GameEvent candidate, bool allowOverride)
    {
        foreach (var rule in _rules)
        {
            var result = rule.Evaluate(context, candidate);
            if (result.IsValid)
            {
                continue;
            }

            if (result.Tier == RuleTier.Overridable && allowOverride)
            {
                return ValidationOutcome.Overridden(result);
            }

            return ValidationOutcome.Rejected(result);
        }

        return ValidationOutcome.Accepted;
    }
}
