namespace Toolbox.Models;

/// <summary>
///     Provides helper methods for working with <see cref="LogLevel"/> values.
/// </summary>
public static class LogLevelExtensions
{
    /// <summary>
    ///     Determines whether the specified <paramref name="level"/> is a defined enum value.
    /// </summary>
    public static bool IsDefined(LogLevel level) => Enum.IsDefined(typeof(LogLevel), level);

    /// <summary>
    ///     Returns an invariant display string for the provided <paramref name="level"/>.
    /// </summary>
    public static string ToDisplayString(this LogLevel level) => level switch
    {
        LogLevel.None => "NONE",
        LogLevel.Error => "ERROR",
        LogLevel.Warn => "WARN",
        LogLevel.Info => "INFO",
        LogLevel.Debug => "DEBUG",
        _ => level.ToString().ToUpperInvariant(),
    };

    /// <summary>
    ///     Tries to parse a <see cref="LogLevel"/> from the provided <paramref name="value"/>.
    /// </summary>
    public static bool TryParse(string? value, out LogLevel level)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            level = LogLevel.Info;
            return false;
        }

        if (Enum.TryParse(value, ignoreCase: true, out level) && IsDefined(level))
        {
            return true;
        }

        level = LogLevel.Info;
        return false;
    }
}
