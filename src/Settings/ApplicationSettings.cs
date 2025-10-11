namespace Toolbox.Settings;

/// <summary>
/// Provides strongly typed information about application settings stored in local storage.
/// </summary>
public static class ApplicationSettings
{
    public const string SplashScreenDurationKey = "SplashScreenDurationSeconds";
    public const int SplashScreenDurationDefaultSeconds = 5;

    public static int[] SplashScreenDurationOptions { get; } = new[] { 0, 5, 10, 60 };
}
