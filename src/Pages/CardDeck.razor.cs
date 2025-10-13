using Markdig;
using Toolbox.Models;
using Toolbox.Resources;

namespace Toolbox.Pages
{
    public partial class CardDeck
    {
        protected override async Task OnInitializedAsync()
        {
            try
            {
                isLoadingDecks = true;
                await LoadDecksAsync();
            }
            finally
            {
                isLoadingDecks = false;
            }
        }

        private readonly Dictionary<string, string> cardDescriptionCache = new(StringComparer.OrdinalIgnoreCase);
        private TarotDeckCollection deckCollection = TarotDeckCollection.Empty;
        private string? descriptionError;
        private bool isLoadingDecks = true;
        private string searchTerm = string.Empty;
        private TarotCardInfo? selectedCard;
        private string? selectedCardDescriptionHtml;
        private int selectedCardIndex = -1;
        private string selectedDeck = string.Empty;
        private TarotDeckCollection DeckCollection => deckCollection;
        private IEnumerable<TarotDeckOption> DeckOptions => DeckCollection.Options;
        private bool HasSearched => !string.IsNullOrWhiteSpace(searchTerm);

        private string SearchTerm
        {
            get => searchTerm;
            set
            {
                if (searchTerm == value)
                {
                    return;
                }

                searchTerm = value ?? string.Empty;
                UpdateSelection();
            }
        }

        private string SelectedDeck
        {
            get => selectedDeck;
            set
            {
                var newValue = string.IsNullOrWhiteSpace(value) ? DeckCollection.DefaultDeck : value;
                if (!DeckCollection.ContainsDeck(newValue))
                {
                    newValue = DeckCollection.DefaultDeck;
                }
                if (selectedDeck == newValue)
                {
                    return;
                }

                selectedDeck = newValue;
                selectedCardIndex = -1;
                UpdateSelection();
            }
        }

        private static TarotDeckCollection BuildDeckCollection(IReadOnlyList<Deck> decks, IReadOnlyList<Spielkarte> cards)
        {
            if (decks is null)
            {
                throw new ArgumentNullException(nameof(decks));
            }

            if (cards is null)
            {
                throw new ArgumentNullException(nameof(cards));
            }

            var cardGroups = cards.GroupBy(card => card.DeckId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                                  .ToDictionary(group => group.Key,
                                                group => group.ToList(),
                                                StringComparer.OrdinalIgnoreCase);

            var deckNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var deck in decks)
            {
                if (string.IsNullOrWhiteSpace(deck?.Id))
                {
                    continue;
                }

                var deckId = deck.Id;
                var deckName = string.IsNullOrWhiteSpace(deck.Name) ? deckId : deck.Name;
                deckNames[deckId] = deckName;
            }

            foreach (var deckId in cardGroups.Keys)
            {
                if (!deckNames.ContainsKey(deckId))
                {
                    deckNames[deckId] = deckId;
                }
            }

            var orderedDecks = deckNames.Select(pair => new DeckMetadata(pair.Key, pair.Value))
                                         .OrderBy(metadata => CreateDeckDisplayName(metadata.DisplayName), StringComparer.OrdinalIgnoreCase)
                                         .ToList();

            var cardsByDeck = new Dictionary<string, IReadOnlyList<TarotCardInfo>>(StringComparer.OrdinalIgnoreCase);
            var deckOptions = new List<TarotDeckOption>(orderedDecks.Count);

            foreach (var deck in orderedDecks)
            {
                deckOptions.Add(new TarotDeckOption(deck.DeckId, CreateDeckDisplayName(deck.DisplayName)));

                if (!cardGroups.TryGetValue(deck.DeckId, out var deckCards))
                {
                    cardsByDeck[deck.DeckId] = Array.Empty<TarotCardInfo>();
                    continue;
                }

                var convertedCards = deckCards.Select(card => CreateCardInfo(deck.DeckId, deck.DisplayName, card))
                                              .OrderBy(card => card.DisplayName, StringComparer.OrdinalIgnoreCase)
                                              .ToList();

                cardsByDeck[deck.DeckId] = convertedCards;
            }

            var defaultDeck = deckOptions.FirstOrDefault()?.DeckId ?? string.Empty;

            return new TarotDeckCollection(cardsByDeck, deckOptions, defaultDeck);
        }

        private static TarotCardInfo CreateCardInfo(string deckId, string deckName, Spielkarte card)
        {
            var cardId = card?.Id ?? string.Empty;
            var displayName = CreateDisplayName(deckName, cardId);
            var key = CreateKey(deckId, cardId);
            var imageDataUrl = CreateImageDataUrl(card?.Image);
            var description = card?.Description ?? string.Empty;

            return new TarotCardInfo(displayName, deckId, cardId, key, imageDataUrl, description);
        }

        private static string CreateDeckDisplayName(string deckName) => deckName.Replace('_', ' ');

        private static string CreateDisplayName(string deckName, string cardId)
        {
            var friendlyDeckName = deckName.Replace('_', ' ');
            var cardName = cardId.Replace('_', ' ');
            return $"{friendlyDeckName}: {cardName}";
        }

        private static string CreateImageDataUrl(byte[]? imageBytes)
        {
            if (imageBytes is null || imageBytes.Length == 0)
            {
                return string.Empty;
            }

            var base64 = Convert.ToBase64String(imageBytes);
            return $"data:image/jpeg;base64,{base64}";
        }

        private static string CreateKey(string deckName, string cardId)
        {
            var composite = string.Concat(deckName, "_", cardId);
            var filtered = composite.Where(char.IsLetterOrDigit);
            return string.Concat(filtered);
        }

        private static int FindMatchingCardIndex(IReadOnlyList<TarotCardInfo> candidates, string normalizedSearch)
        {
            for (var index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].ComparisonKey.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static string NormalizeForComparison(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var filtered = value.Where(char.IsLetterOrDigit)
                                .Select(char.ToLowerInvariant);
            return string.Concat(filtered);
        }

        private void ClearSelectedCardDescription()
        {
            selectedCardDescriptionHtml = null;
            descriptionError = null;
        }

        private async Task LoadDecksAsync()
        {
            await DbHelper.InitializeAsync();

            var decks = await DbHelper.GetAllDecksAsync();
            var cards = await DbHelper.GetAllCardsAsync();

            deckCollection = BuildDeckCollection(decks, cards);

            cardDescriptionCache.Clear();
            selectedCard = null;
            selectedCardIndex = -1;
            selectedCardDescriptionHtml = null;
            descriptionError = null;

            var preferredDeck = string.IsNullOrWhiteSpace(selectedDeck) ? DeckCollection.DefaultDeck : selectedDeck;
            if (!DeckCollection.ContainsDeck(preferredDeck))
            {
                preferredDeck = DeckCollection.DefaultDeck;
            }

            selectedDeck = preferredDeck;
            UpdateSelection();
        }

        private void PrepareCardDescription(TarotCardInfo card)
        {
            var cacheKey = card.Key;
            if (cardDescriptionCache.TryGetValue(cacheKey, out var cachedHtml))
            {
                selectedCardDescriptionHtml = cachedHtml;
                descriptionError = null;
                return;
            }

            descriptionError = null;
            selectedCardDescriptionHtml = null;
            try
            {
                var html = Markdown.ToHtml(card.Description ?? string.Empty);
                cardDescriptionCache[cacheKey] = html;
                selectedCardDescriptionHtml = html;
                descriptionError = null;
            }
            catch
            {
                descriptionError = DisplayTexts.TarotDescriptionLoadError;
                selectedCardDescriptionHtml = null;
            }
        }

        private void ShowCardAtIndex(int index, IReadOnlyList<TarotCardInfo>? candidates = null)
        {
            candidates ??= DeckCollection.GetCards(string.IsNullOrWhiteSpace(selectedDeck) ? DeckCollection.DefaultDeck : selectedDeck);
            if (candidates.Count == 0)
            {
                selectedCard = null;
                selectedCardIndex = -1;
                ClearSelectedCardDescription();
                return;
            }

            var normalizedIndex = ((index % candidates.Count) + candidates.Count) % candidates.Count;
            selectedCardIndex = normalizedIndex;
            selectedCard = candidates[normalizedIndex];
            PrepareCardDescription(selectedCard);
        }

        private void ShowNextCard()
        {
            var candidates = DeckCollection.GetCards(string.IsNullOrWhiteSpace(selectedDeck) ? DeckCollection.DefaultDeck : selectedDeck);
            if (candidates.Count == 0)
            {
                return;
            }

            var nextIndex = selectedCardIndex >= 0 ? selectedCardIndex + 1 : 0;
            ShowCardAtIndex(nextIndex, candidates);
        }

        private void ShowPreviousCard()
        {
            var candidates = DeckCollection.GetCards(string.IsNullOrWhiteSpace(selectedDeck) ? DeckCollection.DefaultDeck : selectedDeck);
            if (candidates.Count == 0)
            {
                return;
            }

            var previousIndex = selectedCardIndex >= 0 ? selectedCardIndex - 1 : candidates.Count - 1;
            ShowCardAtIndex(previousIndex, candidates);
        }

        private void UpdateSelection()
        {
            if (DeckCollection.IsEmpty)
            {
                selectedCard = null;
                selectedCardIndex = -1;
                ClearSelectedCardDescription();
                return;
            }

            var deckToSearch = string.IsNullOrWhiteSpace(selectedDeck) ? DeckCollection.DefaultDeck : selectedDeck;
            var candidates = DeckCollection.GetCards(deckToSearch);

            if (candidates.Count == 0)
            {
                selectedCard = null;
                selectedCardIndex = -1;
                ClearSelectedCardDescription();
                return;
            }

            var normalized = NormalizeForComparison(searchTerm);
            if (normalized.Length == 0)
            {
                if (selectedCardIndex < 0 || selectedCardIndex >= candidates.Count)
                {
                    ShowCardAtIndex(0, candidates);
                }
                else
                {
                    ShowCardAtIndex(selectedCardIndex, candidates);
                }

                return;
            }

            var matchingIndex = FindMatchingCardIndex(candidates, normalized);
            if (matchingIndex >= 0)
            {
                ShowCardAtIndex(matchingIndex, candidates);
            }
            else
            {
                selectedCard = null;
                selectedCardIndex = -1;
                ClearSelectedCardDescription();
            }
        }

        private sealed record TarotCardInfo(string DisplayName, string DeckId, string CardId, string Key, string ImageDataUrl, string Description)
        {
            public string ComparisonKey => NormalizeForComparison(Key);
        }

        private sealed record TarotDeckCollection(IReadOnlyDictionary<string, IReadOnlyList<TarotCardInfo>> CardsByDeck, IReadOnlyList<TarotDeckOption> Options, string DefaultDeck)
        {
            public static TarotDeckCollection Empty { get; } = new(new Dictionary<string, IReadOnlyList<TarotCardInfo>>(StringComparer.OrdinalIgnoreCase), Array.Empty<TarotDeckOption>(), string.Empty);

            public bool IsEmpty => CardsByDeck.Count == 0;

            public IReadOnlyList<TarotCardInfo> GetCards(string deckName)
            {
                if (CardsByDeck.TryGetValue(deckName, out var cards))
                {
                    return cards;
                }

                return Array.Empty<TarotCardInfo>();
            }

            public bool ContainsDeck(string deckName) => CardsByDeck.ContainsKey(deckName);
        }

        private sealed record TarotDeckOption(string DeckId, string DisplayName);

        private sealed record DeckMetadata(string DeckId, string DisplayName);
    }
}
