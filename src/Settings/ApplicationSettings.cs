namespace Toolbox.Settings;

/// <summary>
///     Provides strongly typed information about application settings stored in local storage.
/// </summary>
public static class ApplicationSettings
{
    public const int SplashScreenDurationDefaultSeconds = 5;
    public const string SplashScreenDurationKey = "SplashScreenDurationSeconds";
    public static int[] SplashScreenDurationOptions { get; } = [0, 5, 10, 60];
    public const string WebDavUrlKey = "WebDavUrl";
    public const string WebDavUsernameKey = "WebDavUsername";
    public const string WebDavPasswordKey = "WebDavPassword";
}
