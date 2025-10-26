using System;

namespace Toolbox.Models;

/// <summary>
///     Represents a single entry in the in-memory application log.
/// </summary>
public sealed record LogEntry(DateTimeOffset Timestamp, LogLevel Level, string Message)
{
    /// <summary>
    ///     Formats the log entry for display in the logging view.
    /// </summary>
    /// <returns> A string that contains the timestamp, log level and message. </returns>
    public string ToDisplayString() => $"[{Timestamp:HH:mm:ss}] [{Level.ToDisplayString()}] {Message}";
}
