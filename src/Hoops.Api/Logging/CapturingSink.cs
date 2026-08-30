using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace Hoops.Api.Logging;

/// <summary>
/// A test-support Serilog sink that keeps recent log output in memory so an integration test can
/// assert a plaintext NIN never appears anywhere in the logs. Enabled only when the <c>CaptureLogs</c>
/// configuration flag is set (the test host sets it); off in normal operation.
/// </summary>
public sealed class CapturingSink : ILogEventSink
{
    private const int MaxEntries = 2000;
    private static readonly ConcurrentQueue<string> Entries = new();

    /// <summary>Renders and stores a log event (message template, rendered message, and all properties).</summary>
    public void Emit(LogEvent logEvent)
    {
        var props = string.Join(" ", logEvent.Properties.Select(p => $"{p.Key}={p.Value}"));
        Entries.Enqueue($"{logEvent.MessageTemplate.Text} | {logEvent.RenderMessage()} | {props}");
        while (Entries.Count > MaxEntries && Entries.TryDequeue(out _))
        {
        }
    }

    /// <summary>Clears captured output.</summary>
    public static void Clear() => Entries.Clear();

    /// <summary>A snapshot of everything captured so far.</summary>
    public static IReadOnlyList<string> Snapshot() => Entries.ToArray();

    /// <summary>True if any captured line contains <paramref name="value"/>.</summary>
    public static bool ContainsText(string value) => Entries.Any(e => e.Contains(value, StringComparison.Ordinal));
}
