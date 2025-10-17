using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Toolbox.Helpers;
using Toolbox.Models;
using Toolbox.Models.CardReading;
using Toolbox.Resources;

namespace Toolbox.Components.CardReading;

public sealed partial class TarotCardTable : IAsyncDisposable
{
    private const double ObservationMargin = 24.0;

    private readonly List<RenderSlot> renderSlots = new();
    private readonly Dictionary<string, DeckCards> deckCardsCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SearchResult> searchResults = new();
    private readonly string searchDialogTitleId = $"tarot-card-search-title-{Guid.NewGuid():N}";
    private readonly string searchInputId = $"tarot-card-search-input-{Guid.NewGuid():N}";
    private readonly string searchResultsDescriptionId = $"tarot-card-search-results-{Guid.NewGuid():N}";

    private ElementReference tableSurface;
    private ElementReference searchInputRef;
    private IJSObjectReference? module;
    private DotNetObjectReference<TarotCardTable>? dotNetReference;
    private string cardStyle = string.Empty;
    private double layoutWidthUnits = 1.0;
    private double layoutHeightUnits = 1.62;
    private double cardWidth;
    private bool observationActive;
    private bool isDisposed;
    private RenderSlot? activeSlot;
    private DeckCards? activeDeckCards;
    private bool isSearchDialogOpen;
    private bool isSearching;
    private bool shouldFocusSearchInput;
    private string searchTerm = string.Empty;
    private string? searchError;
    private string currentDeckId = string.Empty;
    private bool isDeckLoading;
    private string? pendingDeckId;
    private bool CanPerformSearch => !isSearching && !string.IsNullOrWhiteSpace(searchTerm) && activeSlot is not null;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    public IndexedDbHelper DbHelper { get; set; } = default!;

    [Inject]
    public InMemoryLogService LogService { get; set; } = default!;

    [Parameter]
    public TarotSpreadLayout Spread { get; set; } = default!;

    [Parameter]
    public string CardBackImage { get; set; } = TarotResourceHelper.CardBackImageDataUrl;

    [Parameter]
    public bool ShowCards { get; set; } = true;

    [Parameter]
    public string SelectedDeckId { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<bool> OnDeckLoadingChanged { get; set; }

    private string SurfaceStyle => cardStyle;

    protected override void OnParametersSet()
    {
        if (Spread is null)
        {
            renderSlots.Clear();
            ResetSearchDialogState();
            pendingDeckId = null;
            _ = UpdateDeckLoadingStateAsync(false);
            return;
        }

        RecalculateLayout();

        var normalizedDeckId = SelectedDeckId ?? string.Empty;
        if (!string.Equals(currentDeckId, normalizedDeckId, StringComparison.OrdinalIgnoreCase))
        {
            currentDeckId = normalizedDeckId;

            foreach (var slot in renderSlots)
            {
                slot.ClearSelection();
            }

            ResetSearchDialogState();
            pendingDeckId = string.IsNullOrWhiteSpace(currentDeckId) ? null : currentDeckId;

            if (string.IsNullOrWhiteSpace(currentDeckId))
            {
                _ = UpdateDeckLoadingStateAsync(false);
            }
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (pendingDeckId is { } deckId)
        {
            pendingDeckId = null;
            await EnsureDeckCardsCachedAsync(deckId).ConfigureAwait(false);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Spread is null || isDisposed)
        {
            return;
        }

        if (firstRender)
        {
            module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/tarotCardTable.js").ConfigureAwait(false);
        }

        if (module is not null && !observationActive)
        {
            dotNetReference ??= DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("observeCardTable", tableSurface, dotNetReference).ConfigureAwait(false);
            observationActive = true;
        }

        if (shouldFocusSearchInput && isSearchDialogOpen)
        {
            shouldFocusSearchInput = false;

            try
            {
                await searchInputRef.FocusAsync();
            }
            catch
            {
                // Ignored if the element is not focusable yet.
            }
        }
    }

    [JSInvokable]
    public Task UpdateDimensions(double width, double height)
    {
        if (Spread is null || isDisposed)
        {
            return Task.CompletedTask;
        }

        var newWidth = CalculateCardWidth(width, height);
        if (Math.Abs(newWidth - cardWidth) < 0.5)
        {
            return Task.CompletedTask;
        }

        UpdateSlotSizes(newWidth);
        return InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        try
        {
            if (module is not null)
            {
                try
                {
                    await module.InvokeVoidAsync("disconnectCardTable", tableSurface).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore cleanup errors.
                }

                await module.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            module = null;
            observationActive = false;
            dotNetReference?.Dispose();
        }
    }

    private void RecalculateLayout()
    {
        renderSlots.Clear();

        if (Spread.Slots.Count == 0)
        {
            layoutWidthUnits = Spread.CardWidthUnits + (Spread.HorizontalPadding * 2);
            layoutHeightUnits = Spread.CardWidthUnits * Spread.CardAspectRatio + (Spread.VerticalPadding * 2);
            UpdateSlotSizes(Spread.DefaultCardWidth);
            return;
        }

        var cardWidthUnits = Spread.CardWidthUnits;
        var cardHeightUnits = cardWidthUnits * Spread.CardAspectRatio;

        var minX = double.MaxValue;
        var maxX = double.MinValue;
        var minY = double.MaxValue;
        var maxY = double.MinValue;

        foreach (var slot in Spread.Slots)
        {
            var widthUnits = slot.Orientation == CardOrientation.Horizontal ? cardHeightUnits : cardWidthUnits;
            var heightUnits = slot.Orientation == CardOrientation.Horizontal ? cardWidthUnits : cardHeightUnits;

            var halfWidth = widthUnits / 2.0;
            var halfHeight = heightUnits / 2.0;

            minX = Math.Min(minX, slot.CenterX - halfWidth);
            maxX = Math.Max(maxX, slot.CenterX + halfWidth);
            minY = Math.Min(minY, slot.CenterY - halfHeight);
            maxY = Math.Max(maxY, slot.CenterY + halfHeight);

            renderSlots.Add(new RenderSlot(slot));
        }

        if (double.IsPositiveInfinity(minX) || double.IsPositiveInfinity(minY))
        {
            minX = -Spread.CardWidthUnits / 2.0;
            maxX = Spread.CardWidthUnits / 2.0;
            minY = -(Spread.CardWidthUnits * Spread.CardAspectRatio) / 2.0;
            maxY = -minY;
        }

        minX -= Spread.HorizontalPadding;
        maxX += Spread.HorizontalPadding;
        minY -= Spread.VerticalPadding;
        maxY += Spread.VerticalPadding;

        layoutWidthUnits = Math.Max(cardWidthUnits, maxX - minX);
        layoutHeightUnits = Math.Max(cardHeightUnits, maxY - minY);

        foreach (var slot in renderSlots)
        {
            slot.UpdatePosition(minX, minY, layoutWidthUnits, layoutHeightUnits);
        }

        UpdateSlotSizes(cardWidth > 0 ? cardWidth : Spread.DefaultCardWidth);
    }

    private void UpdateSlotSizes(double newCardWidth)
    {
        cardWidth = Math.Clamp(newCardWidth, Spread.MinCardWidth, Spread.MaxCardWidth);
        var cardHeight = cardWidth * Spread.CardAspectRatio;

        cardStyle = FormattableString.Invariant($"--tarot-card-width:{cardWidth:F2}px; --tarot-card-height:{cardHeight:F2}px;");

        foreach (var slot in renderSlots)
        {
            slot.UpdateDimensions(cardWidth, cardHeight, cardStyle);
        }
    }

    private async Task HandleCardClickedAsync(RenderSlot slot)
    {
        if (string.IsNullOrWhiteSpace(SelectedDeckId))
        {
            return;
        }

        activeSlot = slot;
        activeDeckCards = null;
        searchError = null;
        searchResults.Clear();
        searchTerm = slot.SelectedCardId ?? string.Empty;
        isSearchDialogOpen = true;
        isSearching = false;
        shouldFocusSearchInput = true;

        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleSearchKeyDown(KeyboardEventArgs args)
    {
        if (!string.Equals(args?.Key, "Enter", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!CanPerformSearch)
        {
            return;
        }

        await PerformSearchAsync().ConfigureAwait(false);
    }

    private async Task PerformSearchAsync()
    {
        if (!CanPerformSearch || activeSlot is null)
        {
            return;
        }

        var deckId = SelectedDeckId;
        if (string.IsNullOrWhiteSpace(deckId))
        {
            searchError = DisplayTexts.TarotSearchNotFound;
            await InvokeAsync(StateHasChanged);
            return;
        }

        isSearching = true;
        searchError = null;
        searchResults.Clear();
        await InvokeAsync(StateHasChanged);

        try
        {
            var deckCards = await GetDeckCardsAsync(deckId).ConfigureAwait(false);
            if (deckCards is null || deckCards.Cards.Count == 0)
            {
                searchError = DisplayTexts.TarotSearchNotFound;
                return;
            }

            activeDeckCards = deckCards;

            var normalized = CardSearchHelper.NormalizeForComparison(searchTerm);
            var matches = CardSearchHelper.FindMatchingCards(deckCards.Cards, deckCards.DeckId, normalized);

            if (matches.Count == 0)
            {
                searchError = DisplayTexts.TarotSearchNotFound;
                return;
            }

            if (matches.Count == 1)
            {
                ApplySelection(deckCards, matches[0]);
                return;
            }

            var ordered = matches.OrderBy(card => CardSearchHelper.CreateDisplayName(deckCards.DeckDisplayName, card.Id), StringComparer.OrdinalIgnoreCase);

            foreach (var card in ordered)
            {
                var displayName = CardSearchHelper.CreateDisplayName(deckCards.DeckDisplayName, card.Id);
                searchResults.Add(new SearchResult(displayName, card));
            }
        }
        finally
        {
            isSearching = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task SelectSearchResult(SearchResult result)
    {
        if (activeDeckCards is null)
        {
            return Task.CompletedTask;
        }

        ApplySelection(activeDeckCards, result.Card);
        return InvokeAsync(StateHasChanged);
    }

    private void ApplySelection(DeckCards deckCards, Spielkarte card)
    {
        if (activeSlot is null)
        {
            return;
        }

        var slot = activeSlot;
        var cardId = card?.Id ?? string.Empty;
        var displayName = CardSearchHelper.CreateDisplayName(deckCards.DeckDisplayName, cardId);
        var imageDataUrl = CardSearchHelper.CreateImageDataUrl(card?.Image);

        slot.UpdateSelection(deckCards.DeckId, cardId, displayName, imageDataUrl);

        ResetSearchDialogState();
    }

    private async Task<DeckCards?> EnsureDeckCardsCachedAsync(string deckId)
    {
        if (string.IsNullOrWhiteSpace(deckId))
        {
            await UpdateDeckLoadingStateAsync(false).ConfigureAwait(false);
            return null;
        }

        return await GetDeckCardsAsync(deckId).ConfigureAwait(false);
    }

    private async Task<DeckCards?> GetDeckCardsAsync(string deckId, bool forceReload = false)
    {
        if (!forceReload && deckCardsCache.TryGetValue(deckId, out var cached))
        {
            LogService.LogDebug($"Kartenspiel '{cached.DeckDisplayName}' ist bereits im Cache verfügbar.");
            await UpdateDeckLoadingStateAsync(false).ConfigureAwait(false);
            return cached;
        }

        return await LoadDeckCardsFromDatabaseAsync(deckId).ConfigureAwait(false);
    }

    private async Task<DeckCards?> LoadDeckCardsFromDatabaseAsync(string deckId)
    {
        if (string.IsNullOrWhiteSpace(deckId))
        {
            await UpdateDeckLoadingStateAsync(false).ConfigureAwait(false);
            return null;
        }

        await UpdateDeckLoadingStateAsync(true).ConfigureAwait(false);

        try
        {
            await DbHelper.InitializeAsync().ConfigureAwait(false);
            var deck = await DbHelper.GetDeckAsync(deckId).ConfigureAwait(false);
            var deckName = string.IsNullOrWhiteSpace(deck?.Name) ? deckId : deck!.Name!;
            var deckDisplayName = CardSearchHelper.CreateDeckDisplayName(deckName);

            LogService.LogDebug($"Lade Kartenspiel '{deckDisplayName}' ({deckId}) aus der Datenbank.");

            var cards = await DbHelper.GetCardsByDeckAsync(deckId).ConfigureAwait(false);
            var orderedCards = cards.OrderBy(card => CardSearchHelper.CreateDisplayName(deckDisplayName, card.Id), StringComparer.OrdinalIgnoreCase)
                                    .ToList();

            LogService.LogDebug($"Kartenspiel '{deckDisplayName}' wurde mit {orderedCards.Count} Karten in den Cache geladen.");

            var deckCards = new DeckCards(deckId, deckDisplayName, orderedCards);
            deckCardsCache[deckId] = deckCards;
            return deckCards;
        }
        catch (Exception exception)
        {
            LogService.LogError($"Fehler beim Laden des Kartenspiels '{deckId}': {exception.Message}");
            return null;
        }
        finally
        {
            await UpdateDeckLoadingStateAsync(false).ConfigureAwait(false);
        }
    }

    private Task CloseSearchDialog()
    {
        if (!isSearchDialogOpen)
        {
            return Task.CompletedTask;
        }

        ResetSearchDialogState();
        return InvokeAsync(StateHasChanged);
    }

    private Task HandleOverlayKeyDown(KeyboardEventArgs args)
    {
        if (string.Equals(args?.Key, "Escape", StringComparison.OrdinalIgnoreCase))
        {
            ResetSearchDialogState();
            return InvokeAsync(StateHasChanged);
        }

        return Task.CompletedTask;
    }

    private async Task UpdateDeckLoadingStateAsync(bool isLoading)
    {
        if (isDeckLoading == isLoading)
        {
            return;
        }

        isDeckLoading = isLoading;

        if (OnDeckLoadingChanged.HasDelegate)
        {
            await InvokeAsync(() => OnDeckLoadingChanged.InvokeAsync(isLoading));
        }

        await InvokeAsync(StateHasChanged);
    }

    private void ResetSearchDialogState()
    {
        isSearchDialogOpen = false;
        isSearching = false;
        shouldFocusSearchInput = false;
        searchTerm = string.Empty;
        searchError = null;
        searchResults.Clear();
        activeSlot = null;
        activeDeckCards = null;
    }

    private double CalculateCardWidth(double width, double height)
    {
        var availableWidth = Math.Max(0, width - (ObservationMargin * 2));
        var availableHeight = Math.Max(0, height - (ObservationMargin * 2));

        if (availableWidth <= 0 || availableHeight <= 0 || layoutWidthUnits <= 0 || layoutHeightUnits <= 0)
        {
            return cardWidth > 0 ? cardWidth : Spread.DefaultCardWidth;
        }

        var scaleX = availableWidth / layoutWidthUnits;
        var scaleY = availableHeight / layoutHeightUnits;
        var calculated = Math.Min(scaleX, scaleY);

        if (double.IsNaN(calculated) || double.IsInfinity(calculated) || calculated <= 0)
        {
            return cardWidth > 0 ? cardWidth : Spread.DefaultCardWidth;
        }

        return Math.Clamp(calculated, Spread.MinCardWidth, Spread.MaxCardWidth);
    }

    private static string GetSlotCssClass(RenderSlot slot) =>
        slot.Orientation == CardOrientation.Horizontal ? "tarot-card-table__slot--horizontal" : string.Empty;

    private sealed class RenderSlot
    {
        public RenderSlot(TarotCardSlot slot)
        {
            Id = slot.Id;
            Orientation = slot.Orientation;
            Label = string.IsNullOrWhiteSpace(slot.Label) ? slot.Id : slot.Label!;
            ZIndex = slot.ZIndex;
            CenterX = slot.CenterX;
            CenterY = slot.CenterY;
        }

        public string Id { get; }

        public CardOrientation Orientation { get; }

        public string Label { get; }

        public int ZIndex { get; }

        public double CenterX { get; }

        public double CenterY { get; }

        public double LeftPercent { get; private set; }

        public double TopPercent { get; private set; }

        public double Width { get; private set; }

        public double Height { get; private set; }

        public string CardStyle { get; private set; } = string.Empty;

        public string? SelectedDeckId { get; private set; }

        public string? SelectedCardId { get; private set; }

        public string? SelectedCardDisplayName { get; private set; }

        public string? SelectedCardImageUrl { get; private set; }

        public string DisplayAltText => string.IsNullOrWhiteSpace(SelectedCardDisplayName) ? Label : SelectedCardDisplayName!;

        public string SlotStyle => FormattableString.Invariant($"left:{LeftPercent:F3}%;top:{TopPercent:F3}%;width:{Width:F2}px;height:{Height:F2}px;z-index:{ZIndex};");

        public void UpdatePosition(double minX, double minY, double widthUnits, double heightUnits)
        {
            LeftPercent = widthUnits <= 0 ? 50 : ((CenterX - minX) / widthUnits) * 100.0;
            TopPercent = heightUnits <= 0 ? 50 : ((CenterY - minY) / heightUnits) * 100.0;
        }

        public void UpdateDimensions(double cardWidth, double cardHeight, string cardStyle)
        {
            Width = Orientation == CardOrientation.Horizontal ? cardHeight : cardWidth;
            Height = Orientation == CardOrientation.Horizontal ? cardWidth : cardHeight;
            CardStyle = cardStyle;
        }

        public void UpdateSelection(string deckId, string cardId, string displayName, string imageDataUrl)
        {
            SelectedDeckId = deckId;
            SelectedCardId = cardId;
            SelectedCardDisplayName = displayName;
            SelectedCardImageUrl = string.IsNullOrWhiteSpace(imageDataUrl) ? null : imageDataUrl;
        }

        public void ClearSelection()
        {
            SelectedDeckId = null;
            SelectedCardId = null;
            SelectedCardDisplayName = null;
            SelectedCardImageUrl = null;
        }
    }

    private sealed record SearchResult(string DisplayName, Spielkarte Card);

    private sealed record DeckCards(string DeckId, string DeckDisplayName, IReadOnlyList<Spielkarte> Cards);
}
