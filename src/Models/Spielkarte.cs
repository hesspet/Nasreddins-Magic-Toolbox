namespace Toolbox.Models;

/// <summary>
///     Represents a single playing card stored in IndexedDB.
/// </summary>
public sealed class Spielkarte
{
    /// <summary>
    ///     Gets or sets the identifier of the deck the card belongs to.
    /// </summary>
    public string DeckId { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the Markdown description of the card.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the unique identifier of the card.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the binary image of the card.
    /// </summary>
    public byte[] Image { get; set; } = Array.Empty<byte>();
}
