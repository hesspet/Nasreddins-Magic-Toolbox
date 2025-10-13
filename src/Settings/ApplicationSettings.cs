using System;

namespace Toolbox.Settings;

/// <summary>
///     Provides strongly typed information about application settings stored in local storage.
/// </summary>
public static class ApplicationSettings
{
    public const int SplashScreenDurationDefaultSeconds = 5;
    public const string SplashScreenDurationKey = "SplashScreenDurationSeconds";
    public static int[] SplashScreenDurationOptions { get; } = [0, 5, 10, 60];

    public const bool CheckForUpdatesOnStartupDefault = true;
    public const string CheckForUpdatesOnStartupKey = "CheckForUpdatesOnStartup";

    public const int CardScalePercentDefault = 100;
    public const int CardScalePercentMinimum = 20;
    public const int CardScalePercentMaximum = 100;
    public const string CardScalePercentKey = "CardScalePercent";

    public static int ClampCardScalePercent(int value) => Math.Clamp(value, CardScalePercentMinimum, CardScalePercentMaximum);
}
