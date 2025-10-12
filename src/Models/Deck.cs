namespace Toolbox.Models;

/// <summary>
/// Represents a deck of cards stored in IndexedDB.
/// </summary>
public sealed class Deck
{
    /// <summary>
    /// Gets or sets the unique identifier of the deck.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the deck.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
