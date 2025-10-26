using System.Globalization;

namespace Toolbox.Settings;

/// <summary>
///     Provides constants that define how camera streaming and capture should behave.
/// </summary>
public static class CameraSettings
{
    public const string CaptureMimeType = "image/jpeg";

    public const double CaptureQuality = 0.75;

    public const int CaptureTargetHeight = 800;

    // Capture configuration
    public const int CaptureTargetWidth = 600;

    public const string StreamFacingMode = "environment";

    public const int StreamIdealFrameRate = 15;

    public const int StreamIdealHeight = 480;

    // Stream constraints
    public const int StreamIdealWidth = 640;

    public const int StreamMaxHeight = 720;
    public const int StreamMaxWidth = 960;

    public static string BuildCaptureErrorMessage(string error) => string.Format(CultureInfo.CurrentCulture, "Fehler beim Aufnehmen des Fotos: {0}", error);

    // Messaging
    public static string BuildUnavailableMessage() => "Die Kamera konnte nicht initialisiert werden. Bitte wähle eine Datei aus.";
}
