using System;
using System.Collections.Generic;
using System.Linq;
using Toolbox.Models;

namespace Toolbox.Helpers;

public static class CardDeckUtilities
{
    public static DeckCardInfo CreateCardInfo(string deckId, string deckName, Spielkarte card)
    {
        if (card is null)
        {
            throw new ArgumentNullException(nameof(card));
        }

        var cardId = card.Id ?? string.Empty;
        var displayName = CreateDisplayName(deckName, cardId);
        var key = CreateKey(deckId, cardId);
        var imageDataUrl = CreateImageDataUrl(card.Image);
        var description = card.Description ?? string.Empty;

        return new DeckCardInfo(displayName, deckId, cardId, key, imageDataUrl, description);
    }

    public static string CreateDeckDisplayName(string deckName) => (deckName ?? string.Empty).Replace('_', ' ');

    public static string CreateDisplayName(string deckName, string cardId)
    {
        var friendlyDeckName = (deckName ?? string.Empty).Replace('_', ' ');
        var cardName = (cardId ?? string.Empty).Replace('_', ' ');
        return $"{friendlyDeckName}: {cardName}";
    }

    public static string CreateImageDataUrl(byte[]? imageBytes)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            return string.Empty;
        }

        var base64 = Convert.ToBase64String(imageBytes);
        return $"data:image/jpeg;base64,{base64}";
    }

    public static string CreateKey(string deckName, string cardId)
    {
        var composite = string.Concat(deckName ?? string.Empty, "_", cardId ?? string.Empty);
        var filtered = composite.Where(char.IsLetterOrDigit);
        return string.Concat(filtered);
    }

    public static int FindMatchingCardIndex(IReadOnlyList<Spielkarte> candidates, string deckId, string normalizedSearch)
    {
        if (candidates is null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var key = NormalizeForComparison(CreateKey(deckId, candidate.Id ?? string.Empty));
            if (key.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    public static string NormalizeForComparison(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var filtered = value.Where(char.IsLetterOrDigit)
                            .Select(char.ToLowerInvariant);
        return string.Concat(filtered);
    }
}
