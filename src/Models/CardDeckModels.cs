namespace Toolbox.Models;

public sealed record DeckOption(string DeckId, string DisplayName);

public sealed record DeckCardInfo(
    string DisplayName,
    string DeckId,
    string CardId,
    string Key,
    string ImageDataUrl,
    string Description);

public sealed record DeckCards(string DeckId, string DeckDisplayName, IReadOnlyList<Spielkarte> Cards);
