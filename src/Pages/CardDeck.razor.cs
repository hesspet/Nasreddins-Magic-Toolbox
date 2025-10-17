using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Toolbox.Helpers;
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

        private CardDeckDataProvider? deckDataProvider;

        [CascadingParameter]
        private MainLayout? Layout { get; set; }

        protected override void OnInitialized()
        {
            Layout?.UpdateCurrentPageTitle(DisplayTexts.TarotPageTitle);
            deckDataProvider ??= new CardDeckDataProvider(DbHelper);
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

                var normalized = CardDeckUtilities.NormalizeForComparison(requestedSearch);
                var index = normalized.Length == 0
                    ? (selectedCardIndex >= 0 && selectedCardIndex < deckCards.Cards.Count ? selectedCardIndex : 0)
                    : CardDeckUtilities.FindMatchingCardIndex(deckCards.Cards, deckCards.DeckId, normalized);

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

        private Task HandleDeckSelectionChangedAsync(DeckOption? option)
        {
            SelectedDeck = option?.DeckId ?? string.Empty;
            return Task.CompletedTask;
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
            deckDataProvider ??= new CardDeckDataProvider(DbHelper);

            await deckDataProvider.LoadDeckOptionsAsync();

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

            deckDataProvider ??= new CardDeckDataProvider(DbHelper);

            var loaded = await deckDataProvider.LoadDeckCardsAsync(deckId);
            cachedDeckCards = loaded;
            return loaded;
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
            selectedCard = CardDeckUtilities.CreateCardInfo(deckCards.DeckId, deckCards.DeckDisplayName, card);
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
    }
}
