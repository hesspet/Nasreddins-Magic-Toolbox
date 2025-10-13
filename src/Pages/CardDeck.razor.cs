using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Toolbox.Models;
using Toolbox.Layout;
using Toolbox.Resources;
using Toolbox.Settings;

namespace Toolbox.Pages
{
    public partial class CardDeck : IAsyncDisposable
    {
        private const string CardFigureElementId = "cardDeckFigure";
        private const string CardDescriptionElementId = "cardDeckDescription";
        private const double SwipeThreshold = 40d;

        private ElementReference cardFigureRef;
        private ElementReference cardDescriptionRef;
        private ElementReference searchInputRef;

        private IJSObjectReference? cardDeckModule;
        private bool cardObserverNeedsRefresh;
        private bool shouldRestoreSearchInputFocus;

        [CascadingParameter]
        private MainLayout? Layout { get; set; }

        protected override void OnInitialized()
        {
            Layout?.UpdateCurrentPageTitle(DisplayTexts.TarotPageTitle);
        }

        protected override async Task OnInitializedAsync()
        {
            try
            {
                isLoadingDecks = true;
                await Task.WhenAll(LoadDecksAsync(), LoadCardScaleAsync(), LoadSearchAutoClearDelayAsync());
            }
            finally
            {
                isLoadingDecks = false;
            }
        }

        private readonly Dictionary<string, string> deckDisplayNames = new(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<DeckOption> deckOptions = Array.Empty<DeckOption>();
        private string? descriptionError;
        private bool isLoadingDecks = true;
        private bool isLoadingCards;
        private string searchTerm = string.Empty;
        private DeckCardInfo? selectedCard;
        private string? selectedCardDescriptionHtml;
        private int selectedCardIndex = -1;
        private string selectedDeck = string.Empty;
        private DeckCards? cachedDeckCards;
        private int cardScalePercent = ApplicationSettings.CardScalePercentDefault;
        private int searchAutoClearDelaySeconds = ApplicationSettings.SearchAutoClearDelayDefaultSeconds;
        private bool isCardFullscreen;
        private double? swipeStartX;
        private bool isSwipeTracking;
        private CancellationTokenSource? searchClearCancellation;

        private IEnumerable<DeckOption> DeckOptions => deckOptions;
        private bool HasSearched => !string.IsNullOrWhiteSpace(searchTerm);
        private bool IsDeckSelected => !string.IsNullOrWhiteSpace(selectedDeck);
        private bool IsSearchEnabled => IsDeckSelected && !isLoadingCards;
        private bool CanNavigateCards => selectedCard is not null && !isLoadingCards;
        private string CardFigureStyle => FormattableString.Invariant($"--deck-card-scale: {ConvertPercentToScaleFactor(cardScalePercent):0.##};");
        private string CardFigureFullscreenValue => isCardFullscreen ? "true" : "false";

        private string SearchTerm
        {
            get => searchTerm;
            set
            {
                var newValue = value ?? string.Empty;
                if (searchTerm == newValue)
                {
                    return;
                }

                searchTerm = newValue;
                _ = ScheduleSelectionUpdateAsync();
                RestartSearchClearTimer();
            }
        }

        private string SelectedDeck
        {
            get => selectedDeck;
            set
            {
                var newValue = value ?? string.Empty;
                if (selectedDeck == newValue)
                {
                    return;
                }

                selectedDeck = newValue;
                selectedCardIndex = -1;
                ClearCurrentCard();
                cachedDeckCards = null;
                _ = ScheduleSelectionUpdateAsync();
            }
        }

        private Task ScheduleSelectionUpdateAsync() => InvokeAsync(UpdateSelectionAsync);

        private async Task UpdateSelectionAsync()
        {
            if (!IsDeckSelected)
            {
                isLoadingCards = false;
                ClearCurrentCard();
                StateHasChanged();
                return;
            }

            var requestedDeck = selectedDeck;
            var requestedSearch = searchTerm;

            isLoadingCards = true;
            StateHasChanged();

            try
            {
                var deckCards = await GetDeckCardsAsync(requestedDeck, allowReload: true);
                if (!string.Equals(requestedDeck, selectedDeck, StringComparison.OrdinalIgnoreCase) || requestedSearch != searchTerm)
                {
                    return;
                }

                if (deckCards is null || deckCards.Cards.Count == 0)
                {
                    ClearCurrentCard();
                    return;
                }

                var normalized = NormalizeForComparison(requestedSearch);
                var index = normalized.Length == 0
                    ? (selectedCardIndex >= 0 && selectedCardIndex < deckCards.Cards.Count ? selectedCardIndex : 0)
                    : FindMatchingCardIndex(deckCards.Cards, deckCards.DeckId, normalized);

                if (normalized.Length > 0 && index < 0)
                {
                    ClearCurrentCard();
                    return;
                }

                if (index < 0)
                {
                    index = 0;
                }

                ShowCardAtIndex(index, deckCards);
            }
            finally
            {
                isLoadingCards = false;

                if (string.Equals(requestedDeck, selectedDeck, StringComparison.OrdinalIgnoreCase))
                {
                    shouldRestoreSearchInputFocus = true;
                }

                StateHasChanged();
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (shouldRestoreSearchInputFocus)
            {
                shouldRestoreSearchInputFocus = false;
                await FocusSearchInputAsync();
            }

            if (!cardObserverNeedsRefresh)
            {
                return;
            }

            cardObserverNeedsRefresh = false;

            if (selectedCard is not null)
            {
                var module = await GetModuleAsync();
                await module.InvokeVoidAsync("observeCardVisibility", cardFigureRef);
            }
            else if (cardDeckModule is not null)
            {
                await cardDeckModule.InvokeVoidAsync("disconnectCardVisibility", cardFigureRef);
            }
        }

        private async Task LoadDecksAsync()
        {
            await DbHelper.InitializeAsync();

            var decks = await DbHelper.GetAllDecksAsync();
            var orderedOptions = decks.Where(deck => !string.IsNullOrWhiteSpace(deck?.Id))
                                      .Select(deck =>
                                      {
                                          var deckId = deck!.Id!;
                                          var name = string.IsNullOrWhiteSpace(deck.Name) ? deckId : deck.Name;
                                          return new DeckOption(deckId, CreateDeckDisplayName(name));
                                      })
                                      .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
                                      .ToList();

            deckOptions = orderedOptions;
            deckDisplayNames.Clear();
            foreach (var option in orderedOptions)
            {
                deckDisplayNames[option.DeckId] = option.DisplayName;
            }

            selectedDeck = string.Empty;
            searchTerm = string.Empty;
            CancelSearchClearTimer();
            ClearCurrentCard();
        }

        private async Task<DeckCards?> GetDeckCardsAsync(string deckId, bool allowReload)
        {
            if (cachedDeckCards is { } cached && string.Equals(cached.DeckId, deckId, StringComparison.OrdinalIgnoreCase))
            {
                return cached;
            }

            if (!allowReload)
            {
                return null;
            }

            var loaded = await FetchDeckCardsAsync(deckId);
            cachedDeckCards = loaded;
            return loaded;
        }

        private async Task<DeckCards> FetchDeckCardsAsync(string deckId)
        {
            var deckDisplayName = GetDeckDisplayName(deckId);
            var cards = await DbHelper.GetCardsByDeckAsync(deckId);
            var orderedCards = cards.OrderBy(card => CreateDisplayName(deckDisplayName, card.Id), StringComparer.OrdinalIgnoreCase)
                                    .ToList();
            return new DeckCards(deckId, deckDisplayName, orderedCards);
        }

        private string GetDeckDisplayName(string deckId)
        {
            if (deckDisplayNames.TryGetValue(deckId, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            return CreateDeckDisplayName(deckId);
        }

        private void ShowCardAtIndex(int index, DeckCards deckCards)
        {
            var candidates = deckCards.Cards;
            if (candidates.Count == 0)
            {
                ClearCurrentCard();
                return;
            }

            var normalizedIndex = ((index % candidates.Count) + candidates.Count) % candidates.Count;
            selectedCardIndex = normalizedIndex;
            var card = candidates[normalizedIndex];
            selectedCard = CreateCardInfo(deckCards.DeckId, deckCards.DeckDisplayName, card);
            PrepareCardDescription(selectedCard);
            isCardFullscreen = false;
            cardObserverNeedsRefresh = true;
        }

        private async Task ShowNextCardAsync()
        {
            if (!IsDeckSelected)
            {
                return;
            }

            isLoadingCards = true;
            StateHasChanged();

            try
            {
                var deckId = selectedDeck;
                var deckCards = await GetDeckCardsAsync(deckId, allowReload: false);
                if (!string.Equals(deckId, selectedDeck, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (deckCards is null)
                {
                    return;
                }

                if (deckCards.Cards.Count == 0)
                {
                    ClearCurrentCard();
                    return;
                }

                var nextIndex = selectedCardIndex >= 0 ? selectedCardIndex + 1 : 0;
                ShowCardAtIndex(nextIndex, deckCards);
            }
            finally
            {
                isLoadingCards = false;
                StateHasChanged();
            }
        }

        private async Task ShowPreviousCardAsync()
        {
            if (!IsDeckSelected)
            {
                return;
            }

            isLoadingCards = true;
            StateHasChanged();

            try
            {
                var deckId = selectedDeck;
                var deckCards = await GetDeckCardsAsync(deckId, allowReload: false);
                if (!string.Equals(deckId, selectedDeck, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (deckCards is null)
                {
                    return;
                }

                if (deckCards.Cards.Count == 0)
                {
                    ClearCurrentCard();
                    return;
                }

                var previousIndex = selectedCardIndex >= 0 ? selectedCardIndex - 1 : deckCards.Cards.Count - 1;
                ShowCardAtIndex(previousIndex, deckCards);
            }
            finally
            {
                isLoadingCards = false;
                StateHasChanged();
            }
        }

        private void PrepareCardDescription(DeckCardInfo card)
        {
            descriptionError = null;
            selectedCardDescriptionHtml = null;

            try
            {
                selectedCardDescriptionHtml = Markdown.ToHtml(card.Description ?? string.Empty);
            }
            catch
            {
                descriptionError = DisplayTexts.TarotDescriptionLoadError;
                selectedCardDescriptionHtml = null;
            }
        }

        private void ClearSelectedCardDescription()
        {
            selectedCardDescriptionHtml = null;
            descriptionError = null;
        }

        private void ClearCurrentCard()
        {
            selectedCard = null;
            selectedCardIndex = -1;
            ClearSelectedCardDescription();
            isCardFullscreen = false;
            cardObserverNeedsRefresh = true;
        }

        private async Task ScrollToDescriptionAsync()
        {
            if (selectedCard is null)
            {
                return;
            }

            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("scrollToDescription", cardDescriptionRef);
        }

        private async ValueTask<IJSObjectReference> GetModuleAsync()
        {
            if (cardDeckModule is not null)
            {
                return cardDeckModule;
            }

            cardDeckModule = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/cardDeck.js");
            return cardDeckModule;
        }

        private async Task LoadCardScaleAsync()
        {
            var storedValue = await LocalStorage.GetItemAsync<int?>(ApplicationSettings.CardScalePercentKey);

            if (storedValue.HasValue)
            {
                cardScalePercent = ApplicationSettings.ClampCardScalePercent(storedValue.Value);

                if (cardScalePercent != storedValue.Value)
                {
                    await LocalStorage.SetItemAsync(ApplicationSettings.CardScalePercentKey, cardScalePercent);
                }
            }
            else
            {
                await LocalStorage.SetItemAsync(ApplicationSettings.CardScalePercentKey, cardScalePercent);
            }
        }

        private async Task LoadSearchAutoClearDelayAsync()
        {
            var storedValue = await LocalStorage.GetItemAsync<int?>(ApplicationSettings.SearchAutoClearDelaySecondsKey);

            if (storedValue.HasValue)
            {
                searchAutoClearDelaySeconds = ApplicationSettings.ClampSearchAutoClearDelaySeconds(storedValue.Value);

                if (searchAutoClearDelaySeconds != storedValue.Value)
                {
                    await LocalStorage.SetItemAsync(ApplicationSettings.SearchAutoClearDelaySecondsKey, searchAutoClearDelaySeconds);
                }
            }
            else
            {
                await LocalStorage.SetItemAsync(ApplicationSettings.SearchAutoClearDelaySecondsKey, searchAutoClearDelaySeconds);
            }
        }

        private void ToggleCardFullscreen()
        {
            if (selectedCard is null)
            {
                return;
            }

            isCardFullscreen = !isCardFullscreen;
        }

        private void HandlePointerDown(PointerEventArgs args)
        {
            if (!CanNavigateCards || !IsSwipePointer(args.PointerType))
            {
                ResetSwipeTracking();
                return;
            }

            swipeStartX = args.ClientX;
            isSwipeTracking = true;
        }

        private async Task HandlePointerUpAsync(PointerEventArgs args)
        {
            if (!isSwipeTracking || !IsSwipePointer(args.PointerType))
            {
                ResetSwipeTracking();
                return;
            }

            var startX = swipeStartX;
            ResetSwipeTracking();

            if (!startX.HasValue)
            {
                return;
            }

            var deltaX = args.ClientX - startX.Value;
            if (Math.Abs(deltaX) < SwipeThreshold)
            {
                return;
            }

            if (deltaX < 0)
            {
                await ShowNextCardAsync();
            }
            else
            {
                await ShowPreviousCardAsync();
            }
        }

        private void HandlePointerCancel(PointerEventArgs args)
        {
            ResetSwipeTracking();
        }

        private void HandleCardKeyDown(KeyboardEventArgs args)
        {
            if (selectedCard is null)
            {
                return;
            }

            if (args.Key is "Enter" or " " or "Spacebar")
            {
                ToggleCardFullscreen();
            }
        }

        private static double ConvertPercentToScaleFactor(int percent)
        {
            var clamped = ApplicationSettings.ClampCardScalePercent(percent);
            return clamped / 100d;
        }

        private void ResetSwipeTracking()
        {
            isSwipeTracking = false;
            swipeStartX = null;
        }

        private static bool IsSwipePointer(string? pointerType) => pointerType is "touch" or "pen";

        private ValueTask FocusSearchInputAsync()
        {
            if (searchInputRef.Context is null)
            {
                return ValueTask.CompletedTask;
            }

            try
            {
                return searchInputRef.FocusAsync();
            }
            catch (InvalidOperationException)
            {
            }
            catch (JSException)
            {
            }

            return ValueTask.CompletedTask;
        }

        private void RestartSearchClearTimer()
        {
            CancelSearchClearTimer();

            if (searchAutoClearDelaySeconds <= 0 || string.IsNullOrEmpty(searchTerm))
            {
                return;
            }

            var cancellationSource = new CancellationTokenSource();
            searchClearCancellation = cancellationSource;
            _ = ClearSearchAfterDelayAsync(cancellationSource);
        }

        private void CancelSearchClearTimer()
        {
            var existing = searchClearCancellation;

            if (existing is null)
            {
                return;
            }

            searchClearCancellation = null;

            try
            {
                existing.Cancel();
            }
            catch
            {
                // Ignored when cancellation source is already disposed.
            }

            existing.Dispose();
        }

        private async Task ClearSearchAfterDelayAsync(CancellationTokenSource cancellationSource)
        {
            try
            {
                var delay = TimeSpan.FromSeconds(searchAutoClearDelaySeconds);
                await Task.Delay(delay, cancellationSource.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (cancellationSource.IsCancellationRequested)
            {
                return;
            }

            if (!ReferenceEquals(searchClearCancellation, cancellationSource))
            {
                cancellationSource.Dispose();
                return;
            }

            searchClearCancellation = null;

            await InvokeAsync(() =>
            {
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    shouldRestoreSearchInputFocus = true;
                    SearchTerm = string.Empty;
                }
            });

            cancellationSource.Dispose();
        }

        private static DeckCardInfo CreateCardInfo(string deckId, string deckName, Spielkarte card)
        {
            var cardId = card?.Id ?? string.Empty;
            var displayName = CreateDisplayName(deckName, cardId);
            var key = CreateKey(deckId, cardId);
            var imageDataUrl = CreateImageDataUrl(card?.Image);
            var description = card?.Description ?? string.Empty;

            return new DeckCardInfo(displayName, deckId, cardId, key, imageDataUrl, description);
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

        private static int FindMatchingCardIndex(IReadOnlyList<Spielkarte> candidates, string deckId, string normalizedSearch)
        {
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

        public async ValueTask DisposeAsync()
        {
            CancelSearchClearTimer();

            if (cardDeckModule is null)
            {
                return;
            }

            try
            {
                await cardDeckModule.InvokeVoidAsync("disconnectCardVisibility", cardFigureRef);
            }
            catch
            {
                // Ignored during disposal.
            }

            await cardDeckModule.DisposeAsync();
        }

        private sealed record DeckCardInfo(string DisplayName, string DeckId, string CardId, string Key, string ImageDataUrl, string Description);

        private sealed record DeckOption(string DeckId, string DisplayName);

        private sealed record DeckCards(string DeckId, string DeckDisplayName, IReadOnlyList<Spielkarte> Cards);
    }
}
