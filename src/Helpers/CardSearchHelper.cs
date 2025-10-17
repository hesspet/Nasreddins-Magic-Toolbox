using System;
using System.Collections.Generic;
using System.Linq;
using Toolbox.Models;

namespace Toolbox.Helpers;

/// <summary>
/// Provides shared helper methods for card search and formatting.
/// </summary>
public static class CardSearchHelper
{
    public static string CreateDeckDisplayName(string deckName) =>
        (deckName ?? string.Empty).Replace('_', ' ');

    public static string CreateDisplayName(string deckName, string cardId)
    {
        var friendlyDeckName = CreateDeckDisplayName(deckName);
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

    public static string CreateKey(string deckId, string cardId)
    {
        var composite = string.Concat(deckId ?? string.Empty, "_", cardId ?? string.Empty);
        var filtered = composite.Where(char.IsLetterOrDigit);
        return string.Concat(filtered);
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

    public static IReadOnlyList<Spielkarte> FindMatchingCards(IReadOnlyList<Spielkarte> candidates, string deckId, string normalizedSearch)
    {
        if (candidates is null || candidates.Count == 0)
        {
            return Array.Empty<Spielkarte>();
        }

        if (string.IsNullOrEmpty(normalizedSearch))
        {
            return candidates.Count > 0 ? new[] { candidates[0] } : Array.Empty<Spielkarte>();
        }

        var matches = new List<Spielkarte>();

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var key = NormalizeForComparison(CreateKey(deckId, candidate?.Id ?? string.Empty));
            var descriptionKey = NormalizeForComparison(candidate?.Description ?? string.Empty);

            if (key.Contains(normalizedSearch, StringComparison.Ordinal)
                || descriptionKey.Contains(normalizedSearch, StringComparison.Ordinal))
            {
                matches.Add(candidate);
            }
        }

        return matches;
    }

    public static int FindFirstMatchingCardIndex(IReadOnlyList<Spielkarte> candidates, string deckId, string normalizedSearch)
    {
        if (candidates is null || candidates.Count == 0)
        {
            return -1;
        }

        if (string.IsNullOrEmpty(normalizedSearch))
        {
            return 0;
        }

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var key = NormalizeForComparison(CreateKey(deckId, candidate?.Id ?? string.Empty));
            var descriptionKey = NormalizeForComparison(candidate?.Description ?? string.Empty);

            if (key.Contains(normalizedSearch, StringComparison.Ordinal)
                || descriptionKey.Contains(normalizedSearch, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
