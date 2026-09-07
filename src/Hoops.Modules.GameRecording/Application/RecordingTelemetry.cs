using System.Diagnostics;

namespace Hoops.Modules.GameRecording.Application;

/// <summary>
/// Tracing for the event write path — the one hot path in the system, and the one where a slow or
/// failed request is felt immediately at the scorer's table. Spans carry the game and sequence so a
/// trace can be tied back to exactly what the official tapped.
/// </summary>
public static class RecordingTelemetry
{
    /// <summary>The activity source name; registered with OpenTelemetry by the host.</summary>
    public const string SourceName = "Hoops.GameRecording";

    /// <summary>The activity source for event submission.</summary>
    public static readonly ActivitySource Source = new(SourceName);

    /// <summary>Starts a span for one submission, tagged with the game.</summary>
    public static Activity? StartSubmission(Guid gameId, int eventCount)
    {
        var activity = Source.StartActivity("game.events.submit", ActivityKind.Internal);
        activity?.SetTag("hoops.game_id", gameId);
        activity?.SetTag("hoops.event_count", eventCount);
        return activity;
    }

    /// <summary>Records the outcome of a submission on the current span.</summary>
    public static void RecordOutcome(Activity? activity, string outcome, long? sequence = null, string? ruleCode = null)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("hoops.outcome", outcome);
        if (sequence is { } seq)
        {
            activity.SetTag("hoops.sequence", seq);
        }

        if (ruleCode is not null)
        {
            activity.SetTag("hoops.rule_code", ruleCode);
            activity.SetStatus(ActivityStatusCode.Error, ruleCode);
        }
    }
}
