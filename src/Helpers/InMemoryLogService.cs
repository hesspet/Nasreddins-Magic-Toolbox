using System;
using System.Collections.Generic;
using System.Linq;
using Toolbox.Models;
using Toolbox.Settings;

namespace Toolbox.Helpers;

/// <summary>
///     Provides an in-memory application log that can be displayed inside the PWA.
/// </summary>
public sealed class InMemoryLogService
{
    private readonly object _syncRoot = new();
    private readonly List<LogEntry> _entries = [];
    private int _maxEntries = ApplicationSettings.LogMaxLinesDefault;

    /// <summary>
    ///     Occurs when log entries have changed.
    /// </summary>
    public event EventHandler? LogsChanged;

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
    ///     Adds the specified message to the log. Multi-line messages are split into individual log entries.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void Log(string message)
    {
        if (message is null)
        {
            return;
        }

        var normalizedMessage = message.ReplaceLineEndings("\n");
        var lines = normalizedMessage.Split('\n');
        var hasChanges = false;

        lock (_syncRoot)
        {
            foreach (var line in lines)
            {
                _entries.Add(new LogEntry(DateTimeOffset.Now, line));
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
    ///     Configures the maximum number of log lines that should be kept in memory.
    /// </summary>
    /// <param name="maxEntries">The desired maximum number of log entries.</param>
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

    /// <summary>
    ///     Gets a snapshot of all log entries.
    /// </summary>
    /// <returns>A read-only list of the current log entries.</returns>
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

    private void OnLogsChanged() => LogsChanged?.Invoke(this, EventArgs.Empty);
}
