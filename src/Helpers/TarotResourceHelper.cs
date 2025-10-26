using Toolbox.Resources;

namespace Toolbox.Helpers;

/// <summary>
///     Provides access to embedded tarot resources.
/// </summary>
public static class TarotResourceHelper
{
    /// <summary>
    ///     Gets the data URL for the default tarot card back image.
    /// </summary>
    public static string CardBackImageDataUrl => cardBackDataUrl.Value;

    private const string CardBackResourceName = "Toolbox.Resources.Images.TarotDeck_Wikipedia.Back.jpg";

    private static readonly Lazy<string> cardBackDataUrl = new(LoadCardBackImage, LazyThreadSafetyMode.ExecutionAndPublication);

    private static string LoadCardBackImage()
    {
        var assembly = typeof(DisplayTexts).Assembly;

        using var stream = assembly.GetManifestResourceStream(CardBackResourceName)
            ?? throw new InvalidOperationException(FormattableString.Invariant($"Resource '{CardBackResourceName}' was not found."));

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);

        var base64 = Convert.ToBase64String(memoryStream.ToArray());
        return FormattableString.Invariant($"data:image/jpeg;base64,{base64}");
    }
}
