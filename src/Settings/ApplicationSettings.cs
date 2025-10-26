using Toolbox.Models;
using LogLevel = Toolbox.Models.LogLevel;

namespace Toolbox.Settings;

/// <summary>
///     Provides strongly typed information about application settings stored in local storage.
/// </summary>
public static class ApplicationSettings
{
    public const string CardReadingChatGptApiUrlDefault = "https://nasreddins-magic.de/api/chat";
    public const string CardReadingChatGptApiUrlKey = "CardReadingChatGptApiUrl";
    public const string CardReadingGptApiKeyDefault = "";
    public const string CardReadingGptApiKeyKey = "CardReadingGptApiKey";
    public const int CardScalePercentDefault = 100;
    public const string CardScalePercentKey = "CardScalePercent";
    public const int CardScalePercentMaximum = 100;
    public const int CardScalePercentMinimum = 20;
    public const bool CheckForUpdatesOnStartupDefault = true;
    public const string CheckForUpdatesOnStartupKey = "CheckForUpdatesOnStartup";
    public const LogLevel LogLevelDefault = Config.DefaultLogLevel;
    public const string LogLevelKey = "LogLevel";
    public const int LogMaxLinesDefault = 1000;
    public const string LogMaxLinesKey = "LogMaxLines";
    public const int LogMaxLinesMaximum = 10000;
    public const int LogMaxLinesMinimum = 100;
    public const int SearchAutoClearDelayDefaultSeconds = 5;
    public const int SearchAutoClearDelayMaximumSeconds = 60;
    public const int SearchAutoClearDelayMinimumSeconds = 0;
    public const string SearchAutoClearDelaySecondsKey = "SearchAutoClearDelaySeconds";
    public const int SplashScreenDurationDefaultSeconds = 5;
    public const string SplashScreenDurationKey = "SplashScreenDurationSeconds";
    public const ThemePreference ThemePreferenceDefault = ThemePreference.Light;
    public const string ThemePreferenceKey = "ThemePreference";
    public static LogLevel[] LogLevelOptions { get; } = Enum.GetValues<LogLevel>();
    public static int[] SplashScreenDurationOptions { get; } = [0, 5, 10, 60];

    public static int ClampCardScalePercent(int value) => Math.Clamp(value, CardScalePercentMinimum, CardScalePercentMaximum);

    public static int ClampLogMaxLines(int value) => Math.Clamp(value, LogMaxLinesMinimum, LogMaxLinesMaximum);

    public static int ClampSearchAutoClearDelaySeconds(int value) => Math.Clamp(value, SearchAutoClearDelayMinimumSeconds, SearchAutoClearDelayMaximumSeconds);

    public static bool IsValidLogLevel(LogLevel level) => LogLevelExtensions.IsDefined(level);

    public static bool TryParseLogLevel(string? value, out LogLevel level) => LogLevelExtensions.TryParse(value, out level);
}
