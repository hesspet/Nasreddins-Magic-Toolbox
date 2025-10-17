namespace Toolbox.Models
{
    /// <summary>
    /// Represents a selectable tarot deck option.
    /// </summary>
    /// <param name="DeckId">The unique deck identifier.</param>
    /// <param name="DisplayName">The display name shown to the user.</param>
    public sealed record DeckOption(string DeckId, string DisplayName);
}

