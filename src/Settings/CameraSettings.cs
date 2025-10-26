using System.Globalization;

namespace Toolbox.Settings;

/// <summary>
///     Bündelt alle Kamera-Einstellungen, die sowohl für den Livestream als auch für den eigentlichen
///     Foto-Schnappschuss verwendet werden. Die Konstanten dienen als zentrale Referenz für
///     JavaScript-Interop und Komponenten, damit Änderungen an einer Stelle konsistent wirken.
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

    /// <summary>
    ///     Erstellt eine lokalisierte Fehlermeldung, falls der Aufnahmevorgang scheitert.
    /// </summary>
    public static string BuildCaptureErrorMessage(string error) => string.Format(CultureInfo.CurrentCulture, "Fehler beim Aufnehmen des Fotos: {0}", error);

    // Messaging
    /// <summary>
    ///     Liefert einen Hinweistext, wenn keine Kamera verfügbar ist und eine Alternative angeboten
    ///     werden soll.
    /// </summary>
    public static string BuildUnavailableMessage() => "Die Kamera konnte nicht initialisiert werden. Bitte wähle eine Datei aus.";
}
