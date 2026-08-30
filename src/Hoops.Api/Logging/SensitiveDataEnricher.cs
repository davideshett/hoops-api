using Serilog.Core;
using Serilog.Events;

namespace Hoops.Api.Logging;

/// <summary>
/// Redacts sensitive structured properties from every log event (§13): <c>nin</c>, <c>ninHmac</c>,
/// <c>guardianPhone</c>, and <c>photoObjectKey</c>. This is defence in depth — the code never logs a
/// NIN in the first place — so an accidental structured log of one of these keys still cannot leak.
/// </summary>
public sealed class SensitiveDataEnricher : ILogEventEnricher
{
    private static readonly HashSet<string> Redacted = new(StringComparer.OrdinalIgnoreCase)
    {
        "nin", "ninHmac", "guardianPhone", "photoObjectKey",
    };

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var name in logEvent.Properties.Keys)
        {
            if (Redacted.Contains(name))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(name, "[REDACTED]"));
            }
        }
    }
}
