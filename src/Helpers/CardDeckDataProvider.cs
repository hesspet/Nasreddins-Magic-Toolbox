using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Toolbox.Models;

namespace Toolbox.Helpers;

public sealed class CardDeckDataProvider
{
    private readonly IndexedDbHelper _dbHelper;
    private readonly Dictionary<string, string> _deckDisplayNames = new(StringComparer.OrdinalIgnoreCase);

    public CardDeckDataProvider(IndexedDbHelper dbHelper)
    {
        _dbHelper = dbHelper ?? throw new ArgumentNullException(nameof(dbHelper));
    }

    public async Task<IReadOnlyList<DeckOption>> LoadDeckOptionsAsync()
    {
        await _dbHelper.InitializeAsync();

        var decks = await _dbHelper.GetAllDecksAsync().ConfigureAwait(false);
        var orderedOptions = decks.Where(deck => !string.IsNullOrWhiteSpace(deck?.Id))
                                  .Select(deck =>
                                  {
                                      var deckId = deck!.Id!;
                                      var name = string.IsNullOrWhiteSpace(deck.Name) ? deckId : deck.Name!;
                                      return new DeckOption(deckId, CardDeckUtilities.CreateDeckDisplayName(name));
                                  })
                                  .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
                                  .ToList();

        _deckDisplayNames.Clear();
        foreach (var option in orderedOptions)
        {
            _deckDisplayNames[option.DeckId] = option.DisplayName;
        }

        return orderedOptions;
    }

    public async Task<DeckCards> LoadDeckCardsAsync(string deckId)
    {
        if (string.IsNullOrWhiteSpace(deckId))
        {
            throw new ArgumentException("Deck identifier is required.", nameof(deckId));
        }

        var deckDisplayName = GetDeckDisplayName(deckId);
        var cards = await _dbHelper.GetCardsByDeckAsync(deckId).ConfigureAwait(false);
        var orderedCards = cards.OrderBy(card => CardDeckUtilities.CreateDisplayName(deckDisplayName, card.Id ?? string.Empty), StringComparer.OrdinalIgnoreCase)
                                .ToList();
        return new DeckCards(deckId, deckDisplayName, orderedCards);
    }

    public string GetDeckDisplayName(string deckId)
    {
        if (_deckDisplayNames.TryGetValue(deckId, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return CardDeckUtilities.CreateDeckDisplayName(deckId);
    }
}
