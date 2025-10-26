using Toolbox.Models;
using Toolbox.Settings;
using LogLevel = Toolbox.Models.LogLevel;

namespace Toolbox.Services;

/// <summary>
///     Provides an in-memory application log that can be displayed inside the PWA.
/// </summary>
public sealed class InMemoryLogService
{
    /// <summary>
    ///     Occurs when log entries have changed.
    /// </summary>
    public event EventHandler? LogsChanged;

    /// <summary>
    ///     Gets the currently configured log level.
    /// </summary>
    public LogLevel CurrentLevel
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentLevel;
            }
        }
    }

    /// <summary>
    ///     Gets the maximum number of log lines that are kept in memory.
    /// </summary>
    public int MaxEntries
    {
        get
        {
            lock (_syncRoot)
            {
                return _maxEntries;
            }
        }
    }

    /// <summary>
    ///     Removes all log entries.
    /// </summary>
    public void Clear()
    {
        bool hadEntries;

        lock (_syncRoot)
        {
            hadEntries = _entries.Count > 0;
            _entries.Clear();
        }

        if (hadEntries)
        {
            OnLogsChanged();
        }
    }

    /// <summary>
    ///     Gets a snapshot of all log entries.
    /// </summary>
    /// <returns> A read-only list of the current log entries. </returns>
    public IReadOnlyList<LogEntry> GetEntries()
    {
        lock (_syncRoot)
        {
            return _entries.ToArray();
        }
    }

    /// <summary>
    ///     Gets the log content as a single string separated by the environment newline.
    /// </summary>
    public string GetLogText()
    {
        lock (_syncRoot)
        {
            return string.Join(Environment.NewLine, _entries.Select(entry => entry.ToDisplayString()));
        }
    }

    /// <summary>
    ///     Adds the specified message to the log with an information severity.
    /// </summary>
    /// <param name="message"> The message to log. </param>
    public void Log(string message) => Log(LogLevel.Info, message);

    /// <summary>
    ///     Adds the specified message to the log. Multi-line messages are split into individual log entries.
    /// </summary>
    /// <param name="level">   The severity of the message. </param>
    /// <param name="message"> The message to log. </param>
    public void Log(LogLevel level, string message)
    {
        if (message is null || level == LogLevel.None)
        {
            return;
        }

        var normalizedMessage = message.ReplaceLineEndings("\n");
        var lines = normalizedMessage.Split('\n');
        var hasChanges = false;

        lock (_syncRoot)
        {
            if (!IsLevelEnabled_NoLock(level))
            {
                return;
            }

            foreach (var line in lines)
            {
                _entries.Add(new LogEntry(DateTimeOffset.Now, level, line));
                hasChanges = true;
            }

            if (hasChanges)
            {
                _ = TrimExcessEntries_NoLock();
            }
        }

        if (hasChanges)
        {
            OnLogsChanged();
        }
    }

    /// <summary>
    ///     Logs a message with <see cref="LogLevel.Debug"/> severity.
    /// </summary>
    public void LogDebug(string message) => Log(LogLevel.Debug, message);

    /// <summary>
    ///     Logs a message with <see cref="LogLevel.Error"/> severity.
    /// </summary>
    public void LogError(string message) => Log(LogLevel.Error, message);

    /// <summary>
    ///     Logs a message with <see cref="LogLevel.Info"/> severity.
    /// </summary>
    public void LogInformation(string message) => Log(LogLevel.Info, message);

    /// <summary>
    ///     Logs a message with <see cref="LogLevel.Warn"/> severity.
    /// </summary>
    public void LogWarning(string message) => Log(LogLevel.Warn, message);

    /// <summary>
    ///     Updates the currently active log level.
    /// </summary>
    /// <param name="level"> The desired log level. </param>
    public void SetLogLevel(LogLevel level)
    {
        if (!LogLevelExtensions.IsDefined(level))
        {
            level = Config.DefaultLogLevel;
        }

        lock (_syncRoot)
        {
            _currentLevel = level;
        }
    }

    /// <summary>
    ///     Configures the maximum number of log lines that should be kept in memory.
    /// </summary>
    /// <param name="maxEntries"> The desired maximum number of log entries. </param>
    public void SetMaxEntries(int maxEntries)
    {
        var clamped = ApplicationSettings.ClampLogMaxLines(maxEntries);
        var updated = false;

        lock (_syncRoot)
        {
            if (_maxEntries == clamped)
            {
                return;
            }

            _maxEntries = clamped;
            updated = true;
            _ = TrimExcessEntries_NoLock();
        }

        if (updated)
        {
            OnLogsChanged();
        }
    }

    private readonly List<LogEntry> _entries = [];
    private readonly object _syncRoot = new();
    private LogLevel _currentLevel = Config.DefaultLogLevel;
    private int _maxEntries = ApplicationSettings.LogMaxLinesDefault;

    private bool IsLevelEnabled_NoLock(LogLevel level)
    {
        if (level == LogLevel.None)
        {
            return false;
        }

        if (_currentLevel == LogLevel.None)
        {
            return false;
        }

        return level <= _currentLevel;
    }

    private void OnLogsChanged() => LogsChanged?.Invoke(this, EventArgs.Empty);

    private bool TrimExcessEntries_NoLock()
    {
        if (_entries.Count <= _maxEntries)
        {
            return false;
        }

        var excess = _entries.Count - _maxEntries;
        var blockSize = Math.Max(1, (int)Math.Ceiling(_maxEntries * 0.1));
        var removalCount = Math.Max(excess, blockSize);
        removalCount = Math.Min(removalCount, _entries.Count);
        _entries.RemoveRange(0, removalCount);
        return true;
    }
}
