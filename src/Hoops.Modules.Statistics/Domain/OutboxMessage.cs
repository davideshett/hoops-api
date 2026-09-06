using Hoops.SharedKernel.Identifiers;

namespace Hoops.Modules.Statistics.Domain;

/// <summary>
/// A durable work item written in the SAME transaction as the state change that produced it (§9.3), so
/// a crash between "game finalised" and "aggregates recomputed" cannot lose the recomputation. Drained
/// by a background service.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage()
    {
        MessageType = null!;
        Payload = null!;
    }

    /// <summary>Primary key.</summary>
    public OutboxMessageId Id { get; private set; }

    /// <summary>What happened, e.g. <c>GameFinalized</c>.</summary>
    public string MessageType { get; private set; }

    /// <summary>The message body (a JSON object).</summary>
    public string Payload { get; private set; }

    /// <summary>When the message was written.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>When it was successfully processed; null while pending.</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>How many delivery attempts have been made.</summary>
    public int Attempts { get; private set; }

    /// <summary>The last failure, if any.</summary>
    public string? LastError { get; private set; }

    /// <summary>Enqueues a message.</summary>
    public static OutboxMessage Enqueue(string messageType, string payload, DateTimeOffset at)
        => new()
        {
            Id = OutboxMessageId.New(),
            MessageType = messageType,
            Payload = payload,
            OccurredAt = at,
        };

    /// <summary>Marks the message processed.</summary>
    public void MarkProcessed(DateTimeOffset at)
    {
        ProcessedAt = at;
        Attempts++;
        LastError = null;
    }

    /// <summary>Records a failed attempt, leaving the message pending for retry.</summary>
    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length > 1000 ? error[..1000] : error;
    }
}
